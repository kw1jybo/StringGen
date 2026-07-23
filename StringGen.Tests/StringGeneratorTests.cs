using System.Text.RegularExpressions;
using StringGen.Core;

namespace StringGen.Tests;

public class StringGeneratorTests
{
    [Theory]
    [InlineData(@"^[A-Z]{3}\d{4}$")]
    [InlineData(@"^\d{3}-\d{2}-\d{4}$")]
    [InlineData(@"^[a-zA-Z0-9]+$")]
    [InlineData(@"^(foo|bar)\d{2}$")]
    [InlineData(@"^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$")]
    [InlineData(@"^\w+$")]
    [InlineData(@"^[^aeiou]{5}$")]
    [InlineData(@"^\d{4}$")]
    [InlineData(@"^[a-z]{2,5}$")]
    [InlineData(@"^(red|green|blue)-(light|dark)$")]
    public void GenerateCompliant_AllStringsMatchPattern(string pattern)
    {
        var generator = new StringGenerator(pattern, seed: 42);
        var regex = new Regex(pattern);

        var results = generator.Generate(100, GenerationMode.Compliant);

        Assert.All(results, x => Assert.True(regex.IsMatch(x),
            $"Expected '{x}' to match '{pattern}'"));
    }

    [Theory]
    [InlineData(@"^[A-Z]{3}\d{4}$")]
    [InlineData(@"^\d{3}-\d{2}-\d{4}$")]
    [InlineData(@"^[a-z]{5}$")]
    [InlineData(@"^\d{4}$")]
    public void GenerateNonCompliant_NoStringMatchesPattern(string pattern)
    {
        var generator = new StringGenerator(pattern, seed: 42);
        var regex = new Regex(pattern);

        var results = generator.Generate(50, GenerationMode.NonCompliant);

        Assert.All(results, x => Assert.False(regex.IsMatch(x),
            $"Expected '{x}' NOT to match '{pattern}'"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void Generate_ReturnsExactCount(int count)
    {
        var generator = new StringGenerator(@"^\d+$", seed: 0);

        Assert.Equal(count, generator.Generate(count, GenerationMode.Compliant).Count);
        Assert.Equal(count, generator.Generate(count, GenerationMode.NonCompliant).Count);
    }

    [Fact]
    public void Generate_SameSeed_ProducesSameOutput()
    {
        const string pattern = @"^[A-Z]{3}\d{4}$";
        var gen1 = new StringGenerator(pattern, seed: 99);
        var gen2 = new StringGenerator(pattern, seed: 99);

        Assert.Equal(
            gen1.Generate(20, GenerationMode.Compliant),
            gen2.Generate(20, GenerationMode.Compliant));
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentOutput()
    {
        const string pattern = @"^[A-Z]{3}\d{4}$";
        var gen1 = new StringGenerator(pattern, seed: 1);
        var gen2 = new StringGenerator(pattern, seed: 2);

        Assert.NotEqual(
            gen1.Generate(20, GenerationMode.Compliant),
            gen2.Generate(20, GenerationMode.Compliant));
    }

    [Fact]
    public void Generate_AlternationWithSingleOption_Works()
    {
        var generator = new StringGenerator(@"^(only)\d$", seed: 0);
        var results = generator.Generate(20, GenerationMode.Compliant);
        Assert.All(results, static x => Assert.Matches(@"^(only)\d$", x));
    }

    [Fact]
    public void Generate_NestedGroups_Works()
    {
        var generator = new StringGenerator(@"^((ab){2}|cd)$", seed: 0);
        var results = generator.Generate(30, GenerationMode.Compliant);
        Assert.All(results, static x => Assert.Matches(@"^((ab){2}|cd)$", x));
    }

    [Fact]
    public void Generate_OptionalQuantifier_Works()
    {
        var generator = new StringGenerator(@"^colou?r$", seed: 0);
        var results = generator.Generate(30, GenerationMode.Compliant);
        Assert.All(results, static x => Assert.Matches(@"^colou?r$", x));
    }

    [Fact]
    public void Generate_MaxRepeat_ControlsUnboundedQuantifierLength()
    {
        // \w+ with maxRepeat=2 must produce strings of length 1–3 (Min=1, cap=1+2=3)
        var generator = new StringGenerator(@"^\w+$", seed: 0, maxRepeat: 2);
        var results = generator.Generate(50, GenerationMode.Compliant);
        Assert.All(results, static x => Assert.InRange(x.Length, 1, 3));
    }

    [Fact]
    public void Generate_MaxRepeat_DefaultIsFive()
    {
        // \d+ default cap is Min+5 = 1+5 = 6
        var generator = new StringGenerator(@"^\d+$", seed: 0);
        var results = generator.Generate(100, GenerationMode.Compliant);
        Assert.All(results, static x => Assert.InRange(x.Length, 1, 6));
    }

    [Fact]
    public void GenerateCompliant_NegatedClassExcludingAllPrintable_Throws()
    {
        // [^ -~] negates the entire printable ASCII range — no printable char can satisfy it.
        // The generator must throw rather than silently return a non-compliant string.
        var generator = new StringGenerator(@"[^ -~]", seed: 0);
        Assert.Throws<InvalidOperationException>(() =>
            generator.Generate(1, GenerationMode.Compliant));
    }

    [Theory]
    [InlineData(@"^[^a-zA-Z0-9]$")]   // excludes most printable chars but leaves some (symbols)
    [InlineData(@"^[^0-9]$")]          // excludes digits — many printable chars remain
    public void GenerateCompliant_NegatedClassWithRemainingChars_Succeeds(string pattern)
    {
        var generator = new StringGenerator(pattern, seed: 0);
        var results = generator.Generate(20, GenerationMode.Compliant);
        Assert.All(results, x => Assert.Matches(pattern, x));
    }

    [Fact]
    public void GenerateCompliant_VerifiesEachString_DoesNotReturnNonCompliantOutput()
    {
        string[] patterns =
        [
            @"^[A-Z]{3}\d{4}$",
            @"^\d{3}-\d{2}-\d{4}$",
            @"^[a-zA-Z0-9]+$",
            @"^(foo|bar)\d{2}$",
            @"^[^aeiou]{5}$",
        ];

        foreach (var pattern in patterns)
        {
            var generator = new StringGenerator(pattern, seed: 0);
            var ex = Record.Exception(() => generator.Generate(50, GenerationMode.Compliant));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Generate_CountZero_ReturnsEmptyList()
    {
        var generator = new StringGenerator(@"^\d{4}$", seed: 0);
        var results = generator.Generate(0, GenerationMode.Compliant);
        Assert.Empty(results);
    }

    [Fact]
    public void Generate_CountZeroNonCompliant_ReturnsEmptyList()
    {
        var generator = new StringGenerator(@"^\d{4}$", seed: 0);
        var results = generator.Generate(0, GenerationMode.NonCompliant);
        Assert.Empty(results);
    }

    [Fact]
    public void Generate_NegativeCount_ThrowsArgumentOutOfRange()
    {
        var generator = new StringGenerator(@"^\d{4}$", seed: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.Generate(-1, GenerationMode.Compliant));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.Generate(-1, GenerationMode.NonCompliant));
    }

    [Fact]
    public void Generate_Compliant_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new StringGenerator(@"^\d{4}$", seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.Generate(5, GenerationMode.Compliant, ct));
    }

    [Fact]
    public void Generate_NonCompliant_PreCancelledToken_ThrowsOperationCancelled()
    {
        var gen = new StringGenerator(@"^\d{4}$", seed: 0);
        var ct = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => gen.Generate(5, GenerationMode.NonCompliant, ct));
    }

    [Fact]
    public void Constructor_NullPattern_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StringGenerator(null!));
    }

    [Fact]
    public void Constructor_MaxRepeatZero_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringGenerator(@"\d+", maxRepeat: 0));
    }

    [Fact]
    public void Constructor_MaxRepeatNegative_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringGenerator(@"\d+", maxRepeat: -1));
    }

    [Theory]
    [InlineData(@"\d{2147483648}")]          // exactly int.MaxValue + 1
    [InlineData(@"\d{9999999999,9999999999}")] // way beyond Int32
    public void Generate_QuantifierOverflowsInt32_ThrowsArgumentException(string pattern)
    {
        // ThrowsAny: .NET Regex also rejects these patterns, throwing RegexParseException
        // (a subtype of ArgumentException) before our parser runs — both outcomes are correct.
        Assert.ThrowsAny<ArgumentException>(() => new StringGenerator(pattern));
    }

    [Fact]
    public void Generate_DeeplyNestedPattern_ThrowsArgumentException()
    {
        var pattern = new string('(', 600) + "a" + new string(')', 600);
        Assert.Throws<ArgumentException>(() => new StringGenerator(pattern));
    }

    [Fact]
    public void GenerateNonCompliant_PatternMatchesAllCandidates_Throws()
    {
        // [\x20-\x7E]* matches every printable ASCII string including "". The star quantifier
        // (Min=0, Max=-1) is unviolable by count, and the char class covers all printable
        // chars so it has no complement. No AST target exists, and BuildRandomString produces
        // only printable strings — all of which match — so the generator exhausts retries.
        var generator = new StringGenerator(@"[\x20-\x7E]*", seed: 0);
        Assert.Throws<InvalidOperationException>(() =>
            generator.Generate(1, GenerationMode.NonCompliant));
    }

    [Fact]
    public void GenerateNonCompliant_LiteralNode_ViolationDoesNotMatch()
    {
        var gen = new StringGenerator(@"^a$", seed: 0);
        var results = gen.Generate(5, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^a$", x));
    }

    [Fact]
    public void GenerateNonCompliant_ShorthandNode_ViolationDoesNotMatch()
    {
        var gen = new StringGenerator(@"^\d$", seed: 0);
        var results = gen.Generate(5, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^\d$", x));
    }

    [Fact]
    public void GenerateNonCompliant_CharClassNode_ViolationDoesNotMatch()
    {
        var gen = new StringGenerator(@"^[a-z]$", seed: 0);
        var results = gen.Generate(5, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^[a-z]$", x));
    }

    [Fact]
    public void GenerateNonCompliant_WildcardNode_ViolationDoesNotMatch()
    {
        // Wildcard violation injects \n which . does not match in default (non-singleline) mode
        var gen = new StringGenerator(@"^.{3}$", seed: 0);
        var results = gen.Generate(5, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^.{3}$", x));
    }

    [Fact]
    public void GenerateNonCompliant_QuantifiedMinViolation_EmptyStringDoesNotMatch()
    {
        // QuantifiedNode(Min=3): violation uses count=0 → empty string, which fails \d{3}
        var gen = new StringGenerator(@"^\d{3}$", seed: 0);
        var results = gen.Generate(1, GenerationMode.NonCompliant);
        Assert.Single(results);
        Assert.DoesNotMatch(@"^\d{3}$", results[0]);
    }

    [Fact]
    public void GenerateNonCompliant_QuantifiedMaxViolation_ExceedingCountDoesNotMatch()
    {
        // QuantifiedNode(Min=0, Max=2): violation uses count=3, exceeding the upper bound
        var gen = new StringGenerator(@"^\d{0,2}$", seed: 0);
        var results = gen.Generate(1, GenerationMode.NonCompliant);
        Assert.Single(results);
        Assert.DoesNotMatch(@"^\d{0,2}$", results[0]);
    }

    [Fact]
    public void GenerateNonCompliant_AlternationPattern_AllDoNotMatch()
    {
        // Targets inside alternation branches are located and violated via TreeContains
        var gen = new StringGenerator(@"^(foo|bar)$", seed: 0);
        var results = gen.Generate(10, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^(foo|bar)$", x));
    }

    [Fact]
    public void GenerateNonCompliant_ComplexPattern_AllDoNotMatch()
    {
        // Multiple node types: char classes, shorthands, quantifiers, literals
        var gen = new StringGenerator(@"^[A-Z]{2}\d{3}-[a-z]+$", seed: 0);
        var results = gen.Generate(20, GenerationMode.NonCompliant);
        Assert.All(results, static x => Assert.DoesNotMatch(@"^[A-Z]{2}\d{3}-[a-z]+$", x));
    }
}
