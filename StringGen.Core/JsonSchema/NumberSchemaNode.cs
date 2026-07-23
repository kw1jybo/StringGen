namespace StringGen.Core.JsonSchema;

internal sealed record NumberSchemaNode(
    double? Minimum = null,
    double? Maximum = null,
    double? ExclusiveMinimum = null,
    double? ExclusiveMaximum = null,
    double? MultipleOf = null,
    bool IsInteger = false) : JsonSchemaNode;
