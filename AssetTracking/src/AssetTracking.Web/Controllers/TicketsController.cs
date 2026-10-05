using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// تذاكر الصيانة والدعم الفني — دورة حياة كاملة:
/// فتح ← تكليف ← جاري العمل ← بانتظار قطع ← تم الحل ← مغلقة.
/// كل تغيير حالة يُسجَّل في TicketLog ويُطلق إشعاراً.
/// العزل بين الشركات مضمون من Global Query Filters على مستوى القاعدة.
/// </summary>
public class TicketsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ISequenceService _seq;
    private readonly ISlaService _sla;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;

    public TicketsController(AppDbContext db, UserManager<ApplicationUser> users,
        ISequenceService seq, ISlaService sla, INotificationService notify, IAuditService audit)
    {
        _db = db;
        _users = users;
        _seq = seq;
        _sla = sla;
        _notify = notify;
        _audit = audit;
    }

    // ────────────────────────────── الفهرس ──────────────────────────────
    public async Task<IActionResult> Index(string? q, TicketStatus? status, TicketPriority? priority,
        TicketType? type, string? technicianId, string? scope, string? sort, int page = 1)
    {
        var me = Me.UserId;

        var vm = new TicketIndexViewModel
        {
            Q = q, Status = status, Priority = priority, Type = type,
            TechnicianId = technicianId, Scope = scope ?? "all", Sort = sort,
            Page = page < 1 ? 1 : page,
            CanAssign = IsManager,
            CanWork = IsTechnician || IsManager
        };

        // الموظف العادي لا يرى إلا تذاكره أو تذاكر عهدته
        var restrictToMe = !IsManager && !IsTechnician;

        var query = _db.MaintenanceTickets.AsNoTracking().AsQueryable();

        if (restrictToMe)
            query = query.Where(t => t.RequestedByUserId == me);

        // عدّادات البطاقات — قبل تطبيق الفلاتر النصية
        var counterBase = query;
        vm.CountOpen = await counterBase.CountAsync(t => t.Status == TicketStatus.Open);
        vm.CountInProgress = await counterBase.CountAsync(t =>
            t.Status == TicketStatus.Assigned || t.Status == TicketStatus.InProgress
            || t.Status == TicketStatus.WaitingParts);
        vm.CountBreached = await counterBase.CountAsync(t => t.IsSlaBreached
            && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        vm.CountUnassigned = await counterBase.CountAsync(t => t.AssignedTechnicianId == null
            && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);

        // النطاقات السريعة
        query = vm.Scope switch
        {
            "mine" => query.Where(t => t.AssignedTechnicianId == me),
            "requested" => query.Where(t => t.RequestedByUserId == me),
            "unassigned" => query.Where(t => t.AssignedTechnicianId == null
                                             && t.Status != TicketStatus.Closed
                                             && t.Status != TicketStatus.Cancelled),
            "breached" => query.Where(t => t.IsSlaBreached
                                          && t.Status != TicketStatus.Closed
                                          && t.Status != TicketStatus.Cancelled),
            "open" => query.Where(t => t.Status != TicketStatus.Closed
                                       && t.Status != TicketStatus.Cancelled),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.TicketNumber.Contains(term)
                                     || t.Title.Contains(term)
                                     || (t.Description != null && t.Description.Contains(term))
                                     || t.Asset!.AssetTag.Contains(term)
                                     || t.Asset!.NameAr.Contains(term));
        }

        if (status.HasValue) query = query.Where(t => t.Status == status);
        if (priority.HasValue) query = query.Where(t => t.Priority == priority);
        if (type.HasValue) query = query.Where(t => t.Type == type);
        if (!string.IsNullOrWhiteSpace(technicianId))
            query = query.Where(t => t.AssignedTechnicianId == technicianId);

        vm.TotalCount = await query.CountAsync();

        query = sort switch
        {
            "priority" => query.OrderByDescending(t => t.Priority).ThenBy(t => t.ResolutionDueAt),
            "due" => query.OrderBy(t => t.ResolutionDueAt ?? DateTime.MaxValue),
            "old" => query.OrderBy(t => t.ReportedAt),
            "status" => query.OrderBy(t => t.Status).ThenByDescending(t => t.ReportedAt),
            _ => query.OrderByDescending(t => t.ReportedAt)
        };

        vm.Items = await query
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize)
            .Select(t => new TicketRow
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Title = t.Title,
                AssetName = t.Asset!.NameAr,
                AssetTag = t.Asset!.AssetTag,
                Type = t.Type,
                Priority = t.Priority,
                Status = t.Status,
                CompanyName = t.Company!.NameAr,
                ReportedAt = t.ReportedAt,
                ResolutionDueAt = t.ResolutionDueAt,
                ResolvedAt = t.ResolvedAt,
                IsSlaBreached = t.IsSlaBreached,
                // نخزّن المعرّفات مؤقتاً في حقول الأسماء ثم نستبدلها باستعلام واحد
                RequesterName = t.RequestedByUserId,
                TechnicianName = t.AssignedTechnicianId
            }).ToListAsync();

        await ResolveNamesAsync(vm.Items);

        vm.Technicians = await TechniciansAsync();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;
        return View(vm);
    }

    /// <summary>يحوّل معرّفات المستخدمين المخزّنة مؤقتاً إلى أسماء — استعلام واحد لا N+1</summary>
    private async Task ResolveNamesAsync(List<TicketRow> rows)
    {
        var ids = rows.Select(r => r.RequesterName)
            .Concat(rows.Select(r => r.TechnicianName))
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;

        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
        {
            r.RequesterName = r.RequesterName != null && names.TryGetValue(r.RequesterName, out var a) ? a : null;
            r.TechnicianName = r.TechnicianName != null && names.TryGetValue(r.TechnicianName, out var b) ? b : null;
        }
    }

    private async Task<List<UserLookupItem>> TechniciansAsync()
    {
        // الفنيون ومديرو الشركة داخل نطاق شركة المستخدم (أو الجميع للـAdmin)
        var techRole = await _db.Roles.AsNoTracking()
            .Where(r => r.Name == Roles.Technician || r.Name == Roles.CompanyManager)
            .Select(r => r.Id).ToListAsync();

        var userIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => techRole.Contains(ur.RoleId))
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

    // ────────────────────────────── التفاصيل ──────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var t = await _db.MaintenanceTickets.AsNoTracking()
            .Include(x => x.Asset).ThenInclude(a => a!.Location)
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFoundOrForbidden();

        var me = Me.UserId;
        var isMine = t.AssignedTechnicianId == me;
        var isRequester = t.RequestedByUserId == me;

        // الموظف لا يفتح إلا تذكرته
        if (!IsManager && !IsTechnician && !isRequester)
            return NotFoundOrForbidden();

        var closed = t.Status is TicketStatus.Closed or TicketStatus.Cancelled;

        var vm = new TicketDetailsViewModel
        {
            Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title, Description = t.Description,
            AssetId = t.AssetId, AssetName = t.Asset?.NameAr, AssetTag = t.Asset?.AssetTag,
            AssetLocation = t.Asset?.Location?.NameAr,
            Type = t.Type, Priority = t.Priority, Status = t.Status,
            TechnicianId = t.AssignedTechnicianId, CompanyName = t.Company?.NameAr,
            ReportedAt = t.ReportedAt, ResponseDueAt = t.ResponseDueAt,
            FirstRespondedAt = t.FirstRespondedAt, ResolutionDueAt = t.ResolutionDueAt,
            ResolvedAt = t.ResolvedAt, ClosedAt = t.ClosedAt, IsSlaBreached = t.IsSlaBreached,
            Resolution = t.Resolution, RootCause = t.RootCause,
            LaborCost = t.LaborCost, PartsCost = t.PartsCost,
            RowVersion = t.RowVersion != null ? Convert.ToBase64String(t.RowVersion) : null,
            IsMine = isMine,
            CanAssign = IsManager && !closed,
            CanWork = (IsManager || isMine) && !closed,
            CanComment = !closed,
            CanClose = IsManager && t.Status == TicketStatus.Resolved
        };

        // أسماء المُبلِّغ والفني
        var uids = new[] { t.RequestedByUserId, t.AssignedTechnicianId }
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        var userNames = uids.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking().Where(u => uids.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

        if (t.RequestedByUserId != null && userNames.TryGetValue(t.RequestedByUserId, out var rn))
            vm.RequesterName = rn;
        if (t.AssignedTechnicianId != null && userNames.TryGetValue(t.AssignedTechnicianId, out var tn))
            vm.TechnicianName = tn;

        // التعليقات — التعليقات الداخلية تُحجب عن الموظف مقدّم الطلب
        var commentsQ = _db.TicketComments.AsNoTracking().Where(c => c.TicketId == id);
        if (!IsManager && !IsTechnician)
            commentsQ = commentsQ.Where(c => !c.IsInternal);

        var rawComments = await commentsQ.OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.AuthorUserId, c.Body, c.IsInternal, c.CreatedAt })
            .ToListAsync();

        var authorIds = rawComments.Select(c => c.AuthorUserId)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        var authorNames = authorIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

        vm.Comments = rawComments.Select(c => new TicketCommentRow
        {
            Id = c.Id,
            Body = c.Body,
            IsInternal = c.IsInternal,
            CreatedAt = c.CreatedAt,
            AuthorName = c.AuthorUserId != null && authorNames.TryGetValue(c.AuthorUserId, out var an)
                ? an : "النظام"
        }).ToList();

        // سجل الحركات
        var rawLogs = await _db.TicketLogs.AsNoTracking().Where(l => l.TicketId == id)
            .OrderByDescending(l => l.OccurredAt)
            .Select(l => new { l.Action, l.FromValue, l.ToValue, l.ByUserId, l.OccurredAt })
            .ToListAsync();

        var logUserIds = rawLogs.Select(l => l.ByUserId)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        var logNames = logUserIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking().Where(u => logUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

        vm.Logs = rawLogs.Select(l => new TicketLogRow
        {
            Field = l.Action,
            FromValue = l.FromValue,
            ToValue = l.ToValue,
            OccurredAt = l.OccurredAt,
            ByName = l.ByUserId != null && logNames.TryGetValue(l.ByUserId, out var ln) ? ln : "النظام"
        }).ToList();

        // قطع الغيار — الجمع على العميل (قيود SQLite على decimal)
        var parts = await _db.TicketParts.AsNoTracking().Where(p => p.TicketId == id)
            .Select(p => new { p.PartName, p.Quantity, p.UnitPrice }).ToListAsync();
        vm.Parts = parts.Select(p => new TicketPartRow
        {
            PartName = p.PartName, Quantity = p.Quantity, UnitCost = p.UnitPrice
        }).ToList();

        if (vm.CanAssign) vm.Technicians = await TechniciansAsync();

        return View(vm);
    }

    // ────────────────────────────── إنشاء ──────────────────────────────
    public async Task<IActionResult> Create(int? assetId)
    {
        var vm = new TicketCreateViewModel();
        if (assetId.HasValue)
        {
            var a = await _db.Assets.AsNoTracking()
                .Where(x => x.Id == assetId)
                .Select(x => new { x.Id, x.AssetTag, x.NameAr }).FirstOrDefaultAsync();
            if (a != null)
            {
                vm.AssetId = a.Id;
                vm.AssetLabel = $"{a.AssetTag} — {a.NameAr}";
            }
        }
        vm.Assets = await AssetPickerAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateViewModel vm)
    {
        var asset = await _db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == vm.AssetId);

        if (asset == null)
            ModelState.AddModelError(nameof(vm.AssetId), "الأصل غير موجود أو لا تملك صلاحية الوصول إليه");

        if (!ModelState.IsValid)
        {
            vm.Assets = await AssetPickerAsync();
            return View(vm);
        }

        var companyId = asset!.CompanyId;
        var now = DateTime.UtcNow;
        var (respDue, resDue) = await _sla.ComputeDueDatesAsync(companyId, vm.Priority, now);

        var t = new MaintenanceTicket
        {
            CompanyId = companyId,
            TicketNumber = await _seq.NextTicketNumberAsync(companyId),
            AssetId = asset.Id,
            Title = vm.Title.Trim(),
            Description = vm.Description.Trim(),
            Type = vm.Type,
            Priority = vm.Priority,
            Status = TicketStatus.Open,
            RequestedByUserId = Me.UserId,
            ReportedAt = now,
            ResponseDueAt = respDue,
            ResolutionDueAt = resDue
        };

        _db.MaintenanceTickets.Add(t);
        await _db.SaveChangesAsync();

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = companyId, TicketId = t.Id, Action = "فتح التذكرة",
            ToValue = DisplayHelper.Name(TicketStatus.Open), ByUserId = Me.UserId, OccurredAt = now
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create", nameof(MaintenanceTicket), t.Id.ToString(),
            null, new { t.TicketNumber, t.Title, t.Priority });

        // نبلّغ مديري الشركة والفنيين بوجود تذكرة جديدة
        await _notify.NotifyRoleAsync(Roles.CompanyManager, companyId, NotificationType.TicketCreated,
            $"تذكرة جديدة {t.TicketNumber}",
            $"{t.Title} — {asset.AssetTag} ({DisplayHelper.Name(t.Priority)})",
            $"/Tickets/Details/{t.Id}");

        ToastSuccess($"تم فتح التذكرة {t.TicketNumber} بنجاح");
        return RedirectToAction(nameof(Details), new { id = t.Id });
    }

    private async Task<List<AssetPickerItem>> AssetPickerAsync()
        => await _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed)
            .OrderBy(a => a.AssetTag)
            .Take(500)
            .Select(a => new AssetPickerItem { Id = a.Id, AssetTag = a.AssetTag, Name = a.NameAr })
            .ToListAsync();

    // ────────────────────────────── تكليف فني ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Assign(int id, string technicianId)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            ToastError("لا يمكن تكليف فني بتذكرة مغلقة");
            return RedirectToAction(nameof(Details), new { id });
        }

        var tech = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == technicianId);
        if (tech == null || (!IsAdmin && tech.CompanyId != t.CompanyId))
        {
            ToastError("الفني غير موجود أو من شركة أخرى");
            return RedirectToAction(nameof(Details), new { id });
        }

        var oldTech = t.AssignedTechnicianId;
        var oldStatus = t.Status;

        t.AssignedTechnicianId = technicianId;
        if (t.Status == TicketStatus.Open) t.Status = TicketStatus.Assigned;
        t.UpdatedAt = DateTime.UtcNow;

        string? oldName = null;
        if (oldTech != null)
            oldName = await _db.Users.AsNoTracking().Where(u => u.Id == oldTech)
                .Select(u => u.FullName).FirstOrDefaultAsync();

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = t.CompanyId, TicketId = t.Id, Action = "تكليف فني",
            FromValue = oldName, ToValue = tech.FullName,
            ByUserId = Me.UserId, OccurredAt = DateTime.UtcNow
        });

        if (oldStatus != t.Status)
            _db.TicketLogs.Add(new TicketLog
            {
                CompanyId = t.CompanyId, TicketId = t.Id, Action = "تغيير الحالة",
                FromValue = DisplayHelper.Name(oldStatus), ToValue = DisplayHelper.Name(t.Status),
                ByUserId = Me.UserId, OccurredAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Assign", nameof(MaintenanceTicket), t.Id.ToString(),
            new { Technician = oldName }, new { Technician = tech.FullName });

        await _notify.NotifyAsync(technicianId, NotificationType.TicketAssigned,
            $"تم تكليفك بالتذكرة {t.TicketNumber}",
            $"{t.Title} — الأولوية {DisplayHelper.Name(t.Priority)}",
            $"/Tickets/Details/{t.Id}", t.CompanyId);

        ToastSuccess($"تم تكليف {tech.FullName} بالتذكرة");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>الفني يسحب التذكرة لنفسه</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> Claim(int id)
        => await Assign(id, Me.UserId!);

    // ────────────────────────────── تغيير الحالة ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> ChangeStatus(int id, TicketStatus status, string? note)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        // الفني لا يعدّل إلا تذكرته
        if (!IsManager && t.AssignedTechnicianId != Me.UserId)
        {
            ToastError("هذه التذكرة ليست مُكلَّفاً بها");
            return RedirectToAction(nameof(Details), new { id });
        }

        // الحل والإغلاق لهما مسارات مخصصة (Resolve / Close)
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ToastError("استخدم زر «تم الحل» أو «إغلاق التذكرة»");
            return RedirectToAction(nameof(Details), new { id });
        }

        var old = t.Status;
        if (old == status)
            return RedirectToAction(nameof(Details), new { id });

        t.Status = status;
        t.UpdatedAt = DateTime.UtcNow;

        // أول استجابة فعلية تُسجَّل مرة واحدة (مقياس SLA)
        if (t.FirstRespondedAt == null && status is TicketStatus.InProgress or TicketStatus.WaitingParts)
            t.FirstRespondedAt = DateTime.UtcNow;

        if (status == TicketStatus.Cancelled)
            t.ClosedAt = DateTime.UtcNow;

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = t.CompanyId, TicketId = t.Id, Action = "تغيير الحالة",
            FromValue = DisplayHelper.Name(old), ToValue = DisplayHelper.Name(status),
            ByUserId = Me.UserId, Notes = note, OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ChangeStatus", nameof(MaintenanceTicket), t.Id.ToString(),
            new { Status = DisplayHelper.Name(old) }, new { Status = DisplayHelper.Name(status) });

        if (t.RequestedByUserId != null && t.RequestedByUserId != Me.UserId)
            await _notify.NotifyAsync(t.RequestedByUserId, NotificationType.TicketStatusChanged,
                $"تحديث التذكرة {t.TicketNumber}",
                $"الحالة الآن: {DisplayHelper.Name(status)}",
                $"/Tickets/Details/{t.Id}", t.CompanyId);

        ToastSuccess($"تم تحديث الحالة إلى «{DisplayHelper.Name(status)}»");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────────────── الحل ──────────────────────────────
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> Resolve(int id)
    {
        var t = await _db.MaintenanceTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (!IsManager && t.AssignedTechnicianId != Me.UserId)
            return NotFoundOrForbidden();

        return View(new TicketResolveViewModel
        {
            Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
            LaborCost = t.LaborCost, PartsCost = t.PartsCost,
            Resolution = t.Resolution ?? string.Empty, RootCause = t.RootCause
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> Resolve(TicketResolveViewModel vm)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (t == null) return NotFoundOrForbidden();

        if (!IsManager && t.AssignedTechnicianId != Me.UserId)
            return NotFoundOrForbidden();

        if (!ModelState.IsValid)
        {
            vm.TicketNumber = t.TicketNumber;
            vm.Title = t.Title;
            return View(vm);
        }

        var old = t.Status;
        var now = DateTime.UtcNow;

        t.Status = TicketStatus.Resolved;
        t.Resolution = vm.Resolution.Trim();
        t.RootCause = string.IsNullOrWhiteSpace(vm.RootCause) ? null : vm.RootCause.Trim();
        t.LaborCost = vm.LaborCost;
        t.PartsCost = vm.PartsCost;
        t.TotalCost = (vm.LaborCost ?? 0m) + (vm.PartsCost ?? 0m);
        t.ResolvedAt = now;
        t.FirstRespondedAt ??= now;
        t.UpdatedAt = now;
        if (t.AssignedTechnicianId == null) t.AssignedTechnicianId = Me.UserId;

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = t.CompanyId, TicketId = t.Id, Action = "تم الحل",
            FromValue = DisplayHelper.Name(old), ToValue = DisplayHelper.Name(TicketStatus.Resolved),
            ByUserId = Me.UserId, Notes = t.Resolution, OccurredAt = now
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Resolve", nameof(MaintenanceTicket), t.Id.ToString(),
            null, new { t.Resolution, t.TotalCost });

        if (t.RequestedByUserId != null)
            await _notify.NotifyAsync(t.RequestedByUserId, NotificationType.TicketResolved,
                $"تم حل التذكرة {t.TicketNumber}",
                t.Resolution!.Length > 120 ? t.Resolution[..120] + "…" : t.Resolution,
                $"/Tickets/Details/{t.Id}", t.CompanyId);

        await _notify.NotifyRoleAsync(Roles.CompanyManager, t.CompanyId, NotificationType.TicketResolved,
            $"تذكرة بانتظار الإغلاق {t.TicketNumber}", t.Title, $"/Tickets/Details/{t.Id}");

        ToastSuccess("تم تسجيل الحل — التذكرة بانتظار الإغلاق من المدير");
        return RedirectToAction(nameof(Details), new { id = t.Id });
    }

    // ────────────────────────────── الإغلاق ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Close(int id, string? note)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (t.Status != TicketStatus.Resolved)
        {
            ToastError("لا تُغلق التذكرة إلا بعد تسجيل الحل");
            return RedirectToAction(nameof(Details), new { id });
        }

        var old = t.Status;
        var now = DateTime.UtcNow;

        t.Status = TicketStatus.Closed;
        t.ClosedAt = now;
        t.ClosedByUserId = Me.UserId;
        t.UpdatedAt = now;

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = t.CompanyId, TicketId = t.Id, Action = "إغلاق التذكرة",
            FromValue = DisplayHelper.Name(old), ToValue = DisplayHelper.Name(TicketStatus.Closed),
            ByUserId = Me.UserId, Notes = note, OccurredAt = now
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Close", nameof(MaintenanceTicket), t.Id.ToString());

        if (t.AssignedTechnicianId != null && t.AssignedTechnicianId != Me.UserId)
            await _notify.NotifyAsync(t.AssignedTechnicianId, NotificationType.TicketStatusChanged,
                $"أُغلقت التذكرة {t.TicketNumber}", t.Title,
                $"/Tickets/Details/{t.Id}", t.CompanyId);

        ToastSuccess($"تم إغلاق التذكرة {t.TicketNumber}");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>إعادة فتح تذكرة مغلقة (المدير فقط)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Reopen(int id, string? note)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (t.Status is not (TicketStatus.Closed or TicketStatus.Resolved or TicketStatus.Cancelled))
        {
            ToastError("التذكرة مفتوحة بالفعل");
            return RedirectToAction(nameof(Details), new { id });
        }

        var old = t.Status;
        t.Status = t.AssignedTechnicianId != null ? TicketStatus.Assigned : TicketStatus.Open;
        t.ClosedAt = null;
        t.ClosedByUserId = null;
        t.ResolvedAt = null;
        t.UpdatedAt = DateTime.UtcNow;

        _db.TicketLogs.Add(new TicketLog
        {
            CompanyId = t.CompanyId, TicketId = t.Id, Action = "إعادة فتح",
            FromValue = DisplayHelper.Name(old), ToValue = DisplayHelper.Name(t.Status),
            ByUserId = Me.UserId, Notes = note, OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Reopen", nameof(MaintenanceTicket), t.Id.ToString());

        if (t.AssignedTechnicianId != null)
            await _notify.NotifyAsync(t.AssignedTechnicianId, NotificationType.TicketStatusChanged,
                $"أُعيد فتح التذكرة {t.TicketNumber}", note ?? t.Title,
                $"/Tickets/Details/{t.Id}", t.CompanyId);

        ToastInfo("تم إعادة فتح التذكرة");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────────────── التعليقات ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(int id, string body, bool isInternal = false)
    {
        var t = await _db.MaintenanceTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (!IsManager && !IsTechnician && t.RequestedByUserId != Me.UserId)
            return NotFoundOrForbidden();

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length < 2)
        {
            ToastError("لا يمكن إضافة تعليق فارغ");
            return RedirectToAction(nameof(Details), new { id });
        }

        // التعليق الداخلي حق للفنيين والمديرين فقط
        var internalFlag = isInternal && (IsManager || IsTechnician);

        _db.TicketComments.Add(new TicketComment
        {
            CompanyId = t.CompanyId, TicketId = t.Id,
            AuthorUserId = Me.UserId,
            Body = body.Trim().Length > 2000 ? body.Trim()[..2000] : body.Trim(),
            IsInternal = internalFlag
        });
        await _db.SaveChangesAsync();

        // نُبلّغ الطرف الآخر فقط (لا نُبلّغ نفسي) وبشرط ألا يكون التعليق داخلياً
        if (!internalFlag)
        {
            var targets = new[] { t.RequestedByUserId, t.AssignedTechnicianId }
                .Where(x => !string.IsNullOrEmpty(x) && x != Me.UserId).Distinct();
            foreach (var target in targets)
                await _notify.NotifyAsync(target!, NotificationType.General,
                    $"تعليق جديد على {t.TicketNumber}",
                    body.Trim().Length > 100 ? body.Trim()[..100] + "…" : body.Trim(),
                    $"/Tickets/Details/{t.Id}", t.CompanyId);
        }

        ToastSuccess("تمت إضافة التعليق");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────────────── قطع الغيار ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.TechnicianOrAbove)]
    public async Task<IActionResult> AddPart(int id, string partName, int quantity, decimal? unitPrice)
    {
        var t = await _db.MaintenanceTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFoundOrForbidden();

        if (!IsManager && t.AssignedTechnicianId != Me.UserId)
            return NotFoundOrForbidden();

        if (string.IsNullOrWhiteSpace(partName) || quantity < 1)
        {
            ToastError("اسم القطعة والكمية مطلوبان");
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.TicketParts.Add(new TicketPart
        {
            CompanyId = t.CompanyId, TicketId = t.Id,
            PartName = partName.Trim(), Quantity = quantity,
            UnitPrice = unitPrice, TotalPrice = (unitPrice ?? 0m) * quantity
        });

        // نُحدّث تكلفة القطع في التذكرة — الجمع على العميل (قيود SQLite)
        await _db.SaveChangesAsync();
        var all = await _db.TicketParts.AsNoTracking().Where(p => p.TicketId == id)
            .Select(p => new { p.UnitPrice, p.Quantity }).ToListAsync();
        t.PartsCost = all.Sum(p => (p.UnitPrice ?? 0m) * p.Quantity);
        t.TotalCost = (t.LaborCost ?? 0m) + t.PartsCost;
        await _db.SaveChangesAsync();

        ToastSuccess("تمت إضافة قطعة الغيار");
        return RedirectToAction(nameof(Details), new { id });
    }
}
