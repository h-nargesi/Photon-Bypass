namespace PhotonBypass.Application.Model;

/// <summary>
/// Server-side paging envelope shared by the paged admin endpoints
/// (wallet history, user list — docs/20 phases 3/4).
/// </summary>
public class PagedResult<T>
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => PageSize < 1 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public IList<T> Items { get; set; } = [];
}
