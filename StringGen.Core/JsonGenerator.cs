using System.Text.Json;
using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Core;

public sealed class JsonGenerator(string schemaJson, int? seed = null, int maxRepeat = 5)
{
    private readonly JsonSchemaNode root = ParseAndValidate(schemaJson, maxRepeat);
    private readonly JsonValueGenerator valueGen = new JsonValueGenerator(
        seed.HasValue ? new Random(seed.Value) : Random.Shared, maxRepeat);
    private readonly JsonNonCompliantGenerator nonCompliantGen = new JsonNonCompliantGenerator(
        seed.HasValue ? new Random(seed.Value) : Random.Shared, maxRepeat);

    public JsonGenerator(JsonElement schema, int? seed = null, int maxRepeat = 5)
        : this(schema.GetRawText(), seed, maxRepeat) { }

    private static JsonSchemaNode ParseAndValidate(string schemaJson, int maxRepeat)
    {
        ArgumentNullException.ThrowIfNull(schemaJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRepeat);
        return new JsonSchemaParser().Parse(schemaJson);
    }

    public IReadOnlyList<JsonNode?> Generate(int count, CancellationToken ct = default, GenerationMode mode = GenerationMode.Compliant)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (mode == GenerationMode.NonCompliant && count > 0)
        {
            return nonCompliantGen.Generate(root, count, ct);
        }
        var results = new JsonNode?[count];
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            results[i] = valueGen.Generate(root);
        }
        return results;
    }

    public IReadOnlyList<string> GenerateJson(int count, JsonSerializerOptions? options = null, GenerationMode mode = GenerationMode.Compliant, CancellationToken ct = default)
    {
        options ??= JsonDefaults.Indented;
        return [.. Generate(count, ct, mode).Select(x => x?.ToJsonString(options) ?? "null")];
    }
}
