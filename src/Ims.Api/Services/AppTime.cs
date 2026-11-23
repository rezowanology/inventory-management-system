using System.Globalization;

namespace Ims.Api.Services;

/// <summary>
/// The display time zone (A12, <c>App:TimeZone</c>) in one place. Times are stored in UTC; days in filters and
/// reports are LOCAL days of this zone, and exported files show local times. Registered as a singleton in Program.cs.
/// </summary>
public class AppTime(IConfiguration config)
{
    public TimeZoneInfo Zone { get; } = Resolve(config["App:TimeZone"]);

    private static TimeZoneInfo Resolve(string? configured)
    {
        // IANA id first (e.g. "Asia/Dhaka"), then the Windows id as a fallback.
        foreach (var id in new[] { configured, "Bangladesh Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>"2026-03-28 10:15:00" in the display zone (what the exported files show).</summary>
    public string Format(DateTime utc) => ToLocal(utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public string? Format(DateTime? utc) => utc is null ? null : Format(utc.Value);

    /// <summary>The local calendar day that this UTC moment falls on.</summary>
    public DateOnly LocalDay(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>The UTC moment at which a local day starts (for comparing with timestamptz columns).</summary>
    public DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);
}
