using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Entities;

/// <summary>عملية جرد مخزون</summary>
public class InventoryAudit : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public int? DepartmentId { get; set; }

    public AuditStatus Status { get; set; } = AuditStatus.Draft;

    public DateTime ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? ResponsibleUserId { get; set; }
    public string? Notes { get; set; }

    public int TotalExpected { get; set; }
    public int TotalScanned { get; set; }
    public int TotalMissing { get; set; }

    public ICollection<InventoryAuditItem> Items { get; set; } = new List<InventoryAuditItem>();
}

/// <summary>بند في عملية جرد</summary>
public class InventoryAuditItem : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int AuditId { get; set; }
    public InventoryAudit? Audit { get; set; }

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public AuditItemResult Result { get; set; } = AuditItemResult.Pending;

    public int? ExpectedLocationId { get; set; }
    public int? ActualLocationId { get; set; }

    public DateTime? ScannedAt { get; set; }
    public string? ScannedByUserId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>إشعار داخلي (In-App) — يُبَث فورياً عبر SignalR</summary>
public class Notification : BaseEntity
{
    public int? CompanyId { get; set; }

    /// <summary>المستلم</summary>
    public string UserId { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.General;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>رابط الإجراء (مثلاً /Tickets/Details/12)</summary>
    public string? ActionUrl { get; set; }

    public string? Icon { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
}

/// <summary>سجل تدقيق شامل لكل العمليات الحساسة</summary>
public class AuditLog : BaseEntity
{
    public int? CompanyId { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>إعداد نظام قابل للتعديل من الواجهة (SMTP، شعار، سياسات ...)</summary>
public class SystemSetting : BaseEntity
{
    public int? CompanyId { get; set; }

    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    /// <summary>قيمة سرية (كلمة مرور SMTP) — لا تُعرض في الواجهة</summary>
    public bool IsSecret { get; set; }
}
