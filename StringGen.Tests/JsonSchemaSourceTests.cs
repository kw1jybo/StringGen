using StringGen.Core.JsonSchema;

namespace StringGen.Tests;

public class JsonSchemaSourceTests
{
    private static JsonSchemaSource Source(string json) =>
        JsonSchemaSource.FromJson(json);

    [Fact]
    public void Root_SimpleStringSchema_ReturnsStringNode()
    {
        var src = Source("""{"type":"string"}""");
        Assert.IsType<StringSchemaNode>(src.Root);
    }

    [Fact]
    public void Root_SchemaWithDefs_ReturnsRootNode()
    {
        var src = Source("""
            {
                "$defs": { "Id": { "type": "integer" } },
                "type": "object",
                "properties": { "id": { "$ref": "#/$defs/Id" } }
            }
            """);
        Assert.IsType<ObjectSchemaNode>(src.Root);
    }

    [Fact]
    public void Resolve_DefsPointer_ReturnsCorrectNode()
    {
        var src = Source("""
            {
                "$defs": { "Name": { "type": "string", "minLength": 1 } },
                "type": "object"
            }
            """);
        var node = Assert.IsType<StringSchemaNode>(src.Resolve("#/$defs/Name"));
        Assert.Equal(1, node.MinLength);
    }

    [Fact]
    public void Resolve_DefinitionsPointer_ReturnsCorrectNode()
    {
        var src = Source("""
            {
                "definitions": { "Age": { "type": "integer" } },
                "type": "object"
            }
            """);
        var node = Assert.IsType<NumberSchemaNode>(src.Resolve("#/definitions/Age"));
        Assert.True(node.IsInteger);
    }

    [Fact]
    public void Resolve_UnreferencedDef_StillAvailable()
    {
        // Defs that are never $ref'd in the schema should still be resolvable.
        var src = Source("""
            {
                "$defs": {
                    "Used":   { "type": "string" },
                    "Unused": { "type": "integer" }
                },
                "$ref": "#/$defs/Used"
            }
            """);
        Assert.IsType<NumberSchemaNode>(src.Resolve("#/$defs/Unused"));
    }

    [Fact]
    public void Resolve_UnknownName_ThrowsArgumentException()
    {
        var src = Source("""{"$defs":{"A":{"type":"string"}}}""");
        var ex = Assert.Throws<ArgumentException>(() => src.Resolve("#/$defs/Missing"));
        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    public void Resolve_ExternalRef_ThrowsNotSupportedException()
    {
        var src = Source("""{"type":"string"}""");
        Assert.Throws<NotSupportedException>(() =>
            src.Resolve("https://example.com/schema.json"));
    }

    [Fact]
    public void Resolve_RelativeRef_ThrowsNotSupportedException()
    {
        var src = Source("""{"type":"string"}""");
        Assert.Throws<NotSupportedException>(() =>
            src.Resolve("other-schema.json#/$defs/Foo"));
    }

    [Fact]
    public void ImplementsISchemaSource()
    {
        ISchemaSource src = Source("""{"type":"boolean"}""");
        Assert.IsType<BooleanSchemaNode>(src.Root);
    }

    [Theory]
    [InlineData("#/$defs/Foo", "Foo")]
    [InlineData("#/definitions/Bar", "Bar")]
    [InlineData("#/$defs/A/B", "A/B")]   // nested name preserved verbatim
    public void ExtractDefinitionName_KnownPrefixes_ReturnsName(string pointer, string expected) =>
        Assert.Equal(expected, JsonSchemaSource.ExtractDefinitionName(pointer));

    [Theory]
    [InlineData("https://example.com/schema.json")]
    [InlineData("#/other/Foo")]
    [InlineData("")]
    public void ExtractDefinitionName_UnknownPrefix_ReturnsNull(string pointer) =>
        Assert.Null(JsonSchemaSource.ExtractDefinitionName(pointer));
}
