using System.ComponentModel.DataAnnotations;
using Ims.Api.Domain;

namespace Ims.Api.Contracts;

// DTOs ("data transfer objects") are the JSON shapes of the API. We never send entities directly.

public record LoginRequest([Required] string UserName, [Required] string Password);

public record RegisterRequest(
    [Required, StringLength(150, MinimumLength = 3)] string UserName,
    [Required] string Password,
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, StringLength(20, MinimumLength = 2)] string EmployeeNo);

public record MeResponse(long Id, string UserName, string DisplayName, string Role, string EmployeeNo, IReadOnlyList<string> Capabilities);

public record ConfigResponse(string TimeZone);


/// <summary>
/// One history row as the list, the reports and the exports show it. The last three values describe the reversal that
/// cancelled this row (if any); <c>CanReverse</c> is only filled in for admins (it is false for everybody else).
/// </summary>
public record HistoryRowDto(
    long Id,
    string UnitBarcode,
    string ProductName,
    string EventType,
    string PointName,
    string? ComponentName,
    string? ComponentBarcode,
    long? ReversesId,
    string Reason,
    string PerformedByEmployeeNo,
    string PerformedByName,
    DateTime OccurredAt,
    long? ReversedById,
    string? ReversedBy,
    DateTime? ReversedAt)
{
    /// <summary>True when an admin has cancelled this row. Computed from the reversal columns above.</summary>
    public bool Reversed => ReversedById is not null;
    public bool CanReverse { get; init; }
}

/// <summary>The "are you sure?" screen of a reversal: the row, the verdict, and what would change.</summary>
public record ReversePreviewDto(HistoryRowDto Row, bool Allowed, string? BlockedReason, string Effect);

public record ReverseRequest([Required, StringLength(500)] string Reason);

public record ReverseResultDto(long ReversalId, string UnitBarcode, string ReversedEventType, string Effect);
