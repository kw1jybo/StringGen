using System.Text;
using System.Text.Json.Nodes;

namespace StringGen.Core.JsonSchema;

internal sealed class JsonValueGenerator(Random random, int maxRepeat = 5)
{
    private const int MaxDepth = 50;
    private const int MaxArrayItems = 10_000;

    public JsonNode? Generate(JsonSchemaNode schema) => Generate(schema, 0);

    private JsonNode? Generate(JsonSchemaNode schema, int depth)
    {
        if (depth >= MaxDepth)
        {
            throw new InvalidOperationException(
                $"Schema nesting exceeded the maximum depth of {MaxDepth}");
        }

        return schema switch
        {
            TrueSchemaNode => GenerateAny(),
            FalseSchemaNode => throw new InvalidOperationException(
                                           "Cannot generate a value for a 'false' schema"),
            StringSchemaNode n => JsonValue.Create(GenerateString(n)),
            NumberSchemaNode n => GenerateNumber(n),
            BooleanSchemaNode => GenerateBoolean(),
            NullSchemaNode => null,
            EnumSchemaNode n => GenerateEnum(n),
            ConstSchemaNode n => n.Value?.DeepClone(),
            ObjectSchemaNode n => GenerateObject(n, depth + 1),
            ArraySchemaNode n => GenerateArray(n, depth + 1),
            AnyOfSchemaNode n => Generate(n.Schemas[random.Next(n.Schemas.Count)], depth + 1),
            OneOfSchemaNode n => Generate(n.Schemas[random.Next(n.Schemas.Count)], depth + 1),
            AllOfSchemaNode n => Generate(SchemaMerger.Merge(n.Schemas), depth + 1),
            RefSchemaNode n => throw new NotSupportedException(
                                           $"Unresolved $ref '{n.Reference}' — resolve via ISchemaSource first"),
            _ => throw new NotSupportedException(
                                           $"Unknown schema node type: {schema.GetType().Name}")
        };
    }

    private string GenerateString(StringSchemaNode node)
    {
        if (node.Pattern is null)
        {
            int min = node.MinLength ?? (node.MaxLength.HasValue ? 0 : 5);
            int max = node.MaxLength ?? Math.Max(min, 20);
            if (min > max)
            {
                throw new InvalidOperationException(
                    $"String minLength ({min}) > maxLength ({max})");
            }
            int len = random.Next(min, max + 1);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                sb.Append(CharSets.Printable[random.Next(CharSets.Printable.Length)]);
            }
            return sb.ToString();
        }

        var ast = new RegexParser(node.Pattern).Parse();
        var gen = new RegexStringGenerator(random, maxRepeat);

        if (node.MinLength is null && node.MaxLength is null)
        {
            return gen.Generate(ast);
        }

        for (int attempt = 0; attempt < 1000; attempt++)
        {
            var s = gen.Generate(ast);
            if ((node.MinLength is null || s.Length >= node.MinLength.Value) &&
                (node.MaxLength is null || s.Length <= node.MaxLength.Value))
            {
                return s;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a string matching '{node.Pattern}' with length in " +
            $"[{node.MinLength?.ToString() ?? "0"}, {node.MaxLength?.ToString() ?? "∞"}] " +
            "after 1000 attempts");
    }

    private JsonNode GenerateNumber(NumberSchemaNode node) =>
        node.IsInteger ? GenerateInteger(node) : GenerateDouble(node);

    private JsonValue GenerateInteger(NumberSchemaNode node)
    {
        long min = node.Minimum.HasValue ? (long)node.Minimum.Value : 0L;
        long max = node.Maximum.HasValue ? (long)node.Maximum.Value : 1000L;

        if (node.ExclusiveMinimum.HasValue)
        {
            min = Math.Max(min, (long)Math.Floor(node.ExclusiveMinimum.Value) + 1);
        }
        if (node.ExclusiveMaximum.HasValue)
        {
            max = Math.Min(max, (long)Math.Ceiling(node.ExclusiveMaximum.Value) - 1);
        }

        if (min > max)
        {
            throw new InvalidOperationException(
                $"Integer constraints produce empty range [{min}, {max}]");
        }

        if (node.MultipleOf.HasValue)
        {
            long step = (long)Math.Abs(node.MultipleOf.Value);
            if (step == 0) throw new ArgumentException("multipleOf must be non-zero");

            long firstRem = ((min % step) + step) % step;
            long first = firstRem == 0 ? min : min + (step - firstRem);
            long lastRem = ((max % step) + step) % step;
            long last = max - lastRem;

            if (first > last)
            {
                throw new InvalidOperationException(
                    $"No multiple of {step} exists in range [{min}, {max}]");
            }

            long count = (last - first) / step + 1;
            return JsonValue.Create(first + random.NextInt64(count) * step);
        }

        return JsonValue.Create(random.NextInt64(min, max + 1));
    }

    private JsonValue GenerateDouble(NumberSchemaNode node)
    {
        double min = node.Minimum ?? 0.0;
        double max = node.Maximum ?? 1000.0;

        if (node.ExclusiveMinimum.HasValue) { min = Math.Max(min, node.ExclusiveMinimum.Value) + 1e-9; }
        if (node.ExclusiveMaximum.HasValue) { max = Math.Min(max, node.ExclusiveMaximum.Value) - 1e-9; }

        if (min > max)
        {
            throw new InvalidOperationException(
                $"Number constraints produce empty range [{min}, {max}]");
        }

        double value = min + random.NextDouble() * (max - min);

        if (node.MultipleOf.HasValue)
        {
            double step = Math.Abs(node.MultipleOf.Value);
            value = Math.Round(value / step) * step;
            value = Math.Clamp(value, min, max);
        }

        return JsonValue.Create(value);
    }

    private JsonValue GenerateBoolean() => JsonValue.Create(random.Next(2) == 0);

    private JsonNode? GenerateEnum(EnumSchemaNode node)
    {
        if (node.Values.Count == 0)
        {
            throw new InvalidOperationException("enum schema has no values to pick from");
        }
        return node.Values[random.Next(node.Values.Count)]?.DeepClone();
    }

    private JsonArray GenerateArray(ArraySchemaNode node, int depth)
    {
        int min = node.MinItems;
        if (min > MaxArrayItems)
        {
            throw new InvalidOperationException(
                $"Array minItems ({min}) exceeds the safety limit of {MaxArrayItems}");
        }

        int max = node.MaxItems.HasValue
            ? Math.Min(node.MaxItems.Value, MaxArrayItems)
            : Math.Max(min, 5);

        if (min > max)
        {
            throw new InvalidOperationException(
                $"Array minItems ({min}) > maxItems ({max})");
        }

        int count = random.Next(min, max + 1);

        if (!node.UniqueItems)
        {
            var arr = new JsonArray();
            for (int i = 0; i < count; i++)
            {
                arr.Add(GenerateItem(node.Items, depth));
            }
            return arr;
        }

        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int maxTries = Math.Max(count * 100, 1000);

        for (int attempt = 0; result.Count < count; attempt++)
        {
            if (attempt >= maxTries)
            {
                throw new InvalidOperationException(
                    $"Could not generate {count} unique array items after {maxTries} attempts");
            }

            var item = GenerateItem(node.Items, depth);
            var key = item?.ToJsonString() ?? "null";
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private JsonNode? GenerateItem(JsonSchemaNode? itemSchema, int depth) =>
        itemSchema is not null ? Generate(itemSchema, depth) : GenerateAny();

    private JsonObject GenerateObject(ObjectSchemaNode node, int depth)
    {
        var obj = new JsonObject();
        var required = new HashSet<string>(node.Required, StringComparer.Ordinal);

        foreach (var (name, schema) in node.Properties)
        {
            if (required.Contains(name) || random.Next(2) == 0)
            {
                obj[name] = Generate(schema, depth);
            }
        }

        if (node.AdditionalProperties is not FalseSchemaNode)
        {
            int extras = random.Next(4);
            for (int i = 0; i < extras; i++)
            {
                var key = GenerateUniqueKey(obj);
                var schema = node.AdditionalProperties is TrueSchemaNode
                             ? null : node.AdditionalProperties;
                obj[key] = schema is not null ? Generate(schema, depth) : GenerateAny();
            }
        }

        return obj;
    }

    private string GenerateUniqueKey(JsonObject existing)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var key = GenerateObjectKey();
            if (!existing.ContainsKey(key)) { return key; }
        }
        return "k" + random.Next(int.MaxValue).ToString("x8");
    }

    private string GenerateObjectKey()
    {
        const string alpha = "abcdefghijklmnopqrstuvwxyz";
        int len = random.Next(3, 9);
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++)
        {
            sb.Append(alpha[random.Next(alpha.Length)]);
        }
        return sb.ToString();
    }

    private JsonNode? GenerateAny() => random.Next(4) switch
    {
        0 => JsonValue.Create(GenerateString(new StringSchemaNode())),
        1 => GenerateInteger(new NumberSchemaNode(IsInteger: true)),
        2 => GenerateBoolean(),
        _ => null
    };
}
