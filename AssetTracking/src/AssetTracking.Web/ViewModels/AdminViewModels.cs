using System.ComponentModel.DataAnnotations;
using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Enums;
using AssetTracking.Web.Helpers;

namespace AssetTracking.Web.ViewModels;

// ═══════════════════ لوحة الإدارة ═══════════════════
public class AdminHomeViewModel
{
    public int Users { get; set; }
    public int ActiveUsers { get; set; }
    public int Companies { get; set; }
    public int Departments { get; set; }
    /// <summary>خطوط الإنتاج — كيانات مستقلة في جدولها، تُدار من شاشة الإدارة.</summary>
    public int ProductionLines { get; set; }

    /// <summary>عدّ المخازن ومواقع الشركة من جدوليهما المستقلين — لا من شرط نوع.</summary>
    public int Warehouses { get; set; }
    public int CompanySites { get; set; }
    public int Categories { get; set; }
    public int Vendors { get; set; }
    public int SlaPolicies { get; set; }
    public int AuditLogs { get; set; }
    public bool IsAdmin { get; set; }
    public string? CompanyName { get; set; }
}

// ═══════════════════ المستخدمون ═══════════════════
public class UserListViewModel
{
    public List<UserRow> Items { get; set; } = new();
    public string? Q { get; set; }
    public string? Role { get; set; }
    public int? CompanyId { get; set; }
    public bool? Active { get; set; }

    public int Page { get; set; } = 1;
    private int _pageSize = 20;
    public int PageSize { get => _pageSize; set => _pageSize = Pagination.ClampSize(value); }
    public int TotalCount { get; set; }
    public int TotalPages => Pagination.TotalPages(TotalCount, PageSize);

    public List<LookupItem> Companies { get; set; } = new();
    public int CountAdmin { get; set; }
    public int CountManager { get; set; }
    public int CountTechnician { get; set; }
    public int CountEmployee { get; set; }
    public bool IsAdmin { get; set; }
}

public class UserRow
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? JobTitle { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentName { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public bool LockedOut { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int AssetsHeld { get; set; }
}

public class UserFormViewModel
{
    public string? Id { get; set; }
    public bool IsNew => string.IsNullOrEmpty(Id);

    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم بين 3 و150 حرفاً")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "رقم الهاتف")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Display(Name = "المسمى الوظيفي")]
    [StringLength(100)]
    public string? JobTitle { get; set; }

    [Display(Name = "الرقم الوظيفي")]
    [StringLength(50)]
    public string? EmployeeNumber { get; set; }

    [Display(Name = "الرقم القومي")]
    [StringLength(20)]
    public string? NationalId { get; set; }

    [Required(ErrorMessage = "الدور مطلوب")]
    [Display(Name = "الدور")]
    public string Role { get; set; } = Domain.Common.Roles.Employee;

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [Display(Name = "الإدارة")]
    public int? DepartmentId { get; set; }

    [Display(Name = "الحساب مُفعَّل")]
    public bool IsActive { get; set; } = true;

    /// <summary>كلمة المرور — مطلوبة عند الإنشاء فقط</summary>
    [Display(Name = "كلمة المرور")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور 8 أحرف على الأقل")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "تأكيد كلمة المرور")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "إلزامه بتغيير كلمة المرور عند أول دخول")]
    public bool MustChangePassword { get; set; }

    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();
    public List<RoleOption> RoleOptions { get; set; } = new();
}

public class RoleOption
{
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// ═══════════════════ قوائم مرجعية عامة ═══════════════════
/// <summary>صف في قائمة بيانات مرجعية (شركة/إدارة/خط إنتاج/تصنيف/مورّد)</summary>
public class RefRow
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public string? CompanyName { get; set; }
    public bool IsActive { get; set; }
    public int AssetsCount { get; set; }
    /// <summary>سطر معلومات إضافي يختلف بحسب نوع الكيان</summary>
    public string? Extra { get; set; }
    public string? Extra2 { get; set; }
}

public class RefListViewModel
{
    /// <summary>
    /// companies | departments | productionlines | categories | vendors | sla.
    /// المخازن ومواقع الشركة ليست أنواعاً هنا: لكل وحدة قائمتها وشاشة أشكالها.
    /// </summary>
    public string Kind { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-collection";
    public List<RefRow> Items { get; set; } = new();
    public string? Q { get; set; }
    public bool? Active { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanCreate { get; set; } = true;
    /// <summary>العنوان المستخدم في العمود الثالث (Extra)</summary>
    public string? ExtraHeader { get; set; }
    public string? Extra2Header { get; set; }
}

// ═══════════════════ نماذج البيانات المرجعية ═══════════════════
public class CompanyFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم الشركة مطلوب")]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "اسم الشركة (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [Required(ErrorMessage = "كود الشركة مطلوب")]
    [StringLength(20, MinimumLength = 2)]
    [Display(Name = "الكود")]
    public string Code { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "الرقم الضريبي")]
    public string? TaxNumber { get; set; }

    [StringLength(50)]
    [Display(Name = "السجل التجاري")]
    public string? CommercialRegister { get; set; }

    [StringLength(300)]
    [Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(80)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    [StringLength(30)]
    [Display(Name = "الهاتف")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
    [StringLength(150)]
    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(200)]
    [Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;
}

public class DepartmentFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم الإدارة مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم الإدارة (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(20)]
    [Display(Name = "الكود")]
    public string? Code { get; set; }

    [Display(Name = "مدير الإدارة")]
    public string? ManagerUserId { get; set; }

    [StringLength(50)]
    [Display(Name = "مركز التكلفة")]
    public string? CostCenter { get; set; }

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;

    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
    public List<UserLookupItem> Managers { get; set; } = new();
}

/// <summary>
/// نموذج خط الإنتاج — كيان تشغيلي مستقل: لا اسم موقع ولا اسم مخزن ولا عمود نوع.
/// الخط ليس فرعاً ولا مخزناً، فأي حقل موضع فيه يعيد خلط الأسئلة الثلاثة.
/// </summary>
public class ProductionLineFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم خط الإنتاج مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم خط الإنتاج")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(30)]
    [Display(Name = "كود الخط")]
    public string? Code { get; set; }

    [StringLength(500)]
    [Display(Name = "وصف الخط")]
    public string? Notes { get; set; }

    [Display(Name = "مُفعَّل")]
    public bool IsActive { get; set; } = true;

    public int? CompanyId { get; set; }
    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();

    public ProductionLineInput ToInput() => new()
    {
        Id = Id, NameAr = NameAr, NameEn = NameEn, Code = Code,
        Notes = Notes, IsActive = IsActive, CompanyId = CompanyId
    };
}

public class CategoryFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم التصنيف مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم التصنيف (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(20)]
    [Display(Name = "الكود")]
    public string? Code { get; set; }

    [Display(Name = "التصنيف الأب")]
    public int? ParentCategoryId { get; set; }

    [Range(0, 60, ErrorMessage = "العمر الإنتاجي بين 0 و60 سنة")]
    [Display(Name = "العمر الإنتاجي (سنة)")]
    public int? UsefulLifeYears { get; set; }

    [Range(0, 1, ErrorMessage = "نسبة القيمة التخريدية بين 0 و1")]
    [Display(Name = "نسبة القيمة التخريدية (0 — 1)")]
    public decimal? SalvageRate { get; set; }

    [Display(Name = "طريقة الإهلاك")]
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

    [StringLength(50)]
    [Display(Name = "الأيقونة (Bootstrap Icons)")]
    public string? Icon { get; set; }

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
    public List<LookupItem> Parents { get; set; } = new();
}

public class VendorFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم المورّد مطلوب")]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "اسم المورّد (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "الاسم بالإنجليزية")]
    public string? NameEn { get; set; }

    [StringLength(20)]
    [Display(Name = "الكود")]
    public string? Code { get; set; }

    [StringLength(150)]
    [Display(Name = "الشخص المسؤول")]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    [Display(Name = "الهاتف")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
    [StringLength(150)]
    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(300)]
    [Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(30)]
    [Display(Name = "الرقم الضريبي")]
    public string? TaxNumber { get; set; }

    [Range(1, 5, ErrorMessage = "التقييم بين 1 و5")]
    [Display(Name = "التقييم (1 — 5)")]
    public int? Rating { get; set; }

    [StringLength(1000)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
}

public class SlaFormViewModel
{
    public int Id { get; set; }
    public bool IsNew => Id == 0;

    [Required(ErrorMessage = "اسم السياسة مطلوب")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "اسم السياسة")]
    public string NameAr { get; set; } = string.Empty;

    [Display(Name = "الأولوية المستهدفة")]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [Range(1, 8760, ErrorMessage = "مدة الاستجابة بين ساعة و8760 ساعة")]
    [Display(Name = "مدة الاستجابة المستهدفة (ساعة)")]
    public int ResponseHours { get; set; } = 4;

    [Range(1, 8760, ErrorMessage = "مدة الحل بين ساعة و8760 ساعة")]
    [Display(Name = "مدة الحل المستهدفة (ساعة)")]
    public int ResolutionHours { get; set; } = 24;

    [Range(0, 720, ErrorMessage = "ساعات التنبيه بين 0 و720")]
    [Display(Name = "التنبيه قبل الاستحقاق (ساعة)")]
    public int EscalationWarningHours { get; set; } = 2;

    [Display(Name = "الشركة")]
    public int? CompanyId { get; set; }

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;

    public bool NeedsCompanyPick { get; set; }
    public List<LookupItem> Companies { get; set; } = new();
}

// ═══════════════════ سجل التدقيق ═══════════════════
public class AuditLogViewModel
{
    public List<AuditLogRow> Items { get; set; } = new();
    public string? Q { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;
    private int _pageSize = 40;
    public int PageSize { get => _pageSize; set => _pageSize = Pagination.ClampSize(value); }
    public int TotalCount { get; set; }
    public int TotalPages => Pagination.TotalPages(TotalCount, PageSize);

    public List<string> Actions { get; set; } = new();
    public List<string> Entities { get; set; } = new();
}

public class AuditLogRow
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; }
}
