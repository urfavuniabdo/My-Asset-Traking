using AssetTracking.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AssetTracking.Infrastructure.Identity;

/// <summary>
/// مستخدم النظام — يوسّع ASP.NET Core Identity بحقول الشركة والقسم.
/// كلمات المرور تُخزَّن بـ PBKDF2 عبر Identity (لا نتعامل معها يدوياً أبداً).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>شركة المستخدم — null تعني Admin يرى كل الشركات</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string? JobTitle { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? NationalId { get; set; }
    public string? AvatarPath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }

    public bool MustChangePassword { get; set; }

    /// <summary>الحذف الناعم</summary>
    public bool IsDeleted { get; set; }
}

/// <summary>الدور — يوسّع IdentityRole بالاسم العربي</summary>
public class ApplicationRole : IdentityRole
{
    public string? NameAr { get; set; }
    public string? Description { get; set; }

    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}
