using System.ComponentModel.DataAnnotations;

namespace Ims.Api.Contracts;

// ---- Requests of the three scan operations (DataAnnotations give automatic 400 answers for missing/overlong values) ----

/// <summary>One component scanned onto a unit: which component type, and its own barcode.</summary>
public record TrackComponentRequest(
    [Required, Range(1, long.MaxValue)] long? ComponentId,
    [Required, StringLength(64)] string ComponentBarcode);

public record TrackRequest(
    [Required, StringLength(64)] string Barcode,
    long? ProductId,
    [Required, Range(1, long.MaxValue)] long? TrackPointId,
    [Required, MinLength(1), MaxLength(50)] List<TrackComponentRequest> Components);

public record ReceiveRequest(
    [Required, StringLength(64)] string Barcode,
    long? ProductId,
    [Required, Range(1, long.MaxValue)] long? ReceivePointId);

public record DeliverRequest(
    [Required, StringLength(64)] string Barcode,
    [Required, Range(1, long.MaxValue)] long? DeliveryPointId);

// ---- Responses ----

public record UnitEventDto(long Id, string EventType, string PointName, string? ComponentName, string? ComponentBarcode,
    string PerformedBy, DateTime OccurredAt);

public record AttachedComponentDto(string ComponentName, string ComponentBarcode, DateTime TrackedAt);

/// <summary>What the scan screens show about a barcode. Also the answer of every ops call (the UI refreshes without another request).</summary>
public record UnitLookupDto(
    string Barcode,
    bool Exists,
    string? Product,
    string Status,          // NotFound | Registered | InStock | Delivered
    string? CurrentPoint,
    DateTime? Since,
    IReadOnlyList<AttachedComponentDto> Components,
    IReadOnlyList<UnitEventDto> RecentEvents);

/// <summary>Filter dropdowns of the history page (archived rows are included: old history still refers to them).</summary>
public record HistoryFilterOptionsDto(IReadOnlyList<OptionDto> Products, IReadOnlyList<OptionDto> Points, IReadOnlyList<OptionDto> Users);
