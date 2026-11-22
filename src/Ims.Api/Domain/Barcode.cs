using System.Text.RegularExpressions;

namespace Ims.Api.Domain;

/// <summary>
/// Barcode rules (assumption A13): trimmed, upper-cased, 6-32 characters of A-Z, 0-9 and "-".
/// The SAME pattern lives in client/src/lib/barcode.ts; a test on each side pins it.
/// </summary>
public static class Barcode
{
    public const string Pattern = "^[A-Z0-9-]{6,32}$";
    private static readonly Regex Regex = new(Pattern, RegexOptions.Compiled);

    public static string Normalize(string? raw) => (raw ?? "").Trim().ToUpperInvariant();

    public static bool IsValid(string normalized) => Regex.IsMatch(normalized);
}
