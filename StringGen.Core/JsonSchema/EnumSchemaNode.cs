using System.Text.Json.Nodes;

namespace StringGen.Core.JsonSchema;

internal sealed record EnumSchemaNode(IReadOnlyList<JsonNode?> Values) : JsonSchemaNode;
