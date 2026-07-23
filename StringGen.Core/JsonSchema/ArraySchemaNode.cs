namespace StringGen.Core.JsonSchema;

internal sealed record ArraySchemaNode(
    JsonSchemaNode? Items = null,
    int MinItems = 0,
    int? MaxItems = null,
    bool UniqueItems = false) : JsonSchemaNode;
