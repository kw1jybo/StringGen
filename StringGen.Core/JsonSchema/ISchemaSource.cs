namespace StringGen.Core.JsonSchema;

internal interface ISchemaSource
{
    JsonSchemaNode Root { get; }

    JsonSchemaNode Resolve(string refPointer);
}
