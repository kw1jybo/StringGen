namespace StringGen.Core.JsonSchema;

internal sealed record AnyOfSchemaNode(IReadOnlyList<JsonSchemaNode> Schemas) : JsonSchemaNode;
