namespace StringGen.Core;

internal sealed record CharClassNode(IReadOnlyList<(char Min, char Max)> Ranges, bool Negated) : RegexNode;
