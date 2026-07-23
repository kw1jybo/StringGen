using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Core.Swagger;

public sealed class SwaggerGenerator
{
    private readonly bool isOpenApi;
    private readonly Random random;
    private readonly int maxRepeat;
    private readonly JsonElement documentRoot;

    private readonly IReadOnlyDictionary<string, JsonSchemaNode> namedSchemas;

    private readonly ConcurrentDictionary<(string method, string path), JsonSchemaNode> requestCache = new();
    private readonly ConcurrentDictionary<(string method, string path, string statusCode), JsonSchemaNode> responseCache = new();

    public SwaggerGenerator(string documentJson, int? seed = null, int maxRepeat = 5)
    {
        ArgumentNullException.ThrowIfNull(documentJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRepeat);
        random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        this.maxRepeat = maxRepeat;

        using var doc = JsonDocument.Parse(documentJson);
        isOpenApi = doc.RootElement.TryGetProperty("openapi", out _);
        documentRoot = doc.RootElement.Clone();

        if (isOpenApi)
        {
            var source = new OpenApiParser().Parse(doc.RootElement);
            namedSchemas = BuildDict(source.SchemaNames,
                name => source.Resolve($"#/components/schemas/{name}"));
        }
        else
        {
            var source = new SwaggerParser().Parse(doc.RootElement);
            namedSchemas = BuildDict(source.DefinitionNames,
                name => source.Resolve($"#/definitions/{name}"));
        }
    }

    public IReadOnlyList<string> GenerateSchema(string schemaName, int count = 1, CancellationToken ct = default, GenerationMode mode = GenerationMode.Compliant)
    {
        ArgumentNullException.ThrowIfNull(schemaName);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!namedSchemas.TryGetValue(schemaName, out var node))
        {
            throw new ArgumentException(
                $"Schema '{schemaName}' not found in document", nameof(schemaName));
        }
        return GenerateFromNode(node, count, ct, mode);
    }

    public IReadOnlyList<string> GenerateRequestBody(
        string method, string path, int count = 1, CancellationToken ct = default, GenerationMode mode = GenerationMode.Compliant)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var key = (method.ToLowerInvariant(), path);
        var root = requestCache.GetOrAdd(key, k =>
            isOpenApi
                ? new OpenApiParser().ParseRequestBody(documentRoot, k.path, k.method).Root
                : new SwaggerParser().ParsePathBody(documentRoot, k.path, k.method).Root);
        return GenerateFromNode(root, count, ct, mode);
    }

    public IReadOnlyList<string> GenerateResponseBody(
        string method, string path, string statusCode = "200", int count = 1, CancellationToken ct = default, GenerationMode mode = GenerationMode.Compliant)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var key = (method.ToLowerInvariant(), path, statusCode);
        var root = responseCache.GetOrAdd(key, k =>
            isOpenApi
                ? new OpenApiParser().ParseResponse(documentRoot, k.path, k.method, k.statusCode).Root
                : new SwaggerParser().ParsePathResponse(documentRoot, k.path, k.method, k.statusCode).Root);
        return GenerateFromNode(root, count, ct, mode);
    }

    private IReadOnlyList<string> GenerateFromNode(JsonSchemaNode node, int count, CancellationToken ct, GenerationMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (mode == GenerationMode.NonCompliant && count > 0)
        {
            var nonCompliantGen = new JsonNonCompliantGenerator(random, maxRepeat);
            return [.. nonCompliantGen.Generate(node, count, ct)
                .Select(x => x?.ToJsonString(JsonDefaults.Indented) ?? "null")];
        }
        var gen = new JsonValueGenerator(random, maxRepeat);
        var results = new string[count];
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var value = gen.Generate(node);
            results[i] = value?.ToJsonString(JsonDefaults.Indented) ?? "null";
        }
        return results;
    }

    private static IReadOnlyDictionary<string, JsonSchemaNode> BuildDict(
        IEnumerable<string> names, Func<string, JsonSchemaNode> resolve)
    {
        var dict = new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            dict[name] = resolve(name);
        }
        return dict;
    }
}
