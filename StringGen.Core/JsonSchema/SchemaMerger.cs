namespace StringGen.Core.JsonSchema;

internal static class SchemaMerger
{
    public static JsonSchemaNode Merge(IReadOnlyList<JsonSchemaNode> schemas)
    {
        if (schemas.Count == 0) { return new TrueSchemaNode(); }

        if (schemas.Any(static x => x is FalseSchemaNode)) { return new FalseSchemaNode(); }

        var effective = schemas.Where(static x => x is not TrueSchemaNode).ToList();
        if (effective.Count == 0) { return new TrueSchemaNode(); }
        if (effective.Count == 1) { return effective[0]; }

        var kinds = effective.Select(static x => Kind(x)).Distinct().ToList();
        if (kinds.Count > 1)
        {
            throw new InvalidOperationException(
                $"allOf sub-schemas have incompatible types: " +
                $"{string.Join(", ", kinds.Select(static x => x.ToString()))}");
        }

        return kinds[0] switch
        {
            SchemaKind.String => MergeStrings(effective.OfType<StringSchemaNode>().ToList()),
            SchemaKind.Number => MergeNumbers(effective.OfType<NumberSchemaNode>().ToList()),
            SchemaKind.Object => MergeObjects(effective.OfType<ObjectSchemaNode>().ToList()),
            SchemaKind.Array => MergeArrays(effective.OfType<ArraySchemaNode>().ToList()),
            SchemaKind.Boolean => new BooleanSchemaNode(),
            SchemaKind.Null => new NullSchemaNode(),
            SchemaKind.Const => MergeConsts(effective.OfType<ConstSchemaNode>().ToList()),
            SchemaKind.Enum => throw new NotSupportedException(
                                      "allOf with multiple enum schemas is not supported — cannot compute intersection"),
            _ => throw new NotSupportedException(
                                      $"Cannot merge allOf for schema kind {kinds[0]}")
        };
    }

    private static StringSchemaNode MergeStrings(IList<StringSchemaNode> nodes)
    {
        var patterns = nodes.Select(static x => x.Pattern)
                            .Where(static x => x is not null)
                            .Distinct()
                            .ToList();
        if (patterns.Count > 1)
        {
            throw new InvalidOperationException(
                $"allOf sub-schemas have conflicting pattern constraints: " +
                $"{string.Join(", ", patterns.Select(static x => $"'{x}'"))}");
        }
        return new(
            MinLength: nodes.Select(static x => x.MinLength).Max(),
            MaxLength: nodes.Select(static x => x.MaxLength).Min(),
            Pattern: patterns.Count == 1 ? patterns[0] : null);
    }

    private static NumberSchemaNode MergeNumbers(IList<NumberSchemaNode> nodes) => new(
        Minimum: nodes.Select(static x => x.Minimum).Max(),
        Maximum: nodes.Select(static x => x.Maximum).Min(),
        ExclusiveMinimum: nodes.Select(static x => x.ExclusiveMinimum).Max(),
        ExclusiveMaximum: nodes.Select(static x => x.ExclusiveMaximum).Min(),
        MultipleOf: MergeMultipleOf(nodes.Select(static x => x.MultipleOf).ToList()),
        IsInteger: nodes.Any(static x => x.IsInteger));

    private static double? MergeMultipleOf(IList<double?> values)
    {
        var nonNull = values.Where(static x => x.HasValue).Select(static x => x.GetValueOrDefault()).ToList();
        if (nonNull.Count == 0) { return null; }
        if (nonNull.Distinct().Count() == 1) { return nonNull[0]; }

        if (nonNull.All(static x => x == Math.Floor(x)))
        {
            return (double)Lcm(nonNull.Select(static x => (long)x).ToList());
        }

        throw new NotSupportedException(
            "Cannot merge non-integer multipleOf constraints in allOf");
    }

    private static ObjectSchemaNode MergeObjects(IList<ObjectSchemaNode> nodes)
    {
        var props = new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            foreach (var (k, v) in node.Properties)
            {
                props[k] = v;
            }
        }

        var required = nodes
            .SelectMany(static x => x.Required)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var addl = nodes
            .Select(static x => x.AdditionalProperties)
            .OrderBy(static x => x is FalseSchemaNode ? 0 : x is TrueSchemaNode ? 2 : 1)
            .First();

        return new ObjectSchemaNode(props, required, addl);
    }

    private static ArraySchemaNode MergeArrays(IList<ArraySchemaNode> nodes) => new(
        Items: nodes.Select(static x => x.Items).FirstOrDefault(static x => x is not null),
        MinItems: nodes.Max(static x => x.MinItems),
        MaxItems: nodes.Select(static x => x.MaxItems).Min(),
        UniqueItems: nodes.Any(static x => x.UniqueItems));

    private static ConstSchemaNode MergeConsts(IList<ConstSchemaNode> nodes)
    {
        var first = nodes[0].Value?.ToJsonString() ?? "null";
        if (nodes.Any(x => (x.Value?.ToJsonString() ?? "null") != first))
        {
            throw new InvalidOperationException(
                "allOf const values are not equal — schema is unsatisfiable");
        }
        return nodes[0];
    }

    private static SchemaKind Kind(JsonSchemaNode n) => n switch
    {
        StringSchemaNode => SchemaKind.String,
        NumberSchemaNode => SchemaKind.Number,
        ObjectSchemaNode => SchemaKind.Object,
        ArraySchemaNode => SchemaKind.Array,
        BooleanSchemaNode => SchemaKind.Boolean,
        NullSchemaNode => SchemaKind.Null,
        EnumSchemaNode => SchemaKind.Enum,
        ConstSchemaNode => SchemaKind.Const,
        _ => SchemaKind.Other
    };

    private static long Gcd(long a, long b)
    {
        while (b != 0) { (a, b) = (b, a % b); }
        return Math.Abs(a);
    }

    private static long Lcm(IList<long> values)
    {
        long result = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            result = result / Gcd(result, values[i]) * values[i];
        }
        return result;
    }
}
