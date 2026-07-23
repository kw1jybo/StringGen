namespace StringGen.Core;

internal sealed class RegexStringGenerator(Random random, int maxRepeat = 5)
{
    private readonly Dictionary<CharClassNode, char[]> charClassCache =
        new(ReferenceEqualityComparer.Instance);

    public string Generate(RegexNode node) => node switch
    {
        LiteralNode n => n.Value.ToString(),
        AnchorNode => string.Empty,
        WildcardNode => Pick(CharSets.Printable).ToString(),
        ShorthandNode n => Pick(CharSets.ForCode(n.Code)).ToString(),
        CharClassNode n => GenerateCharClass(n).ToString(),
        QuantifiedNode n => GenerateQuantified(n),
        SequenceNode n => string.Concat(n.Children.Select(Generate)),
        AlternationNode n => Generate(n.Alternatives[random.Next(n.Alternatives.Count)]),
        GroupNode n => Generate(n.Inner),
        _ => throw new NotSupportedException($"Unknown AST node: {node.GetType().Name}")
    };

    private string GenerateQuantified(QuantifiedNode node)
    {
        int max = node.Max == -1 ? node.Min + maxRepeat : node.Max;
        int count = random.Next(node.Min, max + 1);
        return string.Concat(Enumerable.Range(0, count).Select(x => Generate(node.Inner)));
    }

    private char GenerateCharClass(CharClassNode node)
    {
        if (!charClassCache.TryGetValue(node, out var chars))
        {
            charClassCache[node] = chars = BuildCharSet(node);
        }
        return Pick(chars);
    }

    private char[] BuildCharSet(CharClassNode node)
    {
        var expanded = ExpandRanges(node.Ranges);
        if (!node.Negated) { return expanded; }

        var excluded = new HashSet<char>(expanded);
        var allowed = new List<char>(CharSets.Printable.Length);
        foreach (var c in CharSets.Printable)
        {
            if (!excluded.Contains(c)) { allowed.Add(c); }
        }
        if (allowed.Count == 0)
        {
            throw new InvalidOperationException(
                "Negated character class excludes all characters in the generation universe. " +
                "The generator cannot produce a character that satisfies this constraint.");
        }
        return [.. allowed];
    }

    private char Pick(ReadOnlySpan<char> chars)
    {
        if (chars.Length == 0)
        {
            throw new InvalidOperationException("Character set is empty — cannot pick a character.");
        }
        return chars[random.Next(chars.Length)];
    }

    private static char[] ExpandRanges(IEnumerable<(char Min, char Max)> ranges) =>
        [.. ranges
            .SelectMany(static x => Enumerable.Range(x.Min, x.Max - x.Min + 1).Select(static i => (char)i))
            .Distinct()];
}
