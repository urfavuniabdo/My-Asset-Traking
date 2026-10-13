using AssetTracking.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AssetTracking.Web.Filters;

/// <summary>
/// مصدر وحيد لصلاحيات روابط القائمة الجانبية.
///
/// المشكلة التي يحلّها: القوائم كانت تعيد كتابة أدوار كل رابط يدوياً
/// (`IsInRole(Admin) || IsInRole(CompanyManager)`)، فتفصل عن السياسة الحقيقية.
/// النتيجة المحسوسة: رابط «/Warehouses» كان يظهر للفنّي بينما
/// `WarehousesController` مقفل بـ `ManagerOrAdmin` — فيضغط ويصطدم بـ AccessDenied.
///
/// هنا تُقيَّم السياسة المسجّلة نفسها (نفس الكائن الذي يفحصه `[Authorize]`)
/// مرة واحدة لكل طلب، فتُبنى الروابط من قرار الخادم لا من نسخة يدوية.
/// </summary>
public class NavPolicyFilter : IAsyncActionFilter
{
    /// <summary>السياسات التي قد تحتاجها الواجهات — كلها من <see cref="Policies"/>.</summary>
    private static readonly string[] NavPolicies =
    [
        Policies.AdminOnly,
        Policies.ManagerOrAdmin,
        Policies.TechnicianOrAbove,
        Policies.AuthenticatedUser
    ];

    private readonly IAuthorizationService _authorization;

    public NavPolicyFilter(IAuthorizationService authorization) => _authorization = authorization;

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception != null || executed.Controller is not Controller controller) return;
        if (controller.User.Identity?.IsAuthenticated != true) return;

        var access = new Dictionary<string, bool>(NavPolicies.Length);
        foreach (var policy in NavPolicies)
            access[policy] = (await _authorization.AuthorizeAsync(controller.User, policy)).Succeeded;

        controller.ViewData[ViewDataKeys.PolicyAccess] = access;
    }
}

/// <summary>مفاتيح ViewData المشتركة بين الفلاتر والعروض.</summary>
public static class ViewDataKeys
{
    public const string PolicyAccess = "PolicyAccess";
}
