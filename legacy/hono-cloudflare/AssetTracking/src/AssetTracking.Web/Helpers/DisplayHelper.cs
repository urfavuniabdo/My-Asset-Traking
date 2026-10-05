using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using System.Globalization;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// مساعدات العرض — الأسماء العربية للحالات وألوان الشارات (Badges).
/// تُستخدم في كل الـViews لتوحيد المصطلحات.
/// </summary>
public static class DisplayHelper
{
    // ── ثقافة العرض: أرقام لاتينية + تقويم ميلادي ─────────────
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("ar-EG");

    /// <summary>تنسيق المبلغ بالجنيه المصري: 12,500.00 ج.م</summary>
    public static string Money(decimal? value)
        => value.HasValue
            ? $"{value.Value.ToString("N2", CultureInfo.InvariantCulture)} {AppConstants.CurrencySymbol}"
            : "—";

    /// <summary>تنسيق مختصر للمبالغ الكبيرة: 1.2 م ج.م</summary>
    public static string MoneyShort(decimal? value)
    {
        if (!value.HasValue) return "—";
        var v = value.Value;
        var abs = Math.Abs(v);
        if (abs >= 1_000_000m)
            return $"{(v / 1_000_000m).ToString("N2", CultureInfo.InvariantCulture)} م {AppConstants.CurrencySymbol}";
        if (abs >= 10_000m)
            return $"{(v / 1_000m).ToString("N1", CultureInfo.InvariantCulture)} ألف {AppConstants.CurrencySymbol}";
        return Money(v);
    }

    /// <summary>تاريخ ميلادي مقروء</summary>
    public static string Date(DateTime? d)
        => d.HasValue ? d.Value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : "—";

    /// <summary>تاريخ ووقت</summary>
    public static string DateTimeFull(DateTime? d)
        => d.HasValue ? d.Value.ToString("yyyy/MM/dd — HH:mm", CultureInfo.InvariantCulture) : "—";

    /// <summary>وقت نسبي: "قبل 3 ساعات"</summary>
    public static string Relative(DateTime d)
    {
        var diff = DateTime.UtcNow - d;
        if (diff.TotalSeconds < 60) return "الآن";
        if (diff.TotalMinutes < 60) return $"قبل {(int)diff.TotalMinutes} دقيقة";
        if (diff.TotalHours < 24) return $"قبل {(int)diff.TotalHours} ساعة";
        if (diff.TotalDays < 30) return $"قبل {(int)diff.TotalDays} يوم";
        if (diff.TotalDays < 365) return $"قبل {(int)(diff.TotalDays / 30)} شهر";
        return $"قبل {(int)(diff.TotalDays / 365)} سنة";
    }

    // ── حالة الأصل ──────────────────────────────────────────
    public static string Name(AssetStatus s) => s switch
    {
        AssetStatus.Active => "نشط",
        AssetStatus.InStore => "في المخزن",
        AssetStatus.UnderMaintenance => "تحت الصيانة",
        AssetStatus.Damaged => "تالف",
        AssetStatus.Disposed => "مستبعد",
        AssetStatus.Lost => "مفقود",
        _ => s.ToString()
    };

    public static string Badge(AssetStatus s) => s switch
    {
        AssetStatus.Active => "bg-success",
        AssetStatus.InStore => "bg-secondary",
        AssetStatus.UnderMaintenance => "bg-warning text-dark",
        AssetStatus.Damaged => "bg-danger",
        AssetStatus.Disposed => "bg-dark",
        AssetStatus.Lost => "bg-danger",
        _ => "bg-light text-dark"
    };

    // ── حالة التذكرة ─────────────────────────────────────────
    public static string Name(TicketStatus s) => s switch
    {
        TicketStatus.Open => "مفتوحة",
        TicketStatus.Assigned => "مُكلَّف بها",
        TicketStatus.InProgress => "جاري العمل",
        TicketStatus.WaitingParts => "بانتظار قطع غيار",
        TicketStatus.Resolved => "تم الحل",
        TicketStatus.Closed => "مغلقة",
        TicketStatus.Cancelled => "ملغاة",
        _ => s.ToString()
    };

    public static string Badge(TicketStatus s) => s switch
    {
        TicketStatus.Open => "bg-primary",
        TicketStatus.Assigned => "bg-info text-dark",
        TicketStatus.InProgress => "bg-warning text-dark",
        TicketStatus.WaitingParts => "bg-secondary",
        TicketStatus.Resolved => "bg-success",
        TicketStatus.Closed => "bg-dark",
        TicketStatus.Cancelled => "bg-light text-dark",
        _ => "bg-light text-dark"
    };

    // ── أولوية التذكرة ───────────────────────────────────────
    public static string Name(TicketPriority p) => p switch
    {
        TicketPriority.Low => "منخفضة",
        TicketPriority.Medium => "متوسطة",
        TicketPriority.High => "عالية",
        TicketPriority.Critical => "حرجة",
        _ => p.ToString()
    };

    public static string Badge(TicketPriority p) => p switch
    {
        TicketPriority.Low => "bg-secondary",
        TicketPriority.Medium => "bg-info text-dark",
        TicketPriority.High => "bg-warning text-dark",
        TicketPriority.Critical => "bg-danger",
        _ => "bg-light text-dark"
    };

    // ── نوع التذكرة ──────────────────────────────────────────
    public static string Name(TicketType t) => t switch
    {
        TicketType.Corrective => "صيانة إصلاحية",
        TicketType.Preventive => "صيانة وقائية",
        TicketType.Installation => "تركيب",
        TicketType.Inspection => "فحص",
        _ => t.ToString()
    };

    // ── حالة العهدة ──────────────────────────────────────────
    public static string Name(CustodyStatus s) => s switch
    {
        CustodyStatus.Pending => "بانتظار الموافقة",
        CustodyStatus.Accepted => "مقبولة",
        CustodyStatus.Rejected => "مرفوضة",
        CustodyStatus.Returned => "تم الإرجاع",
        _ => s.ToString()
    };

    public static string Badge(CustodyStatus s) => s switch
    {
        CustodyStatus.Pending => "bg-warning text-dark",
        CustodyStatus.Accepted => "bg-success",
        CustodyStatus.Rejected => "bg-danger",
        CustodyStatus.Returned => "bg-secondary",
        _ => "bg-light text-dark"
    };

    public static string Name(CustodyAction a) => a switch
    {
        CustodyAction.Assign => "تسليم",
        CustodyAction.Transfer => "نقل",
        CustodyAction.Return => "إرجاع",
        _ => a.ToString()
    };

    // ── نوع الموقع ──────────────────────────────────────────
    public static string Name(LocationType t) => t switch
    {
        LocationType.Office => "مكتب",
        LocationType.Factory => "مصنع",
        LocationType.Warehouse => "مخزن",
        LocationType.Building => "مبنى",
        LocationType.Apartment => "شقة",
        LocationType.Branch => "فرع",
        LocationType.Other => "أخرى",
        _ => t.ToString()
    };

    // ── الإهلاك ─────────────────────────────────────────────
    public static string Name(DepreciationMethod m) => m switch
    {
        DepreciationMethod.StraightLine => "القسط الثابت",
        DepreciationMethod.DecliningBalance => "القسط المتناقص",
        _ => m.ToString()
    };

    // ── الجرد ───────────────────────────────────────────────
    public static string Name(AuditStatus s) => s switch
    {
        AuditStatus.Draft => "مسودة",
        AuditStatus.InProgress => "جاري التنفيذ",
        AuditStatus.Completed => "مكتمل",
        AuditStatus.Cancelled => "ملغي",
        _ => s.ToString()
    };

    public static string Badge(AuditStatus s) => s switch
    {
        AuditStatus.Draft => "bg-secondary",
        AuditStatus.InProgress => "bg-warning text-dark",
        AuditStatus.Completed => "bg-success",
        AuditStatus.Cancelled => "bg-light text-dark",
        _ => "bg-light text-dark"
    };

    public static string Name(AuditItemResult r) => r switch
    {
        AuditItemResult.Pending => "لم يُجرد",
        AuditItemResult.Found => "موجود",
        AuditItemResult.Misplaced => "بمكان مختلف",
        AuditItemResult.Missing => "مفقود",
        AuditItemResult.Damaged => "تالف",
        _ => r.ToString()
    };

    public static string Badge(AuditItemResult r) => r switch
    {
        AuditItemResult.Pending => "bg-secondary",
        AuditItemResult.Found => "bg-success",
        AuditItemResult.Misplaced => "bg-warning text-dark",
        AuditItemResult.Missing => "bg-danger",
        AuditItemResult.Damaged => "bg-danger",
        _ => "bg-light text-dark"
    };

    // ── جداول الصيانة ────────────────────────────────────────
    public static string Name(ScheduleStatus s) => s switch
    {
        ScheduleStatus.Active => "مُفعَّل",
        ScheduleStatus.Paused => "موقوف",
        ScheduleStatus.Ended => "منتهي",
        _ => s.ToString()
    };

    public static string Badge(ScheduleStatus s) => s switch
    {
        ScheduleStatus.Active => "bg-success",
        ScheduleStatus.Paused => "bg-warning text-dark",
        ScheduleStatus.Ended => "bg-secondary",
        _ => "bg-light text-dark"
    };

    public static string Name(ScheduleFrequency f) => f switch
    {
        ScheduleFrequency.Daily => "يومي",
        ScheduleFrequency.Weekly => "أسبوعي",
        ScheduleFrequency.Monthly => "شهري",
        ScheduleFrequency.Quarterly => "ربع سنوي",
        ScheduleFrequency.SemiAnnual => "نصف سنوي",
        ScheduleFrequency.Annual => "سنوي",
        _ => f.ToString()
    };

    // ── الإشعارات ────────────────────────────────────────────
    public static string Name(NotificationType t) => t switch
    {
        NotificationType.CustodyAssigned => "تسليم عهدة",
        NotificationType.CustodyAccepted => "قبول عهدة",
        NotificationType.CustodyRejected => "رفض عهدة",
        NotificationType.CustodyReturned => "إرجاع عهدة",
        NotificationType.TicketCreated => "تذكرة جديدة",
        NotificationType.TicketAssigned => "تكليف بتذكرة",
        NotificationType.TicketStatusChanged => "تغيير حالة تذكرة",
        NotificationType.TicketResolved => "حل تذكرة",
        NotificationType.SlaBreached => "تجاوز SLA",
        NotificationType.WarrantyExpiring => "انتهاء ضمان",
        NotificationType.MaintenanceDue => "صيانة مستحقة",
        NotificationType.General => "عام",
        _ => t.ToString()
    };

    // ── الشارات الجاهزة (HTML) ───────────────────────────────
    // ملاحظة مهمة: Badge() تُرجع "اسم الكلاس" فقط (مثل bg-success)،
    // أما Chip() فتُرجع الشارة كاملة <span class="badge ...">الاسم</span>.
    // استخدم Chip داخل @Html.Raw(...) حتى لا يظهر اسم الكلاس كنص للمستخدم.
    private static string Wrap(string cssClass, string text)
        => $"<span class=\"badge {cssClass}\">{System.Net.WebUtility.HtmlEncode(text)}</span>";

    public static string Chip(AssetStatus s) => Wrap(Badge(s), Name(s));
    public static string Chip(TicketStatus s) => Wrap(Badge(s), Name(s));
    public static string Chip(TicketPriority p) => Wrap(Badge(p), Name(p));
    public static string Chip(CustodyStatus s) => Wrap(Badge(s), Name(s));
    public static string Chip(AuditStatus s) => Wrap(Badge(s), Name(s));
    public static string Chip(AuditItemResult r) => Wrap(Badge(r), Name(r));
    public static string Chip(ScheduleStatus s) => Wrap(Badge(s), Name(s));

    /// <summary>ألوان مخصصة للرسوم البيانية — ثابتة لكل حالة</summary>
    public static string ChartColor(int index)
    {
        var palette = new[]
        {
            "#0f766e", "#0891b2", "#7c3aed", "#db2777",
            "#ea580c", "#65a30d", "#0284c7", "#9333ea",
            "#dc2626", "#ca8a04", "#059669", "#4f46e5"
        };
        return palette[index % palette.Length];
    }
}
