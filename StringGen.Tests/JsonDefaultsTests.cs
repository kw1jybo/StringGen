using StringGen.Core;

namespace StringGen.Tests;

public class JsonDefaultsTests
{
    [Fact]
    public void Indented_WriteIndentedIsTrue()
    {
        Assert.True(JsonDefaults.Indented.WriteIndented);
    }

    [Fact]
    public void Document_MaxDepthIs128()
    {
        Assert.Equal(128, JsonDefaults.Document.MaxDepth);
    }
}
