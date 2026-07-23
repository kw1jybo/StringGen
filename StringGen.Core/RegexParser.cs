namespace StringGen.Core;

internal sealed class RegexParser(string pattern)
{
    private const int MaxDepth = 500;

    private int pos;
    private int depth;

    private bool AtEnd => pos >= pattern.Length;
    private char Current => pattern[pos];
    private char Consume() => pattern[pos++];

    public RegexNode Parse()
    {
        var node = ParseAlternation();
        if (!AtEnd)
        {
            throw new ArgumentException($"Unexpected character '{Current}' at position {pos} in pattern '{pattern}'");
        }
        return node;
    }

    private RegexNode ParseAlternation()
    {
        var alternatives = new List<RegexNode> { ParseSequence() };
        while (!AtEnd && Current == '|')
        {
            pos++;
            alternatives.Add(ParseSequence());
        }
        return alternatives.Count == 1 ? alternatives[0] : new AlternationNode(alternatives);
    }

    private RegexNode ParseSequence()
    {
        var children = new List<RegexNode>();
        while (!AtEnd && Current != '|' && Current != ')')
        {
            children.Add(ParseQuantifier(ParseAtom()));
        }

        return children.Count == 0 ? new SequenceNode([])
             : children.Count == 1 ? children[0]
             : new SequenceNode(children);
    }

    private RegexNode ParseAtom()
    {
        char c = Current;
        if (c == '^' || c == '$') { pos++; return new AnchorNode(); }
        if (c == '.') { pos++; return new WildcardNode(); }
        if (c == '(') { return ParseGroup(); }
        if (c == '[') { return ParseCharClass(); }
        if (c == '\\') { return ParseEscape(); }
        pos++;
        return new LiteralNode(c);
    }

    private RegexNode ParseGroup()
    {
        if (++depth > MaxDepth)
        {
            throw new ArgumentException(
                $"Pattern '{pattern}' exceeds the maximum nesting depth of {MaxDepth}.");
        }
        try
        {
            pos++;

            if (!AtEnd && Current == '?')
            {
                pos++;
                if (!AtEnd)
                {
                    switch (Current)
                    {
                        case ':':
                            pos++;
                            break;
                        case '=':
                        case '!':
                            pos++;
                            return SkipToClosingParen();
                        case '<':
                            pos++;
                            if (!AtEnd && (Current == '=' || Current == '!'))
                            {
                                pos++;
                                return SkipToClosingParen();
                            }
                            while (!AtEnd && Current != '>') { pos++; }
                            if (!AtEnd) { pos++; }
                            break;
                    }
                }
            }

            var inner = ParseAlternation();
            if (!AtEnd && Current == ')') { pos++; }
            return new GroupNode(inner);
        }
        finally
        {
            depth--;
        }
    }

    private RegexNode SkipToClosingParen()
    {
        int d = 1;
        while (!AtEnd && d > 0)
        {
            char c = Consume();
            if (c == '\\')
            {
                if (!AtEnd) { Consume(); }
            }
            else if (c == '(') { d++; }
            else if (c == ')') { d--; }
        }
        return new SequenceNode([]);
    }

    private RegexNode ParseCharClass()
    {
        pos++;
        bool negated = !AtEnd && Current == '^';
        if (negated) { pos++; }

        var ranges = new List<(char Min, char Max)>();

        while (!AtEnd && Current != ']')
        {
            if (Current == '\\')
            {
                pos++;
                char code = Consume();
                if (code is 'd' or 'D' or 'w' or 'W' or 's' or 'S')
                {
                    foreach (char c in CharSets.ForCode(code))
                    {
                        ranges.Add((c, c));
                    }
                }
                else
                {
                    char ch = EscapeToChar(code);
                    ranges.Add((ch, ch));
                }
            }
            else
            {
                char start = Consume();
                if (!AtEnd && Current == '-' && pos + 1 < pattern.Length && pattern[pos + 1] != ']')
                {
                    pos++;
                    char end = Current == '\\' ? (pos++, EscapeToChar(Consume())).Item2 : Consume();
                    ranges.Add((start, end));
                }
                else
                {
                    ranges.Add((start, start));
                }
            }
        }

        if (!AtEnd) { pos++; }
        return new CharClassNode(ranges, negated);
    }

    private RegexNode ParseEscape()
    {
        pos++;
        char code = Consume();
        return code switch
        {
            'd' or 'D' or 'w' or 'W' or 's' or 'S' => new ShorthandNode(code),
            'b' or 'B' => new AnchorNode(),
            _ => new LiteralNode(EscapeToChar(code))
        };
    }

    private static char EscapeToChar(char code) => code switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => code
    };

    private RegexNode ParseQuantifier(RegexNode inner)
    {
        if (AtEnd) { return inner; }

        int min, max;
        switch (Current)
        {
            case '*': min = 0; max = -1; pos++; break;
            case '+': min = 1; max = -1; pos++; break;
            case '?': min = 0; max = 1; pos++; break;
            case '{':
                pos++;
                min = ParseInt();
                max = min;
                if (!AtEnd && Current == ',')
                {
                    pos++;
                    max = (!AtEnd && Current == '}') ? -1 : ParseInt();
                }
                if (!AtEnd && Current == '}') { pos++; }
                break;
            default: return inner;
        }

        if (!AtEnd && (Current == '?' || Current == '+')) { pos++; }

        return new QuantifiedNode(inner, min, max);
    }

    private int ParseInt()
    {
        int start = pos;
        while (!AtEnd && char.IsDigit(Current)) { pos++; }
        if (pos == start)
        {
            throw new ArgumentException($"Expected integer at position {pos} in pattern '{pattern}'");
        }
        if (!int.TryParse(pattern[start..pos], out int value))
        {
            throw new ArgumentException(
                $"Quantifier value at position {start} overflows Int32 in pattern '{pattern}'");
        }
        return value;
    }
}
