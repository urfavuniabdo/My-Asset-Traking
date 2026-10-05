namespace AssetTracking.Web.Configuration;

/// <summary>
/// مفاتيح تشغيل الوحدات (Feature Flags).
///
/// الغرض: تبسيط النظام للمستخدم النهائي عن طريق <b>إخفاء</b> وحدات كاملة
/// دون حذف أي سطر كود أو أي عمود في قاعدة البيانات. كل وحدة مُخفاة
/// تبقى جاهزة تماماً، ويكفي تحويل مفتاحها إلى <c>true</c> في
/// <c>appsettings.json</c> لتعود للعمل فوراً في التحديث القادم.
///
/// التأثير عند الإخفاء (false):
///   1) يُخفى الرابط من القائمة الجانبية.
///   2) تُحجب مسارات الوحدة (يُعاد التوجيه للوحة المعلومات) عبر
///      <see cref="Filters.FeatureGateAttribute"/> — فلا يكفي إخفاء الرابط،
///      بل يُمنع الوصول المباشر بكتابة العنوان أيضاً (حاجز أمني).
///   3) تُخفى بطاقات/مؤشرات/أعمدة الوحدة من لوحة المعلومات والتقارير.
/// </summary>
public class FeatureFlags
{
    public const string SectionName = "Features";

    /// <summary>وحدة تذاكر الدعم الفني. مُخفاة حالياً — تحديث قادم.</summary>
    public bool Tickets { get; set; } = false;

    /// <summary>وحدة الصيانة الوقائية الدورية. مُخفاة حالياً — تحديث قادم.</summary>
    public bool PreventiveMaintenance { get; set; } = false;

    /// <summary>
    /// البيانات المالية والإهلاك (قيمة الشراء، القيمة التخريدية، القيمة الدفترية،
    /// طريقة الإهلاك، جدول الإهلاك، تقرير الإهلاك). مُخفاة حالياً — تحديث قادم.
    /// </summary>
    public bool Financials { get; set; } = false;

    /// <summary>لوحة المهام المجدولة (Hangfire). مُخفاة حالياً — تحديث قادم.</summary>
    public bool ScheduledJobs { get; set; } = false;

    /// <summary>وحدة الجرد الدوري.</summary>
    public bool InventoryAudits { get; set; } = true;

    /// <summary>وحدة التقارير.</summary>
    public bool Reports { get; set; } = true;
}
