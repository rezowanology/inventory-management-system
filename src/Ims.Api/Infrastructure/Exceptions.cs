namespace Ims.Api.Infrastructure;

/// <summary>A business-rule violation (e.g. "deliver a unit that is not in stock"). Mapped to HTTP 422.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; init; }
}

/// <summary>The scan repeats a state the unit is already in. Mapped to HTTP 409 with when/where/by.</summary>
public class DuplicateScanException(string message, DateTime when, string where, string by) : Exception(message)
{
    public DateTime When { get; } = when;
    public string Where { get; } = where;
    public string By { get; } = by;
}
