namespace StringGen.Core;

internal sealed record QuantifiedNode(RegexNode Inner, int Min, int Max) : RegexNode;
