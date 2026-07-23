using System.Text.Json;

namespace StringGen.Core.JsonSchema;

internal sealed class JsonSchemaSource(
    JsonSchemaNode root,
    IReadOnlyDictionary<string, JsonSchemaNode> defs) : ISchemaSource
{
    public static JsonSchemaSource FromJson(string json) =>
        new JsonSchemaParser().ParseSource(json);

    public static JsonSchemaSource FromJson(JsonElement element) =>
        new JsonSchemaParser().ParseSource(element);

    public JsonSchemaNode Root => root;

    public JsonSchemaNode Resolve(string refPointer)
    {
        var name = ExtractDefinitionName(refPointer)
            ?? throw new NotSupportedException(
                $"Cannot resolve $ref '{refPointer}' — only intra-document refs " +
                "(#/$defs/... or #/definitions/...) are supported here");

        if (!defs.TryGetValue(name, out var node))
        {
            throw new ArgumentException(
                $"$ref '{refPointer}' refers to unknown definition '{name}'");
        }

        return node;
    }

    internal static string? ExtractDefinitionName(string refPointer)
    {
        if (refPointer.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            return refPointer["#/$defs/".Length..];
        }
        if (refPointer.StartsWith("#/definitions/", StringComparison.Ordinal))
        {
            return refPointer["#/definitions/".Length..];
        }
        return null;
    }
}
