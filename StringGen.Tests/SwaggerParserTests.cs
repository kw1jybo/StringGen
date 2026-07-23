using StringGen.Core.JsonSchema;
using StringGen.Core.Swagger;

namespace StringGen.Tests;

public class SwaggerParserTests
{
    private static SwaggerSchemaSource Parse(string json, string? root = null) =>
        new SwaggerParser().Parse(json, root);

    private const string MinimalSwagger = """
        {
            "swagger": "2.0",
            "info": { "title": "Test", "version": "1.0" },
            "paths": {},
            "definitions": {}
        }
        """;

    [Fact]
    public void Parse_ValidSwagger20_DoesNotThrow() =>
        _ = Parse(MinimalSwagger);

    [Fact]
    public void Parse_MissingSwaggerField_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Parse("""{"info":{"title":"T","version":"1.0"},"paths":{}}"""));

    [Fact]
    public void Parse_WrongVersion_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Parse("""{"swagger":"3.0","info":{"title":"T","version":"1.0"},"paths":{}}"""));

    [Fact]
    public void Parse_NoRootDefinition_RootIsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(Parse(MinimalSwagger).Root);

    [Fact]
    public void Parse_WithRootDefinition_RootIsNamedDefinitionNode()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "Age": { "type": "integer", "minimum": 0, "maximum": 120 }
                }
            }
            """;
        var src = Parse(json, "Age");
        var node = Assert.IsType<NumberSchemaNode>(src.Root);
        Assert.True(node.IsInteger);
        Assert.Equal(0, node.Minimum);
        Assert.Equal(120, node.Maximum);
    }

    [Fact]
    public void Parse_UnknownRootDefinition_Throws() =>
        Assert.Throws<ArgumentException>(() => Parse(MinimalSwagger, "DoesNotExist"));

    [Fact]
    public void Resolve_DefinitionsPointer_ReturnsCorrectNode()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "Name": { "type": "string", "minLength": 1, "maxLength": 50 }
                }
            }
            """;
        var src = Parse(json);
        var node = Assert.IsType<StringSchemaNode>(src.Resolve("#/definitions/Name"));
        Assert.Equal(1, node.MinLength);
        Assert.Equal(50, node.MaxLength);
    }

    [Fact]
    public void Resolve_UnknownDefinition_Throws()
    {
        var src = Parse(MinimalSwagger);
        Assert.Throws<ArgumentException>(() => src.Resolve("#/definitions/Missing"));
    }

    [Fact]
    public void Resolve_NonDefinitionsPointer_ThrowsNotSupported()
    {
        var src = Parse(MinimalSwagger);
        Assert.Throws<NotSupportedException>(() => src.Resolve("#/$defs/Foo"));
    }

    [Fact]
    public void Resolve_ExternalRef_ThrowsNotSupported()
    {
        var src = Parse(MinimalSwagger);
        Assert.Throws<NotSupportedException>(() =>
            src.Resolve("https://example.com/schema.json"));
    }

    [Fact]
    public void Parse_MultipleDefinitions_AllResolvable()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "Id":   { "type": "integer" },
                    "Name": { "type": "string" },
                    "Flag": { "type": "boolean" }
                }
            }
            """;
        var src = Parse(json);
        Assert.IsType<NumberSchemaNode>(src.Resolve("#/definitions/Id"));
        Assert.IsType<StringSchemaNode>(src.Resolve("#/definitions/Name"));
        Assert.IsType<BooleanSchemaNode>(src.Resolve("#/definitions/Flag"));
    }

    [Fact]
    public void DefinitionNames_ReflectsAllParsedDefinitions()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "A": { "type": "string" },
                    "B": { "type": "integer" }
                }
            }
            """;
        var names = Parse(json).DefinitionNames.ToHashSet();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
    }

    [Fact]
    public void Parse_DefinitionRefToAnotherDefinition_ResolvesCorrectly()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "Category": { "type": "string" },
                    "Pet": {
                        "type": "object",
                        "properties": {
                            "name":     { "type": "string" },
                            "category": { "$ref": "#/definitions/Category" }
                        }
                    }
                }
            }
            """;
        var src = Parse(json);
        var pet = Assert.IsType<ObjectSchemaNode>(src.Resolve("#/definitions/Pet"));
        Assert.IsType<StringSchemaNode>(pet.Properties["category"]);
    }

    [Fact]
    public void WithRoot_ChangesRootToNamedDefinition()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": {
                    "Foo": { "type": "boolean" },
                    "Bar": { "type": "string" }
                }
            }
            """;
        var src = Parse(json).WithRoot("Foo");
        Assert.IsType<BooleanSchemaNode>(src.Root);
        Assert.IsType<StringSchemaNode>(src.Resolve("#/definitions/Bar"));
    }

    [Fact]
    public void FromJson_Factory_ReturnsISchemaSource()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "definitions": { "X": { "type": "null" } }
            }
            """;
        ISchemaSource src = SwaggerSchemaSource.FromJson(json, "X");
        Assert.IsType<NullSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathBody_BodyParameter_ResolvesRefToDefinition()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/pets": {
                        "post": {
                            "parameters": [
                                { "in": "body", "name": "body",
                                  "schema": { "$ref": "#/definitions/Pet" } }
                            ],
                            "responses": {}
                        }
                    }
                },
                "definitions": {
                    "Pet": { "type": "object" }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathBody(json, "/pets", "post");
        Assert.IsType<ObjectSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathResponse_ResponseSchema_ResolvesRefToDefinition()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/pets/{id}": {
                        "get": {
                            "responses": {
                                "200": { "schema": { "$ref": "#/definitions/Pet" } }
                            }
                        }
                    }
                },
                "definitions": {
                    "Pet": { "type": "object" }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathResponse(json, "/pets/{id}", "get", "200");
        Assert.IsType<ObjectSchemaNode>(src.Root);
    }

    [Fact]
    public void Parse_NoDefinitionsSection_EmptySource()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {}
            }
            """;
        var src = Parse(json);
        Assert.IsType<TrueSchemaNode>(src.Root);
        Assert.Empty(src.DefinitionNames);
    }

    [Fact]
    public void ParsePathBody_InlineSchema_ParsedDirectly()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/items": {
                        "post": {
                            "parameters": [
                                { "in": "body", "name": "body",
                                  "schema": { "type": "string" } }
                            ],
                            "responses": {}
                        }
                    }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathBody(json, "/items", "post");
        Assert.IsType<StringSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathBody_InlineSchemaWithNestedRef_Resolves()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/orders": {
                        "post": {
                            "parameters": [
                                { "in": "body", "name": "body",
                                  "schema": {
                                      "type": "object",
                                      "properties": {
                                          "item": { "$ref": "#/definitions/Item" }
                                      }
                                  }
                                }
                            ],
                            "responses": {}
                        }
                    }
                },
                "definitions": {
                    "Item": { "type": "integer" }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathBody(json, "/orders", "post");
        var obj = Assert.IsType<ObjectSchemaNode>(src.Root);
        Assert.IsType<NumberSchemaNode>(obj.Properties["item"]);
    }

    [Fact]
    public void ParsePathResponse_InlineSchema_ParsedDirectly()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/ping": {
                        "get": {
                            "responses": {
                                "200": { "schema": { "type": "boolean" } }
                            }
                        }
                    }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathResponse(json, "/ping", "get", "200");
        Assert.IsType<BooleanSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathBody_PathNotFound_ReturnsTrueNode()
    {
        var src = new SwaggerParser().ParsePathBody(MinimalSwagger, "/nonexistent", "post");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathBody_MethodNotFound_ReturnsTrueNode()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": { "/items": { "get": { "responses": {} } } }
            }
            """;
        var src = new SwaggerParser().ParsePathBody(json, "/items", "delete");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathBody_NoBodyParameter_ReturnsTrueNode()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/items": {
                        "get": {
                            "parameters": [ { "in": "query", "name": "q" } ],
                            "responses": {}
                        }
                    }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathBody(json, "/items", "get");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathResponse_PathNotFound_ReturnsTrueNode()
    {
        var src = new SwaggerParser().ParsePathResponse(MinimalSwagger, "/nonexistent", "get", "200");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParsePathResponse_StatusCodeNotFound_ReturnsTrueNode()
    {
        const string json = """
            {
                "swagger": "2.0",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/items": {
                        "get": { "responses": { "200": {} } }
                    }
                }
            }
            """;
        var src = new SwaggerParser().ParsePathResponse(json, "/items", "get", "404");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }
}
