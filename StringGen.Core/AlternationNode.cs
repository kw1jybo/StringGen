namespace StringGen.Core;

internal sealed record AlternationNode(IReadOnlyList<RegexNode> Alternatives) : RegexNode;
