namespace Ims.Api.Infrastructure;

/// <summary>Common list parameters from the query string: ?q=&amp;page=&amp;pageSize=&amp;sort=</summary>
public class ListQuery
{
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Sort { get; set; }

    /// <summary>Clamp to sane values so nobody can ask for page 0 or 1,000,000 rows.</summary>
    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, 200);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
