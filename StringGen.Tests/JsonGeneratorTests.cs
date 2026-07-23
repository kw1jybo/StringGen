using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using StringGen.Core;

namespace StringGen.Tests;

public class JsonGeneratorTests
{
    private static T Val<T>(JsonNode? node) =>
        Assert.IsAssignableFrom<JsonValue>(node).GetValue<T>();

    [Fact]
    public void Constructor_StringSchema_DoesNotThrow() =>
        _ = new JsonGenerator("""{"type":"string"}""");

    [Fact]
    public void Constructor_JsonElement_DoesNotThrow()
    {
        using var doc = JsonDocument.Parse("""{"type":"integer"}""");
        _ = new JsonGenerator(doc.RootElement);
    }

    [Fact]
    public void Constructor_InvalidSchema_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new JsonGenerator("""{"type":"unknown_type"}"""));

    [Fact]
    public void Generate_ZeroCount_ReturnsEmptyList()
    {
        var g = new JsonGenerator("""{"type":"string"}""", seed: 0);
        Assert.Empty(g.Generate(0));
    }

    [Fact]
    public void Generate_NegativeCount_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JsonGenerator("""{"type":"string"}""").Generate(-1));

    [Fact]
    public void Generate_StringSchema_ReturnsCorrectCount()
    {
        var results = new JsonGenerator("""{"type":"string"}""", seed: 42).Generate(5);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Generate_StringSchema_AllValuesAreStrings()
    {
        var g = new JsonGenerator("""{"type":"string","minLength":1,"maxLength":10}""", seed: 42);
        Assert.All(g.Generate(10), x =>
        {
            var s = Val<string>(x);
            Assert.InRange(s.Length, 1, 10);
        });
    }

    [Fact]
    public void Generate_IntegerSchema_ValuesInRange()
    {
        var g = new JsonGenerator("""{"type":"integer","minimum":1,"maximum":10}""", seed: 42);
        Assert.All(g.Generate(20), x => Assert.InRange(Val<long>(x), 1L, 10L));
    }

    [Fact]
    public void Generate_NumberSchema_ValuesInRange()
    {
        var g = new JsonGenerator("""{"type":"number","minimum":0.0,"maximum":1.0}""", seed: 42);
        Assert.All(g.Generate(20), x => Assert.InRange(Val<double>(x), 0.0, 1.0));
    }

    [Fact]
    public void Generate_BooleanSchema_ReturnsBooleans()
    {
        var g = new JsonGenerator("""{"type":"boolean"}""", seed: 42);
        var results = g.Generate(20).Select(x => Val<bool>(x)).ToList();
        Assert.Contains(true, results);
        Assert.Contains(false, results);
    }

    [Fact]
    public void Generate_NullSchema_ReturnsNulls()
    {
        var g = new JsonGenerator("""{"type":"null"}""", seed: 0);
        Assert.All(g.Generate(5), Assert.Null);
    }

    [Fact]
    public void Generate_EnumSchema_AlwaysPicksFromAllowedValues()
    {
        var g = new JsonGenerator("""{"enum":["a","b","c"]}""", seed: 42);
        string[] allowed = ["a", "b", "c"];
        Assert.All(g.Generate(20), x => Assert.Contains(Val<string>(x), allowed));
    }

    [Fact]
    public void Generate_ConstSchema_AlwaysReturnsSameValue()
    {
        var g = new JsonGenerator("""{"const":99}""", seed: 42);
        Assert.All(g.Generate(5), x => Assert.Equal(99, Val<int>(x)));
    }

    [Fact]
    public void Generate_SameSeed_ProducesSameSequence()
    {
        var r1 = new JsonGenerator("""{"type":"integer"}""", seed: 7)
            .Generate(10).Select(x => Val<long>(x)).ToList();
        var r2 = new JsonGenerator("""{"type":"integer"}""", seed: 7)
            .Generate(10).Select(x => Val<long>(x)).ToList();
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentSequences()
    {
        var r1 = new JsonGenerator("""{"type":"integer"}""", seed: 1)
            .Generate(10).Select(x => Val<long>(x)).ToList();
        var r2 = new JsonGenerator("""{"type":"integer"}""", seed: 2)
            .Generate(10).Select(x => Val<long>(x)).ToList();
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void GenerateJson_ReturnsCorrectCount()
    {
        var jsons = new JsonGenerator("""{"type":"string"}""", seed: 0).GenerateJson(3);
        Assert.Equal(3, jsons.Count);
    }

    [Fact]
    public void GenerateJson_StringSchema_EachIsValidJsonString()
    {
        var g = new JsonGenerator("""{"type":"string"}""", seed: 0);
        foreach (var json in g.GenerateJson(5))
        {
            Assert.StartsWith("\"", json);
            Assert.EndsWith("\"", json);
        }
    }

    [Fact]
    public void GenerateJson_NullSchema_ReturnsNullLiteral()
    {
        var g = new JsonGenerator("""{"type":"null"}""", seed: 0);
        Assert.All(g.GenerateJson(3), static x => Assert.Equal("null", x));
    }

    [Fact]
    public void GenerateJson_IntegerSchema_ProducesParseableIntegers()
    {
        var g = new JsonGenerator("""{"type":"integer","minimum":0,"maximum":9}""", seed: 0);
        foreach (var json in g.GenerateJson(10))
        {
            Assert.InRange(long.Parse(json), 0L, 9L);
        }
    }

    [Fact]
    public void GenerateJson_CustomOptions_UsedForSerialization()
    {
        var g = new JsonGenerator("""{"const":"hi"}""", seed: 0);
        var compact = new JsonSerializerOptions { WriteIndented = false };
        Assert.All(g.GenerateJson(3, compact), static x => Assert.Equal("\"hi\"", x));
    }

    [Fact]
    public void Generate_RefToDefs_ResolvesAndGeneratesCorrectly()
    {
        var g = new JsonGenerator("""
            {
                "$defs": { "Age": { "type": "integer", "minimum": 0, "maximum": 120 } },
                "$ref": "#/$defs/Age"
            }
            """, seed: 42);
        Assert.All(g.Generate(10), x => Assert.InRange(Val<long>(x), 0L, 120L));
    }

    [Fact]
    public void Generate_RefToDefinitions_ResolvesAndGeneratesCorrectly()
    {
        var g = new JsonGenerator("""
            {
                "definitions": { "Code": { "type": "string", "minLength": 3, "maxLength": 3 } },
                "$ref": "#/definitions/Code"
            }
            """, seed: 42);
        Assert.All(g.Generate(10), x => Assert.Equal(3, Val<string>(x).Length));
    }

    [Fact]
    public void Generate_RefSharedAcrossProperties_EachPropertyGeneratesIndependently()
    {
        // The same $defs entry can be referenced multiple times; each call generates
        // a fresh value from the same schema node.
        var g = new JsonGenerator("""
            {
                "$defs": { "Flag": { "type": "boolean" } },
                "anyOf": [
                    { "$ref": "#/$defs/Flag" },
                    { "$ref": "#/$defs/Flag" }
                ]
            }
            """, seed: 1);
        var results = g.Generate(10);
        Assert.Equal(10, results.Count);
        Assert.All(results, x => _ = Val<bool>(x));
    }

    [Fact]
    public void Generate_CircularRef_ThrowsAtConstruction()
    {
        // A circular $ref must throw (ArgumentException from the parser) during
        // JsonGenerator construction, not stack-overflow during Generate().
        Assert.Throws<ArgumentException>(() => new JsonGenerator("""
            {
                "$defs": { "Loop": { "$ref": "#/$defs/Loop" } },
                "$ref": "#/$defs/Loop"
            }
            """));
    }

    [Fact]
    public void Generate_UnknownRef_ThrowsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() =>
            new JsonGenerator("""{"$ref":"#/$defs/NoSuchDef"}"""));
    }

    [Fact]
    public void GenerateJson_RefSchema_ProducesValidJson()
    {
        var g = new JsonGenerator("""
            {
                "$defs": { "Score": { "type": "integer", "minimum": 1, "maximum": 10 } },
                "$ref": "#/$defs/Score"
            }
            """, seed: 7);
        foreach (var json in g.GenerateJson(5))
        {
            Assert.InRange(long.Parse(json), 1L, 10L);
        }
    }

    [Fact]
    public void Generate_AllOf_ConflictingConsts_Throws()
    {
        var gen = new JsonGenerator("""{"allOf":[{"const":1},{"const":2}]}""");
        Assert.Throws<InvalidOperationException>(() => gen.Generate(1));
    }

    [Fact]
    public void Generate_AllOf_EqualConsts_Succeeds()
    {
        var gen = new JsonGenerator("""{"allOf":[{"const":42},{"const":42}]}""", seed: 1);
        var result = gen.Generate(1);
        Assert.Single(result);
        Assert.Equal(42, Val<int>(result[0]));
    }

    [Fact]
    public void Generate_AllOf_FractionalMultipleOfConflict_Throws()
    {
        var gen = new JsonGenerator(
            """{"allOf":[{"type":"number","multipleOf":0.3},{"type":"number","multipleOf":0.7}]}""");
        Assert.Throws<NotSupportedException>(() => gen.Generate(1));
    }

    [Fact]
    public void Generate_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new JsonGenerator("""{"type":"string"}""", seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.Generate(5, ct));
    }

    [Fact]
    public void Constructor_NullSchema_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonGenerator(null!));
    }

    [Fact]
    public void Constructor_MaxRepeatZero_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JsonGenerator("""{"type":"string"}""", maxRepeat: 0));
    }

    [Fact]
    public void Generate_Array_UniqueItems_ExhaustedValueSpace_Throws()
    {
        // boolean has only 2 distinct values; requesting 5 unique items is impossible
        var gen = new JsonGenerator(
            """{"type":"array","items":{"type":"boolean"},"minItems":5,"uniqueItems":true}""",
            seed: 1);
        Assert.Throws<InvalidOperationException>(() => gen.Generate(1));
    }

    [Fact]
    public void GenerateNonCompliant_CountZero_ReturnsEmpty()
    {
        var g = new JsonGenerator("""{"type":"string"}""", seed: 0);
        g.Generate(0, mode: GenerationMode.NonCompliant).Should().BeEmpty();
    }

    [Fact]
    public void GenerateNonCompliant_TrueSchema_Throws()
    {
        var g = new JsonGenerator("""{}""", seed: 0);
        g.Invoking(x => x.Generate(1, mode: GenerationMode.NonCompliant))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GenerateNonCompliant_StringSchema_AllWrongType()
    {
        var g = new JsonGenerator("""{"type":"string"}""", seed: 0);
        var results = g.Generate(3, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(3);
        foreach (var n in results)
        {
            n.Should().BeAssignableTo<JsonValue>().Which.GetValue<int>().Should().Be(42);
        }
    }

    [Fact]
    public void GenerateNonCompliant_StringMinLength_ProducesWrongTypeAndEmptyString()
    {
        var g = new JsonGenerator("""{"type":"string","minLength":3}""", seed: 0);
        var results = g.Generate(2, mode: GenerationMode.NonCompliant);
        results[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<int>().Should().Be(42);
        results[1].Should().BeAssignableTo<JsonValue>().Which.GetValue<string>().Should().Be("");
    }

    [Fact]
    public void GenerateNonCompliant_StringMaxLength_ProducesOversizedString()
    {
        var g = new JsonGenerator("""{"type":"string","maxLength":2}""", seed: 0);
        var results = g.Generate(2, mode: GenerationMode.NonCompliant);
        results[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<int>().Should().Be(42);
        results[1].Should().BeAssignableTo<JsonValue>().Which.GetValue<string>()
            .Length.Should().BeGreaterThan(2);
    }

    [Fact]
    public void GenerateNonCompliant_IntegerSchema_ProducesAllFourViolations()
    {
        var g = new JsonGenerator("""{"type":"integer","minimum":1,"maximum":10}""", seed: 0);
        var results = g.Generate(4, mode: GenerationMode.NonCompliant);
        results[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<string>().Should().Be("NaN");
        results[1].Should().BeAssignableTo<JsonValue>().Which.GetValue<double>().Should().Be(1.5);
        results[2].Should().BeAssignableTo<JsonValue>().Which.GetValue<double>().Should().Be(0.0);
        results[3].Should().BeAssignableTo<JsonValue>().Which.GetValue<double>().Should().Be(11.0);
    }

    [Fact]
    public void GenerateNonCompliant_ObjectSchema_WrongType()
    {
        var g = new JsonGenerator(
            """{"type":"object","required":["id"],"properties":{"id":{"type":"integer"}}}""", seed: 0);
        g.Generate(1, mode: GenerationMode.NonCompliant)[0].Should().BeOfType<JsonArray>();
    }

    [Fact]
    public void GenerateNonCompliant_ObjectSchema_MissingRequired()
    {
        var g = new JsonGenerator(
            """{"type":"object","required":["id"],"properties":{"id":{"type":"integer"}}}""", seed: 0);
        var results = g.Generate(2, mode: GenerationMode.NonCompliant);
        var obj = results[1].Should().BeOfType<JsonObject>().Subject;
        obj.ContainsKey("id").Should().BeFalse();
    }

    [Fact]
    public void GenerateNonCompliant_EnumSchema_NotInEnum()
    {
        var g = new JsonGenerator("""{"enum":["a","b","c"]}""", seed: 0);
        var result = g.Generate(1, mode: GenerationMode.NonCompliant);
        var s = result[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<string>();
        new[] { "a", "b", "c" }.Should().NotContain(s);
    }

    [Fact]
    public void GenerateNonCompliant_ArrayMinItems_ProducesWrongTypeAndEmptyArray()
    {
        var g = new JsonGenerator("""{"type":"array","minItems":2}""", seed: 0);
        var results = g.Generate(2, mode: GenerationMode.NonCompliant);
        results[0].Should().BeOfType<JsonObject>();
        results[1].Should().BeOfType<JsonArray>().Which.Count.Should().Be(0);
    }

    [Fact]
    public void GenerateNonCompliant_GenerateJson_ProducesNonCompliantJson()
    {
        var g = new JsonGenerator("""{"type":"string"}""", seed: 0);
        var results = g.GenerateJson(3, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(3).And.AllSatisfy(json => json.Should().Be("42"));
    }

    [Fact]
    public void GenerateNonCompliant_AnyOfStringOrNull_ProducesBooleanViolatingBothBranches()
    {
        // anyOf:[{type:string},{type:null}] — OAS 3.0 nullable pattern.
        // Violation must fail BOTH branches: not a string AND not null.
        var g = new JsonGenerator("""{"anyOf":[{"type":"string"},{"type":"null"}]}""", seed: 0);
        var result = g.Generate(1, mode: GenerationMode.NonCompliant);
        result[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<bool>().Should().Be(true);
    }

    [Fact]
    public void GenerateNonCompliant_AnyOfStringOrNumber_ProducesBooleanViolatingBothBranches()
    {
        // anyOf:[{type:string},{type:number}] — both type branches covered, boolean violates both.
        var g = new JsonGenerator("""{"anyOf":[{"type":"string"},{"type":"number"}]}""", seed: 0);
        var result = g.Generate(1, mode: GenerationMode.NonCompliant);
        result[0].Should().BeAssignableTo<JsonValue>().Which.GetValue<bool>().Should().Be(true);
    }

    [Fact]
    public void GenerateNonCompliant_SeededGenerator_RepeatedCallsAdvanceRandomState()
    {
        // Seed fix: nonCompliantGen is now a field, so its Random advances across calls.
        // For an array schema with maxItems, the oversized-array strategy uses random
        // to generate item values — repeated calls should NOT reset to the same sequence.
        const string schema = """{"type":"array","maxItems":1,"items":{"type":"integer","minimum":0,"maximum":1000000}}""";
        var g = new JsonGenerator(schema, seed: 1);
        // count=2 → strategy[0]=JsonObject (wrong type), strategy[1]=array-with-2-items (uses random)
        var first = g.Generate(2, mode: GenerationMode.NonCompliant);
        var second = g.Generate(2, mode: GenerationMode.NonCompliant);
        // The array at index 1 should contain different integer values across the two calls
        var firstArr = first[1].Should().BeOfType<JsonArray>().Subject;
        var secondArr = second[1].Should().BeOfType<JsonArray>().Subject;
        // At least one item value must differ — with seed=1 these do differ in practice
        var firstVals = firstArr.Select(x => x!.ToJsonString()).ToList();
        var secondVals = secondArr.Select(x => x!.ToJsonString()).ToList();
        firstVals.Should().NotEqual(secondVals);
    }
}
