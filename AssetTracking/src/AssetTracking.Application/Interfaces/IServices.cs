using AssetTracking.Application.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Application.Interfaces;

/// <summary>توليد الأرقام التسلسلية AST-YYYY-NNNNN / TKT-YYYY-NNNNN</summary>
public interface ISequenceService
{
    Task<string> NextAssetTagAsync(int companyId, CancellationToken ct = default);
    Task<string> NextTicketNumberAsync(int companyId, CancellationToken ct = default);
    Task<string> NextAuditCodeAsync(int companyId, CancellationToken ct = default);
}

/// <summary>خدمة الإشعارات المركزية — تقرر القناة والمستلمين</summary>
public interface INotificationService
{
    Task NotifyAsync(string userId, NotificationType type, string title, string body,
        string? actionUrl = null, int? companyId = null, NotificationChannel channel = NotificationChannel.InApp,
        CancellationToken ct = default);

    Task NotifyRoleAsync(string role, int? companyId, NotificationType type, string title, string body,
        string? actionUrl = null, CancellationToken ct = default);

    Task<int> UnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkReadAsync(int notificationId, string userId, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
}

/// <summary>البث الفوري عبر SignalR</summary>
public interface IRealtimeNotifier
{
    Task PushToUserAsync(string userId, object payload);
}

/// <summary>إرسال البريد عبر MailKit (يُنفَّذ من طابور Hangfire)</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>توليد صور QR للأصول (QRCoder)</summary>
public interface IQrCodeService
{
    /// <summary>يولّد PNG ويعيد المسار النسبي المحفوظ</summary>
    Task<string> GenerateForAssetAsync(string assetTag, string baseUrl, CancellationToken ct = default);
    byte[] GeneratePng(string content, int pixelsPerModule = 10);
}

/// <summary>سجل التدقيق الشامل</summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? entityId,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default);

    /// <summary>
    /// نفس سطر التدقيق وقيوده، لكن بقيم نصية جاهزة بدل كائنات تُسلسل إلى JSON.
    /// موجودة لمسارات الإدارة التي كانت تكتب نصاً خاماً؛ الهدف كاتب واحد للصف لا كاتبان.
    /// </summary>
    Task LogTextAsync(string action, string entityName, string? entityId,
        string? oldText, string? newText, CancellationToken ct = default);
}

/// <summary>محرّك SLA — يحسب المواعيد ويكتشف الخرق</summary>
public interface ISlaService
{
    Task<(DateTime? responseDue, DateTime? resolutionDue)> ComputeDueDatesAsync(
        int companyId, TicketPriority priority, DateTime reportedAt, CancellationToken ct = default);
}

/// <summary>خدمة الإهلاك</summary>
public interface IDepreciationService
{
    /// <summary>يحسب قسط الشهر لأصل واحد ويحدّث القيمة الدفترية (بحد أدنى القيمة التخريدية)</summary>
    Task<decimal> AccrueMonthlyAsync(int assetId, int year, int month, CancellationToken ct = default);

    /// <summary>يشغّل الإهلاك لكل الأصول المؤهلة — تستدعيها مهمة Hangfire الشهرية</summary>
    Task<int> RunMonthlyForAllAsync(int year, int month, CancellationToken ct = default);
}

/// <summary>خدمة العهد</summary>
public interface ICustodyService
{
    Task<Result<CustodyLog>> AssignAsync(int assetId, string newUserId, string? reason, CancellationToken ct = default);
    Task<Result> RespondAsync(int custodyLogId, bool accept, string? note, CancellationToken ct = default);

    /// <summary>
    /// إرجاع عهدة. <paramref name="toWarehouseId"/> = المخزن الذي يُرجع إليه الأصل —
    /// null يُبقي الأصل في مكانه الحالي. المخزن لا موقع الشركة: الوجهة سؤال تحفظ.
    /// </summary>
    Task<Result> ReturnAsync(int assetId, int? toWarehouseId, string? reason, string? condition,
        CancellationToken ct = default);
}

/// <summary>مدخلات حفظ موقع/فرع شركة — نسخة طبقة التطبيق حتى لا تعرف الخدمة شيئًا عن Web.</summary>
public sealed record CompanySiteInput
{
    public int Id { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Code { get; init; }
    public SiteKind Kind { get; init; } = SiteKind.Office;
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Governorate { get; init; }
    public string? ContactPerson { get; init; }
    public string? ContactPhone { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;

    /// <summary>اختيار الشركة — يُستخدم فقط عندما لا تكون للمستخدم شركة (مدير النظام).</summary>
    public int? CompanyId { get; init; }
}

/// <summary>مدخلات حفظ مخزن. لا «نوع» هنا: النوع كان ثغرة اللبس — الكيان نفسه هو التعريف.</summary>
public sealed record WarehouseInput
{
    public int Id { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Code { get; init; }
    public string? Notes { get; init; }
    public string? Address { get; init; }

    /// <summary>موقع الشركة الذي يحوي المخزن — اختياري، وnull وضع تشغيلي كامل.</summary>
    public int? CompanySiteId { get; init; }

    public bool IsActive { get; init; } = true;

    public int? CompanyId { get; init; }
}

/// <summary>
/// الكاتب الوحيد لمواقع الشركة. منفصل عن IWarehouseService عن قصد: فعل حفظ واحد
/// بكلمة «نوع» هو ما كان يجعل إضافة مخزن تنتهي موقعًا، فلا رجوع عن الفصل بتوحيد الكاتب.
/// </summary>
public interface ICompanySiteService
{
    Task<Result<CompanySite>> CreateAsync(CompanySiteInput input, CancellationToken ct = default);
    Task<Result<CompanySite>> UpdateAsync(CompanySiteInput input, CancellationToken ct = default);
}

/// <summary>
/// الكاتب الوحيد للمخازن — يحرس الكود داخل الشركة وملكية الموقع المرتبط.
/// </summary>
public interface IWarehouseService
{
    Task<Result<Warehouse>> CreateAsync(WarehouseInput input, CancellationToken ct = default);
    Task<Result<Warehouse>> UpdateAsync(WarehouseInput input, CancellationToken ct = default);
}

/// <summary>مدخلات حفظ خط إنتاج — بلا «نوع» ولا مرجع موضع: الخط كيان مستقل.</summary>
public sealed record ProductionLineInput
{
    public int Id { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Code { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;

    public int? CompanyId { get; init; }
}

/// <summary>
/// الكاتب الوحيد لخطوط الإنتاج. منفصل عن الكاتبين السابقين: خط الإنتاج ليس فرعًا
/// ولا مخزنًا، فأي توحيد للكاتبين الثلاثة يعيد صف «Locations بنوعه» الذي جئنا نزاله.
/// </summary>
public interface IProductionLineService
{
    Task<Result<ProductionLine>> CreateAsync(ProductionLineInput input, CancellationToken ct = default);
    Task<Result<ProductionLine>> UpdateAsync(ProductionLineInput input, CancellationToken ct = default);
}

/// <summary>إعدادات النظام (SMTP وغيرها) من قاعدة البيانات</summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, int? companyId = null, CancellationToken ct = default);
    Task SetAsync(string key, string? value, int? companyId = null, CancellationToken ct = default);
    Task<Dictionary<string, string?>> GetCategoryAsync(string category, int? companyId = null, CancellationToken ct = default);
}
