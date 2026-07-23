using System.Reflection;
using System.Text.RegularExpressions;
using StringGen.Core;
using StringGen.Core.Swagger;

namespace StringGen.Tests;

[Trait("Category", "Security")]
public class SecurityTests
{
    [Fact]
    public void StringGenerator_CompiledRegexHasFiniteMatchTimeout()
    {
        // The primary ReDoS defence is a finite MatchTimeout on the compiled Regex.
        // Removing or changing it to Infinite would leave callers exposed to catastrophic backtracking.
        var gen = new StringGenerator(@"\d+", seed: 0);
        var regexField = typeof(StringGenerator)
            .GetField("regex", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(regexField);
        var regex = Assert.IsType<Regex>(regexField.GetValue(gen));
        Assert.True(regex.MatchTimeout > TimeSpan.Zero,
            $"Expected a finite positive MatchTimeout; got {regex.MatchTimeout}");
    }

    [Fact]
    public void JsonGenerator_SchemaNestingBeyondMaxDepth_ThrowsInvalidOperation()
    {
        // MaxDepth = 50; deeply nested array schemas must throw rather than stack-overflow.
        // Array nesting adds 1 JSON depth per generator depth, keeping JSON depth < 64 (parser limit)
        // while still hitting the generator's MaxDepth guard at depth >= 50.
        var schema = BuildNestedArraySchema(55);
        var gen = new JsonGenerator(schema, seed: 0);
        Assert.Throws<InvalidOperationException>(() => gen.Generate(1));
    }

    [Fact]
    public void SwaggerGenerator_SchemaNestingBeyondMaxDepth_ThrowsInvalidOperation()
    {
        // Same depth guard applies when schema comes from a Swagger document.
        var inner = BuildNestedArraySchema(55);
        var swagger =
            "{\"swagger\":\"2.0\",\"info\":{\"title\":\"T\",\"version\":\"1\"}," +
            "\"paths\":{},\"definitions\":{\"Deep\":" + inner + "}}";
        var gen = new SwaggerGenerator(swagger, seed: 0);
        Assert.Throws<InvalidOperationException>(() => gen.GenerateSchema("Deep", 1));
    }

    // Each level of array nesting increments generator depth by 1 but only JSON depth by 1,
    // so 55 levels stays well under System.Text.Json's default 64-level JSON parse limit.
    private static string BuildNestedArraySchema(int depth)
    {
        var schema = "{\"type\":\"string\"}";
        for (int i = 0; i < depth; i++)
        {
            schema = "{\"type\":\"array\",\"minItems\":1,\"maxItems\":1,\"items\":" + schema + "}";
        }
        return schema;
    }
}
