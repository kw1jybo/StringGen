using StringGen.Core;

namespace StringGen.Tests;

public class RegexParserTests
{
    private static RegexNode Parse(string pattern) => new RegexParser(pattern).Parse();

    [Fact]
    public void Parse_SingleChar_ReturnsLiteralNode()
    {
        var node = Assert.IsType<LiteralNode>(Parse("a"));
        Assert.Equal('a', node.Value);
    }

    [Fact]
    public void Parse_Sequence_ReturnsSequenceOfLiterals()
    {
        var node = Assert.IsType<SequenceNode>(Parse("abc"));
        Assert.Equal(3, node.Children.Count);
        Assert.All(node.Children, static x => Assert.IsType<LiteralNode>(x));
        Assert.Equal('a', ((LiteralNode)node.Children[0]).Value);
        Assert.Equal('b', ((LiteralNode)node.Children[1]).Value);
        Assert.Equal('c', ((LiteralNode)node.Children[2]).Value);
    }

    [Theory]
    [InlineData("\\n", '\n')]
    [InlineData("\\r", '\r')]
    [InlineData("\\t", '\t')]
    [InlineData("\\.", '.')]
    [InlineData("\\(", '(')]
    [InlineData("\\)", ')')]
    public void Parse_EscapedLiteral_ReturnsCorrectChar(string pattern, char expected)
    {
        var node = Assert.IsType<LiteralNode>(Parse(pattern));
        Assert.Equal(expected, node.Value);
    }

    [Theory]
    [InlineData("^a", 'a')]
    [InlineData("a$", 'a')]
    public void Parse_AnchorIsStripped_LeavesCoreNode(string pattern, char expectedLiteral)
    {
        var root = Parse(pattern);
        var literals = Flatten(root).OfType<LiteralNode>().ToList();
        Assert.Contains(literals, x => x.Value == expectedLiteral);
        Assert.Contains(Flatten(root), static x => x is AnchorNode);
    }

    [Fact]
    public void Parse_CharClass_SingleRange()
    {
        var node = Assert.IsType<CharClassNode>(Parse("[a-z]"));
        Assert.False(node.Negated);
        Assert.Single(node.Ranges);
        Assert.Equal(('a', 'z'), node.Ranges[0]);
    }

    [Fact]
    public void Parse_CharClass_MultipleRanges()
    {
        var node = Assert.IsType<CharClassNode>(Parse("[a-zA-Z0-9]"));
        Assert.False(node.Negated);
        Assert.Equal(3, node.Ranges.Count);
        Assert.Contains(('a', 'z'), node.Ranges);
        Assert.Contains(('A', 'Z'), node.Ranges);
        Assert.Contains(('0', '9'), node.Ranges);
    }

    [Fact]
    public void Parse_NegatedCharClass_SetsNegatedFlag()
    {
        var node = Assert.IsType<CharClassNode>(Parse("[^0-9]"));
        Assert.True(node.Negated);
        Assert.Contains(('0', '9'), node.Ranges);
    }

    [Fact]
    public void Parse_CharClass_SingleChar_RangeMinEqualsMax()
    {
        var node = Assert.IsType<CharClassNode>(Parse("[x]"));
        Assert.Single(node.Ranges);
        Assert.Equal(('x', 'x'), node.Ranges[0]);
    }

    [Theory]
    [InlineData("\\d", 'd')]
    [InlineData("\\D", 'D')]
    [InlineData("\\w", 'w')]
    [InlineData("\\W", 'W')]
    [InlineData("\\s", 's')]
    [InlineData("\\S", 'S')]
    public void Parse_Shorthand_ReturnsShorthandNode(string pattern, char code)
    {
        var node = Assert.IsType<ShorthandNode>(Parse(pattern));
        Assert.Equal(code, node.Code);
    }

    [Fact]
    public void Parse_Dot_ReturnsWildcardNode()
    {
        Assert.IsType<WildcardNode>(Parse("."));
    }

    [Theory]
    [InlineData("a*", 0, -1)]
    [InlineData("a+", 1, -1)]
    [InlineData("a?", 0, 1)]
    [InlineData("a{3}", 3, 3)]
    [InlineData("a{2,5}", 2, 5)]
    [InlineData("a{1,}", 1, -1)]
    public void Parse_Quantifier_SetsMinMax(string pattern, int expectedMin, int expectedMax)
    {
        var node = Assert.IsType<QuantifiedNode>(Parse(pattern));
        Assert.Equal(expectedMin, node.Min);
        Assert.Equal(expectedMax, node.Max);
        Assert.IsType<LiteralNode>(node.Inner);
    }

    [Fact]
    public void Parse_LazyQuantifier_IgnoresLazyModifier()
    {
        // Lazy '?' after quantifier is valid regex; generation ignores it
        var node = Assert.IsType<QuantifiedNode>(Parse("a+?"));
        Assert.Equal(1, node.Min);
        Assert.Equal(-1, node.Max);
    }

    [Fact]
    public void Parse_Alternation_ReturnsTwoAlternatives()
    {
        var node = Assert.IsType<AlternationNode>(Parse("a|b"));
        Assert.Equal(2, node.Alternatives.Count);
    }

    [Fact]
    public void Parse_Alternation_MultipleOptions()
    {
        var node = Assert.IsType<AlternationNode>(Parse("foo|bar|baz"));
        Assert.Equal(3, node.Alternatives.Count);
    }

    [Fact]
    public void Parse_CapturingGroup_ReturnsGroupNode()
    {
        var node = Assert.IsType<GroupNode>(Parse("(a)"));
        Assert.IsType<LiteralNode>(node.Inner);
    }

    [Fact]
    public void Parse_NonCapturingGroup_ReturnsGroupNode()
    {
        var node = Assert.IsType<GroupNode>(Parse("(?:abc)"));
        Assert.IsType<SequenceNode>(node.Inner);
    }

    [Fact]
    public void Parse_NamedGroup_ReturnsGroupNode()
    {
        var node = Assert.IsType<GroupNode>(Parse("(?<name>abc)"));
        Assert.IsType<SequenceNode>(node.Inner);
    }

    [Fact]
    public void Parse_Lookahead_ReturnsEmptySequence()
    {
        // Lookaheads are skipped; they produce an empty sequence node
        var seq = Assert.IsType<SequenceNode>(Parse("a(?=b)"));
        Assert.Contains(seq.Children, static x => x is SequenceNode { Children.Count: 0 });
    }

    [Fact]
    public void Parse_UnmatchedCloseParen_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Parse("a)"));
    }

    [Fact]
    public void Parse_CurlyBraceFollowedByNonDigit_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Parse("a{b}"));
    }

    [Fact]
    public void Parse_ShorthandInsideCharClass_ExpandsToCharacterRanges()
    {
        var node = Assert.IsType<CharClassNode>(Parse(@"[\d\s]"));
        Assert.False(node.Negated);
        // \d = 10 digit chars, \s = 4 whitespace chars, each added as an individual (c, c) range
        Assert.Equal(14, node.Ranges.Count);
        Assert.Contains(('0', '0'), node.Ranges);
        Assert.Contains((' ', ' '), node.Ranges);
    }

    [Fact]
    public void Parse_NegativeLookahead_ProducesEmptySequenceNode()
    {
        var seq = Assert.IsType<SequenceNode>(Parse("a(?!b)"));
        Assert.Contains(seq.Children, static x => x is SequenceNode { Children.Count: 0 });
    }

    [Fact]
    public void Parse_PositiveLookbehind_ProducesEmptySequenceNode()
    {
        var seq = Assert.IsType<SequenceNode>(Parse(@"a(?<=b)"));
        Assert.Contains(seq.Children, static x => x is SequenceNode { Children.Count: 0 });
    }

    [Fact]
    public void Parse_NegativeLookbehind_ProducesEmptySequenceNode()
    {
        var seq = Assert.IsType<SequenceNode>(Parse(@"a(?<!b)"));
        Assert.Contains(seq.Children, static x => x is SequenceNode { Children.Count: 0 });
    }

    [Fact]
    public void Parse_PossessiveQuantifier_ParsedWithoutError()
    {
        // Possessive '+' after a quantifier is consumed and ignored.
        var node = Assert.IsType<QuantifiedNode>(Parse("a++"));
        Assert.Equal(1, node.Min);
        Assert.Equal(-1, node.Max);
        Assert.IsType<LiteralNode>(node.Inner);
    }

    [Fact]
    public void Parse_InlineFlagGroup_ParsedWithoutError()
    {
        // Inline flag groups like (?i) fall through to the non-capturing case;
        // the flag letter is treated as a literal inside the group.
        var node = Assert.IsType<SequenceNode>(Parse("(?i)abc"));
        Assert.Equal(4, node.Children.Count);
        Assert.IsType<GroupNode>(node.Children[0]);
    }

    private static IEnumerable<RegexNode> Flatten(RegexNode node)
    {
        yield return node;
        IEnumerable<RegexNode> children = node switch
        {
            SequenceNode n => n.Children,
            AlternationNode n => n.Alternatives,
            GroupNode n => [n.Inner],
            QuantifiedNode n => [n.Inner],
            _ => []
        };
        foreach (var child in children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
