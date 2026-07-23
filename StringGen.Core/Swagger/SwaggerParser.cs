using System.Text.Json;
using StringGen.Core.JsonSchema;

namespace StringGen.Core.Swagger;

internal sealed class SwaggerParser
{
    public SwaggerSchemaSource Parse(string json, string? rootDefinition = null)
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        return Parse(doc.RootElement, rootDefinition);
    }

    public SwaggerSchemaSource Parse(JsonElement document, string? rootDefinition = null)
    {
        ValidateVersion(document);

        var definitions = ParseDefinitions(document);

        JsonSchemaNode root = rootDefinition is not null
            ? (definitions.TryGetValue(rootDefinition, out var def) ? def
               : throw new ArgumentException(
                   $"Definition '{rootDefinition}' not found in Swagger document"))
            : new TrueSchemaNode();

        return new SwaggerSchemaSource(root, definitions);
    }

    public SwaggerSchemaSource ParsePathBody(
        string json, string path, string method)
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        ValidateVersion(doc.RootElement);

        var definitions = ParseDefinitions(doc.RootElement);
        var schema = FindPathBodySchema(doc.RootElement, path, method, definitions)
                          ?? new TrueSchemaNode();

        return new SwaggerSchemaSource(schema, definitions);
    }

    public SwaggerSchemaSource ParsePathResponse(
        string json, string path, string method, string statusCode)
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        ValidateVersion(doc.RootElement);

        var definitions = ParseDefinitions(doc.RootElement);
        var schema = FindPathResponseSchema(doc.RootElement, path, method, statusCode, definitions)
                          ?? new TrueSchemaNode();

        return new SwaggerSchemaSource(schema, definitions);
    }

    public SwaggerSchemaSource ParsePathBody(JsonElement document, string path, string method)
    {
        ValidateVersion(document);
        var definitions = ParseDefinitions(document);
        var schema = FindPathBodySchema(document, path, method, definitions)
                          ?? new TrueSchemaNode();
        return new SwaggerSchemaSource(schema, definitions);
    }

    public SwaggerSchemaSource ParsePathResponse(
        JsonElement document, string path, string method, string statusCode)
    {
        ValidateVersion(document);
        var definitions = ParseDefinitions(document);
        var schema = FindPathResponseSchema(document, path, method, statusCode, definitions)
                          ?? new TrueSchemaNode();
        return new SwaggerSchemaSource(schema, definitions);
    }

    private static void ValidateVersion(JsonElement document)
    {
        if (!document.TryGetProperty("swagger", out var ver) || ver.GetString() != "2.0")
        {
            throw new ArgumentException(
                "Document is not a valid Swagger 2.0 document " +
                "(expected top-level 'swagger' property with value '2.0')");
        }
    }

    private static IReadOnlyDictionary<string, JsonSchemaNode> ParseDefinitions(JsonElement document)
    {
        if (!document.TryGetProperty("definitions", out var defs))
        {
            return new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        }

        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        writer.WriteStartObject();
        writer.WritePropertyName("definitions");
        defs.WriteTo(writer);
        writer.WriteEndObject();
        writer.Flush();

        ms.Position = 0;
        using var syntheticDoc = JsonDocument.Parse(ms, JsonDefaults.Document);
        var source = new JsonSchemaParser().ParseSource(syntheticDoc.RootElement);

        var result = new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        foreach (var prop in defs.EnumerateObject())
        {
            result[prop.Name] = source.Resolve($"#/definitions/{prop.Name}");
        }

        return result;
    }

    private static JsonSchemaNode? FindPathBodySchema(
        JsonElement document, string path, string method,
        IReadOnlyDictionary<string, JsonSchemaNode> definitions)
    {
        if (!TryGetOperation(document, path, method, out var op)) { return null; }

        if (!op.TryGetProperty("parameters", out var parameters)) { return null; }

        foreach (var p in parameters.EnumerateArray())
        {
            if (p.TryGetProperty("in", out var loc) && loc.GetString() == "body" &&
                p.TryGetProperty("schema", out var schema))
            {
                return ResolveSchemaElement(schema, definitions, document);
            }
        }
        return null;
    }

    private static JsonSchemaNode? FindPathResponseSchema(
        JsonElement document, string path, string method, string statusCode,
        IReadOnlyDictionary<string, JsonSchemaNode> definitions)
    {
        if (!TryGetOperation(document, path, method, out var op)) { return null; }

        if (!op.TryGetProperty("responses", out var responses)) { return null; }

        if (responses.TryGetProperty(statusCode, out var response) &&
            response.TryGetProperty("schema", out var schema))
        {
            return ResolveSchemaElement(schema, definitions, document);
        }

        return null;
    }

    private static bool TryGetOperation(
        JsonElement document, string path, string method, out JsonElement operation)
    {
        operation = default;
        return document.TryGetProperty("paths", out var paths) &&
               paths.TryGetProperty(path, out var pathItem) &&
               pathItem.TryGetProperty(method.ToLowerInvariant(), out operation);
    }

    private static JsonSchemaNode ResolveSchemaElement(
        JsonElement schemaEl,
        IReadOnlyDictionary<string, JsonSchemaNode> definitions,
        JsonElement document)
    {
        if (schemaEl.ValueKind == JsonValueKind.Object &&
            schemaEl.TryGetProperty("$ref", out var refEl))
        {
            var refStr = refEl.GetString()
                ?? throw new ArgumentException("$ref value must be a string");
            const string prefix = "#/definitions/";
            if (!refStr.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Swagger source only supports '#/definitions/...' refs, got '{refStr}'");
            }
            var name = refStr[prefix.Length..];
            if (!definitions.TryGetValue(name, out var node))
            {
                throw new ArgumentException(
                    $"$ref '{refStr}' refers to unknown definition '{name}'");
            }
            return node;
        }

        if (document.TryGetProperty("definitions", out var rawDefs))
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WritePropertyName("definitions");
            rawDefs.WriteTo(writer);
            foreach (var prop in schemaEl.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();
            ms.Position = 0;
            using var inlineDoc = JsonDocument.Parse(ms, JsonDefaults.Document);
            return new JsonSchemaParser().Parse(inlineDoc.RootElement);
        }

        return new JsonSchemaParser().Parse(schemaEl);
    }
}
