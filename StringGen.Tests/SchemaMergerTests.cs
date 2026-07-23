using System.Text.Json.Nodes;
using StringGen.Core.JsonSchema;

namespace StringGen.Tests;

public class SchemaMergerTests
{
    [Fact]
    public void Merge_EmptyList_ReturnsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(SchemaMerger.Merge([]));

    [Fact]
    public void Merge_SingleTrueNode_ReturnsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(SchemaMerger.Merge([new TrueSchemaNode()]));

    [Fact]
    public void Merge_MultipleTrueNodes_ReturnsTrueNode() =>
        Assert.IsType<TrueSchemaNode>(SchemaMerger.Merge([new TrueSchemaNode(), new TrueSchemaNode()]));

    [Fact]
    public void Merge_SingleNonTrueSchema_ReturnsThatSchema()
    {
        var schema = new StringSchemaNode(MinLength: 3);
        Assert.Same(schema, SchemaMerger.Merge([schema]));
    }

    [Fact]
    public void Merge_SingleNonTrueSchema_WithTrueSchemas_ReturnsThatSchema()
    {
        var schema = new NumberSchemaNode(IsInteger: true);
        var result = SchemaMerger.Merge([new TrueSchemaNode(), schema, new TrueSchemaNode()]);
        Assert.Same(schema, result);
    }

    [Fact]
    public void Merge_ContainsFalseSchemaNode_ReturnsFalseNode() =>
        Assert.IsType<FalseSchemaNode>(
            SchemaMerger.Merge([new StringSchemaNode(), new FalseSchemaNode()]));

    [Fact]
    public void Merge_OnlyFalseSchemaNode_ReturnsFalseNode() =>
        Assert.IsType<FalseSchemaNode>(SchemaMerger.Merge([new FalseSchemaNode()]));

    [Fact]
    public void Merge_StringAndNumber_ThrowsIncompatibleTypes() =>
        Assert.Throws<InvalidOperationException>(() =>
            SchemaMerger.Merge([new StringSchemaNode(), new NumberSchemaNode()]));

    [Fact]
    public void Merge_ObjectAndArray_ThrowsIncompatibleTypes() =>
        Assert.Throws<InvalidOperationException>(() =>
            SchemaMerger.Merge([
                new ObjectSchemaNode(new Dictionary<string, JsonSchemaNode>(), [], new TrueSchemaNode()),
                new ArraySchemaNode()
            ]));

    [Fact]
    public void Merge_BooleanAndNull_ThrowsIncompatibleTypes() =>
        Assert.Throws<InvalidOperationException>(() =>
            SchemaMerger.Merge([new BooleanSchemaNode(), new NullSchemaNode()]));

    [Fact]
    public void Merge_AnyOfNodeInAllOf_ThrowsNotSupported() =>
        Assert.Throws<NotSupportedException>(() =>
            SchemaMerger.Merge([
                new AnyOfSchemaNode([new StringSchemaNode()]),
                new AnyOfSchemaNode([new NumberSchemaNode()])
            ]));

    [Fact]
    public void Merge_RefNodeInAllOf_ThrowsNotSupported() =>
        Assert.Throws<NotSupportedException>(() =>
            SchemaMerger.Merge([
                new RefSchemaNode("#/$defs/Foo"),
                new RefSchemaNode("#/$defs/Bar")
            ]));

    [Fact]
    public void Merge_Strings_TakesMaxMinLength()
    {
        var result = Assert.IsType<StringSchemaNode>(
            SchemaMerger.Merge([
                new StringSchemaNode(MinLength: 3),
                new StringSchemaNode(MinLength: 7)
            ]));
        Assert.Equal(7, result.MinLength);
    }

    [Fact]
    public void Merge_Strings_TakesMinMaxLength()
    {
        var result = Assert.IsType<StringSchemaNode>(
            SchemaMerger.Merge([
                new StringSchemaNode(MaxLength: 20),
                new StringSchemaNode(MaxLength: 10)
            ]));
        Assert.Equal(10, result.MaxLength);
    }

    [Fact]
    public void Merge_Strings_ConflictingPatterns_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SchemaMerger.Merge([
                new StringSchemaNode(Pattern: @"^\d+$"),
                new StringSchemaNode(Pattern: @"^[a-z]+$")
            ]));
    }

    [Fact]
    public void Merge_Strings_SinglePattern_Retained()
    {
        var result = Assert.IsType<StringSchemaNode>(
            SchemaMerger.Merge([
                new StringSchemaNode(),
                new StringSchemaNode(Pattern: @"^\d+$")
            ]));
        Assert.Equal(@"^\d+$", result.Pattern);
    }

    [Fact]
    public void Merge_Strings_SamePatternRepeated_Retained()
    {
        var result = Assert.IsType<StringSchemaNode>(
            SchemaMerger.Merge([
                new StringSchemaNode(Pattern: @"^\d+$"),
                new StringSchemaNode(Pattern: @"^\d+$")
            ]));
        Assert.Equal(@"^\d+$", result.Pattern);
    }

    [Fact]
    public void Merge_Strings_NullMaxLengths_NullResult()
    {
        var result = Assert.IsType<StringSchemaNode>(
            SchemaMerger.Merge([new StringSchemaNode(), new StringSchemaNode()]));
        Assert.Null(result.MaxLength);
    }

    [Fact]
    public void Merge_Numbers_IntersectsRange()
    {
        var result = Assert.IsType<NumberSchemaNode>(
            SchemaMerger.Merge([
                new NumberSchemaNode(Minimum: 5, Maximum: 100),
                new NumberSchemaNode(Minimum: 10, Maximum: 50)
            ]));
        Assert.Equal(10, result.Minimum);
        Assert.Equal(50, result.Maximum);
    }

    [Fact]
    public void Merge_Numbers_IsIntegerIfAnyIsInteger()
    {
        var result = Assert.IsType<NumberSchemaNode>(
            SchemaMerger.Merge([
                new NumberSchemaNode(IsInteger: false),
                new NumberSchemaNode(IsInteger: true)
            ]));
        Assert.True(result.IsInteger);
    }

    [Fact]
    public void Merge_Numbers_IntegerMultipleOfLcm()
    {
        var result = Assert.IsType<NumberSchemaNode>(
            SchemaMerger.Merge([
                new NumberSchemaNode(MultipleOf: 3, IsInteger: true),
                new NumberSchemaNode(MultipleOf: 5, IsInteger: true)
            ]));
        Assert.Equal(15, result.MultipleOf);
    }

    [Fact]
    public void Merge_Numbers_SameMultipleOf_ReturnsItUnchanged()
    {
        var result = Assert.IsType<NumberSchemaNode>(
            SchemaMerger.Merge([
                new NumberSchemaNode(MultipleOf: 7, IsInteger: true),
                new NumberSchemaNode(MultipleOf: 7, IsInteger: true)
            ]));
        Assert.Equal(7, result.MultipleOf);
    }

    [Fact]
    public void Merge_Numbers_FractionalMultipleOfConflict_ThrowsNotSupported() =>
        Assert.Throws<NotSupportedException>(() =>
            SchemaMerger.Merge([
                new NumberSchemaNode(MultipleOf: 0.3),
                new NumberSchemaNode(MultipleOf: 0.7)
            ]));

    [Fact]
    public void Merge_Numbers_OneMultipleOfNull_KeepsNonNull()
    {
        var result = Assert.IsType<NumberSchemaNode>(
            SchemaMerger.Merge([
                new NumberSchemaNode(),
                new NumberSchemaNode(MultipleOf: 4, IsInteger: true)
            ]));
        Assert.Equal(4, result.MultipleOf);
    }

    [Fact]
    public void Merge_Objects_UnionsProperties()
    {
        var result = Assert.IsType<ObjectSchemaNode>(
            SchemaMerger.Merge([
                new ObjectSchemaNode(
                    new Dictionary<string, JsonSchemaNode> { ["a"] = new StringSchemaNode() },
                    [], new TrueSchemaNode()),
                new ObjectSchemaNode(
                    new Dictionary<string, JsonSchemaNode> { ["b"] = new NumberSchemaNode() },
                    [], new TrueSchemaNode())
            ]));
        Assert.True(result.Properties.ContainsKey("a"));
        Assert.True(result.Properties.ContainsKey("b"));
    }

    [Fact]
    public void Merge_Objects_UnionsRequired()
    {
        var result = Assert.IsType<ObjectSchemaNode>(
            SchemaMerger.Merge([
                new ObjectSchemaNode(new Dictionary<string, JsonSchemaNode>(), ["x"], new TrueSchemaNode()),
                new ObjectSchemaNode(new Dictionary<string, JsonSchemaNode>(), ["y"], new TrueSchemaNode())
            ]));
        Assert.Contains("x", result.Required);
        Assert.Contains("y", result.Required);
    }

    [Fact]
    public void Merge_Objects_FalseAdditionalPropertiesWins()
    {
        var result = Assert.IsType<ObjectSchemaNode>(
            SchemaMerger.Merge([
                new ObjectSchemaNode(new Dictionary<string, JsonSchemaNode>(), [], new TrueSchemaNode()),
                new ObjectSchemaNode(new Dictionary<string, JsonSchemaNode>(), [], new FalseSchemaNode())
            ]));
        Assert.IsType<FalseSchemaNode>(result.AdditionalProperties);
    }

    [Fact]
    public void Merge_Arrays_TakesMaxMinItems()
    {
        var result = Assert.IsType<ArraySchemaNode>(
            SchemaMerger.Merge([
                new ArraySchemaNode(MinItems: 2),
                new ArraySchemaNode(MinItems: 5)
            ]));
        Assert.Equal(5, result.MinItems);
    }

    [Fact]
    public void Merge_Arrays_TakesMinMaxItems()
    {
        var result = Assert.IsType<ArraySchemaNode>(
            SchemaMerger.Merge([
                new ArraySchemaNode(MaxItems: 10),
                new ArraySchemaNode(MaxItems: 3)
            ]));
        Assert.Equal(3, result.MaxItems);
    }

    [Fact]
    public void Merge_Arrays_UniqueItemsTrueIfAnyIsTrue()
    {
        var result = Assert.IsType<ArraySchemaNode>(
            SchemaMerger.Merge([
                new ArraySchemaNode(UniqueItems: false),
                new ArraySchemaNode(UniqueItems: true)
            ]));
        Assert.True(result.UniqueItems);
    }

    [Fact]
    public void Merge_EqualConsts_ReturnsFirstConst()
    {
        var node = new ConstSchemaNode(JsonValue.Create(42));
        var result = Assert.IsType<ConstSchemaNode>(
            SchemaMerger.Merge([node, new ConstSchemaNode(JsonValue.Create(42))]));
        Assert.Equal(42, Assert.IsAssignableFrom<JsonValue>(result.Value).GetValue<int>());
    }

    [Fact]
    public void Merge_ConflictingConsts_ThrowsInvalidOperation() =>
        Assert.Throws<InvalidOperationException>(() =>
            SchemaMerger.Merge([
                new ConstSchemaNode(JsonValue.Create(1)),
                new ConstSchemaNode(JsonValue.Create(2))
            ]));

    [Fact]
    public void Merge_NullConsts_ReturnNullConst()
    {
        var result = Assert.IsType<ConstSchemaNode>(
            SchemaMerger.Merge([new ConstSchemaNode(null), new ConstSchemaNode(null)]));
        Assert.Null(result.Value);
    }

    [Fact]
    public void Merge_Booleans_ReturnsBooleanNode() =>
        Assert.IsType<BooleanSchemaNode>(
            SchemaMerger.Merge([new BooleanSchemaNode(), new BooleanSchemaNode()]));

    [Fact]
    public void Merge_Nulls_ReturnsNullNode() =>
        Assert.IsType<NullSchemaNode>(
            SchemaMerger.Merge([new NullSchemaNode(), new NullSchemaNode()]));

    [Fact]
    public void Merge_MultipleEnums_ThrowsNotSupported()
    {
        var first = new EnumSchemaNode([JsonValue.Create("a"), JsonValue.Create("b")]);
        var second = new EnumSchemaNode([JsonValue.Create("c")]);
        Assert.Throws<NotSupportedException>(() => SchemaMerger.Merge([first, second]));
    }

    [Fact]
    public void Merge_SingleEnum_ReturnsThatNode()
    {
        // Single (non-allOf) enum schema passes through; no intersection needed.
        var node = new EnumSchemaNode([JsonValue.Create("x")]);
        Assert.Same(node, SchemaMerger.Merge([node]));
    }
}
