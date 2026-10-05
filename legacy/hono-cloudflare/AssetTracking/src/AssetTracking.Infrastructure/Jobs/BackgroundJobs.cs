using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetTracking.Infrastructure.Jobs;

/// <summary>
/// المهام الخلفية المجدولة (Hangfire).
/// كلها تعمل بلا مستخدم مسجَّل ⇒ تُعطِّل فلتر الشركة صراحةً عبر IgnoreCompanyFilter.
/// </summary>
public class MaintenanceJobs
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    private readonly IDepreciationService _depreciation;
    private readonly ISequenceService _seq;
    private readonly ILogger<MaintenanceJobs> _log;

    public MaintenanceJobs(AppDbContext db, INotificationService notify,
        IDepreciationService depreciation, ISequenceService seq, ILogger<MaintenanceJobs> log)
    {
        _db = db; _notify = notify; _depreciation = depreciation; _seq = seq; _log = log;
        _db.IgnoreCompanyFilter = true;
    }

    /// <summary>كل 15 دقيقة — فحص خرق SLA وتصعيد التذاكر</summary>
    public async Task SlaEscalationJob()
    {
        var now = DateTime.UtcNow;

        var breached = await _db.MaintenanceTickets
            .Include(t => t.Asset)
            .Where(t => !t.IsSlaBreached
                        && t.ResolutionDueAt != null
                        && t.ResolutionDueAt < now
                        && t.Status != TicketStatus.Resolved
                        && t.Status != TicketStatus.Closed
                        && t.Status != TicketStatus.Cancelled)
            .ToListAsync();

        foreach (var t in breached)
        {
            t.IsSlaBreached = true;
            t.SlaBreachedAt = now;
            t.IsEscalated = true;

            _db.TicketLogs.Add(new TicketLog
            {
                CompanyId = t.CompanyId,
                TicketId = t.Id,
                Action = "SlaBreach",
                ToValue = "تجاوز موعد الحل المستهدف",
                OccurredAt = now,
                Notes = $"الموعد المستهدف: {t.ResolutionDueAt:yyyy-MM-dd HH:mm}"
            });

            // تنبيه الفني المكلَّف ومديري الشركة
            if (t.AssignedTechnicianId != null)
            {
                await _notify.NotifyAsync(t.AssignedTechnicianId, NotificationType.SlaBreached,
                    "⚠️ تجاوز SLA",
                    $"التذكرة {t.TicketNumber} تجاوزت موعد الحل المستهدف.",
                    $"/Tickets/Details/{t.Id}", t.CompanyId);
            }

            await _notify.NotifyRoleAsync(Roles.CompanyManager, t.CompanyId,
                NotificationType.SlaBreached, "⚠️ تصعيد تذكرة",
                $"التذكرة {t.TicketNumber} تجاوزت SLA وتحتاج متابعة.",
                $"/Tickets/Details/{t.Id}");
        }

        if (breached.Count > 0)
        {
            await _db.SaveChangesAsync();
            _log.LogWarning("مهمة SLA: تم تصعيد {Count} تذكرة", breached.Count);
        }
    }

    /// <summary>شهرياً (أول يوم) — احتساب قسط الإهلاك وتحديث القيمة الدفترية</summary>
    public async Task DepreciationJob()
    {
        var now = DateTime.UtcNow;
        var count = await _depreciation.RunMonthlyForAllAsync(now.Year, now.Month);
        _log.LogInformation("مهمة الإهلاك: {Count} قيد جديد", count);
    }

    /// <summary>يومياً 06:00 — توليد تذاكر الصيانة الوقائية المستحقة</summary>
    public async Task PreventiveMaintenanceJob()
    {
        var today = DateTime.UtcNow.Date;

        var due = await _db.MaintenanceSchedules
            .Include(s => s.Asset)
            .Where(s => s.Status == ScheduleStatus.Active
                        && s.NextDueDate.Date <= today.AddDays(s.LeadTimeDays))
            .ToListAsync();

        var created = 0;

        foreach (var s in due)
        {
            if (s.Asset == null) continue;
            if (s.Asset.Status is AssetStatus.Disposed or AssetStatus.Lost) continue;

            // لا نولّد تذكرة إن كانت هناك واحدة مفتوحة لنفس الجدول
            var hasOpen = await _db.MaintenanceTickets.AnyAsync(t =>
                t.ScheduleId == s.Id &&
                t.Status != TicketStatus.Closed &&
                t.Status != TicketStatus.Cancelled &&
                t.Status != TicketStatus.Resolved);
            if (hasOpen) continue;

            var number = await _seq.NextTicketNumberAsync(s.CompanyId);

            var ticket = new MaintenanceTicket
            {
                CompanyId = s.CompanyId,
                TicketNumber = number,
                AssetId = s.AssetId,
                Title = $"صيانة وقائية: {s.Title}",
                Description = s.Description ?? s.Checklist,
                Type = TicketType.Preventive,
                Priority = s.DefaultPriority,
                Status = s.DefaultTechnicianId != null ? TicketStatus.Assigned : TicketStatus.Open,
                AssignedTechnicianId = s.DefaultTechnicianId,
                ReportedAt = DateTime.UtcNow,
                ResolutionDueAt = s.NextDueDate,
                ScheduleId = s.Id
            };

            _db.MaintenanceTickets.Add(ticket);

            s.LastGeneratedAt = DateTime.UtcNow;
            s.NextDueDate = AdvanceDate(s.NextDueDate, s.Frequency);
            if (s.EndDate != null && s.NextDueDate > s.EndDate) s.Status = ScheduleStatus.Ended;

            created++;

            if (s.DefaultTechnicianId != null)
            {
                await _notify.NotifyAsync(s.DefaultTechnicianId, NotificationType.MaintenanceDue,
                    "صيانة وقائية مستحقة",
                    $"تم توليد تذكرة صيانة وقائية للأصل «{s.Asset.NameAr}».",
                    null, s.CompanyId);
            }
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync();
            _log.LogInformation("مهمة الصيانة الوقائية: تم توليد {Count} تذكرة", created);
        }
    }

    /// <summary>يومياً 07:00 — تنبيه انتهاء الضمان قبل 30 و7 أيام</summary>
    public async Task WarrantyExpiryAlertJob()
    {
        var today = DateTime.UtcNow.Date;
        var targets = new[] { today.AddDays(30), today.AddDays(7) };

        var assets = await _db.Assets
            .Where(a => a.WarrantyEndDate != null
                        && targets.Contains(a.WarrantyEndDate.Value.Date)
                        && a.Status != AssetStatus.Disposed
                        && a.Status != AssetStatus.Lost)
            .ToListAsync();

        foreach (var a in assets)
        {
            var days = (a.WarrantyEndDate!.Value.Date - today).Days;

            await _notify.NotifyRoleAsync(Roles.CompanyManager, a.CompanyId,
                NotificationType.WarrantyExpiring,
                "🛡️ ضمان على وشك الانتهاء",
                $"ضمان الأصل «{a.NameAr}» ({a.AssetTag}) ينتهي بعد {days} يوم.",
                $"/Assets/Details/{a.Id}");
        }

        if (assets.Count > 0)
            _log.LogInformation("مهمة الضمان: تم تنبيه {Count} أصل", assets.Count);
    }

    /// <summary>يومياً 08:00 — تذكير الفنيين بالتذاكر المفتوحة</summary>
    public async Task TicketReminderJob()
    {
        var open = await _db.MaintenanceTickets
            .Where(t => t.AssignedTechnicianId != null
                        && (t.Status == TicketStatus.Assigned
                            || t.Status == TicketStatus.InProgress
                            || t.Status == TicketStatus.WaitingParts))
            .ToListAsync();

        foreach (var g in open.GroupBy(t => t.AssignedTechnicianId!))
        {
            await _notify.NotifyAsync(g.Key, NotificationType.General,
                "تذكير بالتذاكر المفتوحة",
                $"لديك {g.Count()} تذكرة مفتوحة تحتاج متابعة.",
                "/Tickets?mine=true", g.First().CompanyId);
        }
    }

    /// <summary>أسبوعياً — أرشفة الإشعارات المقروءة الأقدم من 90 يوماً</summary>
    public async Task DataCleanupJob()
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);

        var old = await _db.Notifications
            .Where(n => n.IsRead && n.CreatedAt < cutoff)
            .ToListAsync();

        if (old.Count == 0) return;

        _db.Notifications.RemoveRange(old);
        await _db.SaveChangesAsync();

        _log.LogInformation("مهمة التنظيف: تم أرشفة {Count} إشعار", old.Count);
    }

    private static DateTime AdvanceDate(DateTime from, ScheduleFrequency f) => f switch
    {
        ScheduleFrequency.Daily => from.AddDays(1),
        ScheduleFrequency.Weekly => from.AddDays(7),
        ScheduleFrequency.Monthly => from.AddMonths(1),
        ScheduleFrequency.Quarterly => from.AddMonths(3),
        ScheduleFrequency.SemiAnnual => from.AddMonths(6),
        ScheduleFrequency.Annual => from.AddYears(1),
        _ => from.AddMonths(1)
    };
}
