using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// الأساس لكل الـControllers المحمية.
/// يوفّر رسائل Toast موحدة وفحص ملكية السجل (الحاجز الرابع في الدفاع المتعدد).
/// </summary>
[Authorize]
public abstract class BaseController : Controller
{
    protected ICurrentUser Me => HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

    protected void ToastSuccess(string message) => TempData["ToastSuccess"] = message;
    protected void ToastError(string message) => TempData["ToastError"] = message;
    protected void ToastInfo(string message) => TempData["ToastInfo"] = message;

    /// <summary>
    /// ⚠️ مهم أمنياً: نرجع 404 (لا 403) عند محاولة الوصول لسجل شركة أخرى
    /// حتى لا يستطيع المهاجم تعداد المعرّفات ومعرفة ما هو موجود.
    /// </summary>
    protected IActionResult NotFoundOrForbidden()
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return NotFound(new { error = "السجل غير موجود أو لا تملك صلاحية الوصول إليه" });

        ToastError("السجل غير موجود أو لا تملك صلاحية الوصول إليه");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>شركة المستخدم الحالي — يرمي إن كان Admin بلا شركة محددة</summary>
    protected int RequireCompanyId()
    {
        var id = Me.CompanyId;
        if (id == null)
            throw new InvalidOperationException("هذه العملية تتطلب مستخدماً مرتبطاً بشركة.");
        return id.Value;
    }

    protected bool IsAdmin => Me.IsAdmin;
    protected bool IsManager => Me.IsInRole(Roles.CompanyManager) || IsAdmin;
    protected bool IsTechnician => Me.IsInRole(Roles.Technician);
}
