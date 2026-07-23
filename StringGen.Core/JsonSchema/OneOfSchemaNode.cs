namespace StringGen.Core.JsonSchema;

internal sealed record OneOfSchemaNode(IReadOnlyList<JsonSchemaNode> Schemas) : JsonSchemaNode;
