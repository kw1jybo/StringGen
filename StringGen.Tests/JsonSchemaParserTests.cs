using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Tests;

public class JsonSchemaParserTests
{
    private static JsonSchemaNode Parse(string json) =>
        new JsonSchemaParser().Parse(json);

    [Fact]
    public void Parse_True_ReturnsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(Parse("true"));

    [Fact]
    public void Parse_False_ReturnsFalseNode() =>
        Assert.IsType<FalseSchemaNode>(Parse("false"));

    [Fact]
    public void Parse_StringType_ReturnsStringNode_NoConstraints()
    {
        var node = Assert.IsType<StringSchemaNode>(Parse("""{"type":"string"}"""));
        Assert.Null(node.MinLength);
        Assert.Null(node.MaxLength);
        Assert.Null(node.Pattern);
    }

    [Fact]
    public void Parse_StringType_SetsMinMaxLengthAndPattern()
    {
        var node = Assert.IsType<StringSchemaNode>(
            Parse("""{"type":"string","minLength":3,"maxLength":10,"pattern":"^\\d+$"}"""));
        Assert.Equal(3, node.MinLength);
        Assert.Equal(10, node.MaxLength);
        Assert.Equal(@"^\d+$", node.Pattern);
    }

    [Fact]
    public void Parse_NumberType_IsNotInteger()
    {
        var node = Assert.IsType<NumberSchemaNode>(Parse("""{"type":"number"}"""));
        Assert.False(node.IsInteger);
    }

    [Fact]
    public void Parse_IntegerType_IsInteger()
    {
        var node = Assert.IsType<NumberSchemaNode>(Parse("""{"type":"integer"}"""));
        Assert.True(node.IsInteger);
    }

    [Fact]
    public void Parse_NumberType_SetsBoundsAndMultipleOf()
    {
        var node = Assert.IsType<NumberSchemaNode>(
            Parse("""{"type":"number","minimum":1.5,"maximum":9.5,"multipleOf":0.5}"""));
        Assert.Equal(1.5, node.Minimum);
        Assert.Equal(9.5, node.Maximum);
        Assert.Equal(0.5, node.MultipleOf);
    }

    [Fact]
    public void Parse_NumberType_SetsExclusiveBounds()
    {
        var node = Assert.IsType<NumberSchemaNode>(
            Parse("""{"type":"number","exclusiveMinimum":0,"exclusiveMaximum":100}"""));
        Assert.Equal(0, node.ExclusiveMinimum);
        Assert.Equal(100, node.ExclusiveMaximum);
    }

    [Fact]
    public void Parse_BooleanType_ReturnsBooleanNode() =>
        Assert.IsType<BooleanSchemaNode>(Parse("""{"type":"boolean"}"""));

    [Fact]
    public void Parse_NullType_ReturnsNullNode() =>
        Assert.IsType<NullSchemaNode>(Parse("""{"type":"null"}"""));

    [Fact]
    public void Parse_ObjectType_ReturnsObjectNode_WithPropertiesAndRequired()
    {
        var node = Assert.IsType<ObjectSchemaNode>(
            Parse("""{"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}"""));
        Assert.Single(node.Properties);
        Assert.IsType<NumberSchemaNode>(node.Properties["id"]);
        Assert.Equal(new[] { "id" }, node.Required);
        Assert.IsType<TrueSchemaNode>(node.AdditionalProperties);
    }

    [Fact]
    public void Parse_ObjectType_AdditionalPropertiesFalse_SetsFalseNode()
    {
        var node = Assert.IsType<ObjectSchemaNode>(
            Parse("""{"type":"object","additionalProperties":false}"""));
        Assert.IsType<FalseSchemaNode>(node.AdditionalProperties);
    }

    [Fact]
    public void Parse_ObjectType_AdditionalPropertiesSchema_SetsSchemaNode()
    {
        var node = Assert.IsType<ObjectSchemaNode>(
            Parse("""{"type":"object","additionalProperties":{"type":"string"}}"""));
        Assert.IsType<StringSchemaNode>(node.AdditionalProperties);
    }

    [Fact]
    public void Parse_ObjectType_EmptyObject_HasNoPropertiesOrRequired()
    {
        var node = Assert.IsType<ObjectSchemaNode>(Parse("""{"type":"object"}"""));
        Assert.Empty(node.Properties);
        Assert.Empty(node.Required);
    }

    [Fact]
    public void Parse_ArrayType_ReturnsArrayNode_WithItems()
    {
        var node = Assert.IsType<ArraySchemaNode>(
            Parse("""{"type":"array","items":{"type":"string"},"minItems":1,"maxItems":5}"""));
        Assert.IsType<StringSchemaNode>(node.Items);
        Assert.Equal(1, node.MinItems);
        Assert.Equal(5, node.MaxItems);
    }

    [Fact]
    public void Parse_ArrayType_UniqueItems_SetsFlag()
    {
        var node = Assert.IsType<ArraySchemaNode>(
            Parse("""{"type":"array","uniqueItems":true}"""));
        Assert.True(node.UniqueItems);
    }

    [Fact]
    public void Parse_ArrayType_NoItems_ItemsIsNull()
    {
        var node = Assert.IsType<ArraySchemaNode>(Parse("""{"type":"array"}"""));
        Assert.Null(node.Items);
    }

    [Fact]
    public void Parse_EnumKeyword_ReturnsEnumNode_WithCorrectCount()
    {
        var node = Assert.IsType<EnumSchemaNode>(
            Parse("""{"enum":["a","b","c"]}"""));
        Assert.Equal(3, node.Values.Count);
    }

    [Fact]
    public void Parse_EnumKeyword_IncludesNullValue()
    {
        var node = Assert.IsType<EnumSchemaNode>(
            Parse("""{"enum":["x",null]}"""));
        Assert.Equal(2, node.Values.Count);
        Assert.Null(node.Values[1]);
    }

    [Fact]
    public void Parse_ConstKeyword_ReturnsConstNode()
    {
        var node = Assert.IsType<ConstSchemaNode>(
            Parse("""{"const":42}"""));
        Assert.Equal(42, Assert.IsAssignableFrom<JsonValue>(node.Value).GetValue<int>());
    }

    [Fact]
    public void Parse_ConstKeyword_NullValue_ReturnsConstNodeWithNullValue()
    {
        var node = Assert.IsType<ConstSchemaNode>(Parse("""{"const":null}"""));
        Assert.Null(node.Value);
    }

    [Fact]
    public void Parse_AnyOf_ReturnsAnyOfNode_WithCorrectSubSchemas()
    {
        var node = Assert.IsType<AnyOfSchemaNode>(
            Parse("""{"anyOf":[{"type":"string"},{"type":"integer"}]}"""));
        Assert.Equal(2, node.Schemas.Count);
        Assert.IsType<StringSchemaNode>(node.Schemas[0]);
        Assert.IsType<NumberSchemaNode>(node.Schemas[1]);
    }

    [Fact]
    public void Parse_OneOf_ReturnsOneOfNode()
    {
        var node = Assert.IsType<OneOfSchemaNode>(
            Parse("""{"oneOf":[{"type":"string"},{"type":"integer"}]}"""));
        Assert.Equal(2, node.Schemas.Count);
    }

    [Fact]
    public void Parse_AllOf_ReturnsAllOfNode()
    {
        var node = Assert.IsType<AllOfSchemaNode>(
            Parse("""{"allOf":[{"minimum":0},{"maximum":100}]}"""));
        Assert.Equal(2, node.Schemas.Count);
    }

    [Fact]
    public void Parse_TypeArray_ReturnsAnyOfNode()
    {
        var node = Assert.IsType<AnyOfSchemaNode>(
            Parse("""{"type":["string","null"]}"""));
        Assert.Equal(2, node.Schemas.Count);
        Assert.IsType<StringSchemaNode>(node.Schemas[0]);
        Assert.IsType<NullSchemaNode>(node.Schemas[1]);
    }

    [Fact]
    public void Parse_TypeArraySingleEntry_ReturnsBareNode()
    {
        Assert.IsType<StringSchemaNode>(Parse("""{"type":["string"]}"""));
    }

    [Fact]
    public void Parse_EmptyObject_ReturnsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(Parse("{}"));

    [Fact]
    public void Parse_RefToDefs_ResolvesInlineToTargetNode()
    {
        var node = Assert.IsType<StringSchemaNode>(Parse("""
            {
                "$defs": { "Name": { "type": "string", "minLength": 5 } },
                "$ref": "#/$defs/Name"
            }
            """));
        Assert.Equal(5, node.MinLength);
    }

    [Fact]
    public void Parse_RefToDefinitions_ResolvesInlineToTargetNode()
    {
        var node = Assert.IsType<NumberSchemaNode>(Parse("""
            {
                "definitions": { "Age": { "type": "integer" } },
                "$ref": "#/definitions/Age"
            }
            """));
        Assert.True(node.IsInteger);
    }

    [Fact]
    public void Parse_ObjectPropertyWithRef_ResolvesCorrectly()
    {
        var obj = Assert.IsType<ObjectSchemaNode>(Parse("""
            {
                "$defs": { "Id": { "type": "integer" } },
                "type": "object",
                "properties": { "id": { "$ref": "#/$defs/Id" } }
            }
            """));
        Assert.IsType<NumberSchemaNode>(obj.Properties["id"]);
    }

    [Fact]
    public void Parse_UnknownRef_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Parse("""{"$ref":"#/$defs/DoesNotExist"}"""));
        Assert.Contains("DoesNotExist", ex.Message);
    }

    [Fact]
    public void Parse_CircularRef_Throws()
    {
        Assert.Throws<ArgumentException>(() => Parse("""
            {
                "$defs": { "A": { "$ref": "#/$defs/A" } },
                "$ref": "#/$defs/A"
            }
            """));
    }

    [Fact]
    public void Parse_ExternalRef_EmitsRefNode()
    {
        // External $ref (non-intra-doc) cannot be resolved here; a RefSchemaNode is emitted.
        var node = Assert.IsType<RefSchemaNode>(
            Parse("""{"$ref":"https://example.com/schema.json"}"""));
        Assert.Equal("https://example.com/schema.json", node.Reference);
    }

    [Fact]
    public void Parse_MinimumWithoutType_InfersNumberNode()
    {
        var node = Assert.IsType<NumberSchemaNode>(Parse("""{"minimum":0}"""));
        Assert.Equal(0, node.Minimum);
    }

    [Fact]
    public void Parse_MaximumWithoutType_InfersNumberNode() =>
        Assert.IsType<NumberSchemaNode>(Parse("""{"maximum":100}"""));

    [Fact]
    public void Parse_MinLengthWithoutType_InfersStringNode()
    {
        var node = Assert.IsType<StringSchemaNode>(Parse("""{"minLength":1}"""));
        Assert.Equal(1, node.MinLength);
    }

    [Fact]
    public void Parse_MaxLengthWithoutType_InfersStringNode() =>
        Assert.IsType<StringSchemaNode>(Parse("""{"maxLength":50}"""));

    [Fact]
    public void Parse_PatternWithoutType_InfersStringNode()
    {
        var node = Assert.IsType<StringSchemaNode>(Parse("""{"pattern":"^\\d+$"}"""));
        Assert.Equal(@"^\d+$", node.Pattern);
    }

    [Fact]
    public void Parse_PropertiesWithoutType_InfersObjectNode()
    {
        var node = Assert.IsType<ObjectSchemaNode>(
            Parse("""{"properties":{"x":{"type":"string"}}}"""));
        Assert.True(node.Properties.ContainsKey("x"));
    }

    [Fact]
    public void Parse_MinItemsWithoutType_InfersArrayNode()
    {
        var node = Assert.IsType<ArraySchemaNode>(Parse("""{"minItems":2}"""));
        Assert.Equal(2, node.MinItems);
    }

    [Fact]
    public void Parse_ItemsWithoutType_InfersArrayNode() =>
        Assert.IsType<ArraySchemaNode>(Parse("""{"items":{"type":"string"}}"""));

    [Fact]
    public void Parse_TypeIsNumber_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => Parse("""{"type":42}"""));

    [Fact]
    public void Parse_UnknownTypeString_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => Parse("""{"type":"unicorn"}"""));
}
