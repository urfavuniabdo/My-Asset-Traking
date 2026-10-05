using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Entities;

/// <summary>الشركة — جذر عزل البيانات (Multi-Company Isolation)</summary>
public class Company : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? CommercialRegister { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

/// <summary>الإدارة / القسم</summary>
public class Department : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public string? ManagerUserId { get; set; }
    public string? CostCenter { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

/// <summary>الموقع الجغرافي (مبنى / مصنع / مخزن ...)</summary>
public class Location : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public LocationType Type { get; set; } = LocationType.Office;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

/// <summary>المورّد / الشركة الموردة</summary>
public class Vendor : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
    /// <summary>تقييم المورّد 1..5</summary>
    public int? Rating { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

/// <summary>فئة الأصل — تدعم التسلسل الهرمي (فئة أب / فئة فرعية)</summary>
public class Category : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    /// <summary>العمر الافتراضي بالسنوات (يُستخدم في احتساب الإهلاك)</summary>
    public int? UsefulLifeYears { get; set; }

    /// <summary>نسبة القيمة التخريدية من قيمة الشراء (0..1)</summary>
    public decimal? SalvageRate { get; set; }

    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
