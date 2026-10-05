using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>سطر واحد في جدول الأصول</summary>
public class AssetRow
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? CategoryName { get; set; }
    public string? LocationName { get; set; }
    public string? DepartmentName { get; set; }
    public string? CompanyName { get; set; }
    public AssetStatus Status { get; set; }
    public decimal? PurchaseValue { get; set; }
    public decimal? BookValue { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? CustodyUserName { get; set; }
    public int OpenTickets { get; set; }
}

/// <summary>فهرس الأصول مع الفلاتر</summary>
public class AssetIndexViewModel
{
    public List<AssetRow> Items { get; set; } = new();

    // ── الفلاتر ──
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public int? LocationId { get; set; }
    public int? DepartmentId { get; set; }
    public AssetStatus? Status { get; set; }
    public string? Sort { get; set; }

    // ── الترقيم ──
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    // ── قوائم منسدلة ──
    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Locations { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();

    // ── مؤشرات سريعة ──
    public decimal SumPurchaseValue { get; set; }
    public decimal SumBookValue { get; set; }
    public bool CanEdit { get; set; }
}

public class LookupItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UserLookupItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>صفحة تفاصيل الأصل</summary>
public class AssetDetailsViewModel
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public AssetStatus Status { get; set; }

    public string? CategoryName { get; set; }
    public string? DepartmentName { get; set; }
    public string? LocationName { get; set; }
    public string? VendorName { get; set; }
    public string? CompanyName { get; set; }

    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specifications { get; set; }
    public string? Color { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseValue { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? BookValue { get; set; }
    public int? UsefulLifeYears { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public string? InvoiceNumber { get; set; }

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? WarrantyProvider { get; set; }

    public string? CustodyUserName { get; set; }
    public string? CustodyUserEmail { get; set; }
    public DateTime? CustodySince { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public decimal TotalDepreciated { get; set; }
    public decimal TotalMaintenanceCost { get; set; }

    public List<TicketRow> Tickets { get; set; } = new();
    public List<CustodyRow> CustodyHistory { get; set; } = new();
    public List<ScheduleRow> Schedules { get; set; } = new();
    public List<DepreciationRow> Depreciation { get; set; } = new();

    public bool CanEdit { get; set; }
    public bool IsWarrantyValid => WarrantyEndDate.HasValue && WarrantyEndDate.Value.Date >= DateTime.UtcNow.Date;
}

public class DepreciationRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public decimal BookValueAfter { get; set; }
}

public class ScheduleRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AssetName { get; set; }
    public string? AssetTag { get; set; }
    public ScheduleFrequency Frequency { get; set; }
    public ScheduleStatus Status { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastRunDate { get; set; }
    public string? TechnicianName { get; set; }
    public bool IsOverdue => NextDueDate.Date < DateTime.UtcNow.Date && Status == ScheduleStatus.Active;
}

/// <summary>نموذج إضافة/تعديل أصل</summary>
public class AssetFormViewModel
{
    public int Id { get; set; }
    public string? AssetTag { get; set; }

    [Required(ErrorMessage = "اسم الأصل مطلوب")]
    [StringLength(200, ErrorMessage = "الاسم لا يزيد عن ٢٠٠ حرف")]
    [Display(Name = "اسم الأصل")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "التصنيف مطلوب")]
    [Display(Name = "التصنيف")]
    public int CategoryId { get; set; }

    [Display(Name = "الإدارة")]
    public int? DepartmentId { get; set; }

    [Display(Name = "الموقع")]
    public int? LocationId { get; set; }

    [Display(Name = "المورد")]
    public int? VendorId { get; set; }

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [StringLength(100)][Display(Name = "الماركة")] public string? Brand { get; set; }
    [StringLength(100)][Display(Name = "الموديل")] public string? Model { get; set; }
    [StringLength(100)][Display(Name = "الرقم التسلسلي")] public string? SerialNumber { get; set; }
    [StringLength(1000)][Display(Name = "المواصفات")] public string? Specifications { get; set; }
    [StringLength(50)][Display(Name = "اللون")] public string? Color { get; set; }

    [Display(Name = "تاريخ الشراء")]
    [DataType(DataType.Date)]
    public DateTime? PurchaseDate { get; set; }

    [Range(0, 999999999, ErrorMessage = "قيمة غير صحيحة")]
    [Display(Name = "قيمة الشراء")]
    public decimal? PurchaseValue { get; set; }

    [Range(0, 999999999, ErrorMessage = "قيمة غير صحيحة")]
    [Display(Name = "القيمة التخريدية")]
    public decimal? SalvageValue { get; set; }

    [Range(1, 60, ErrorMessage = "العمر الإنتاجي بين ١ و ٦٠ سنة")]
    [Display(Name = "العمر الإنتاجي (سنوات)")]
    public int? UsefulLifeYears { get; set; }

    [Display(Name = "طريقة الإهلاك")]
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

    [StringLength(50)][Display(Name = "رقم الفاتورة")] public string? InvoiceNumber { get; set; }

    [DataType(DataType.Date)][Display(Name = "بداية الضمان")] public DateTime? WarrantyStartDate { get; set; }
    [DataType(DataType.Date)][Display(Name = "نهاية الضمان")] public DateTime? WarrantyEndDate { get; set; }
    [StringLength(200)][Display(Name = "جهة الضمان")] public string? WarrantyProvider { get; set; }

    [Display(Name = "الحالة")]
    public AssetStatus Status { get; set; } = AssetStatus.InStore;

    [StringLength(1000)][Display(Name = "ملاحظات")] public string? Notes { get; set; }

    /// <summary>التزامن المتفائل</summary>
    public string? RowVersion { get; set; }

    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();
    public List<LookupItem> Locations { get; set; } = new();
    public List<LookupItem> Vendors { get; set; } = new();
    public List<LookupItem> Companies { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool IsNew => Id == 0;
}
