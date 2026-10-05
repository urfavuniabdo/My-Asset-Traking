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
    Task<Result> ReturnAsync(int assetId, string? reason, string? condition, CancellationToken ct = default);
}

/// <summary>إعدادات النظام (SMTP وغيرها) من قاعدة البيانات</summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, int? companyId = null, CancellationToken ct = default);
    Task SetAsync(string key, string? value, int? companyId = null, CancellationToken ct = default);
    Task<Dictionary<string, string?>> GetCategoryAsync(string category, int? companyId = null, CancellationToken ct = default);
}
