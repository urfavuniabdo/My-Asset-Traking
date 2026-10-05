using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetTracking.Infrastructure.Services;

/// <summary>
/// خدمة الإشعارات المركزية — كل أحداث النظام تمر من هنا فتقرر القناة والمستلمين.
/// القناتان: In-App فوري عبر SignalR، وEmail من طابور خلفي.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IRealtimeNotifier _realtime;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(AppDbContext db, IRealtimeNotifier realtime,
        UserManager<ApplicationUser> users, ILogger<NotificationService> log)
    {
        _db = db; _realtime = realtime; _users = users; _log = log;
    }

    public async Task NotifyAsync(string userId, NotificationType type, string title, string body,
        string? actionUrl = null, int? companyId = null,
        NotificationChannel channel = NotificationChannel.InApp, CancellationToken ct = default)
    {
        var n = new Notification
        {
            UserId = userId,
            CompanyId = companyId,
            Type = type,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            Icon = IconFor(type)
        };

        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);

        // بث فوري — الجرس يتحدث بدون Refresh
        try
        {
            await _realtime.PushToUserAsync(userId, new
            {
                id = n.Id, title = n.Title, body = n.Body,
                actionUrl = n.ActionUrl, icon = n.Icon,
                createdAt = n.CreatedAt
            });
        }
        catch (Exception ex)
        {
            // فشل البث لا يجب أن يُفشل العملية الأساسية
            _log.LogWarning(ex, "فشل بث الإشعار الفوري للمستخدم {UserId}", userId);
        }
    }

    public async Task NotifyRoleAsync(string role, int? companyId, NotificationType type,
        string title, string body, string? actionUrl = null, CancellationToken ct = default)
    {
        var inRole = await _users.GetUsersInRoleAsync(role);
        var targets = inRole.Where(u => u.IsActive && !u.IsDeleted &&
                                        (companyId == null || u.CompanyId == companyId || u.CompanyId == null));

        foreach (var u in targets)
            await NotifyAsync(u.Id, type, title, body, actionUrl, companyId, ct: ct);
    }

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default)
        => _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(int notificationId, string userId, CancellationToken ct = default)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n == null || n.IsRead) return;

        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var list = await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead).ToListAsync(ct);

        foreach (var n in list) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }

    private static string IconFor(NotificationType t) => t switch
    {
        NotificationType.CustodyAssigned => "bi-box-arrow-in-down",
        NotificationType.CustodyAccepted => "bi-check-circle",
        NotificationType.CustodyRejected => "bi-x-circle",
        NotificationType.CustodyReturned => "bi-box-arrow-up",
        NotificationType.TicketCreated => "bi-ticket-perforated",
        NotificationType.TicketAssigned => "bi-person-check",
        NotificationType.TicketStatusChanged => "bi-arrow-repeat",
        NotificationType.TicketResolved => "bi-check2-all",
        NotificationType.SlaBreached => "bi-exclamation-triangle",
        NotificationType.WarrantyExpiring => "bi-shield-exclamation",
        NotificationType.MaintenanceDue => "bi-tools",
        _ => "bi-bell"
    };
}
