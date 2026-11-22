using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Infrastructure;

/// <summary>
/// Common list parameters read from the query string: ?q=&amp;page=&amp;pageSize=&amp;sort=
/// A controller action takes it as <c>[FromQuery] ListQuery query</c> (model binding fills the properties by name).
/// Lists with extra filters inherit from this class and add their own properties.
/// </summary>
public class ListQuery
{
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Sort { get; set; }
    /// <summary>?format=csv|xlsx asks for a file instead of JSON (built on Day 4; until then the API answers 501).</summary>
    public string? Format { get; set; }

    /// <summary>Clamp to sane values so nobody can ask for page 0 or 1,000,000 rows.</summary>
    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, 200);

    /// <summary>The search text with surrounding spaces removed, or null when empty.</summary>
    public string? SearchText => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();

    /// <summary>
    /// Builds an ILIKE pattern ("contains, ignoring case") and escapes the wildcard characters % and _ so that
    /// a user typing "50%" searches for the text "50%" instead of matching everything.
    /// </summary>
    public static string Contains(string text) =>
        "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>
    /// Runs COUNT(*) and then the page query (OFFSET/LIMIT) on an already filtered and ordered query.
    /// Every list endpoint uses this, so paging is written exactly once.
    /// </summary>
    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> ordered, ListQuery query, CancellationToken ct = default)
    {
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, query.SafePage, query.SafePageSize, total);
    }

    /// <summary>Same page, items converted to another type (entity -> DTO).</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> convert) =>
        new(Items.Select(convert).ToList(), Page, PageSize, TotalCount);
}
