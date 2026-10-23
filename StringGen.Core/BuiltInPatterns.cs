namespace StringGen.Core;

public static class BuiltInPatterns
{
    public static IReadOnlyList<BuiltInPattern> All { get; } =
    [
        // Identity / Contact
        new("phone-us",       @"\([2-9]\d{2}\) [2-9]\d{2}-\d{4}",                                                          "US phone (NXX) NXX-XXXX"),
        new("phone-us-e164",  @"\+1[2-9]\d{9}",                                                                             "US phone E.164 (+1XXXXXXXXXX)"),
        new("email",          @"[a-z][a-z0-9.]{2,8}@[a-z]{3,10}\.(com|net|org|io)",                                        "Email address"),
        new("ssn",            @"\d{3}-\d{2}-\d{4}",                                                                         "US Social Security Number"),
        // Address
        new("zip-us",         @"\d{5}",                                                                                      "US ZIP code"),
        new("zip-us-plus4",   @"\d{5}-\d{4}",                                                                               "US ZIP+4 code"),
        // Finance
        new("credit-card",    @"4\d{15}",                                                                                    "Visa-format credit card number"),
        new("routing-number", @"\d{9}",                                                                                      "US bank routing number"),
        new("iban",           @"GB\d{2}[A-Z]{4}\d{14}",                                                                     "GB IBAN"),
        // Dates / Times
        new("date-iso",       @"(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])",                                      "ISO 8601 date (YYYY-MM-DD)"),
        new("datetime-iso",   @"(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])T([01]\d|2[0-3]):[0-5]\d:[0-5]\d",     "ISO 8601 datetime"),
        new("time-24",        @"([01]\d|2[0-3]):[0-5]\d:[0-5]\d",                                                           "24-hour time (HH:MM:SS)"),
        // Identifiers
        new("uuid",           @"[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}",                    "UUID v4"),
        new("ipv4",           @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}",                                                       "IPv4 address"),
        new("mac-address",    @"([0-9A-F]{2}:){5}[0-9A-F]{2}",                                                             "MAC address"),
        new("hex-color",      @"#[0-9A-F]{6}",                                                                              "CSS hex color"),
        // Web
        new("url",            @"https://[a-z]{3,10}\.(com|net|org|io)/[a-z0-9-]{3,20}",                                    "HTTPS URL"),
        new("slug",           @"[a-z][a-z0-9-]{3,20}-\d{1,3}",                                                             "URL slug"),
    ];

    private static readonly Dictionary<string, string> Index =
        All.ToDictionary(static x => x.Name, static x => x.Regex, StringComparer.OrdinalIgnoreCase);

    public static bool TryResolve(string name, out string regex) =>
        Index.TryGetValue(name, out regex!);
}
