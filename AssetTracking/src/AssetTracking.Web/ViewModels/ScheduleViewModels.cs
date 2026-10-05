using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>فهرس جداول الصيانة الوقائية</summary>
public class ScheduleIndexViewModel
{
    public List<ScheduleListRow> Items { get; set; } = new();

    public string? Q { get; set; }
    public ScheduleStatus? Status { get; set; }
    public ScheduleFrequency? Frequency { get; set; }
    public string? TechnicianId { get; set; }
    public string? Scope { get; set; }   // all | due | overdue | mine

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<UserLookupItem> Technicians { get; set; } = new();

    public int CountActive { get; set; }
    public int CountDueSoon { get; set; }
    public int CountOverdue { get; set; }
    public int CountPaused { get; set; }

    public bool CanEdit { get; set; }
}

/// <summary>سطر مفصَّل في فهرس الجداول</summary>
public class ScheduleListRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int AssetId { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public string? CategoryName { get; set; }
    public string? CompanyName { get; set; }

    public ScheduleFrequency Frequency { get; set; }
    public ScheduleStatus Status { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public int LeadTimeDays { get; set; }

    public string? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public TicketPriority DefaultPriority { get; set; }
    public decimal? EstimatedCost { get; set; }

    public int GeneratedTicketsCount { get; set; }

    /// <summary>عدد الأيام حتى الاستحقاق — سالب يعني متأخر</summary>
    public int DaysUntilDue => (int)(NextDueDate.Date - DateTime.UtcNow.Date).TotalDays;
    public bool IsOverdue => Status == ScheduleStatus.Active && DaysUntilDue < 0;
    public bool IsDueSoon => Status == ScheduleStatus.Active && DaysUntilDue >= 0 && DaysUntilDue <= LeadTimeDays;
}

/// <summary>تفاصيل جدول صيانة</summary>
public class ScheduleDetailsViewModel : ScheduleListRow
{
    public string? Description { get; set; }
    public string? Checklist { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TicketRow> Tickets { get; set; } = new();
    public bool CanEdit { get; set; }

    /// <summary>بنود قائمة الفحص مُفصَّلة سطراً سطراً</summary>
    public List<string> ChecklistItems => string.IsNullOrWhiteSpace(Checklist)
        ? new List<string>()
        : Checklist.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>نموذج إضافة/تعديل جدول صيانة وقائية</summary>
public class ScheduleFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "يجب اختيار الأصل")]
    [Display(Name = "الأصل")]
    public int AssetId { get; set; }

    [Required(ErrorMessage = "عنوان الجدول مطلوب")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "العنوان بين ٣ و ٢٠٠ حرف")]
    [Display(Name = "عنوان الجدول")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [StringLength(3000)]
    [Display(Name = "قائمة الفحص (بند في كل سطر)")]
    public string? Checklist { get; set; }

    [Display(Name = "التكرار")]
    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Monthly;

    [Display(Name = "الحالة")]
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;

    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    [DataType(DataType.Date)]
    [Display(Name = "تاريخ البداية")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    [Display(Name = "تاريخ النهاية (اختياري)")]
    public DateTime? EndDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "تاريخ الاستحقاق القادم")]
    public DateTime NextDueDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0, 90, ErrorMessage = "مدة التنبيه المسبق بين ٠ و ٩٠ يوماً")]
    [Display(Name = "أيام التنبيه المسبق")]
    public int LeadTimeDays { get; set; } = 3;

    [Display(Name = "الفني المسؤول")]
    public string? DefaultTechnicianId { get; set; }

    [Display(Name = "أولوية التذاكر المتولّدة")]
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

    [Range(0, 99999999)]
    [Display(Name = "التكلفة التقديرية")]
    public decimal? EstimatedCost { get; set; }

    public List<AssetPickerItem> Assets { get; set; } = new();
    public List<UserLookupItem> Technicians { get; set; } = new();
    public string? AssetLabel { get; set; }
}
