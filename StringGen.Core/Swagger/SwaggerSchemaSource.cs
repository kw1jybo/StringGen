using StringGen.Core.JsonSchema;

namespace StringGen.Core.Swagger;

internal sealed class SwaggerSchemaSource(
    JsonSchemaNode root,
    IReadOnlyDictionary<string, JsonSchemaNode> definitions) : ISchemaSource
{
    private const string Prefix = "#/definitions/";

    public static SwaggerSchemaSource FromJson(string json, string? rootDefinition = null) =>
        new SwaggerParser().Parse(json, rootDefinition);

    public JsonSchemaNode Root => root;

    public IEnumerable<string> DefinitionNames => definitions.Keys;

    public JsonSchemaNode Resolve(string refPointer)
    {
        if (!refPointer.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Swagger source only resolves '{Prefix}...' pointers, got '{refPointer}'");
        }

        var name = refPointer[Prefix.Length..];
        if (!definitions.TryGetValue(name, out var node))
        {
            throw new ArgumentException(
                $"$ref '{refPointer}' refers to unknown definition '{name}'");
        }

        return node;
    }

    public SwaggerSchemaSource WithRoot(string definitionName) =>
        new(Resolve($"{Prefix}{definitionName}"), definitions);
}
