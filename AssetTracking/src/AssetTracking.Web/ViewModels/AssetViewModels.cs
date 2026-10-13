using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;
using AssetTracking.Domain.Rules;
using AssetTracking.Web.Helpers;

namespace AssetTracking.Web.ViewModels;

/// <summary>
/// اسم موضع الأصل للعرض: المخزن أولًا لأنه إجابة «أين يُحفظ؟»، ثم الفرع، ثم الخط.
/// الترتيب في خاصية واحدة لأن ست شاشات تعرض الموضع — لو كُتب في كل شاشة بترتيبه
/// الخاص لتباعدت الإجابات على سؤال واحد.
/// </summary>
public record AssetPlace
{
    public string? WarehouseName { get; init; }
    public string? SiteName { get; init; }
    public string? LineName { get; init; }
    public string? PlaceName => WarehouseName ?? SiteName ?? LineName;
    public bool IsInWarehouse => WarehouseName != null;
}

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
    public AssetPlace Place { get; set; } = new();
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

    /// <summary>فلتر المخزن — لا «LocationId» واحد: المعرّفان من جدولين يتكرّران،
    /// ففلتر موحّد كان ليعيد أصول فرع عندما يقصد المستخدم مخزنًا بنفس الرقم.</summary>
    public int? WarehouseId { get; set; }
    public int? CompanySiteId { get; set; }
    public int? ProductionLineId { get; set; }
    public int? DepartmentId { get; set; }
    public AssetStatus? Status { get; set; }
    public string? Sort { get; set; }

    // ── الترقيم ──
    public int Page { get; set; } = 1;
    private int _pageSize = 20;
    public int PageSize { get => _pageSize; set => _pageSize = Pagination.ClampSize(value); }
    public int TotalCount { get; set; }
    public int TotalPages => Pagination.TotalPages(TotalCount, PageSize);

    // ── قوائم منسدلة ──
    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Warehouses { get; set; } = new();
    public List<LookupItem> CompanySites { get; set; } = new();
    public List<LookupItem> ProductionLines { get; set; } = new();
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

    /// <summary>
    /// التصنيف الأب — يُستخدم لربط قائمة «التصنيف الفرعي» بقائمة «التصنيف الرئيسي»
    /// في المتصفح. يبقى null لبقية القوائم (الإدارات، المواقع، المورّدين).
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// الشركة المالكة — مدير النظام يرى شركات متعددة، فنحتاج تصفية القائمة
    /// بحسب الشركة المختارة حتى لا تتكرر الأسماء نفسها لكل شركة.
    /// </summary>
    public int? CompanyId { get; set; }

    /// <summary>
    /// اسم الكيان الأب عند الحاجة (موقع الشركة الذي يحوي المخزن). لا علامة
    /// «هل هذا مخزن؟» هنا: القوائم صارت مصدرين مستقلين، فالسؤال لا يُسأل.
    /// </summary>
    public string? ParentName { get; set; }

    /// <summary>
    /// السجل الحيّ وحده صالح كوجهة نقل — قوائم الفلاتر ونموذج الأصل تُبقي
    /// المعطَّل حتى لا يفقد أصلٌ مقيمٌ فيه قيمته عند التعديل.
    /// </summary>
    public bool IsActive { get; set; } = true;
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
    public AssetPlace Place { get; set; } = new();
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
    public bool IsWarrantyValid => AssetRules.IsWarrantyActive(WarrantyEndDate, DateTime.UtcNow);
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

    /// <summary>المخزن الذي يحفظ الأصل — سؤال التحفظ.</summary>
    [Display(Name = "المخزن")]
    public int? WarehouseId { get; set; }

    /// <summary>فرع/موقع الشركة الذي يقف فيه الأصل حين لا يكون في مخزن — سؤال المنشأة.</summary>
    [Display(Name = "موقع الشركة")]
    public int? CompanySiteId { get; set; }

    /// <summary>خط الإنتاج القائم عليه الأصل — سؤال تشغيلي، وليس موضع حفظ.</summary>
    [Display(Name = "خط الإنتاج")]
    public int? ProductionLineId { get; set; }

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

    /// <summary>
    /// التصنيف الرئيسي المختار في القائمة الأولى. لا يُحفَظ في الأصل — فهو
    /// مجرد وسيلة لتضييق قائمة «التصنيف الفرعي»، والمحفوظ فعلاً هو CategoryId.
    /// عند اختيار تصنيف رئيسي بلا أبناء يبقى هو نفسه تصنيف الأصل.
    /// </summary>
    [Display(Name = "التصنيف الرئيسي")]
    public int? RootCategoryId { get; set; }

    /// <summary>كل التصنيفات (رئيسية وفرعية) — الفرز والتصفية يتمّان في العرض</summary>
    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();
    public List<LookupItem> Warehouses { get; set; } = new();
    public List<LookupItem> CompanySites { get; set; } = new();
    public List<LookupItem> ProductionLines { get; set; } = new();
    public List<LookupItem> Vendors { get; set; } = new();
    public List<LookupItem> Companies { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool IsNew => Id == 0;
}
