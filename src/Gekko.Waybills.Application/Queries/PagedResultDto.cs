namespace Gekko.Waybills.Application.Queries;

/// <summary>Generic paged response.</summary>
public sealed class PagedResultDto<TItem>
{
    /// <summary>Items for the current page.</summary>
    public List<TItem> Items { get; set; } = [];

    /// <summary>Total number of matching items.</summary>
    public int TotalCount { get; set; }

    /// <summary>Requested page.</summary>
    public int Page { get; set; }

    /// <summary>Requested page size.</summary>
    public int PageSize { get; set; }
}
