namespace StringGen.Core.JsonSchema;

internal sealed record ObjectSchemaNode(
    IReadOnlyDictionary<string, JsonSchemaNode> Properties,
    IReadOnlyList<string> Required,
    JsonSchemaNode AdditionalProperties) : JsonSchemaNode;
