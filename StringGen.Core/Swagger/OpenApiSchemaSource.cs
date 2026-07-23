using StringGen.Core.JsonSchema;

namespace StringGen.Core.Swagger;

internal sealed class OpenApiSchemaSource(
    JsonSchemaNode root,
    IReadOnlyDictionary<string, JsonSchemaNode> schemas) : ISchemaSource
{
    private const string Prefix = "#/components/schemas/";

    public static OpenApiSchemaSource FromJson(string json, string? rootSchema = null) =>
        new OpenApiParser().Parse(json, rootSchema);

    public JsonSchemaNode Root => root;

    public IEnumerable<string> SchemaNames => schemas.Keys;

    public JsonSchemaNode Resolve(string refPointer)
    {
        if (!refPointer.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"OpenAPI source only resolves '{Prefix}...' pointers, got '{refPointer}'");
        }

        var name = refPointer[Prefix.Length..];
        if (!schemas.TryGetValue(name, out var node))
        {
            throw new ArgumentException(
                $"$ref '{refPointer}' refers to unknown schema '{name}'");
        }

        return node;
    }

    public OpenApiSchemaSource WithRoot(string schemaName) =>
        new(Resolve($"{Prefix}{schemaName}"), schemas);
}
