namespace AssetTracking.Application.Interfaces;

/// <summary>
/// يوفّر هوية المستخدم الحالي لطبقة البنية التحتية دون اعتماد على HttpContext.
/// هذا ما يسمح لـ Global Query Filters بمعرفة شركة المستخدم.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    string? FullName { get; }

    /// <summary>شركة المستخدم — null للـ Admin</summary>
    int? CompanyId { get; }

    /// <summary>هل يملك صلاحية رؤية كل الشركات؟</summary>
    bool IsAdmin { get; }

    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsInRole(string role);
}
