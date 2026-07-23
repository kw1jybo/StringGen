using System.Text.Json;
using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Core.Swagger;

internal sealed class OpenApiParser
{
    public OpenApiSchemaSource Parse(string json, string? rootSchema = null)
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        return Parse(doc.RootElement, rootSchema);
    }

    public OpenApiSchemaSource Parse(JsonElement document, string? rootSchema = null)
    {
        var version = ValidateVersion(document);
        bool is30 = version is { Major: 3, Minor: 0 };

        var schemas = ParseComponentSchemas(document, is30);

        JsonSchemaNode root = rootSchema is not null
            ? (schemas.TryGetValue(rootSchema, out var s) ? s
               : throw new ArgumentException(
                   $"Schema '{rootSchema}' not found in OpenAPI document"))
            : new TrueSchemaNode();

        return new OpenApiSchemaSource(root, schemas);
    }

    public OpenApiSchemaSource ParseRequestBody(
        string json, string path, string method,
        string mediaType = "application/json")
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        var version = ValidateVersion(doc.RootElement);
        bool is30 = version is { Major: 3, Minor: 0 };
        var schemas = ParseComponentSchemas(doc.RootElement, is30);
        var schemaEl = FindRequestBodySchema(doc.RootElement, path, method, mediaType);
        var root = schemaEl.HasValue
                          ? ResolveInlineOrRef(schemaEl.Value, schemas, is30)
                          : new TrueSchemaNode();

        return new OpenApiSchemaSource(root, schemas);
    }

    public OpenApiSchemaSource ParseResponse(
        string json, string path, string method, string statusCode,
        string mediaType = "application/json")
    {
        using var doc = JsonDocument.Parse(json, JsonDefaults.Document);
        var version = ValidateVersion(doc.RootElement);
        bool is30 = version is { Major: 3, Minor: 0 };
        var schemas = ParseComponentSchemas(doc.RootElement, is30);
        var schemaEl = FindResponseSchema(doc.RootElement, path, method, statusCode, mediaType);
        var root = schemaEl.HasValue
                          ? ResolveInlineOrRef(schemaEl.Value, schemas, is30)
                          : new TrueSchemaNode();

        return new OpenApiSchemaSource(root, schemas);
    }

    public OpenApiSchemaSource ParseRequestBody(
        JsonElement document, string path, string method,
        string mediaType = "application/json")
    {
        var version = ValidateVersion(document);
        bool is30 = version is { Major: 3, Minor: 0 };
        var schemas = ParseComponentSchemas(document, is30);
        var schemaEl = FindRequestBodySchema(document, path, method, mediaType);
        var root = schemaEl.HasValue
                       ? ResolveInlineOrRef(schemaEl.Value, schemas, is30)
                       : new TrueSchemaNode();
        return new OpenApiSchemaSource(root, schemas);
    }

    public OpenApiSchemaSource ParseResponse(
        JsonElement document, string path, string method, string statusCode,
        string mediaType = "application/json")
    {
        var version = ValidateVersion(document);
        bool is30 = version is { Major: 3, Minor: 0 };
        var schemas = ParseComponentSchemas(document, is30);
        var schemaEl = FindResponseSchema(document, path, method, statusCode, mediaType);
        var root = schemaEl.HasValue
                       ? ResolveInlineOrRef(schemaEl.Value, schemas, is30)
                       : new TrueSchemaNode();
        return new OpenApiSchemaSource(root, schemas);
    }

    private static OasVersion ValidateVersion(JsonElement document)
    {
        if (!document.TryGetProperty("openapi", out var ver))
        {
            throw new ArgumentException(
                "Document is not an OpenAPI 3.x document (missing 'openapi' field)");
        }

        var vStr = ver.GetString() ?? "";
        var parts = vStr.Split('.');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out int major) && major == 3 &&
            int.TryParse(parts[1], out int minor))
        {
            return new OasVersion(major, minor);
        }

        throw new ArgumentException(
            $"Document is not an OpenAPI 3.x document (got 'openapi: \"{vStr}\"')");
    }

    private static IReadOnlyDictionary<string, JsonSchemaNode> ParseComponentSchemas(
        JsonElement document, bool is30)
    {
        if (!document.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var rawSchemas))
        {
            return new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        }

        var defsNode = new JsonObject();
        foreach (var prop in rawSchemas.EnumerateObject())
        {
            defsNode[prop.Name] = NormalizeSchema(
                JsonNode.Parse(prop.Value.GetRawText()), is30);
        }

        var synthetic = new JsonObject { ["$defs"] = defsNode };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(synthetic);
        using var syntheticDoc = JsonDocument.Parse(bytes, JsonDefaults.Document);
        var source = new JsonSchemaParser().ParseSource(syntheticDoc.RootElement);

        var result = new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        foreach (var prop in rawSchemas.EnumerateObject())
        {
            result[prop.Name] = source.Resolve($"#/$defs/{prop.Name}");
        }

        return result;
    }

    private static JsonNode? NormalizeSchema(JsonNode? node, bool is30)
    {
        if (node is not JsonObject obj) { return node?.DeepClone(); }

        var result = new JsonObject();
        bool nullable = false;

        foreach (var (key, val) in obj)
        {
            switch (key)
            {
                case "nullable" when is30:
                    nullable = val?.GetValue<bool>() ?? false;
                    break;

                case "$ref":
                    var refStr = val?.GetValue<string>() ?? "";
                    result["$ref"] = JsonValue.Create(
                        refStr.Replace("#/components/schemas/", "#/$defs/",
                                       StringComparison.Ordinal));
                    break;

                case "items":
                case "additionalProperties":
                case "not":
                case "if":
                case "then":
                case "else":
                case "contains":
                    result[key] = NormalizeSchema(val, is30);
                    break;

                case "properties":
                case "patternProperties":
                case "$defs":
                case "definitions":
                    result[key] = NormalizeSchemaMap(val as JsonObject, is30);
                    break;

                case "anyOf":
                case "oneOf":
                case "allOf":
                    result[key] = NormalizeSchemaArray(val as JsonArray, is30);
                    break;

                default:
                    result[key] = val?.DeepClone();
                    break;
            }
        }

        if (nullable)
        {
            return new JsonObject
            {
                ["anyOf"] = new JsonArray
                {
                    result,
                    new JsonObject { ["type"] = JsonValue.Create("null") }
                }
            };
        }

        return result;
    }

    private static JsonObject NormalizeSchemaMap(JsonObject? map, bool is30)
    {
        var result = new JsonObject();
        if (map is null) { return result; }
        foreach (var (key, val) in map)
        {
            result[key] = NormalizeSchema(val, is30);
        }
        return result;
    }

    private static JsonArray NormalizeSchemaArray(JsonArray? arr, bool is30)
    {
        var result = new JsonArray();
        if (arr is null) { return result; }
        foreach (var item in arr)
        {
            result.Add(NormalizeSchema(item, is30));
        }
        return result;
    }

    private static JsonElement? FindRequestBodySchema(
        JsonElement document, string path, string method, string mediaType)
    {
        if (!TryGetOperation(document, path, method, out var op)) { return null; }
        if (!op.TryGetProperty("requestBody", out var body)) { return null; }
        return GetContentSchema(body, mediaType);
    }

    private static JsonElement? FindResponseSchema(
        JsonElement document, string path, string method,
        string statusCode, string mediaType)
    {
        if (!TryGetOperation(document, path, method, out var op)) { return null; }
        if (!op.TryGetProperty("responses", out var responses)) { return null; }
        if (!responses.TryGetProperty(statusCode, out var response)) { return null; }
        return GetContentSchema(response, mediaType);
    }

    private static JsonElement? GetContentSchema(JsonElement parent, string mediaType)
    {
        if (!parent.TryGetProperty("content", out var content)) { return null; }
        if (!content.TryGetProperty(mediaType, out var media)) { return null; }
        if (!media.TryGetProperty("schema", out var schema)) { return null; }
        return schema;
    }

    private static bool TryGetOperation(
        JsonElement document, string path, string method, out JsonElement operation)
    {
        operation = default;
        return document.TryGetProperty("paths", out var paths) &&
               paths.TryGetProperty(path, out var pathItem) &&
               pathItem.TryGetProperty(method.ToLowerInvariant(), out operation);
    }

    private static JsonSchemaNode ResolveInlineOrRef(
        JsonElement schemaEl,
        IReadOnlyDictionary<string, JsonSchemaNode> schemas,
        bool is30)
    {
        if (schemaEl.ValueKind == JsonValueKind.Object &&
            schemaEl.TryGetProperty("$ref", out var refEl))
        {
            var refStr = refEl.GetString()
                ?? throw new ArgumentException("$ref value must be a string");
            const string prefix = "#/components/schemas/";
            if (!refStr.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"OpenAPI source only supports '{prefix}...' refs, got '{refStr}'");
            }
            var name = refStr[prefix.Length..];
            return schemas.TryGetValue(name, out var node) ? node
                : throw new ArgumentException(
                    $"$ref '{refStr}' refers to unknown schema '{name}'");
        }

        var normalised = NormalizeSchema(JsonNode.Parse(schemaEl.GetRawText()), is30);
        if (normalised is null)
        {
            return new JsonSchemaParser().Parse(schemaEl);
        }
        var inlineBytes = JsonSerializer.SerializeToUtf8Bytes(normalised);
        using var inlineDoc = JsonDocument.Parse(inlineBytes, JsonDefaults.Document);
        return new JsonSchemaParser().Parse(inlineDoc.RootElement);
    }
}
