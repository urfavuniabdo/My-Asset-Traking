using AssetTracking.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AssetTracking.Web.Filters;

/// <summary>أسماء الوحدات القابلة للإخفاء.</summary>
public enum AppFeature
{
    Tickets,
    PreventiveMaintenance,
    Financials,
    ScheduledJobs,
    InventoryAudits,
    Reports
}

/// <summary>
/// حاجز الوحدات المُخفاة.
///
/// إخفاء الرابط من القائمة الجانبية <b>لا يكفي</b> — أي مستخدم يستطيع كتابة
/// العنوان مباشرة. هذا الفلتر يحجب المسار فعلياً على مستوى الخادم:
/// إن كانت الوحدة مُطفأة يُعاد التوجيه إلى لوحة المعلومات برسالة واضحة،
/// وتُرجَع 404 لطلبات AJAX/JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class FeatureGateAttribute : Attribute, IAsyncActionFilter
{
    private readonly AppFeature _feature;

    public FeatureGateAttribute(AppFeature feature) => _feature = feature;

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var flags = ctx.HttpContext.RequestServices
            .GetService(typeof(IOptions<FeatureFlags>)) as IOptions<FeatureFlags>;

        var f = flags?.Value ?? new FeatureFlags();

        var enabled = _feature switch
        {
            AppFeature.Tickets => f.Tickets,
            AppFeature.PreventiveMaintenance => f.PreventiveMaintenance,
            AppFeature.Financials => f.Financials,
            AppFeature.ScheduledJobs => f.ScheduledJobs,
            AppFeature.InventoryAudits => f.InventoryAudits,
            AppFeature.Reports => f.Reports,
            _ => true
        };

        if (enabled)
        {
            await next();
            return;
        }

        // طلبات AJAX / JSON ⇒ 404 نظيفة
        var req = ctx.HttpContext.Request;
        var isAjax = req.Headers["X-Requested-With"] == "XMLHttpRequest"
                     || (req.Headers.Accept.ToString()?.Contains("application/json") ?? false);

        if (isAjax)
        {
            ctx.Result = new NotFoundObjectResult(new
            {
                message = "هذه الوحدة غير متاحة حالياً — ستتوفر في تحديث قادم."
            });
            return;
        }

        if (ctx.Controller is Controller c)
            c.TempData["ToastInfo"] = "هذه الوحدة غير متاحة حالياً — ستتوفر في تحديث قادم.";

        ctx.Result = new RedirectToActionResult("Index", "Home", null);
    }
}
