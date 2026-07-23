using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Core;

internal sealed class JsonNonCompliantGenerator(Random random, int maxRepeat)
{
    public IReadOnlyList<JsonNode?> Generate(JsonSchemaNode root, int count, CancellationToken ct)
    {
        List<Func<JsonNode?>> strategies = [];
        CollectStrategies(root, strategies);
        if (strategies.Count == 0)
        {
            throw new InvalidOperationException(
                "Schema has no violable constraints; non-compliant generation is not possible.");
        }
        var results = new JsonNode?[count];
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            results[i] = strategies[i % strategies.Count]();
        }
        return results;
    }

    private void CollectStrategies(JsonSchemaNode node, List<Func<JsonNode?>> strategies)
    {
        switch (node)
        {
            case TrueSchemaNode:
                break;

            case FalseSchemaNode:
                strategies.Add(static () => JsonValue.Create(1));
                break;

            case ConstSchemaNode c:
                strategies.Add(() => ViolateConst(c.Value));
                break;

            case EnumSchemaNode:
                strategies.Add(static () => JsonValue.Create("__invalid__"));
                break;

            case StringSchemaNode s:
                strategies.Add(static () => JsonValue.Create(42));
                if (s.MinLength is { } minLen && minLen > 0)
                {
                    strategies.Add(static () => JsonValue.Create(""));
                }
                if (s.MaxLength is { } maxLen)
                {
                    strategies.Add(() => JsonValue.Create(new string('x', maxLen + 1)));
                }
                break;

            case NumberSchemaNode n:
                strategies.Add(static () => JsonValue.Create("NaN"));
                if (n.IsInteger)
                {
                    strategies.Add(static () => JsonValue.Create(1.5));
                }
                if (n.Minimum is { } min)
                {
                    strategies.Add(() => JsonValue.Create(min - 1));
                }
                if (n.Maximum is { } max)
                {
                    strategies.Add(() => JsonValue.Create(max + 1));
                }
                if (n.ExclusiveMinimum is { } exMin)
                {
                    strategies.Add(() => JsonValue.Create(exMin));
                }
                if (n.ExclusiveMaximum is { } exMax)
                {
                    strategies.Add(() => JsonValue.Create(exMax));
                }
                break;

            case BooleanSchemaNode:
                strategies.Add(static () => JsonValue.Create("true"));
                break;

            case NullSchemaNode:
                strategies.Add(static () => JsonValue.Create(42));
                break;

            case ObjectSchemaNode o:
                strategies.Add(static () => new JsonArray());
                if (o.Required.Count > 0)
                {
                    strategies.Add(static () => new JsonObject());
                }
                foreach (var (name, propNode) in o.Properties)
                {
                    if (!o.Required.Contains(name))
                    {
                        continue;
                    }
                    var propStrategies = new List<Func<JsonNode?>>();
                    CollectStrategies(propNode, propStrategies);
                    foreach (var strategy in propStrategies)
                    {
                        var captured = (name, strategy);
                        strategies.Add(() => new JsonObject { [captured.name] = captured.strategy() });
                    }
                }
                break;

            case ArraySchemaNode a:
                strategies.Add(static () => new JsonObject());
                if (a.MinItems > 0)
                {
                    strategies.Add(static () => new JsonArray());
                }
                if (a.MaxItems is { } maxItems)
                {
                    var itemGen = new JsonValueGenerator(random, maxRepeat);
                    var targetCount = maxItems + 1;
                    strategies.Add(() =>
                    {
                        var arr = new JsonArray();
                        for (int j = 0; j < targetCount; j++)
                        {
                            arr.Add(a.Items is not null ? itemGen.Generate(a.Items) : JsonValue.Create(j));
                        }
                        return arr;
                    });
                }
                if (a.UniqueItems && a.Items is not null)
                {
                    var dupGen = new JsonValueGenerator(random, maxRepeat);
                    strategies.Add(() =>
                    {
                        var item = dupGen.Generate(a.Items);
                        var itemJson = item?.ToJsonString() ?? "null";
                        return new JsonArray(JsonNode.Parse(itemJson), JsonNode.Parse(itemJson));
                    });
                }
                break;

            case AnyOfSchemaNode anyOf:
                AddAnyOfViolation(anyOf.Schemas, strategies);
                break;

            default:
                strategies.Add(static () => JsonValue.Create("__invalid__"));
                break;
        }
    }

    private static void AddAnyOfViolation(IReadOnlyList<JsonSchemaNode> schemas, List<Func<JsonNode?>> strategies)
    {
        var allowedTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            switch (schema)
            {
                case StringSchemaNode: allowedTypes.Add("string"); break;
                case NumberSchemaNode: allowedTypes.Add("number"); break;
                case BooleanSchemaNode: allowedTypes.Add("boolean"); break;
                case NullSchemaNode: allowedTypes.Add("null"); break;
                case ObjectSchemaNode: allowedTypes.Add("object"); break;
                case ArraySchemaNode: allowedTypes.Add("array"); break;
                case FalseSchemaNode: break;
                default: return; // unknown branch — cannot guarantee a violation across all branches
            }
        }
        // Pick a value whose type is absent from every branch of the anyOf
        if (!allowedTypes.Contains("boolean")) { strategies.Add(static () => JsonValue.Create(true)); return; }
        if (!allowedTypes.Contains("array")) { strategies.Add(static () => new JsonArray()); return; }
        if (!allowedTypes.Contains("object")) { strategies.Add(static () => new JsonObject()); return; }
        if (!allowedTypes.Contains("number")) { strategies.Add(static () => JsonValue.Create(42)); return; }
        if (!allowedTypes.Contains("string")) { strategies.Add(static () => JsonValue.Create("__invalid__")); return; }
        if (!allowedTypes.Contains("null")) { strategies.Add(static () => (JsonNode?)null); return; }
        // All six JSON types are covered — nothing can violate this anyOf
    }

    private static JsonNode? ViolateConst(JsonNode? constValue)
    {
        return constValue is JsonValue v && v.TryGetValue<string>(out _)
            ? JsonValue.Create(42)
            : JsonValue.Create("__not_const__");
    }
}
