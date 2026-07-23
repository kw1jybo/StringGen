namespace StringGen.Core.JsonSchema;

internal sealed record StringSchemaNode(
    int? MinLength = null,
    int? MaxLength = null,
    string? Pattern = null) : JsonSchemaNode;
