using StringGen.Core.JsonSchema;
using StringGen.Core.Swagger;

namespace StringGen.Tests;

public class OpenApiParserTests
{
    private static OpenApiSchemaSource Parse(string json, string? root = null) =>
        new OpenApiParser().Parse(json, root);

    private const string MinimalOas30 = """
        {
            "openapi": "3.0.3",
            "info": { "title": "T", "version": "1" },
            "paths": {},
            "components": { "schemas": {} }
        }
        """;

    private const string MinimalOas31 = """
        {
            "openapi": "3.1.0",
            "info": { "title": "T", "version": "1" },
            "paths": {},
            "components": { "schemas": {} }
        }
        """;

    [Fact]
    public void Parse_ValidOas30_DoesNotThrow() => _ = Parse(MinimalOas30);

    [Fact]
    public void Parse_ValidOas31_DoesNotThrow() => _ = Parse(MinimalOas31);

    [Fact]
    public void Parse_MissingOpenapiField_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Parse("""{"info":{"title":"T","version":"1"},"paths":{}}"""));

    [Fact]
    public void Parse_SwaggerDocument_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Parse("""{"swagger":"2.0","info":{"title":"T","version":"1"},"paths":{}}"""));

    [Fact]
    public void Parse_Oas20OrWrongMajor_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Parse("""{"openapi":"2.0.0","info":{"title":"T","version":"1"},"paths":{}}"""));

    [Fact]
    public void Parse_NoRootSchema_RootIsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(Parse(MinimalOas30).Root);

    [Fact]
    public void Parse_WithRootSchema_RootIsNamedNode()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Age": { "type": "integer", "minimum": 0, "maximum": 130 }
                    }
                }
            }
            """;
        var src = Parse(json, "Age");
        var node = Assert.IsType<NumberSchemaNode>(src.Root);
        Assert.True(node.IsInteger);
        Assert.Equal(0, node.Minimum);
        Assert.Equal(130, node.Maximum);
    }

    [Fact]
    public void Parse_UnknownRootSchema_Throws() =>
        Assert.Throws<ArgumentException>(() => Parse(MinimalOas30, "NoSuch"));

    [Fact]
    public void Resolve_ComponentsSchemasPointer_ReturnsCorrectNode()
    {
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Name": { "type": "string", "minLength": 2, "maxLength": 100 }
                    }
                }
            }
            """;
        var node = Assert.IsType<StringSchemaNode>(
            Parse(json).Resolve("#/components/schemas/Name"));
        Assert.Equal(2, node.MinLength);
        Assert.Equal(100, node.MaxLength);
    }

    [Fact]
    public void Resolve_UnknownSchema_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Parse(MinimalOas30).Resolve("#/components/schemas/Missing"));
    }

    [Fact]
    public void Resolve_NonComponentsPointer_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            Parse(MinimalOas30).Resolve("#/$defs/Foo"));
    }

    [Fact]
    public void Resolve_DefinitionsPointer_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            Parse(MinimalOas30).Resolve("#/definitions/Foo"));
    }

    [Fact]
    public void Parse_Oas30_NullableString_ProducesAnyOfWithNull()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "MaybeString": { "type": "string", "nullable": true }
                    }
                }
            }
            """;
        var src = Parse(json);
        var node = Assert.IsType<AnyOfSchemaNode>(src.Resolve("#/components/schemas/MaybeString"));
        Assert.Equal(2, node.Schemas.Count);
        Assert.IsType<StringSchemaNode>(node.Schemas[0]);
        Assert.IsType<NullSchemaNode>(node.Schemas[1]);
    }

    [Fact]
    public void Parse_Oas30_NullableInteger_ProducesAnyOfWithNull()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "MaybeInt": { "type": "integer", "nullable": true }
                    }
                }
            }
            """;
        var src = Parse(json);
        var node = Assert.IsType<AnyOfSchemaNode>(src.Resolve("#/components/schemas/MaybeInt"));
        Assert.IsType<NumberSchemaNode>(node.Schemas[0]);
        Assert.IsType<NullSchemaNode>(node.Schemas[1]);
    }

    [Fact]
    public void Parse_Oas30_NonNullable_DoesNotProduceAnyOf()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Str": { "type": "string" }
                    }
                }
            }
            """;
        Assert.IsType<StringSchemaNode>(Parse(json).Resolve("#/components/schemas/Str"));
    }

    [Fact]
    public void Parse_Oas31_NullableViaTypeArray_ProducesAnyOfWithNull()
    {
        // In OAS 3.1 the idiomatic way to express nullable is type: ["string", "null"]
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "MaybeString": { "type": ["string", "null"] }
                    }
                }
            }
            """;
        var src = Parse(json);
        var node = Assert.IsType<AnyOfSchemaNode>(
            src.Resolve("#/components/schemas/MaybeString"));
        Assert.Equal(2, node.Schemas.Count);
        Assert.IsType<StringSchemaNode>(node.Schemas[0]);
        Assert.IsType<NullSchemaNode>(node.Schemas[1]);
    }

    [Fact]
    public void Parse_Oas31_NullableKeyword_IsIgnoredAndDoesNotProduceAnyOf()
    {
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "MaybeString": { "type": "string", "nullable": true }
                    }
                }
            }
            """;
        var src = Parse(json);
        Assert.IsType<StringSchemaNode>(src.Resolve("#/components/schemas/MaybeString"));
    }

    [Fact]
    public void Parse_CrossSchemaRef_ResolvesCorrectly()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Category": { "type": "string" },
                        "Pet": {
                            "type": "object",
                            "properties": {
                                "category": { "$ref": "#/components/schemas/Category" }
                            }
                        }
                    }
                }
            }
            """;
        var src = Parse(json);
        var pet = Assert.IsType<ObjectSchemaNode>(src.Resolve("#/components/schemas/Pet"));
        Assert.IsType<StringSchemaNode>(pet.Properties["category"]);
    }

    [Fact]
    public void SchemaNames_ReflectsAllParsedSchemas()
    {
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "A": { "type": "string" },
                        "B": { "type": "integer" },
                        "C": { "type": "boolean" }
                    }
                }
            }
            """;
        var names = Parse(json).SchemaNames.ToHashSet();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
        Assert.Contains("C", names);
    }

    [Fact]
    public void WithRoot_ChangesRoot()
    {
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": {
                        "Foo": { "type": "boolean" },
                        "Bar": { "type": "string"  }
                    }
                }
            }
            """;
        var src = Parse(json).WithRoot("Foo");
        Assert.IsType<BooleanSchemaNode>(src.Root);
        Assert.IsType<StringSchemaNode>(src.Resolve("#/components/schemas/Bar"));
    }

    [Fact]
    public void FromJson_Factory_ImplementsISchemaSource()
    {
        const string json = """
            {
                "openapi": "3.1.0",
                "info": { "title": "T", "version": "1" },
                "paths": {},
                "components": {
                    "schemas": { "X": { "type": "null" } }
                }
            }
            """;
        ISchemaSource src = OpenApiSchemaSource.FromJson(json, "X");
        Assert.IsType<NullSchemaNode>(src.Root);
    }

    [Fact]
    public void Parse_NoComponentsSection_EmptySource()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {}
            }
            """;
        var src = Parse(json);
        Assert.IsType<TrueSchemaNode>(src.Root);
        Assert.Empty(src.SchemaNames);
    }

    [Fact]
    public void ParseRequestBody_RefSchema_ResolvesCorrectly()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/pets": {
                        "post": {
                            "requestBody": {
                                "content": {
                                    "application/json": {
                                        "schema": { "$ref": "#/components/schemas/Pet" }
                                    }
                                }
                            },
                            "responses": {}
                        }
                    }
                },
                "components": {
                    "schemas": { "Pet": { "type": "object" } }
                }
            }
            """;
        var src = new OpenApiParser().ParseRequestBody(json, "/pets", "post");
        Assert.IsType<ObjectSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseResponse_RefSchema_ResolvesCorrectly()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/pets/{id}": {
                        "get": {
                            "responses": {
                                "200": {
                                    "content": {
                                        "application/json": {
                                            "schema": { "$ref": "#/components/schemas/Pet" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                "components": {
                    "schemas": { "Pet": { "type": "object" } }
                }
            }
            """;
        var src = new OpenApiParser().ParseResponse(json, "/pets/{id}", "get", "200");
        Assert.IsType<ObjectSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseRequestBody_InlineSchema_ParsedDirectly()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/items": {
                        "post": {
                            "requestBody": {
                                "content": {
                                    "application/json": {
                                        "schema": { "type": "string" }
                                    }
                                }
                            },
                            "responses": {}
                        }
                    }
                }
            }
            """;
        var src = new OpenApiParser().ParseRequestBody(json, "/items", "post");
        Assert.IsType<StringSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseResponse_InlineSchema_ParsedDirectly()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/ping": {
                        "get": {
                            "responses": {
                                "200": {
                                    "content": {
                                        "application/json": {
                                            "schema": { "type": "boolean" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;
        var src = new OpenApiParser().ParseResponse(json, "/ping", "get", "200");
        Assert.IsType<BooleanSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseRequestBody_PathNotFound_ReturnsTrueNode()
    {
        var src = new OpenApiParser().ParseRequestBody(MinimalOas30, "/nonexistent", "post");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseRequestBody_MethodNotFound_ReturnsTrueNode()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": { "/items": { "get": { "responses": {} } } }
            }
            """;
        var src = new OpenApiParser().ParseRequestBody(json, "/items", "delete");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseResponse_PathNotFound_ReturnsTrueNode()
    {
        var src = new OpenApiParser().ParseResponse(MinimalOas30, "/nonexistent", "get", "200");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }

    [Fact]
    public void ParseResponse_StatusCodeNotFound_ReturnsTrueNode()
    {
        const string json = """
            {
                "openapi": "3.0.3",
                "info": { "title": "T", "version": "1" },
                "paths": {
                    "/items": {
                        "get": { "responses": { "200": {} } }
                    }
                }
            }
            """;
        var src = new OpenApiParser().ParseResponse(json, "/items", "get", "404");
        Assert.IsType<TrueSchemaNode>(src.Root);
    }
}
