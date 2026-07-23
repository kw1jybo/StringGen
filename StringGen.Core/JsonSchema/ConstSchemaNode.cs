using System.Text.Json.Nodes;

namespace StringGen.Core.JsonSchema;

internal sealed record ConstSchemaNode(JsonNode? Value) : JsonSchemaNode;
