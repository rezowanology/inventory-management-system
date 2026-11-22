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

public record DashboardResponse(int TotalUnits, int StockCount);

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
    DateTime OccurredAt);
