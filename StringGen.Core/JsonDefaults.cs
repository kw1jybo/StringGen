using System.Text.Json;

namespace StringGen.Core;

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    internal static readonly JsonDocumentOptions Document = new() { MaxDepth = 128 };
}
