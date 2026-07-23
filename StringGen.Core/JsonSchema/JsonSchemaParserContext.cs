using System.Text.Json;

namespace StringGen.Core.JsonSchema;

internal sealed class JsonSchemaParserContext
{
    public readonly Dictionary<string, JsonElement> RawDefs = new(StringComparer.Ordinal);
    public readonly Dictionary<string, JsonSchemaNode> ParsedDefs = new(StringComparer.Ordinal);
    public readonly HashSet<string> Resolving = new(StringComparer.Ordinal);
}
