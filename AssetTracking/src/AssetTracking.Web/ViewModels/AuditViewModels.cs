using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>فهرس عمليات الجرد</summary>
public class AuditIndexViewModel
{
    public List<AuditListRow> Items { get; set; } = new();

    public string? Q { get; set; }
    public AuditStatus? Status { get; set; }
    public int? LocationId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<LookupItem> Locations { get; set; } = new();

    public int CountDraft { get; set; }
    public int CountInProgress { get; set; }
    public int CountCompleted { get; set; }
    public int CountMissing { get; set; }

    public bool CanEdit { get; set; }
}

public class AuditListRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public AuditStatus Status { get; set; }

    public string? LocationName { get; set; }
    public string? CompanyName { get; set; }
    public string? ResponsibleUserId { get; set; }
    public string? ResponsibleName { get; set; }

    public DateTime ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int TotalExpected { get; set; }
    public int TotalScanned { get; set; }
    public int TotalMissing { get; set; }

    public int ProgressPercent => TotalExpected == 0
        ? 0
        : (int)Math.Round(TotalScanned * 100.0 / TotalExpected);
}

/// <summary>تفاصيل عملية جرد + بنودها</summary>
public class AuditDetailsViewModel : AuditListRow
{
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<AuditItemRow> Items { get; set; } = new();

    /// <summary>فلتر نتيجة البنود المعروضة</summary>
    public AuditItemResult? ResultFilter { get; set; }
    public string? Q { get; set; }

    public int CountPending { get; set; }
    public int CountFound { get; set; }
    public int CountMisplaced { get; set; }
    public int CountMissingItems { get; set; }
    public int CountDamaged { get; set; }

    public bool CanEdit { get; set; }
    public bool CanScan { get; set; }

    public List<LookupItem> Locations { get; set; } = new();
}

public class AuditItemRow
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public string? CategoryName { get; set; }
    public AssetStatus AssetStatus { get; set; }

    public AuditItemResult Result { get; set; }
    public string? ExpectedLocationName { get; set; }
    public string? ActualLocationName { get; set; }
    public int? ActualLocationId { get; set; }

    public DateTime? ScannedAt { get; set; }
    public string? ScannedByUserId { get; set; }
    public string? ScannedByName { get; set; }
    public string? Notes { get; set; }
    public string? HolderName { get; set; }
}

/// <summary>نموذج إنشاء/تعديل عملية جرد</summary>
public class AuditFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "عنوان الجرد مطلوب")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "العنوان بين ٣ و ٢٠٠ حرف")]
    [Display(Name = "عنوان عملية الجرد")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "الوصف / نطاق الجرد")]
    public string? Description { get; set; }

    [Display(Name = "الموقع (اتركه فارغاً لجرد كل المواقع)")]
    public int? LocationId { get; set; }

    [Display(Name = "المسؤول عن الجرد")]
    public string? ResponsibleUserId { get; set; }

    [Required(ErrorMessage = "تاريخ الجرد مطلوب")]
    [DataType(DataType.Date)]
    [Display(Name = "تاريخ الجرد المخطَّط")]
    public DateTime ScheduledDate { get; set; } = DateTime.UtcNow.Date;

    [StringLength(1000)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>تُستخدم فقط لحساب المدير العام (غير المرتبط بشركة واحدة)</summary>
    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    /// <summary>هل يجب على المستخدم اختيار الشركة يدوياً؟ (المدير العام)</summary>
    public bool NeedsCompanyPick { get; set; }

    public List<LookupItem> Companies { get; set; } = new();
    public List<LookupItem> Locations { get; set; } = new();
    public List<UserLookupItem> Users { get; set; } = new();

    /// <summary>عدد الأصول المتوقّع تضمينها بالنطاق الحالي (للعرض فقط)</summary>
    public int ExpectedAssetsCount { get; set; }
}
