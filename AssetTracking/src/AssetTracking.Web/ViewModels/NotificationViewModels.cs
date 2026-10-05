using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

public class NotificationRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public NotificationType Type { get; set; }
}

public class NotificationListViewModel
{
    public List<NotificationRow> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public bool UnreadOnly { get; set; }

    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}
