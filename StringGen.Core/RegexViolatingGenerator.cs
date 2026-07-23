namespace StringGen.Core;

internal sealed class RegexViolatingGenerator(Random random, int maxRepeat)
{
    private readonly RegexStringGenerator compliantGen = new(random, maxRepeat);

    public static IReadOnlyList<RegexNode> CollectTargets(RegexNode node)
    {
        var targets = new List<RegexNode>();
        Collect(node, targets);
        return targets;
    }

    public string Generate(RegexNode root, RegexNode target) => GenerateWith(root, target);

    private static void Collect(RegexNode node, List<RegexNode> targets)
    {
        switch (node)
        {
            case LiteralNode:
            case ShorthandNode:
            case WildcardNode:
                targets.Add(node);
                break;
            case CharClassNode n:
                if (BuildComplement(n).Length > 0)
                {
                    targets.Add(n);
                }
                break;
            case QuantifiedNode n:
                if (n.Min > 0 || (n.Max > 0 && n.Max != -1))
                {
                    targets.Add(n);
                }
                Collect(n.Inner, targets);
                break;
            case SequenceNode n:
                foreach (var child in n.Children)
                {
                    Collect(child, targets);
                }
                break;
            case AlternationNode n:
                foreach (var alt in n.Alternatives)
                {
                    Collect(alt, targets);
                }
                break;
            case GroupNode n:
                Collect(n.Inner, targets);
                break;
            case AnchorNode:
                break;
        }
    }

    private string GenerateWith(RegexNode node, RegexNode target)
    {
        if (ReferenceEquals(node, target))
        {
            return ViolateNode(node);
        }

        return node switch
        {
            SequenceNode n => string.Concat(n.Children.Select(x => GenerateWith(x, target))),
            AlternationNode n => GenerateAlternationWith(n, target),
            GroupNode n => GenerateWith(n.Inner, target),
            QuantifiedNode n => GenerateQuantifiedWith(n, target),
            _ => compliantGen.Generate(node)
        };
    }

    private string GenerateQuantifiedWith(QuantifiedNode node, RegexNode target)
    {
        int max = node.Max == -1 ? node.Min + maxRepeat : node.Max;
        // Force at least one repetition so an inner target has a chance to be visited
        int min = TreeContains(node.Inner, target) ? Math.Max(1, node.Min) : node.Min;
        int count = random.Next(min, max + 1);
        return string.Concat(Enumerable.Range(0, count).Select(x => GenerateWith(node.Inner, target)));
    }

    private string GenerateAlternationWith(AlternationNode node, RegexNode target)
    {
        // Prefer the branch that contains the target to maximise the chance of hitting it
        var branch = node.Alternatives.FirstOrDefault(x => TreeContains(x, target))
                     ?? node.Alternatives[random.Next(node.Alternatives.Count)];
        return GenerateWith(branch, target);
    }

    private static bool TreeContains(RegexNode tree, RegexNode target)
    {
        if (ReferenceEquals(tree, target))
        {
            return true;
        }

        return tree switch
        {
            SequenceNode n => n.Children.Any(x => TreeContains(x, target)),
            AlternationNode n => n.Alternatives.Any(x => TreeContains(x, target)),
            GroupNode n => TreeContains(n.Inner, target),
            QuantifiedNode n => TreeContains(n.Inner, target),
            _ => false
        };
    }

    private string ViolateNode(RegexNode node) => node switch
    {
        LiteralNode n => ViolateLiteral(n),
        CharClassNode n => ViolateCharClass(n),
        ShorthandNode n => ViolateShorthand(n),
        WildcardNode => "\n",
        QuantifiedNode n => ViolateQuantified(n),
        _ => throw new NotSupportedException($"Cannot violate node type {node.GetType().Name}")
    };

    private string ViolateLiteral(LiteralNode node)
    {
        char c;
        do
        {
            c = Pick(CharSets.Printable);
        }
        while (c == node.Value);
        return c.ToString();
    }

    private string ViolateCharClass(CharClassNode node)
    {
        var complement = BuildComplement(node);
        return complement[random.Next(complement.Length)].ToString();
    }

    private string ViolateShorthand(ShorthandNode node)
    {
        var complementCode = node.Code switch
        {
            'd' => 'D',
            'D' => 'd',
            'w' => 'W',
            'W' => 'w',
            's' => 'S',
            'S' => 's',
            _ => throw new NotSupportedException($"Unknown shorthand \\{node.Code}")
        };
        return Pick(CharSets.ForCode(complementCode)).ToString();
    }

    private string ViolateQuantified(QuantifiedNode node)
    {
        if (node.Min > 0)
        {
            return string.Empty;
        }
        // Min == 0 and Max > 0: exceed the upper bound
        int count = node.Max + 1;
        return string.Concat(Enumerable.Range(0, count).Select(x => compliantGen.Generate(node.Inner)));
    }

    private static char[] BuildComplement(CharClassNode node)
    {
        var inClass = new HashSet<char>(ExpandRanges(node.Ranges));
        var result = new List<char>();
        foreach (var c in CharSets.Printable)
        {
            bool inSet = inClass.Contains(c);
            if (node.Negated ? inSet : !inSet)
            {
                result.Add(c);
            }
        }
        return [.. result];
    }

    private static IEnumerable<char> ExpandRanges(IEnumerable<(char Min, char Max)> ranges) =>
        ranges.SelectMany(static x => Enumerable.Range(x.Min, x.Max - x.Min + 1))
              .Select(static x => (char)x);

    private char Pick(ReadOnlySpan<char> chars) => chars[random.Next(chars.Length)];
}
