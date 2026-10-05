using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// الصيانة الوقائية — جداول دورية تُولِّد تذاكر تلقائياً عبر مهمة Hangfire
/// (PreventiveMaintenanceJob) قبل موعد الاستحقاق بعدد أيام LeadTimeDays.
/// يوفّر هذا الـController أيضاً توليداً يدوياً للتذكرة عند الحاجة.
/// </summary>
[Authorize(Policy = Policies.TechnicianOrAbove)]
public class MaintenanceSchedulesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ISequenceService _seq;
    private readonly ISlaService _sla;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;

    public MaintenanceSchedulesController(AppDbContext db, ISequenceService seq, ISlaService sla,
        INotificationService notify, IAuditService audit)
    {
        _db = db;
        _seq = seq;
        _sla = sla;
        _notify = notify;
        _audit = audit;
    }

    // ────────────────────────── الفهرس ──────────────────────────
    public async Task<IActionResult> Index(string? q, ScheduleStatus? status,
        ScheduleFrequency? frequency, string? technicianId, string? scope, int page = 1)
    {
        var me = Me.UserId;
        var today = DateTime.UtcNow.Date;

        var vm = new ScheduleIndexViewModel
        {
            Q = q, Status = status, Frequency = frequency,
            TechnicianId = technicianId, Scope = scope ?? "all",
            Page = page < 1 ? 1 : page,
            CanEdit = IsManager
        };

        var query = _db.MaintenanceSchedules.AsNoTracking().AsQueryable();

        vm.CountActive = await query.CountAsync(s => s.Status == ScheduleStatus.Active);
        vm.CountPaused = await query.CountAsync(s => s.Status == ScheduleStatus.Paused);
        vm.CountOverdue = await query.CountAsync(s => s.Status == ScheduleStatus.Active
                                                     && s.NextDueDate < today);
        // "قريبة الاستحقاق" = خلال أسبوع ولم تتأخر بعد
        var weekAhead = today.AddDays(7);
        vm.CountDueSoon = await query.CountAsync(s => s.Status == ScheduleStatus.Active
                                                     && s.NextDueDate >= today
                                                     && s.NextDueDate <= weekAhead);

        query = vm.Scope switch
        {
            "due" => query.Where(s => s.Status == ScheduleStatus.Active
                                      && s.NextDueDate >= today && s.NextDueDate <= weekAhead),
            "overdue" => query.Where(s => s.Status == ScheduleStatus.Active && s.NextDueDate < today),
            "mine" => query.Where(s => s.DefaultTechnicianId == me),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Title.Contains(term)
                                     || (s.Description != null && s.Description.Contains(term))
                                     || s.Asset!.AssetTag.Contains(term)
                                     || s.Asset!.NameAr.Contains(term));
        }

        if (status.HasValue) query = query.Where(s => s.Status == status);
        if (frequency.HasValue) query = query.Where(s => s.Frequency == frequency);
        if (!string.IsNullOrWhiteSpace(technicianId))
            query = query.Where(s => s.DefaultTechnicianId == technicianId);

        vm.TotalCount = await query.CountAsync();

        vm.Items = await query
            .OrderBy(s => s.Status).ThenBy(s => s.NextDueDate)
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize)
            .Select(s => new ScheduleListRow
            {
                Id = s.Id, Title = s.Title,
                AssetId = s.AssetId,
                AssetTag = s.Asset!.AssetTag,
                AssetName = s.Asset!.NameAr,
                CategoryName = s.Asset!.Category != null ? s.Asset!.Category!.NameAr : null,
                CompanyName = s.Asset!.Company != null ? s.Asset!.Company!.NameAr : null,
                Frequency = s.Frequency, Status = s.Status,
                StartDate = s.StartDate, EndDate = s.EndDate,
                NextDueDate = s.NextDueDate, LastGeneratedAt = s.LastGeneratedAt,
                LeadTimeDays = s.LeadTimeDays,
                TechnicianId = s.DefaultTechnicianId,
                DefaultPriority = s.DefaultPriority,
                EstimatedCost = s.EstimatedCost,
                GeneratedTicketsCount = s.GeneratedTickets.Count()
            }).ToListAsync();

        await ResolveTechNamesAsync(vm.Items);
        vm.Technicians = await TechniciansAsync();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;
        return View(vm);
    }

    private async Task ResolveTechNamesAsync(IEnumerable<ScheduleListRow> rows)
    {
        var list = rows.ToList();
        var ids = list.Select(r => r.TechnicianId)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in list)
            if (r.TechnicianId != null && names.TryGetValue(r.TechnicianId, out var n))
                r.TechnicianName = n;
    }

    private async Task<List<UserLookupItem>> TechniciansAsync()
    {
        var roleIds = await _db.Roles.AsNoTracking()
            .Where(r => r.Name == Roles.Technician || r.Name == Roles.CompanyManager)
            .Select(r => r.Id).ToListAsync();

        var userIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => roleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId).Distinct().ToListAsync();

        var q = _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive);
        if (!IsAdmin)
        {
            var cid = Me.CompanyId;
            q = q.Where(u => u.CompanyId == cid);
        }

        return await q.OrderBy(u => u.FullName)
            .Select(u => new UserLookupItem { Id = u.Id, Name = u.FullName })
            .ToListAsync();
    }

    // ────────────────────────── التفاصيل ──────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var s = await _db.MaintenanceSchedules.AsNoTracking()
            .Include(x => x.Asset).ThenInclude(a => a!.Category)
            .Include(x => x.Asset).ThenInclude(a => a!.Company)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s == null) return NotFoundOrForbidden();

        var vm = new ScheduleDetailsViewModel
        {
            Id = s.Id, Title = s.Title, Description = s.Description, Checklist = s.Checklist,
            AssetId = s.AssetId, AssetTag = s.Asset?.AssetTag, AssetName = s.Asset?.NameAr,
            CategoryName = s.Asset?.Category?.NameAr, CompanyName = s.Asset?.Company?.NameAr,
            Frequency = s.Frequency, Status = s.Status,
            StartDate = s.StartDate, EndDate = s.EndDate,
            NextDueDate = s.NextDueDate, LastGeneratedAt = s.LastGeneratedAt,
            LeadTimeDays = s.LeadTimeDays,
            TechnicianId = s.DefaultTechnicianId,
            DefaultPriority = s.DefaultPriority, EstimatedCost = s.EstimatedCost,
            CreatedAt = s.CreatedAt,
            CanEdit = IsManager
        };

        if (s.DefaultTechnicianId != null)
            vm.TechnicianName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == s.DefaultTechnicianId)
                .Select(u => u.FullName).FirstOrDefaultAsync();

        var rawTickets = await _db.MaintenanceTickets.AsNoTracking()
            .Where(t => t.ScheduleId == id)
            .OrderByDescending(t => t.ReportedAt).Take(30)
            .Select(t => new TicketRow
            {
                Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                AssetTag = t.Asset!.AssetTag, AssetName = t.Asset!.NameAr,
                Type = t.Type, Priority = t.Priority, Status = t.Status,
                ReportedAt = t.ReportedAt, ResolutionDueAt = t.ResolutionDueAt,
                ResolvedAt = t.ResolvedAt, IsSlaBreached = t.IsSlaBreached,
                TechnicianName = t.AssignedTechnicianId
            }).ToListAsync();

        // حلّ أسماء الفنيين في استعلام واحد
        var techIds = rawTickets.Select(t => t.TechnicianName)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (techIds.Count > 0)
        {
            var names = await _db.Users.AsNoTracking().Where(u => techIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);
            foreach (var t in rawTickets)
                t.TechnicianName = t.TechnicianName != null && names.TryGetValue(t.TechnicianName, out var n)
                    ? n : null;
        }

        vm.Tickets = rawTickets;
        vm.GeneratedTicketsCount = rawTickets.Count;
        return View(vm);
    }

    // ────────────────────────── إنشاء ──────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create(int? assetId)
    {
        var vm = new ScheduleFormViewModel
        {
            StartDate = DateTime.UtcNow.Date,
            NextDueDate = DateTime.UtcNow.Date.AddMonths(1)
        };

        if (assetId.HasValue)
        {
            var a = await _db.Assets.AsNoTracking().Where(x => x.Id == assetId)
                .Select(x => new { x.Id, x.AssetTag, x.NameAr }).FirstOrDefaultAsync();
            if (a != null)
            {
                vm.AssetId = a.Id;
                vm.AssetLabel = $"{a.AssetTag} — {a.NameAr}";
            }
        }

        await FillFormLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create(ScheduleFormViewModel vm)
    {
        var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == vm.AssetId);
        if (asset == null)
            ModelState.AddModelError(nameof(vm.AssetId), "الأصل غير موجود أو لا تملك صلاحية الوصول إليه");

        ValidateForm(vm);

        if (!ModelState.IsValid)
        {
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var s = new MaintenanceSchedule
        {
            CompanyId = asset!.CompanyId,
            AssetId = asset.Id,
            Title = vm.Title.Trim(),
            Description = Clean(vm.Description),
            Checklist = Clean(vm.Checklist),
            Frequency = vm.Frequency,
            Status = vm.Status,
            StartDate = vm.StartDate.Date,
            EndDate = vm.EndDate?.Date,
            NextDueDate = vm.NextDueDate.Date,
            LeadTimeDays = vm.LeadTimeDays,
            DefaultTechnicianId = string.IsNullOrWhiteSpace(vm.DefaultTechnicianId) ? null : vm.DefaultTechnicianId,
            DefaultPriority = vm.DefaultPriority,
            EstimatedCost = vm.EstimatedCost
        };

        _db.MaintenanceSchedules.Add(s);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create", nameof(MaintenanceSchedule), s.Id.ToString(),
            null, new { s.Title, s.Frequency, s.NextDueDate });

        ToastSuccess($"تم إنشاء جدول الصيانة «{s.Title}» بنجاح");
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    // ────────────────────────── التعديل ──────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.MaintenanceSchedules.AsNoTracking()
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s == null) return NotFoundOrForbidden();

        var vm = new ScheduleFormViewModel
        {
            Id = s.Id, AssetId = s.AssetId,
            AssetLabel = s.Asset != null ? $"{s.Asset.AssetTag} — {s.Asset.NameAr}" : null,
            Title = s.Title, Description = s.Description, Checklist = s.Checklist,
            Frequency = s.Frequency, Status = s.Status,
            StartDate = s.StartDate, EndDate = s.EndDate, NextDueDate = s.NextDueDate,
            LeadTimeDays = s.LeadTimeDays,
            DefaultTechnicianId = s.DefaultTechnicianId,
            DefaultPriority = s.DefaultPriority,
            EstimatedCost = s.EstimatedCost
        };

        await FillFormLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(ScheduleFormViewModel vm)
    {
        var s = await _db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (s == null) return NotFoundOrForbidden();

        ValidateForm(vm);

        if (!ModelState.IsValid)
        {
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var before = new { s.Title, s.Frequency, s.Status, s.NextDueDate };

        s.Title = vm.Title.Trim();
        s.Description = Clean(vm.Description);
        s.Checklist = Clean(vm.Checklist);
        s.Frequency = vm.Frequency;
        s.Status = vm.Status;
        s.StartDate = vm.StartDate.Date;
        s.EndDate = vm.EndDate?.Date;
        s.NextDueDate = vm.NextDueDate.Date;
        s.LeadTimeDays = vm.LeadTimeDays;
        s.DefaultTechnicianId = string.IsNullOrWhiteSpace(vm.DefaultTechnicianId) ? null : vm.DefaultTechnicianId;
        s.DefaultPriority = vm.DefaultPriority;
        s.EstimatedCost = vm.EstimatedCost;
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", nameof(MaintenanceSchedule), s.Id.ToString(),
            before, new { s.Title, s.Frequency, s.Status, s.NextDueDate });

        ToastSuccess("تم تحديث جدول الصيانة");
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    private void ValidateForm(ScheduleFormViewModel vm)
    {
        if (vm.EndDate.HasValue && vm.EndDate.Value.Date < vm.StartDate.Date)
            ModelState.AddModelError(nameof(vm.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية");

        if (vm.NextDueDate.Date < vm.StartDate.Date)
            ModelState.AddModelError(nameof(vm.NextDueDate), "تاريخ الاستحقاق القادم لا يمكن أن يسبق تاريخ البداية");

        if (vm.EndDate.HasValue && vm.NextDueDate.Date > vm.EndDate.Value.Date)
            ModelState.AddModelError(nameof(vm.NextDueDate), "تاريخ الاستحقاق القادم يتجاوز تاريخ نهاية الجدول");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task FillFormLookupsAsync(ScheduleFormViewModel vm)
    {
        vm.Assets = await _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed)
            .OrderBy(a => a.AssetTag).Take(500)
            .Select(a => new AssetPickerItem { Id = a.Id, AssetTag = a.AssetTag, Name = a.NameAr })
            .ToListAsync();

        vm.Technicians = await TechniciansAsync();
    }

    // ────────────────────── إيقاف / تفعيل ──────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Toggle(int id)
    {
        var s = await _db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFoundOrForbidden();

        if (s.Status == ScheduleStatus.Ended)
        {
            ToastError("لا يمكن تفعيل جدول منتهي — عدّل تاريخ النهاية أولاً");
            return RedirectToAction(nameof(Details), new { id });
        }

        var old = s.Status;
        s.Status = s.Status == ScheduleStatus.Active ? ScheduleStatus.Paused : ScheduleStatus.Active;
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ToggleSchedule", nameof(MaintenanceSchedule), s.Id.ToString(),
            new { Status = DisplayHelper.Name(old) }, new { Status = DisplayHelper.Name(s.Status) });

        ToastSuccess(s.Status == ScheduleStatus.Active
            ? "تم تفعيل الجدول — سيولّد تذاكر تلقائياً"
            : "تم إيقاف الجدول مؤقتاً");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────── توليد تذكرة يدوياً ──────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> GenerateNow(int id)
    {
        var s = await _db.MaintenanceSchedules
            .Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s == null) return NotFoundOrForbidden();

        if (s.Status != ScheduleStatus.Active)
        {
            ToastError("الجدول غير مُفعَّل — فعّله أولاً لتوليد التذاكر");
            return RedirectToAction(nameof(Details), new { id });
        }

        // نمنع التوليد المزدوج: تذكرة مفتوحة لنفس الجدول تعني أن الدورة الحالية جارية
        var hasOpen = await _db.MaintenanceTickets.AnyAsync(t => t.ScheduleId == s.Id
            && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);

        if (hasOpen)
        {
            ToastError("توجد تذكرة مفتوحة بالفعل لهذا الجدول");
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;
        var (respDue, resDue) = await _sla.ComputeDueDatesAsync(s.CompanyId, s.DefaultPriority, now);

        var t = new MaintenanceTicket
        {
            CompanyId = s.CompanyId,
            TicketNumber = await _seq.NextTicketNumberAsync(s.CompanyId),
            AssetId = s.AssetId,
            Title = s.Title,
            Description = s.Description ?? "تذكرة صيانة وقائية مولّدة يدوياً من الجدول الدوري."
                          + (string.IsNullOrWhiteSpace(s.Checklist) ? "" : $"\n\nقائمة الفحص:\n{s.Checklist}"),
            Type = TicketType.Preventive,
            Priority = s.DefaultPriority,
            Status = s.DefaultTechnicianId != null ? TicketStatus.Assigned : TicketStatus.Open,
            RequestedByUserId = Me.UserId,
            AssignedTechnicianId = s.DefaultTechnicianId,
            ReportedAt = now,
            ResponseDueAt = respDue,
            ResolutionDueAt = resDue,
            ScheduleId = s.Id
        };

        _db.MaintenanceTickets.Add(t);

        s.LastGeneratedAt = now;
        s.NextDueDate = AdvanceDate(s.NextDueDate, s.Frequency);
        if (s.EndDate != null && s.NextDueDate > s.EndDate) s.Status = ScheduleStatus.Ended;
        s.UpdatedAt = now;

        await _db.SaveChangesAsync();

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = s.CompanyId, TicketId = t.Id,
            Action = "توليد من جدول صيانة وقائية",
            ToValue = DisplayHelper.Name(t.Status),
            ByUserId = Me.UserId, OccurredAt = now
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("GenerateTicket", nameof(MaintenanceSchedule), s.Id.ToString(),
            null, new { t.TicketNumber, s.NextDueDate });

        if (s.DefaultTechnicianId != null)
            await _notify.NotifyAsync(s.DefaultTechnicianId, NotificationType.MaintenanceDue,
                $"صيانة وقائية مستحقة {t.TicketNumber}",
                $"{s.Title} — {s.Asset?.AssetTag}",
                $"/Tickets/Details/{t.Id}", s.CompanyId);

        ToastSuccess($"تم توليد التذكرة {t.TicketNumber} — الاستحقاق القادم {s.NextDueDate:yyyy/MM/dd}");
        return RedirectToAction("Details", "Tickets", new { id = t.Id });
    }

    /// <summary>نفس منطق التقديم المستخدم في مهمة Hangfire — يضمن اتساق المواعيد</summary>
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
