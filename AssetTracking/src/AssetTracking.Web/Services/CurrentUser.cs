using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using System.Security.Claims;

namespace AssetTracking.Web.Services;

/// <summary>
/// يقرأ هوية المستخدم الحالي من الـClaims — وهو ما تعتمد عليه
/// EF Core Global Query Filters لعزل بيانات الشركات.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);
    public string? FullName => Principal?.FindFirstValue(AppClaims.FullName);

    public int? CompanyId
    {
        get
        {
            var raw = Principal?.FindFirstValue(AppClaims.CompanyId);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) ?? false;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => _accessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = _accessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : (ua.Length > 480 ? ua[..480] : ua);
        }
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
