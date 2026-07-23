namespace StringGen.Core.JsonSchema;

internal sealed record AllOfSchemaNode(IReadOnlyList<JsonSchemaNode> Schemas) : JsonSchemaNode;
