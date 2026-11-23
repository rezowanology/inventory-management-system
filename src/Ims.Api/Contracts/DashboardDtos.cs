namespace Ims.Api.Contracts;

/// <summary>A unit that has been in stock for a long time.</summary>
public record StuckUnitDto(string Barcode, string ProductName, string PointName, int DaysInStock);

/// <summary>
/// Everything the dashboard shows. Counts of "today" are for the local day in App:TimeZone. <c>Recent</c> is empty for
/// roles without history.view. <c>TotalUnits</c> and <c>StockCount</c> keep their Day 1 names.
/// </summary>
public record DashboardResponse(
    int TotalUnits, int StockCount, int TodayReceived, int TodayDelivered, int TodayReversed, int UnitsRegisteredToday,
    IReadOnlyList<PointCountDto> StockByPoint, IReadOnlyList<StuckUnitDto> StuckLongest, IReadOnlyList<HistoryRowDto> Recent);
