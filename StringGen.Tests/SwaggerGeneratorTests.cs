using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using StringGen.Core;
using StringGen.Core.Swagger;

namespace StringGen.Tests;

public class SwaggerGeneratorTests
{
    private const string PetstoreSwagger = """
        {
            "swagger": "2.0",
            "info": { "title": "Petstore", "version": "1.0" },
            "definitions": {
                "Pet": {
                    "type": "object",
                    "required": ["id", "name"],
                    "properties": {
                        "id":   { "type": "integer", "minimum": 1 },
                        "name": { "type": "string",  "minLength": 1, "maxLength": 50 },
                        "tag":  { "type": "string" }
                    }
                },
                "Error": {
                    "type": "object",
                    "required": ["code", "message"],
                    "properties": {
                        "code":    { "type": "integer" },
                        "message": { "type": "string"  }
                    }
                }
            },
            "paths": {
                "/pets": {
                    "post": {
                        "parameters": [
                            {
                                "in": "body",
                                "name": "body",
                                "schema": { "$ref": "#/definitions/Pet" }
                            }
                        ],
                        "responses": {
                            "200": { "schema": { "$ref": "#/definitions/Pet"   } },
                            "400": { "schema": { "$ref": "#/definitions/Error" } }
                        }
                    },
                    "get": {
                        "responses": {
                            "200": {
                                "schema": {
                                    "type": "array",
                                    "items": { "$ref": "#/definitions/Pet" }
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

    private const string PetstoreOpenApi = """
        {
            "openapi": "3.0.0",
            "info": { "title": "Petstore", "version": "1.0" },
            "components": {
                "schemas": {
                    "Pet": {
                        "type": "object",
                        "required": ["id", "name"],
                        "properties": {
                            "id":   { "type": "integer", "minimum": 1 },
                            "name": { "type": "string",  "minLength": 1, "maxLength": 50 },
                            "tag":  { "type": "string" }
                        }
                    },
                    "Error": {
                        "type": "object",
                        "required": ["code", "message"],
                        "properties": {
                            "code":    { "type": "integer" },
                            "message": { "type": "string"  }
                        }
                    }
                }
            },
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
                        "responses": {
                            "200": {
                                "content": {
                                    "application/json": {
                                        "schema": { "$ref": "#/components/schemas/Pet" }
                                    }
                                }
                            },
                            "400": {
                                "content": {
                                    "application/json": {
                                        "schema": { "$ref": "#/components/schemas/Error" }
                                    }
                                }
                            }
                        }
                    },
                    "get": {
                        "responses": {
                            "200": {
                                "content": {
                                    "application/json": {
                                        "schema": {
                                            "type": "array",
                                            "items": { "$ref": "#/components/schemas/Pet" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

    private static JsonObject ParseObject(string json) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(json));

    private static T Val<T>(JsonNode? node) =>
        Assert.IsAssignableFrom<JsonValue>(node).GetValue<T>();

    [Fact]
    public void GenerateSchema_Swagger_Pet_ReturnsValidJson()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 1);
        var result = gen.GenerateSchema("Pet", 1);

        Assert.Single(result);
        var obj = ParseObject(result[0]);
        Assert.True(obj.ContainsKey("id"), "id required");
        Assert.True(obj.ContainsKey("name"), "name required");
        Assert.True(Val<long>(obj["id"]) >= 1);
        Assert.True(Val<string>(obj["name"]).Length >= 1);
    }

    [Fact]
    public void GenerateSchema_Swagger_Count_ReturnsCorrectCount()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var results = gen.GenerateSchema("Pet", 5);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void GenerateSchema_Swagger_ZeroCount_ReturnsEmpty()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Empty(gen.GenerateSchema("Pet", 0));
    }

    [Fact]
    public void GenerateSchema_Swagger_UnknownName_Throws()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger);
        Assert.Throws<ArgumentException>(() => gen.GenerateSchema("Nonexistent"));
    }

    [Fact]
    public void GenerateSchema_Swagger_Error_HasRequiredFields()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 42);
        var obj = ParseObject(gen.GenerateSchema("Error", 1)[0]);
        Assert.True(obj.ContainsKey("code"));
        Assert.True(obj.ContainsKey("message"));
    }

    [Fact]
    public void GenerateSchema_OpenApi_Pet_ReturnsValidJson()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 1);
        var result = gen.GenerateSchema("Pet", 1);

        Assert.Single(result);
        var obj = ParseObject(result[0]);
        Assert.True(obj.ContainsKey("id"), "id required");
        Assert.True(obj.ContainsKey("name"), "name required");
        Assert.True(Val<long>(obj["id"]) >= 1);
    }

    [Fact]
    public void GenerateSchema_OpenApi_Count_ReturnsCorrectCount()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 0);
        Assert.Equal(3, gen.GenerateSchema("Pet", 3).Count);
    }

    [Fact]
    public void GenerateSchema_OpenApi_UnknownName_Throws()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi);
        Assert.Throws<ArgumentException>(() => gen.GenerateSchema("Missing"));
    }

    [Fact]
    public void GenerateSchema_SameSeed_ProducesSameOutput()
    {
        var gen1 = new SwaggerGenerator(PetstoreSwagger, seed: 99);
        var gen2 = new SwaggerGenerator(PetstoreSwagger, seed: 99);
        Assert.Equal(gen1.GenerateSchema("Pet"), gen2.GenerateSchema("Pet"));
    }

    [Fact]
    public void GenerateSchema_DifferentSeeds_ProduceDifferentOutput()
    {
        var gen1 = new SwaggerGenerator(PetstoreSwagger, seed: 1);
        var gen2 = new SwaggerGenerator(PetstoreSwagger, seed: 2);
        var r1 = gen1.GenerateSchema("Pet");
        var r2 = gen2.GenerateSchema("Pet");
        Assert.NotEqual(r1[0], r2[0]);
    }

    [Fact]
    public void GenerateRequestBody_Swagger_Post_Pets_IsValidPet()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 5);
        var obj = ParseObject(gen.GenerateRequestBody("POST", "/pets", 1)[0]);
        Assert.True(obj.ContainsKey("id"));
        Assert.True(obj.ContainsKey("name"));
    }

    [Fact]
    public void GenerateRequestBody_Swagger_Count_ReturnsCorrectCount()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Equal(4, gen.GenerateRequestBody("POST", "/pets", 4).Count);
    }

    [Fact]
    public void GenerateRequestBody_Swagger_MethodCaseInsensitive()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 7);
        var upper = gen.GenerateRequestBody("POST", "/pets");
        var lower = gen.GenerateRequestBody("post", "/pets");
        Assert.Single(upper);
        Assert.Single(lower);
    }

    [Fact]
    public void GenerateRequestBody_OpenApi_Post_Pets_IsValidPet()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 3);
        var obj = ParseObject(gen.GenerateRequestBody("POST", "/pets", 1)[0]);
        Assert.True(obj.ContainsKey("id"));
        Assert.True(obj.ContainsKey("name"));
    }

    [Fact]
    public void GenerateResponseBody_Swagger_200_IsValidPet()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 10);
        var obj = ParseObject(gen.GenerateResponseBody("POST", "/pets", "200", 1)[0]);
        Assert.True(obj.ContainsKey("id"));
        Assert.True(obj.ContainsKey("name"));
    }

    [Fact]
    public void GenerateResponseBody_Swagger_400_IsValidError()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 11);
        var obj = ParseObject(gen.GenerateResponseBody("POST", "/pets", "400", 1)[0]);
        Assert.True(obj.ContainsKey("code"));
        Assert.True(obj.ContainsKey("message"));
    }

    [Fact]
    public void GenerateResponseBody_Swagger_DefaultStatusCode_Is200()
    {
        var gen1 = new SwaggerGenerator(PetstoreSwagger, seed: 20);
        var gen2 = new SwaggerGenerator(PetstoreSwagger, seed: 20);
        var r1 = gen1.GenerateResponseBody("POST", "/pets");
        var r2 = gen2.GenerateResponseBody("POST", "/pets", "200");
        Assert.Equal(r1[0], r2[0]);
    }

    [Fact]
    public void GenerateResponseBody_Swagger_GetPets_ReturnsArray()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 30);
        var json = gen.GenerateResponseBody("GET", "/pets", "200")[0];
        var node = JsonNode.Parse(json);
        Assert.IsType<JsonArray>(node);
    }

    [Fact]
    public void GenerateResponseBody_OpenApi_200_IsValidPet()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 12);
        var obj = ParseObject(gen.GenerateResponseBody("POST", "/pets", "200", 1)[0]);
        Assert.True(obj.ContainsKey("id"));
        Assert.True(obj.ContainsKey("name"));
    }

    [Fact]
    public void GenerateResponseBody_OpenApi_400_IsValidError()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 13);
        var obj = ParseObject(gen.GenerateResponseBody("POST", "/pets", "400", 1)[0]);
        Assert.True(obj.ContainsKey("code"));
        Assert.True(obj.ContainsKey("message"));
    }

    [Fact]
    public void Constructor_Swagger_DetectsFormat()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.NotNull(gen);
    }

    [Fact]
    public void Constructor_OpenApi_DetectsFormat()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 0);
        Assert.NotNull(gen);
    }

    [Fact]
    public void Constructor_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new SwaggerGenerator("not json"));
    }

    [Fact]
    public void Constructor_SwaggerWrongVersion_Throws()
    {
        const string bad = """{"swagger":"1.0","info":{"title":"T","version":"1"},"paths":{}}""";
        Assert.Throws<ArgumentException>(() => new SwaggerGenerator(bad));
    }

    [Fact]
    public void GenerateSchema_NegativeCount_Throws()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.GenerateSchema("Pet", -1));
    }

    [Fact]
    public void GenerateRequestBody_NegativeCount_Throws()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            gen.GenerateRequestBody("post", "/pets", -1));
    }

    [Fact]
    public void GenerateResponseBody_NegativeCount_Throws()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            gen.GenerateResponseBody("post", "/pets", "200", -1));
    }

    [Fact]
    public void GenerateSchema_NullName_ThrowsArgumentNull()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        Assert.Throws<ArgumentNullException>(() => gen.GenerateSchema(null!));
    }

    [Fact]
    public void GenerateRequestBody_UnknownPath_GeneratesWithoutThrowing()
    {
        // When the path does not exist, the parser returns TrueSchemaNode.
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var docs = gen.GenerateRequestBody("get", "/nonexistent", 1);
        Assert.Single(docs);
    }

    [Fact]
    public void GenerateResponseBody_UnknownPath_GeneratesWithoutThrowing()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var docs = gen.GenerateResponseBody("get", "/nonexistent", "200", 1);
        Assert.Single(docs);
    }

    [Fact]
    public void GenerateRequestBody_CalledTwiceSamePath_ReturnsBothResults()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 42);
        var r1 = gen.GenerateRequestBody("post", "/pets", 1);
        var r2 = gen.GenerateRequestBody("post", "/pets", 1);
        Assert.Single(r1);
        Assert.Single(r2);
    }

    [Fact]
    public void GenerateResponseBody_CalledTwiceSamePath_ReturnsBothResults()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 99);
        var r1 = gen.GenerateResponseBody("post", "/pets", "200", 1);
        var r2 = gen.GenerateResponseBody("post", "/pets", "200", 1);
        Assert.Single(r1);
        Assert.Single(r2);
    }

    [Fact]
    public void GenerateSchema_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.GenerateSchema("Pet", 5, ct));
    }

    [Fact]
    public void GenerateRequestBody_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.GenerateRequestBody("post", "/pets", 5, ct));
    }

    [Fact]
    public void GenerateResponseBody_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.GenerateResponseBody("post", "/pets", "200", 5, ct));
    }

    [Fact]
    public void GenerateRequestBody_MethodCaseInsensitive_CacheHit()
    {
        var gen1 = new SwaggerGenerator(PetstoreSwagger, seed: 5);
        gen1.GenerateRequestBody("POST", "/pets", 1);
        gen1.GenerateRequestBody("post", "/pets", 1); // second call must not throw
        Assert.True(true);
    }

    [Fact]
    public void GenerateSchema_NonCompliant_FirstResultIsWrongType()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var results = gen.GenerateSchema("Pet", 1, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(1);
        JsonNode.Parse(results[0]).Should().BeOfType<JsonArray>();
    }

    [Fact]
    public void GenerateSchema_NonCompliant_CyclesThroughStrategies()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var results = gen.GenerateSchema("Pet", 8, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(8);
        var emptyObj = JsonNode.Parse(results[1]).Should().BeOfType<JsonObject>().Subject;
        emptyObj.Count.Should().Be(0);
        var withLowId = JsonNode.Parse(results[4]).Should().BeOfType<JsonObject>().Subject;
        withLowId["id"].Should().BeAssignableTo<JsonValue>().Which.GetValue<double>().Should().Be(0.0);
    }

    [Fact]
    public void GenerateRequestBody_NonCompliant_ProducesWrongType()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var results = gen.GenerateRequestBody("post", "/pets", 1, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(1);
        JsonNode.Parse(results[0]).Should().BeOfType<JsonArray>();
    }

    [Fact]
    public void GenerateResponseBody_NonCompliant_ProducesWrongType()
    {
        var gen = new SwaggerGenerator(PetstoreSwagger, seed: 0);
        var results = gen.GenerateResponseBody("post", "/pets", "200", 1, mode: GenerationMode.NonCompliant);
        results.Should().HaveCount(1);
        JsonNode.Parse(results[0]).Should().BeOfType<JsonArray>();
    }

    [Fact]
    public void GenerateSchema_OpenApi_NonCompliant_ProducesWrongType()
    {
        var gen = new SwaggerGenerator(PetstoreOpenApi, seed: 0);
        var results = gen.GenerateSchema("Pet", 1, mode: GenerationMode.NonCompliant);
        JsonNode.Parse(results[0]).Should().BeOfType<JsonArray>();
    }
}
