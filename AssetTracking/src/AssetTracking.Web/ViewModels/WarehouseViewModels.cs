using System.ComponentModel.DataAnnotations;
using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

// ═══════════════════════════ المخازن ═══════════════════════════

/// <summary>
/// سطر مخزن. «أين الموقع؟» عمود اختياري بالمعنى الحقيقي: null حالة كاملة
/// لا نقص يُنبَّه عليه في كل سطر.
/// </summary>
public class WarehouseRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public string? Address { get; set; }
    public string? SiteName { get; set; }
    public int AssetCount { get; set; }
    public decimal TotalValue { get; set; }
    public int UnderMaintenance { get; set; }
    public int InCustodyCount { get; set; }
}

public class WarehousesIndexViewModel
{
    public List<WarehouseRow> Items { get; set; } = new();
    public List<LookupItem> TopWarehouses { get; set; } = new();

    /// <summary>أصول بلا أي موضع (لا مخزن ولا موقع) — الرقم الذي يبرر وجود شاشة المخازن.</summary>
    public int UnplacedAssets { get; set; }

    public int TotalAssets => Items.Sum(w => w.AssetCount);
    public decimal TotalValue => Items.Sum(w => w.TotalValue);
    public int ActiveCount => Items.Count(w => w.IsActive);
}

/// <summary>نموذج المخزن — لا عمود «النوع» فيه: نوع السجل هو الكيان الذي يُحفظ فيه.</summary>
public class WarehouseFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم المخزن مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم المخزن")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(30)]
    [Display(Name = "كود المخزن")]
    public string? Code { get; set; }

    [StringLength(500)]
    [Display(Name = "وصف المخزن")]
    public string? Notes { get; set; }

    [StringLength(300)]
    [Display(Name = "عنوان أو مكان التخزين")]
    public string? Address { get; set; }

    [Display(Name = "موقع الشركة")]
    public int? CompanySiteId { get; set; }

    [Display(Name = "مُفعَّل")]
    public bool IsActive { get; set; } = true;

    public int? CompanyId { get; set; }
    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
    public List<LookupItem> Sites { get; set; } = new();

    public WarehouseInput ToInput() => new()
    {
        Id = Id, NameAr = NameAr, NameEn = NameEn, Code = Code, Notes = Notes,
        Address = Address, CompanySiteId = CompanySiteId, IsActive = IsActive, CompanyId = CompanyId
    };
}

/// <summary>سطر أصل داخل مخزن (شاشة التفاصيل)</summary>
public class WarehouseAssetRow
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public AssetStatus Status { get; set; }
}

/// <summary>نقلة مكانية من LocationLogs — الإجابة المؤرخة على «متى دخل/خرج؟»</summary>
public class WarehouseMoveRow
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string? ToName { get; set; }
    public DateTime MovedAt { get; set; }
    public string? Reason { get; set; }
}

public class WarehouseDetailsViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public string? Address { get; set; }
    public string? SiteName { get; set; }
    public int? SiteId { get; set; }

    public int AssetCount { get; set; }
    public decimal TotalValue { get; set; }
    public int UnderMaintenance { get; set; }
    public int InCustodyCount { get; set; }

    public List<WarehouseAssetRow> Assets { get; set; } = new();
    public List<WarehouseMoveRow> Moves { get; set; } = new();

    /// <summary>الأصول أكثر مما تُعرض — لإظهار «عرض كل أصول المخزن».</summary>
    public bool HasMoreAssets => AssetCount > Assets.Count;
}

// ═══════════════════════ مواقع / فروع الشركة ═══════════════════════

/// <summary>
/// سطر موقع شركة. يوجد بلا مخازن: عدد المخازن صفر حالة طبيعية، لا تنبيه.
/// </summary>
public class SiteRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? Code { get; set; }
    public SiteKind Kind { get; set; }
    public bool IsActive { get; set; }
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public string? Notes { get; set; }
    public List<LookupItem> Warehouses { get; set; } = new();

    /// <summary>أصول تقف في الموقع نفسه (ما ليس داخل مخزن تحته).</summary>
    public int DirectAssetCount { get; set; }

    public int WarehouseCount => Warehouses.Count;
}

public class CompanySitesIndexViewModel
{
    public List<SiteRow> Items { get; set; } = new();
    public int TotalSites => Items.Count;
    public int ActiveCount => Items.Count(s => s.IsActive);
    public int TotalWarehousesInside => Items.Sum(s => s.WarehouseCount);
}

public class CompanySiteFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم الموقع مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم الموقع")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(30)]
    [Display(Name = "كود الموقع")]
    public string? Code { get; set; }

    [Display(Name = "صفة المنشأة")]
    public SiteKind Kind { get; set; } = SiteKind.Office;

    [StringLength(300)]
    [Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    [StringLength(100)]
    [Display(Name = "المحافظة")]
    public string? Governorate { get; set; }

    [StringLength(80)]
    [Display(Name = "مسؤول التواصل")]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    [Display(Name = "هاتف التواصل")]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    [Display(Name = "وصف الموقع")]
    public string? Notes { get; set; }

    [Display(Name = "مُفعَّل")]
    public bool IsActive { get; set; } = true;

    public int? CompanyId { get; set; }
    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();

    public CompanySiteInput ToInput() => new()
    {
        Id = Id, NameAr = NameAr, NameEn = NameEn, Code = Code, Kind = Kind,
        Address = Address, City = City, Governorate = Governorate,
        ContactPerson = ContactPerson, ContactPhone = ContactPhone,
        Notes = Notes, IsActive = IsActive, CompanyId = CompanyId
    };
}

public class CompanySiteDetailsViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public SiteKind Kind { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public List<LookupItem> Warehouses { get; set; } = new();
    public List<WarehouseAssetRow> Assets { get; set; } = new();
    public List<WarehouseMoveRow> Moves { get; set; } = new();

    public int DirectAssetCount { get; set; }
    public bool HasMoreAssets => DirectAssetCount > Assets.Count;
}
