using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>سطر في جدول التذاكر</summary>
public class TicketRow
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AssetName { get; set; }
    public string? AssetTag { get; set; }
    public TicketType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public string? RequesterName { get; set; }
    public string? TechnicianName { get; set; }
    public string? CompanyName { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsSlaBreached { get; set; }

    /// <summary>الوقت المتبقي لموعد الحل — سالب يعني تأخر</summary>
    public double? HoursRemaining => ResolutionDueAt.HasValue && ResolvedAt == null
        ? (ResolutionDueAt.Value - DateTime.UtcNow).TotalHours
        : null;
}

public class TicketIndexViewModel
{
    public List<TicketRow> Items { get; set; } = new();

    public string? Q { get; set; }
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public TicketType? Type { get; set; }
    public string? TechnicianId { get; set; }
    public string? Scope { get; set; }   // all | mine | unassigned | breached
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<UserLookupItem> Technicians { get; set; } = new();

    public int CountOpen { get; set; }
    public int CountInProgress { get; set; }
    public int CountBreached { get; set; }
    public int CountUnassigned { get; set; }

    public bool CanAssign { get; set; }
    public bool CanWork { get; set; }
}

public class TicketCommentRow
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketLogRow
{
    public string Field { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? ByName { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class TicketPartRow
{
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal Total => (UnitCost ?? 0m) * Quantity;
}

public class TicketDetailsViewModel
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int AssetId { get; set; }
    public string? AssetName { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetLocation { get; set; }

    public TicketType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }

    public string? RequesterName { get; set; }
    public string? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public string? CompanyName { get; set; }

    public DateTime ReportedAt { get; set; }
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsSlaBreached { get; set; }

    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public decimal? LaborCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal TotalCost => (LaborCost ?? 0m) + (PartsCost ?? 0m);

    public List<TicketCommentRow> Comments { get; set; } = new();
    public List<TicketLogRow> Logs { get; set; } = new();
    public List<TicketPartRow> Parts { get; set; } = new();
    public List<UserLookupItem> Technicians { get; set; } = new();

    public string? RowVersion { get; set; }

    public bool CanAssign { get; set; }
    public bool CanWork { get; set; }
    public bool CanComment { get; set; }
    public bool CanClose { get; set; }
    public bool IsMine { get; set; }
}

public class TicketCreateViewModel
{
    [Required(ErrorMessage = "يجب اختيار الأصل")]
    [Display(Name = "الأصل")]
    public int AssetId { get; set; }

    [Required(ErrorMessage = "الموضوع مطلوب")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "الموضوع بين ٥ و ٢٠٠ حرف")]
    [Display(Name = "موضوع المشكلة")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "وصف المشكلة مطلوب")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "الوصف ١٠ أحرف على الأقل")]
    [Display(Name = "وصف تفصيلي")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "النوع")]
    public TicketType Type { get; set; } = TicketType.Corrective;

    [Display(Name = "الأولوية")]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public List<AssetPickerItem> Assets { get; set; } = new();
    public string? AssetLabel { get; set; }
}

public class AssetPickerItem
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Label => $"{AssetTag} — {Name}";
}

public class TicketResolveViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "وصف الحل مطلوب")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "وصف الحل ١٠ أحرف على الأقل")]
    [Display(Name = "ما تم عمله")]
    public string Resolution { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "السبب الجذري")]
    public string? RootCause { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "تكلفة العمالة")]
    public decimal? LaborCost { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "تكلفة قطع الغيار")]
    public decimal? PartsCost { get; set; }

    public string? TicketNumber { get; set; }
    public string? Title { get; set; }
}
