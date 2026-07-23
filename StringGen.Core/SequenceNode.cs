namespace StringGen.Core;

internal sealed record SequenceNode(IReadOnlyList<RegexNode> Children) : RegexNode;
