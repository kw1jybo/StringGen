using System.Text;
using System.Text.RegularExpressions;

namespace StringGen.Core;

public sealed class StringGenerator(string pattern, int? seed = null, int maxRepeat = 5)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    private readonly Regex regex = CreateRegex(pattern, maxRepeat);
    private readonly RegexNode ast = new RegexParser(pattern).Parse();
    private readonly Random random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    private readonly RegexStringGenerator generator = new RegexStringGenerator(
        seed.HasValue ? new Random(seed.Value) : Random.Shared, maxRepeat);

    private static Regex CreateRegex(string pattern, int maxRepeat)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRepeat);
        return new Regex(pattern, RegexOptions.Compiled, RegexTimeout);
    }

    public IReadOnlyList<string> Generate(int count, GenerationMode mode, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return mode == GenerationMode.Compliant
            ? GenerateCompliant(count, ct)
            : GenerateNonCompliant(count, ct);
    }

    private IReadOnlyList<string> GenerateCompliant(int count, CancellationToken ct)
    {
        var results = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var s = generator.Generate(ast);
            if (!IsMatch(s))
            {
                throw new InvalidOperationException(
                    $"Generator produced non-compliant string '{s}' for pattern '{regex}'. " +
                    "This is a bug — please report it at https://github.com/kw1jybo/StringGen/issues");
            }
            results.Add(s);
        }
        return results;
    }

    private IReadOnlyList<string> GenerateNonCompliant(int count, CancellationToken ct)
    {
        var targets = RegexViolatingGenerator.CollectTargets(ast);
        var violatingGen = new RegexViolatingGenerator(random, maxRepeat);
        var results = new List<string>(count);

        foreach (var target in targets)
        {
            if (results.Count >= count) { break; }
            ct.ThrowIfCancellationRequested();
            var candidate = violatingGen.Generate(ast, target);
            if (!IsMatch(candidate)) { results.Add(candidate); }
        }

        const int maxAttempts = 10_000;
        int attempts = 0;
        while (results.Count < count)
        {
            ct.ThrowIfCancellationRequested();
            if (++attempts > maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Could not generate {count} non-compliant strings after {maxAttempts} attempts. " +
                    "The pattern may match too broadly.");
            }
            var candidate = BuildRandomString();
            if (!IsMatch(candidate)) { results.Add(candidate); }
        }

        return results;
    }

    private bool IsMatch(string s)
    {
        try
        {
            return regex.IsMatch(s);
        }
        catch (RegexMatchTimeoutException)
        {
            throw new ArgumentException(
                $"Pattern '{regex}' causes catastrophic backtracking. Simplify the regex or reduce quantifier nesting.");
        }
    }

    private string BuildRandomString()
    {
        int length = random.Next(1, 25);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(CharSets.Printable[random.Next(CharSets.Printable.Length)]);
        }
        return sb.ToString();
    }
}
