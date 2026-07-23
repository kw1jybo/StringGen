namespace StringGen.Core;

internal static class CharSets
{
    private static char[] MakeRange(char from, char to) =>
        Enumerable.Range(from, to - from + 1).Select(i => (char)i).ToArray();

    private static char[] Subtract(char[] source, char[] excluded)
    {
        var set = new HashSet<char>(excluded);
        return source.Where(c => !set.Contains(c)).ToArray();
    }

    private static readonly char[] s_printable = MakeRange(' ', '~');
    private static readonly char[] s_digits = MakeRange('0', '9');
    private static readonly char[] s_nonDigits = Subtract(s_printable, s_digits);
    private static readonly char[] s_wordChars = [.. MakeRange('a', 'z'), .. MakeRange('A', 'Z'), .. MakeRange('0', '9'), '_'];
    private static readonly char[] s_nonWordChars = Subtract(s_printable, s_wordChars);
    private static readonly char[] s_whitespace = [' ', '\t', '\n', '\r'];
    private static readonly char[] s_nonWhitespace = Subtract(s_printable, s_whitespace);

    public static ReadOnlySpan<char> Printable => s_printable;
    public static ReadOnlySpan<char> Digits => s_digits;
    public static ReadOnlySpan<char> NonDigits => s_nonDigits;
    public static ReadOnlySpan<char> WordChars => s_wordChars;
    public static ReadOnlySpan<char> NonWordChars => s_nonWordChars;
    public static ReadOnlySpan<char> Whitespace => s_whitespace;
    public static ReadOnlySpan<char> NonWhitespace => s_nonWhitespace;

    public static ReadOnlySpan<char> ForCode(char code) => code switch
    {
        'd' => s_digits,
        'D' => s_nonDigits,
        'w' => s_wordChars,
        'W' => s_nonWordChars,
        's' => s_whitespace,
        'S' => s_nonWhitespace,
        _ => throw new NotSupportedException($"Unknown shorthand \\{code}")
    };
}
