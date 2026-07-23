using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Tests;

public class JsonValueGeneratorTests
{
    private static JsonValueGenerator Gen(int seed = 42) =>
        new(new Random(seed));

    private static JsonSchemaNode Parse(string json) =>
        new JsonSchemaParser().Parse(json);

    private static T Val<T>(JsonNode? node) =>
        Assert.IsAssignableFrom<JsonValue>(node).GetValue<T>();

    [Fact]
    public void Generate_StringNoConstraints_LengthBetween5And20()
    {
        var g = Gen();
        for (int i = 0; i < 50; i++)
        {
            var s = Val<string>(g.Generate(new StringSchemaNode()));
            Assert.InRange(s.Length, 5, 20);
        }
    }

    [Fact]
    public void Generate_StringMinAndMax_LengthInRange()
    {
        var g = Gen();
        var node = new StringSchemaNode(MinLength: 3, MaxLength: 7);
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<string>(g.Generate(node)).Length, 3, 7);
        }
    }

    [Fact]
    public void Generate_StringMinOnly_AtLeastMin()
    {
        var g = Gen();
        var node = new StringSchemaNode(MinLength: 10);
        for (int i = 0; i < 20; i++)
        {
            Assert.True(Val<string>(g.Generate(node)).Length >= 10);
        }
    }

    [Fact]
    public void Generate_StringMaxOnly_AtMostMax()
    {
        var g = Gen();
        var node = new StringSchemaNode(MaxLength: 3);
        for (int i = 0; i < 20; i++)
        {
            Assert.True(Val<string>(g.Generate(node)).Length <= 3);
        }
    }

    [Fact]
    public void Generate_StringMinGreaterThanMax_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new StringSchemaNode(MinLength: 10, MaxLength: 5)));

    [Fact]
    public void Generate_StringWithPattern_MatchesPattern()
    {
        var g = Gen();
        var node = new StringSchemaNode(Pattern: @"^\d{4}$");
        for (int i = 0; i < 20; i++)
        {
            Assert.Matches(@"^\d{4}$", Val<string>(g.Generate(node)));
        }
    }

    [Fact]
    public void Generate_StringPatternNoLengthConstraint_DoesNotThrow()
    {
        var g = Gen();
        var node = new StringSchemaNode(Pattern: @"^[a-z]+$");
        for (int i = 0; i < 10; i++)
        {
            g.Generate(node);
        }
    }

    [Fact]
    public void Generate_IntegerNoConstraints_InDefaultRange()
    {
        var g = Gen();
        var node = new NumberSchemaNode(IsInteger: true);
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<long>(g.Generate(node)), 0L, 1000L);
        }
    }

    [Fact]
    public void Generate_IntegerWithBounds_InRange()
    {
        var g = Gen();
        var node = new NumberSchemaNode(Minimum: 5, Maximum: 10, IsInteger: true);
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<long>(g.Generate(node)), 5L, 10L);
        }
    }

    [Fact]
    public void Generate_IntegerExclusiveBounds_RespectedAsOpenInterval()
    {
        var g = Gen();
        // exclusiveMinimum: 0, exclusiveMaximum: 5  →  valid integers: 1, 2, 3, 4
        var node = new NumberSchemaNode(ExclusiveMinimum: 0, ExclusiveMaximum: 5, IsInteger: true);
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<long>(g.Generate(node)), 1L, 4L);
        }
    }

    [Fact]
    public void Generate_IntegerMultipleOf_IsMultiple()
    {
        var g = Gen();
        var node = new NumberSchemaNode(Minimum: 0, Maximum: 100, MultipleOf: 7, IsInteger: true);
        for (int i = 0; i < 30; i++)
        {
            Assert.Equal(0L, Val<long>(g.Generate(node)) % 7);
        }
    }

    [Fact]
    public void Generate_IntegerEmptyRange_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new NumberSchemaNode(Minimum: 10, Maximum: 5, IsInteger: true)));

    [Fact]
    public void Generate_DoubleNoConstraints_InDefaultRange()
    {
        var g = Gen();
        var node = new NumberSchemaNode();
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<double>(g.Generate(node)), 0.0, 1000.0);
        }
    }

    [Fact]
    public void Generate_DoubleWithBounds_InRange()
    {
        var g = Gen();
        var node = new NumberSchemaNode(Minimum: 1.5, Maximum: 2.5);
        for (int i = 0; i < 50; i++)
        {
            Assert.InRange(Val<double>(g.Generate(node)), 1.5, 2.5);
        }
    }

    [Fact]
    public void Generate_DoubleMultipleOf_IsExactMultiple()
    {
        var g = Gen();
        // 0.5 = 2^-1, exact in IEEE 754 — multiples are exact too
        var node = new NumberSchemaNode(Minimum: 0, Maximum: 10, MultipleOf: 0.5);
        for (int i = 0; i < 30; i++)
        {
            var v = Val<double>(g.Generate(node));
            Assert.Equal(0.0, v % 0.5);
        }
    }

    [Fact]
    public void Generate_DoubleExclusiveBounds_StrictlyInside()
    {
        var g = Gen();
        var node = new NumberSchemaNode(ExclusiveMinimum: 0.0, ExclusiveMaximum: 1.0);
        for (int i = 0; i < 50; i++)
        {
            var v = Val<double>(g.Generate(node));
            Assert.True(v > 0.0 && v < 1.0, $"value {v} is not strictly in (0, 1)");
        }
    }

    [Fact]
    public void Generate_Boolean_ProducesBothValues()
    {
        var g = Gen();
        var values = Enumerable.Range(0, 50)
            .Select(x => Val<bool>(g.Generate(new BooleanSchemaNode())))
            .ToList();
        Assert.Contains(true, values);
        Assert.Contains(false, values);
    }

    [Fact]
    public void Generate_Null_AlwaysReturnsNull()
    {
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.Null(g.Generate(new NullSchemaNode()));
        }
    }

    [Fact]
    public void Generate_TrueSchema_DoesNotThrow()
    {
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            g.Generate(new TrueSchemaNode());
        }
    }

    [Fact]
    public void Generate_FalseSchema_ThrowsInvalidOperation() =>
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new FalseSchemaNode()));

    [Fact]
    public void Generate_Enum_AlwaysPicksFromValues()
    {
        var node = Parse("""{"enum":["a","b","c"]}""");
        var g = Gen();
        string[] allowed = ["a", "b", "c"];
        for (int i = 0; i < 30; i++)
        {
            Assert.Contains(Val<string>(g.Generate(node)), allowed);
        }
    }

    [Fact]
    public void Generate_EnumWithMultipleValues_ProducesVariety()
    {
        var node = Parse("""{"enum":["x","y","z"]}""");
        var g = Gen();
        var results = Enumerable.Range(0, 30)
            .Select(x => Val<string>(g.Generate(node)))
            .ToHashSet();
        Assert.True(results.Count > 1, "Expected more than one distinct enum value");
    }

    [Fact]
    public void Generate_EnumContainingNull_CanReturnNull()
    {
        var node = Parse("""{"enum":[1, null]}""");
        var g = Gen();
        var results = Enumerable.Range(0, 50).Select(x => g.Generate(node)).ToList();
        Assert.Contains(results, static x => x is null);
    }

    [Fact]
    public void Generate_EmptyEnum_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new EnumSchemaNode([])));

    [Fact]
    public void Generate_ConstInteger_AlwaysReturnsThatValue()
    {
        var node = Parse("""{"const":42}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(42, Val<int>(g.Generate(node)));
        }
    }

    [Fact]
    public void Generate_ConstString_AlwaysReturnsThatString()
    {
        var node = Parse("""{"const":"hello"}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal("hello", Val<string>(g.Generate(node)));
        }
    }

    [Fact]
    public void Generate_ConstNull_AlwaysReturnsNull()
    {
        var node = Parse("""{"const":null}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            Assert.Null(g.Generate(node));
        }
    }

    [Fact]
    public void Generate_AllOf_AcceptanceTest_IntegerInMergedRange()
    {
        var node = Parse("""{"allOf":[{"type":"integer","minimum":1},{"maximum":5}]}""");
        var g = Gen();
        for (int i = 0; i < 30; i++)
        {
            Assert.InRange(Val<long>(g.Generate(node)), 1L, 5L);
        }
    }

    [Fact]
    public void Generate_AllOf_MergesNumberBounds()
    {
        var node = Parse("""{"allOf":[{"type":"number","minimum":10},{"maximum":20}]}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.InRange(Val<double>(g.Generate(node)), 10.0, 20.0);
        }
    }

    [Fact]
    public void Generate_AllOf_MergesStringLengths()
    {
        var node = Parse("""{"allOf":[{"type":"string","minLength":4},{"maxLength":6}]}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.InRange(Val<string>(g.Generate(node)).Length, 4, 6);
        }
    }

    [Fact]
    public void Generate_AllOf_TrueSchemaIsNeutral()
    {
        var node = Parse("""{"allOf":[{"type":"boolean"},true]}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            _ = Val<bool>(g.Generate(node));
        }
    }

    [Fact]
    public void Generate_AllOf_FalseSchema_Throws()
    {
        var node = Parse("""{"allOf":[{"type":"string"},false]}""");
        Assert.Throws<InvalidOperationException>(() => Gen().Generate(node));
    }

    [Fact]
    public void Generate_AllOf_ConflictingTypes_Throws()
    {
        var node = Parse("""{"allOf":[{"type":"string"},{"type":"integer"}]}""");
        Assert.Throws<InvalidOperationException>(() => Gen().Generate(node));
    }

    [Fact]
    public void Generate_AllOf_MergesObjectProperties()
    {
        var node = Parse("""
            {"allOf":[
                {"type":"object","properties":{"a":{"type":"integer"}},"required":["a"]},
                {"type":"object","properties":{"b":{"type":"string"}}, "required":["b"]}
            ]}
            """);
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            var obj = Assert.IsType<JsonObject>(g.Generate(node));
            Assert.True(obj.ContainsKey("a"), "merged required 'a' missing");
            Assert.True(obj.ContainsKey("b"), "merged required 'b' missing");
        }
    }

    [Fact]
    public void Generate_AllOf_MergesArrayItemCounts()
    {
        var node = Parse("""{"allOf":[{"type":"array","minItems":3},{"maxItems":5}]}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.InRange(Assert.IsType<JsonArray>(g.Generate(node)).Count, 3, 5);
        }
    }

    [Fact]
    public void Generate_AllOf_SingleSchema_ReturnsGeneratedValue()
    {
        var node = Parse("""{"allOf":[{"type":"null"}]}""");
        Assert.Null(Gen().Generate(node));
    }

    [Fact]
    public void Generate_AllOf_MultipleOfMergedWithLcm()
    {
        var node = Parse("""{"allOf":[{"type":"integer","minimum":0,"maximum":30,"multipleOf":3},{"multipleOf":5}]}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            var v = Val<long>(g.Generate(node));
            Assert.Equal(0L, v % 15); // LCM(3,5) = 15
        }
    }

    [Fact]
    public void Generate_AnyOf_ProducesValuesFromBothBranches()
    {
        var node = Parse("""{"anyOf":[{"type":"string"},{"type":"integer"}]}""");
        var g = Gen();
        var results = Enumerable.Range(0, 40).Select(x => g.Generate(node)).ToList();

        Assert.Contains(results, static x => x is JsonValue v && v.TryGetValue<string>(out _));
        Assert.Contains(results, static x => x is JsonValue v && v.TryGetValue<long>(out _));
    }

    [Fact]
    public void Generate_AnyOf_SingleBranch_AlwaysProducesThatType()
    {
        var node = Parse("""{"anyOf":[{"type":"boolean"}]}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            _ = Val<bool>(g.Generate(node));
        }
    }

    [Fact]
    public void Generate_OneOf_ProducesValuesFromBothBranches()
    {
        var node = Parse("""{"oneOf":[{"type":"null"},{"type":"integer"}]}""");
        var g = Gen();
        var results = Enumerable.Range(0, 40).Select(x => g.Generate(node)).ToList();

        Assert.Contains(results, static x => x is null);
        Assert.Contains(results, static x => x is JsonValue);
    }

    [Fact]
    public void Generate_AnyOf_NullableString_CanProduceNullAndString()
    {
        // Pattern produced by OAS 3.0 nullable: true normalisation
        var node = Parse("""{"anyOf":[{"type":"string"},{"type":"null"}]}""");
        var g = Gen();
        var results = Enumerable.Range(0, 40).Select(x => g.Generate(node)).ToList();

        Assert.Contains(results, static x => x is null);
        Assert.Contains(results, static x => x is JsonValue v && v.TryGetValue<string>(out _));
    }

    [Fact]
    public void Generate_AnyOf_EmptySchemas_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Gen().Generate(new AnyOfSchemaNode([])));

    [Fact]
    public void Generate_ArrayNoConstraints_LengthBetween0And5()
    {
        var node = Parse("""{"type":"array"}""");
        var g = Gen();
        for (int i = 0; i < 30; i++)
        {
            Assert.InRange(Assert.IsType<JsonArray>(g.Generate(node)).Count, 0, 5);
        }
    }

    [Fact]
    public void Generate_ArrayMinMaxItems_LengthInRange()
    {
        var node = Parse("""{"type":"array","items":{"type":"integer"},"minItems":2,"maxItems":4}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.InRange(Assert.IsType<JsonArray>(g.Generate(node)).Count, 2, 4);
        }
    }

    [Fact]
    public void Generate_ArrayItemsSchema_ElementsMatchSchema()
    {
        var node = Parse("""{"type":"array","items":{"type":"integer","minimum":0,"maximum":9},"minItems":3,"maxItems":3}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            var arr = Assert.IsType<JsonArray>(g.Generate(node));
            Assert.Equal(3, arr.Count);
            foreach (var el in arr)
            {
                Assert.InRange(Val<long>(el), 0L, 9L);
            }
        }
    }

    [Fact]
    public void Generate_ArrayMinItemsOnly_AtLeastMin()
    {
        var node = Parse("""{"type":"array","minItems":3}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            Assert.True(Assert.IsType<JsonArray>(g.Generate(node)).Count >= 3);
        }
    }

    [Fact]
    public void Generate_ArrayFixedLength_ExactCount()
    {
        var node = Parse("""{"type":"array","items":{"type":"boolean"},"minItems":5,"maxItems":5}""");
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(5, Assert.IsType<JsonArray>(g.Generate(node)).Count);
        }
    }

    [Fact]
    public void Generate_ArrayUniqueItems_NoDuplicates()
    {
        var node = Parse("""{"type":"array","items":{"type":"integer","minimum":0,"maximum":100},"minItems":5,"maxItems":5,"uniqueItems":true}""");
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            var arr = Assert.IsType<JsonArray>(g.Generate(node));
            var keys = arr.Select(static x => x?.ToJsonString()).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }

    [Fact]
    public void Generate_ArrayNoItems_UsesAnyValue()
    {
        var node = Parse("""{"type":"array","minItems":3,"maxItems":3}""");
        var g = Gen();
        var arr = Assert.IsType<JsonArray>(g.Generate(node));
        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void Generate_ArrayMinGreaterThanMax_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new ArraySchemaNode(MinItems: 5, MaxItems: 2)));

    [Fact]
    public void Generate_EmptyObject_ReturnsEmptyJsonObject()
    {
        var node = Parse("""{"type":"object"}""");
        var obj = Assert.IsType<JsonObject>(Gen().Generate(node));
        Assert.NotNull(obj);
    }

    [Fact]
    public void Generate_RequiredProperties_AlwaysPresent()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {
                    "id":   { "type": "integer" },
                    "name": { "type": "string"  }
                },
                "required": ["id", "name"]
            }
            """);
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            var obj = Assert.IsType<JsonObject>(g.Generate(node));
            Assert.True(obj.ContainsKey("id"), "required 'id' missing");
            Assert.True(obj.ContainsKey("name"), "required 'name' missing");
        }
    }

    [Fact]
    public void Generate_RequiredProperties_HaveCorrectTypes()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {
                    "count": { "type": "integer", "minimum": 0, "maximum": 99 }
                },
                "required": ["count"]
            }
            """);
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            var obj = Assert.IsType<JsonObject>(g.Generate(node));
            Assert.InRange(Val<long>(obj["count"]), 0L, 99L);
        }
    }

    [Fact]
    public void Generate_OptionalProperties_AppearsAndAbsentOverSamples()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {
                    "opt": { "type": "boolean" }
                }
            }
            """);
        var g = Gen();
        var results = Enumerable.Range(0, 40)
            .Select(x => Assert.IsType<JsonObject>(g.Generate(node)))
            .ToList();

        Assert.Contains(results, static x => x.ContainsKey("opt"));
        Assert.Contains(results, static x => !x.ContainsKey("opt"));
    }

    [Fact]
    public void Generate_AdditionalPropertiesFalse_OnlyDeclaredKeysPresent()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {
                    "x": { "type": "integer" },
                    "y": { "type": "integer" }
                },
                "required": ["x", "y"],
                "additionalProperties": false
            }
            """);
        var g = Gen();
        for (int i = 0; i < 20; i++)
        {
            var obj = Assert.IsType<JsonObject>(g.Generate(node));
            var keys = obj.Select(static x => x.Key).ToHashSet();
            Assert.True(keys.IsSubsetOf(["x", "y"]),
                $"Unexpected keys: {string.Join(", ", keys.Except(["x", "y"]))}");
        }
    }

    [Fact]
    public void Generate_AdditionalPropertiesSchema_ExtrasMatchSchema()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {},
                "additionalProperties": { "type": "integer", "minimum": 0, "maximum": 9 }
            }
            """);
        var g = Gen(seed: 1);
        var allObjs = Enumerable.Range(0, 30)
            .Select(x => Assert.IsType<JsonObject>(g.Generate(node)))
            .ToList();

        foreach (var obj in allObjs)
        {
            foreach (var kv in obj)
            {
                Assert.InRange(Val<long>(kv.Value), 0L, 9L);
            }
        }
    }

    [Fact]
    public void Generate_NestedObject_InnerObjectGenerated()
    {
        var node = Parse("""
            {
                "type": "object",
                "properties": {
                    "address": {
                        "type": "object",
                        "properties": {
                            "city": { "type": "string" }
                        },
                        "required": ["city"]
                    }
                },
                "required": ["address"]
            }
            """);
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            var outer = Assert.IsType<JsonObject>(g.Generate(node));
            var inner = Assert.IsType<JsonObject>(outer["address"]);
            Assert.True(inner.ContainsKey("city"), "nested required 'city' missing");
            _ = Val<string>(inner["city"]);
        }
    }

    [Fact]
    public void Generate_ObjectWithNoRequiredAndNoOptional_ReturnsJsonObject()
    {
        var node = Parse("""
            {"type":"object","properties":{},"additionalProperties":false}
            """);
        var g = Gen();
        for (int i = 0; i < 10; i++)
        {
            var obj = Assert.IsType<JsonObject>(g.Generate(node));
            Assert.Empty(obj);
        }
    }

    [Fact]
    public void Generate_DeepAnyOfNesting_ThrowsWhenDepthExceeded()
    {
        // Each Generate call for an AnyOfSchemaNode passes depth+1 to the branch;
        // the generator throws InvalidOperationException at depth >= 50.
        JsonSchemaNode schema = new StringSchemaNode();
        for (int i = 0; i < 55; i++)
        {
            schema = new AnyOfSchemaNode([schema]);
        }

        var gen = new JsonValueGenerator(new Random(42));
        Assert.Throws<InvalidOperationException>(() => gen.Generate(schema));
    }

    [Fact]
    public void Generate_ShallowNesting_DoesNotThrow()
    {
        JsonSchemaNode schema = new StringSchemaNode();
        for (int i = 0; i < 10; i++)
        {
            schema = new AnyOfSchemaNode([schema]);
        }

        var gen = new JsonValueGenerator(new Random(42));
        var result = gen.Generate(schema);
        _ = Val<string>(result);
    }

    [Fact]
    public void Generate_ArrayUniqueItems_NullOnlySchema_ExhaustsAndThrows()
    {
        // NullSchemaNode always serializes as "null" — every item is identical,
        // so 2+ unique items can never be produced.
        var gen = new JsonValueGenerator(new Random(0));
        Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(new ArraySchemaNode(Items: new NullSchemaNode(), MinItems: 2, UniqueItems: true)));
    }

    [Fact]
    public void Generate_ArrayUniqueItems_BooleanSchema_MoreThanTwoRequired_Throws()
    {
        // BooleanSchemaNode has only 2 distinct values; requiring 3+ unique items is unsatisfiable.
        var gen = new JsonValueGenerator(new Random(0));
        Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(new ArraySchemaNode(Items: new BooleanSchemaNode(), MinItems: 3, UniqueItems: true)));
    }

    [Fact]
    public void Generate_ArrayMinItemsExceedsCap_Throws()
    {
        // minItems > 10,000 is rejected to prevent accidental OOM.
        var gen = new JsonValueGenerator(new Random(42));
        Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(new ArraySchemaNode(MinItems: 10_001)));
    }

    [Fact]
    public void Generate_IntegerMultipleOfZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Gen().Generate(new NumberSchemaNode(MultipleOf: 0, IsInteger: true)));
    }

    [Fact]
    public void Generate_IntegerExclusiveBoundsEmptyRange_ThrowsInvalidOperation()
    {
        // exclusiveMinimum:5, exclusiveMaximum:6 → effective integer range is [6, 5] → empty
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new NumberSchemaNode(ExclusiveMinimum: 5, ExclusiveMaximum: 6, IsInteger: true)));
    }

    [Fact]
    public void Generate_IntegerMultipleOfNoValidMultipleInRange_ThrowsInvalidOperation()
    {
        // minimum:3, maximum:4, multipleOf:5 → no multiple of 5 exists in [3, 4]
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new NumberSchemaNode(Minimum: 3, Maximum: 4, MultipleOf: 5, IsInteger: true)));
    }

    [Fact]
    public void Generate_StringPatternIncompatibleWithLengthConstraint_ThrowsInvalidOperation()
    {
        // "^a$" always produces a single character; minLength:10 can never be satisfied.
        Assert.Throws<InvalidOperationException>(() =>
            Gen().Generate(new StringSchemaNode(Pattern: @"^a$", MinLength: 10)));
    }

    [Fact]
    public void Generate_ArrayMaxItemsAboveCap_CappedSilently()
    {
        // maxItems: 50_000 is silently clamped to 10,000; minItems: 0 so the array can be empty.
        var gen = new JsonValueGenerator(new Random(42));
        var arr = Assert.IsType<JsonArray>(gen.Generate(new ArraySchemaNode(MaxItems: 50_000)));
        Assert.InRange(arr.Count, 0, 10_000);
    }
}
