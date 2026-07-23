using System.Text.Json;
using System.Text.Json.Nodes;

namespace StringGen.Core.JsonSchema;

internal sealed class JsonSchemaParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 128 };

    public JsonSchemaNode Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, DocumentOptions);
        return ParseRoot(doc.RootElement);
    }

    public JsonSchemaNode Parse(JsonElement element) => ParseRoot(element);

    internal JsonSchemaSource ParseSource(string json)
    {
        using var doc = JsonDocument.Parse(json, DocumentOptions);
        return BuildSource(doc.RootElement);
    }

    internal JsonSchemaSource ParseSource(JsonElement element) => BuildSource(element);

    private JsonSchemaNode ParseRoot(JsonElement root)
    {
        var ctx = PrepareContext(root);
        return ParseElement(root, ctx);
    }

    private JsonSchemaSource BuildSource(JsonElement root)
    {
        var ctx = PrepareContext(root);
        var rootNode = ParseElement(root, ctx);

        foreach (var name in ctx.RawDefs.Keys)
        {
            if (ctx.ParsedDefs.ContainsKey(name)) { continue; }
            if (!ctx.Resolving.Add(name)) { continue; }
            try { ctx.ParsedDefs[name] = ParseElement(ctx.RawDefs[name], ctx); }
            finally { ctx.Resolving.Remove(name); }
        }

        return new JsonSchemaSource(rootNode, ctx.ParsedDefs);
    }

    private static JsonSchemaParserContext PrepareContext(JsonElement root)
    {
        var ctx = new JsonSchemaParserContext();
        if (root.ValueKind != JsonValueKind.Object) { return ctx; }

        if (root.TryGetProperty("$defs", out var defs))
        {
            foreach (var p in defs.EnumerateObject())
            {
                ctx.RawDefs[p.Name] = p.Value;
            }
        }
        else if (root.TryGetProperty("definitions", out var definitions))
        {
            foreach (var p in definitions.EnumerateObject())
            {
                ctx.RawDefs[p.Name] = p.Value;
            }
        }

        return ctx;
    }

    private JsonSchemaNode ParseElement(JsonElement el, JsonSchemaParserContext ctx) =>
        el.ValueKind switch
        {
            JsonValueKind.True => new TrueSchemaNode(),
            JsonValueKind.False => new FalseSchemaNode(),
            JsonValueKind.Object => ParseSchemaObject(el, ctx),
            _ => throw new ArgumentException(
                $"Expected a JSON Schema (object or boolean), got {el.ValueKind}")
        };

    private JsonSchemaNode ParseSchemaObject(JsonElement el, JsonSchemaParserContext ctx)
    {
        if (el.TryGetProperty("$ref", out var refEl))
        {
            return ResolveRef(
                refEl.GetString() ?? throw new ArgumentException("$ref must be a string"),
                ctx);
        }

        if (el.TryGetProperty("anyOf", out var anyOf))
        {
            return new AnyOfSchemaNode(ParseSchemaArray(anyOf, ctx));
        }
        if (el.TryGetProperty("oneOf", out var oneOf))
        {
            return new OneOfSchemaNode(ParseSchemaArray(oneOf, ctx));
        }
        if (el.TryGetProperty("allOf", out var allOf))
        {
            return new AllOfSchemaNode(ParseSchemaArray(allOf, ctx));
        }

        if (el.TryGetProperty("enum", out var enumEl))
        {
            return new EnumSchemaNode(
                [.. enumEl.EnumerateArray().Select(static x => ToJsonNode(x))]);
        }
        if (el.TryGetProperty("const", out var constEl))
        {
            return new ConstSchemaNode(ToJsonNode(constEl));
        }

        if (el.TryGetProperty("type", out var typeEl))
        {
            if (typeEl.ValueKind == JsonValueKind.String)
            {
                var typeStr = typeEl.GetString()
                    ?? throw new ArgumentException("'type' string value must not be null");
                return ParseTyped(typeStr, el, ctx);
            }

            if (typeEl.ValueKind == JsonValueKind.Array)
            {
                JsonSchemaNode[] schemas = [.. typeEl.EnumerateArray()
                    .Select(x => ParseTyped(
                        x.GetString() ?? throw new ArgumentException("type array entries must be strings"),
                        el, ctx))];
                return schemas.Length == 1 ? schemas[0] : new AnyOfSchemaNode(schemas);
            }

            throw new ArgumentException($"'type' must be a string or array, got {typeEl.ValueKind}");
        }

        if (el.TryGetProperty("minimum", out _) || el.TryGetProperty("maximum", out _) ||
            el.TryGetProperty("exclusiveMinimum", out _) || el.TryGetProperty("exclusiveMaximum", out _) ||
            el.TryGetProperty("multipleOf", out _))
        {
            return ParseNumberNode(el, isInteger: false);
        }

        if (el.TryGetProperty("minLength", out _) || el.TryGetProperty("maxLength", out _) ||
            el.TryGetProperty("pattern", out _))
        {
            return ParseStringNode(el);
        }

        if (el.TryGetProperty("properties", out _) ||
            el.TryGetProperty("required", out _) ||
            el.TryGetProperty("additionalProperties", out _))
        {
            return ParseObjectNode(el, ctx);
        }

        if (el.TryGetProperty("items", out _) || el.TryGetProperty("minItems", out _) ||
            el.TryGetProperty("maxItems", out _) || el.TryGetProperty("uniqueItems", out _))
        {
            return ParseArrayNode(el, ctx);
        }

        return new TrueSchemaNode();
    }

    private JsonSchemaNode ParseTyped(string type, JsonElement el, JsonSchemaParserContext ctx) => type switch
    {
        "string" => ParseStringNode(el),
        "number" => ParseNumberNode(el, isInteger: false),
        "integer" => ParseNumberNode(el, isInteger: true),
        "boolean" => new BooleanSchemaNode(),
        "null" => new NullSchemaNode(),
        "object" => ParseObjectNode(el, ctx),
        "array" => ParseArrayNode(el, ctx),
        _ => throw new ArgumentException($"Unknown JSON Schema type '{type}'")
    };

    private static StringSchemaNode ParseStringNode(JsonElement el) => new(
        MinLength: el.TryGetProperty("minLength", out var minL) ? minL.GetInt32() : null,
        MaxLength: el.TryGetProperty("maxLength", out var maxL) ? maxL.GetInt32() : null,
        Pattern: el.TryGetProperty("pattern", out var pat) ? pat.GetString() : null);

    private static NumberSchemaNode ParseNumberNode(JsonElement el, bool isInteger) => new(
        Minimum: el.TryGetProperty("minimum", out var min) ? min.GetDouble() : null,
        Maximum: el.TryGetProperty("maximum", out var max) ? max.GetDouble() : null,
        ExclusiveMinimum: el.TryGetProperty("exclusiveMinimum", out var exMn) ? exMn.GetDouble() : null,
        ExclusiveMaximum: el.TryGetProperty("exclusiveMaximum", out var exMx) ? exMx.GetDouble() : null,
        MultipleOf: el.TryGetProperty("multipleOf", out var mul) ? mul.GetDouble() : null,
        IsInteger: isInteger);

    private ObjectSchemaNode ParseObjectNode(JsonElement el, JsonSchemaParserContext ctx)
    {
        var props = new Dictionary<string, JsonSchemaNode>(StringComparer.Ordinal);
        if (el.TryGetProperty("properties", out var propsEl))
        {
            foreach (var p in propsEl.EnumerateObject())
            {
                props[p.Name] = ParseElement(p.Value, ctx);
            }
        }

        var required = new List<string>();
        if (el.TryGetProperty("required", out var reqEl))
        {
            foreach (var r in reqEl.EnumerateArray())
            {
                required.Add(r.GetString()
                    ?? throw new ArgumentException("required entries must be strings"));
            }
        }

        JsonSchemaNode additionalProps = new TrueSchemaNode();
        if (el.TryGetProperty("additionalProperties", out var addlEl))
        {
            additionalProps = ParseElement(addlEl, ctx);
        }

        return new ObjectSchemaNode(props, required, additionalProps);
    }

    private ArraySchemaNode ParseArrayNode(JsonElement el, JsonSchemaParserContext ctx) => new(
        Items: el.TryGetProperty("items", out var items) ? ParseElement(items, ctx) : null,
        MinItems: el.TryGetProperty("minItems", out var minI) ? minI.GetInt32() : 0,
        MaxItems: el.TryGetProperty("maxItems", out var maxI) ? maxI.GetInt32() : null,
        UniqueItems: el.TryGetProperty("uniqueItems", out var uniq) && uniq.GetBoolean());

    private JsonSchemaNode[] ParseSchemaArray(JsonElement el, JsonSchemaParserContext ctx) =>
        [.. el.EnumerateArray().Select(x => ParseElement(x, ctx))];

    private JsonSchemaNode ResolveRef(string refStr, JsonSchemaParserContext ctx)
    {
        const string defsPrefix = "#/$defs/";
        const string definitionsPrefix = "#/definitions/";

        string? name = null;
        if (refStr.StartsWith(defsPrefix, StringComparison.Ordinal))
        {
            name = refStr[defsPrefix.Length..];
        }
        else if (refStr.StartsWith(definitionsPrefix, StringComparison.Ordinal))
        {
            name = refStr[definitionsPrefix.Length..];
        }

        if (name is null)
        {
            return new RefSchemaNode(refStr);
        }

        if (ctx.ParsedDefs.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (!ctx.RawDefs.TryGetValue(name, out var rawDef))
        {
            throw new ArgumentException($"$ref '{refStr}' refers to unknown definition '{name}'");
        }

        if (!ctx.Resolving.Add(name))
        {
            throw new ArgumentException($"Circular $ref detected while resolving '{refStr}'");
        }

        try
        {
            var node = ParseElement(rawDef, ctx);
            ctx.ParsedDefs[name] = node;
            return node;
        }
        finally
        {
            ctx.Resolving.Remove(name);
        }
    }

    private static JsonNode? ToJsonNode(JsonElement el) =>
        el.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(el.GetRawText());
}
