using FluentAssertions;
using StringGen.Core;

namespace StringGen.Tests;

public sealed class BuiltInPatternsTests
{
    [Fact]
    public void All_HasExpectedCount()
    {
        BuiltInPatterns.All.Should().HaveCount(18);
    }

    [Fact]
    public void TryResolve_KnownName_ReturnsTrueAndRegex()
    {
        BuiltInPatterns.TryResolve("email", out var regex).Should().BeTrue();
        regex.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryResolve_IsCaseInsensitive()
    {
        BuiltInPatterns.TryResolve("EMAIL", out var regex).Should().BeTrue();
        regex.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryResolve_UnknownName_ReturnsFalse()
    {
        BuiltInPatterns.TryResolve("not-a-pattern", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("phone-us")]
    [InlineData("phone-us-e164")]
    [InlineData("email")]
    [InlineData("ssn")]
    [InlineData("zip-us")]
    [InlineData("zip-us-plus4")]
    [InlineData("credit-card")]
    [InlineData("routing-number")]
    [InlineData("iban")]
    [InlineData("date-iso")]
    [InlineData("datetime-iso")]
    [InlineData("time-24")]
    [InlineData("uuid")]
    [InlineData("ipv4")]
    [InlineData("mac-address")]
    [InlineData("hex-color")]
    [InlineData("url")]
    [InlineData("slug")]
    public void AllPatterns_GenerateCompliantStrings(string name)
    {
        BuiltInPatterns.TryResolve(name, out var regex).Should().BeTrue();
        var generator = new StringGenerator(regex, seed: 42);
        var results = generator.Generate(5, GenerationMode.Compliant);
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(s => s.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void AllEntries_HaveNonEmptyFields()
    {
        foreach (var p in BuiltInPatterns.All)
        {
            p.Name.Should().NotBeNullOrEmpty();
            p.Regex.Should().NotBeNullOrEmpty();
            p.Description.Should().NotBeNullOrEmpty();
        }
    }
}
