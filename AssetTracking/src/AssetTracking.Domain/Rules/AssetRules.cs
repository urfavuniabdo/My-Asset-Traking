using System.Linq.Expressions;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Rules;

/// <summary>
/// قواعد العمل النقية على الأصل — المصدر الوحيد للحقائق التي كانت مكرّسة
/// بتعابير مختلفة في Controllers وJobs وViews.
///
/// كل دالة تأخذ <paramref name="now"/> صراحةً: الحساب لا يعتمد على ساعة الخادم
/// داخله، فيصبح قابلاً للاختبار بحدود معروفة (29/30/31 يوماً مثلاً).
/// </summary>
public static class AssetRules
{
    /// <summary>عدد الأيام الذي يُعتبر بعده الضمان «شارف الانتهاء» في اللوحات والقوائم.</summary>
    public const int WarrantyWarningDays = 30;

    /// <summary>عتبة التنبيه الحَرِج المستخدمة في مهمة تنبيهات الضمان.</summary>
    public const int WarrantyCriticalDays = 7;

    /// <summary>
    /// الخارج عن الخدمة: مستبعد أو مفقود. نفس التعريف يُجيب عن «هل يُسلَّم في
    /// عهدة؟» و«هل يُهلك؟» — فلا ينجرف سؤال إلى عضوية مختلفة عن الآخر بصمت.
    /// </summary>
    public static bool IsOutOfService(AssetStatus status)
        => status is AssetStatus.Disposed or AssetStatus.Lost;

    /// <summary>
    /// predicate للاستخدام داخل استعلام EF (يُترجَم إلى SQL) — نفس تعريف
    /// <see cref="IsOutOfService"/> بلا تكرار منطقي.
    /// </summary>
    public static Expression<Func<Asset, bool>> InService()
        => a => a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Lost;

    // ── الضمان ────────────────────────────────────────────────

    /// <summary>الضمان لا يزال سارياً (يوم انتهائه نفسه داخل المدة).</summary>
    public static bool IsWarrantyActive(DateTime? warrantyEndUtc, DateTime nowUtc)
        => warrantyEndUtc.HasValue && warrantyEndUtc.Value.Date >= nowUtc.Date;

    /// <summary>الضمان انتهى — دلالة مختلفة عن «شارف الانتهاء»، ولا تُخلط بينهما.</summary>
    public static bool IsWarrantyExpired(DateTime? warrantyEndUtc, DateTime nowUtc)
        => warrantyEndUtc.HasValue && warrantyEndUtc.Value.Date < nowUtc.Date;

    /// <summary>سارٍ وينتهي خلال <paramref name="days"/> يوماً أو أقل.</summary>
    public static bool IsWarrantyExpiringWithin(DateTime? warrantyEndUtc, DateTime nowUtc, int days)
        => IsWarrantyActive(warrantyEndUtc, nowUtc)
           && warrantyEndUtc!.Value.Date <= nowUtc.Date.AddDays(days);

    /// <summary>شارف الانتهاء على نافذة التحذير المعتمدة (<see cref="WarrantyWarningDays"/>).</summary>
    public static bool IsWarrantyExpiringSoon(DateTime? warrantyEndUtc, DateTime nowUtc)
        => IsWarrantyExpiringWithin(warrantyEndUtc, nowUtc, WarrantyWarningDays);

    /// <summary>
    /// صيغة الاستعلام نفسه داخل EF — تُستدعى من اللوحات والتقارير حتى يبقى
    /// «عدد الأصول التي ينتهي ضمانها قريباً» تعريفاً واحداً في كل السطح.
    /// القيم تُحسب خارج التعبير ثم تُلتقط كمعاملات SQL، لأن بعض المزودين
    /// لا يترجمون DateTime.AddDays داخل الاستعلام.
    /// </summary>
    public static Expression<Func<Asset, bool>> WarrantyExpiringSoonWithin(DateTime nowUtc, int days)
    {
        var from = nowUtc.Date;
        var until = from.AddDays(days);

        return a => a.WarrantyEndDate != null
                    && a.WarrantyEndDate >= from
                    && a.WarrantyEndDate <= until
                    && a.Status != AssetStatus.Disposed
                    && a.Status != AssetStatus.Lost;
    }

    /// <summary>عدد الأيام المتبقية على نهاية الضمان (سالب يعني منتهٍ).</summary>
    public static int? WarrantyDaysRemaining(DateTime? warrantyEndUtc, DateTime nowUtc)
        => warrantyEndUtc.HasValue ? (warrantyEndUtc.Value.Date - nowUtc.Date).Days : null;

    // ── المالية ───────────────────────────────────────────────

    /// <summary>
    /// الأصول التي تدخل في إجمالي قيمة المحفظة: المستبعد يخرج منها.
    /// ملاحظة: المفقود يبقى داخل التقييم لأنه ما زال ملكاً للشركة.
    /// </summary>
    public static bool CountsInPortfolioValue(AssetStatus status) => status != AssetStatus.Disposed;

    public static Expression<Func<Asset, bool>> InPortfolioValue()
        => a => a.Status != AssetStatus.Disposed;

    /// <summary>
    /// أصل مُسند في عهدة فعلية (غير مستبعد) — السؤال الذي يعدّ «عهد الموظفين»
    /// في التقارير ولوحة المستخدمين. كان مكرراً بثلاث صيغ.
    /// </summary>
    public static Expression<Func<Asset, bool>> HeldInCustody()
        => a => a.CurrentCustodyUserId != null && a.Status != AssetStatus.Disposed;

    /// <summary>عمر الأصل بالأشهر من تاريخ الشراء.</summary>
    public static int AgeInMonths(DateTime? purchaseDate, DateTime nowUtc)
        => purchaseDate.HasValue
            ? Math.Max(0, (int)((nowUtc - purchaseDate.Value).TotalDays / 30.44))
            : 0;

    /// <summary>
    /// العمر النافع المعتمد — قيمة الأصل، وإلا عمر التصنيف، وإلا الافتراضي النظامي
    /// <see cref="DefaultUsefulLifeYears"/>. يمنع انتشار الرقم 5 حرفياً في الكود.
    /// </summary>
    public const int DefaultUsefulLifeYears = 5;

    public static int ResolveUsefulLifeYears(int? assetLife, int? categoryLife)
    {
        var life = assetLife ?? categoryLife ?? DefaultUsefulLifeYears;
        return life <= 0 ? DefaultUsefulLifeYears : life;
    }
}
