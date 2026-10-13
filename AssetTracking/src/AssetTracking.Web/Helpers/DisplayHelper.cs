using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using System.Globalization;
using System.Security.Claims;

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

    // ── صيغة القشرة الحديثة (Tailwind): فواصل بدل الشرطة ────────
    // كانت أربع صفحات تصنع CultureInfo("ar-EG") لكل صفحة وتكتب النص نفسه.

    /// <summary>تاريخ بصيغة القشرة الحديثة: 2026-10-11</summary>
    public static string DateDash(DateTime d)
        => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>تاريخ بصيغة القشرة الحديثة، و«—» عند الغياب</summary>
    public static string DateDash(DateTime? d)
        => d.HasValue ? DateDash(d.Value) : "—";

    /// <summary>وقت 12 ساعة بـ ص/م بدل AM/PM</summary>
    public static string TimeDash(DateTime d)
        => d.ToString("hh:mm tt", CultureInfo.InvariantCulture).Replace("AM", "ص").Replace("PM", "م");

    /// <summary>تاريخ ووقت بصيغة القشرة الحديثة: 2026-10-11 - 12:24 م</summary>
    public static string DateTimeDash(DateTime d)
        => $"{DateDash(d)} - {TimeDash(d)}";

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

    // ── الفئة الفنية للعطل ─────────────────────────
    public static string Name(IssueCategory c) => c switch
    {
        IssueCategory.Mechanical => "ميكانيكي / تآكل واهتراء",
        IssueCategory.Electrical => "كهربائي / تأسيسات",
        IssueCategory.Sensor => "حساسات / معايرة بصرية",
        IssueCategory.Software => "برمجيات / اتصال MES",
        IssueCategory.Pneumatic => "هوائي / هيدروليكي",
        IssueCategory.Calibration => "ضبط ومعايرة",
        IssueCategory.Consumable => "مستهلكات",
        IssueCategory.Other => "أخرى",
        _ => c.ToString()
    };

    public static string Icon(IssueCategory c) => c switch
    {
        IssueCategory.Mechanical => "bi-gear-wide-connected",
        IssueCategory.Electrical => "bi-lightning-charge",
        IssueCategory.Sensor => "bi-eye",
        IssueCategory.Software => "bi-hdd-network",
        IssueCategory.Pneumatic => "bi-wind",
        IssueCategory.Calibration => "bi-sliders",
        IssueCategory.Consumable => "bi-box-seam",
        _ => "bi-tools"
    };

    // ── من قام بالحل ──────────────────────────────
    public static string Name(SolvedBy s) => s switch
    {
        SolvedBy.EngineerDirectly => "المهندس مباشرة",
        SolvedBy.TechnicianSupervised => "فني تحت الإشراف",
        SolvedBy.TechnicianAlone => "فني مستقل",
        SolvedBy.ExternalVendor => "جهة خارجية",
        _ => s.ToString()
    };

    public static string Badge(SolvedBy s) => s switch
    {
        SolvedBy.EngineerDirectly => "bg-primary",
        SolvedBy.TechnicianSupervised => "bg-info text-dark",
        SolvedBy.TechnicianAlone => "bg-success",
        SolvedBy.ExternalVendor => "bg-warning text-dark",
        _ => "bg-light text-dark"
    };

    // ── مرحلة الصورة ────────────────────────────
    public static string Name(PhotoStage p) => p switch
    {
        PhotoStage.Before => "قبل الإصلاح",
        PhotoStage.After => "بعد الإصلاح",
        _ => "مستند"
    };

    /// <summary>زمن التوقف بصيغة مقروءة: 45 د / 1:15 س</summary>
    public static string Downtime(int? minutes)
    {
        if (!minutes.HasValue) return "—";
        var m = minutes.Value;
        if (m < 60) return $"{m} دقيقة";
        var h = m / 60; var r = m % 60;
        return r == 0 ? $"{h} ساعة" : $"{h} ساعة و{r} دقيقة";
    }

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

    // ── صفة المنشأة ─────────────────────────────────────────
    // SiteKind لا LocationType: «مخزن» و«خط إنتاج» صارا كيانين مستقلين، فلا معنى
    // لوصفهما كنوعين من موقع الشركة.
    public static string Name(SiteKind k) => k switch
    {
        SiteKind.Office => "مكتب",
        SiteKind.Factory => "مصنع",
        SiteKind.Building => "مبنى / موقع مشروع",
        SiteKind.Apartment => "شقة / سكن",
        SiteKind.Branch => "فرع",
        SiteKind.Other => "أخرى",
        _ => k.ToString()
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

    /// <summary>
    /// اسم الدور بالعربية من الـClaims. كان هذا التعبير الثلاثي منسوخاً في 7 قوالب
    /// (الشريط العلوي + 6 صفحات حديثة)، فتظهر تسمية مختلفة للدور في قالب عند تعديل غيره.
    /// </summary>
    public static string RoleLabel(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? "مدير النظام"
        : user.IsInRole(Roles.CompanyManager) ? "مدير شركة"
        : user.IsInRole(Roles.Technician) ? "فني دعم" : "موظف";

    /// <summary>
    /// أيقونة التصنيف. نسخة لوحة التحكم كانت أقصر بثلاثة فروع (حساس/تحكم/معدات) من
    /// نسخة القوائم، فيظهر التصنيف بأيقونتين مختلفتين حسب الصفحة.
    /// </summary>
    public static string CategoryIcon(string? categoryName)
    {
        var c = categoryName ?? string.Empty;
        return c.Contains("لابتوب") || c.Contains("حاسوب") ? "fa-laptop"
            : c.Contains("طابع") ? "fa-print"
            : c.Contains("شاشة") ? "fa-desktop"
            : c.Contains("عرض") || c.Contains("بروجكتور") || c.Contains("بروجكتر") ? "fa-tv"
            : c.Contains("شبك") || c.Contains("راوتر") || c.Contains("مودم") ? "fa-wifi"
            : c.Contains("هاتف") || c.Contains("موبايل") ? "fa-mobile-screen"
            : c.Contains("كامير") ? "fa-camera"
            : c.Contains("حساس") ? "fa-tower-broadcast"
            : c.Contains("تحكم") ? "fa-microchip"
            : c.Contains("معدات") || c.Contains("محرّك") || c.Contains("موتور") || c.Contains("مكين") ? "fa-gears"
            : "fa-box";
    }

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
