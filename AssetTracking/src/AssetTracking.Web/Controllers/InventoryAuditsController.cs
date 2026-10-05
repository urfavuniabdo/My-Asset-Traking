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
/// الجرد الدوري للأصول — إنشاء عملية جرد، تعبئة بنودها من الأصول،
/// ثم مسح الأصول بالباركود/الوسم وتسجيل النتيجة (موجود / بمكان مختلف / مفقود / تالف).
/// </summary>
[Authorize(Policy = Policies.TechnicianOrAbove)]
public class InventoryAuditsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ISequenceService _seq;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public InventoryAuditsController(AppDbContext db, ISequenceService seq,
        IAuditService audit, INotificationService notify)
    {
        _db = db;
        _seq = seq;
        _audit = audit;
        _notify = notify;
    }

    // ────────────────────────── الفهرس ──────────────────────────
    public async Task<IActionResult> Index(string? q, AuditStatus? status, int? locationId, int page = 1)
    {
        var vm = new AuditIndexViewModel
        {
            Q = q, Status = status, LocationId = locationId,
            Page = page < 1 ? 1 : page,
            CanEdit = IsManager
        };

        var all = _db.InventoryAudits.AsNoTracking();
        vm.CountDraft = await all.CountAsync(a => a.Status == AuditStatus.Draft);
        vm.CountInProgress = await all.CountAsync(a => a.Status == AuditStatus.InProgress);
        vm.CountCompleted = await all.CountAsync(a => a.Status == AuditStatus.Completed);
        vm.CountMissing = await _db.InventoryAuditItems.AsNoTracking()
            .CountAsync(i => i.Result == AuditItemResult.Missing);

        var query = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.Code.Contains(term)
                                     || a.Title.Contains(term)
                                     || (a.Description != null && a.Description.Contains(term)));
        }

        if (status.HasValue) query = query.Where(a => a.Status == status);
        if (locationId.HasValue) query = query.Where(a => a.LocationId == locationId);

        vm.TotalCount = await query.CountAsync();

        vm.Items = await query
            .OrderBy(a => a.Status).ThenByDescending(a => a.ScheduledDate)
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize)
            .Select(a => new AuditListRow
            {
                Id = a.Id, Code = a.Code, Title = a.Title, Status = a.Status,
                LocationName = a.Location != null ? a.Location.NameAr : null,
                CompanyName = a.Company != null ? a.Company.NameAr : null,
                ResponsibleUserId = a.ResponsibleUserId,
                ScheduledDate = a.ScheduledDate,
                StartedAt = a.StartedAt, CompletedAt = a.CompletedAt,
                TotalExpected = a.TotalExpected, TotalScanned = a.TotalScanned,
                TotalMissing = a.TotalMissing
            }).ToListAsync();

        await ResolveUserNamesAsync(vm.Items);
        vm.Locations = await LocationsAsync();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;
        return View(vm);
    }

    private async Task ResolveUserNamesAsync(IEnumerable<AuditListRow> rows)
    {
        var list = rows.ToList();
        var ids = list.Select(r => r.ResponsibleUserId)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in list)
            if (r.ResponsibleUserId != null && names.TryGetValue(r.ResponsibleUserId, out var n))
                r.ResponsibleName = n;
    }

    private async Task<List<LookupItem>> LocationsAsync() =>
        await _db.Locations.AsNoTracking().Where(l => l.IsActive)
            .OrderBy(l => l.NameAr)
            .Select(l => new LookupItem { Id = l.Id, Name = l.NameAr })
            .ToListAsync();

    private async Task<List<UserLookupItem>> ResponsiblesAsync()
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
    public async Task<IActionResult> Details(int id, AuditItemResult? result, string? q)
    {
        var a = await _db.InventoryAudits.AsNoTracking()
            .Include(x => x.Location)
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a == null) return NotFoundOrForbidden();

        var vm = new AuditDetailsViewModel
        {
            Id = a.Id, Code = a.Code, Title = a.Title, Status = a.Status,
            Description = a.Description, Notes = a.Notes,
            LocationName = a.Location?.NameAr, CompanyName = a.Company?.NameAr,
            ResponsibleUserId = a.ResponsibleUserId,
            ScheduledDate = a.ScheduledDate, StartedAt = a.StartedAt, CompletedAt = a.CompletedAt,
            TotalExpected = a.TotalExpected, TotalScanned = a.TotalScanned, TotalMissing = a.TotalMissing,
            CreatedAt = a.CreatedAt,
            ResultFilter = result, Q = q,
            CanEdit = IsManager,
            CanScan = a.Status == AuditStatus.InProgress
        };

        if (a.ResponsibleUserId != null)
            vm.ResponsibleName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == a.ResponsibleUserId)
                .Select(u => u.FullName).FirstOrDefaultAsync();

        var itemsQuery = _db.InventoryAuditItems.AsNoTracking().Where(i => i.AuditId == id);

        vm.CountPending = await itemsQuery.CountAsync(i => i.Result == AuditItemResult.Pending);
        vm.CountFound = await itemsQuery.CountAsync(i => i.Result == AuditItemResult.Found);
        vm.CountMisplaced = await itemsQuery.CountAsync(i => i.Result == AuditItemResult.Misplaced);
        vm.CountMissingItems = await itemsQuery.CountAsync(i => i.Result == AuditItemResult.Missing);
        vm.CountDamaged = await itemsQuery.CountAsync(i => i.Result == AuditItemResult.Damaged);

        if (result.HasValue) itemsQuery = itemsQuery.Where(i => i.Result == result);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            itemsQuery = itemsQuery.Where(i => i.Asset!.AssetTag.Contains(term)
                                               || i.Asset!.NameAr.Contains(term));
        }

        vm.Items = await itemsQuery
            .OrderBy(i => i.Result).ThenBy(i => i.Asset!.AssetTag)
            .Take(400)
            .Select(i => new AuditItemRow
            {
                Id = i.Id, AssetId = i.AssetId,
                AssetTag = i.Asset!.AssetTag, AssetName = i.Asset!.NameAr,
                CategoryName = i.Asset!.Category != null ? i.Asset!.Category!.NameAr : null,
                AssetStatus = i.Asset!.Status,
                Result = i.Result,
                ExpectedLocationName = i.ExpectedLocationId != null
                    ? _db.Locations.Where(l => l.Id == i.ExpectedLocationId).Select(l => l.NameAr).FirstOrDefault()
                    : null,
                ActualLocationId = i.ActualLocationId,
                ActualLocationName = i.ActualLocationId != null
                    ? _db.Locations.Where(l => l.Id == i.ActualLocationId).Select(l => l.NameAr).FirstOrDefault()
                    : null,
                ScannedAt = i.ScannedAt, ScannedByUserId = i.ScannedByUserId,
                Notes = i.Notes,
                HolderName = i.Asset!.CurrentCustodyUserId
            }).ToListAsync();

        await ResolveItemNamesAsync(vm.Items);
        vm.Locations = await LocationsAsync();
        return View(vm);
    }

    /// <summary>حلّ أسماء الماسحين وحاملي العهدة في استعلام واحد</summary>
    private async Task ResolveItemNamesAsync(List<AuditItemRow> rows)
    {
        var ids = rows.Select(r => r.ScannedByUserId)
            .Concat(rows.Select(r => r.HolderName))
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
        {
            if (r.ScannedByUserId != null && names.TryGetValue(r.ScannedByUserId, out var n1))
                r.ScannedByName = n1;
            r.HolderName = r.HolderName != null && names.TryGetValue(r.HolderName, out var n2) ? n2 : null;
        }
    }

    // ────────────────────────── إنشاء ──────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create()
    {
        var vm = new AuditFormViewModel { ScheduledDate = DateTime.UtcNow.Date };
        await FillFormLookupsAsync(vm);
        vm.ExpectedAssetsCount = await ScopeQuery(null).CountAsync();
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create(AuditFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        // المدير العام غير مرتبط بشركة واحدة → يختارها من النموذج
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null)
        {
            ModelState.AddModelError(nameof(vm.CompanyId), "يجب اختيار الشركة");
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var a = new InventoryAudit
        {
            CompanyId = companyId.Value,
            Code = await _seq.NextAuditCodeAsync(companyId.Value),
            Title = vm.Title.Trim(),
            Description = Clean(vm.Description),
            LocationId = vm.LocationId,
            ResponsibleUserId = string.IsNullOrWhiteSpace(vm.ResponsibleUserId) ? null : vm.ResponsibleUserId,
            ScheduledDate = vm.ScheduledDate.Date,
            Notes = Clean(vm.Notes),
            Status = AuditStatus.Draft
        };

        _db.InventoryAudits.Add(a);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create", nameof(InventoryAudit), a.Id.ToString(),
            null, new { a.Code, a.Title, a.ScheduledDate });

        ToastSuccess($"تم إنشاء عملية الجرد {a.Code} — ابدأ الجرد لتعبئة بنودها من الأصول");
        return RedirectToAction(nameof(Details), new { id = a.Id });
    }

    // ────────────────────────── التعديل ──────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.InventoryAudits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status == AuditStatus.Completed)
        {
            ToastError("لا يمكن تعديل عملية جرد مكتملة");
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new AuditFormViewModel
        {
            Id = a.Id, Title = a.Title, Description = a.Description,
            LocationId = a.LocationId, ResponsibleUserId = a.ResponsibleUserId,
            ScheduledDate = a.ScheduledDate, Notes = a.Notes
        };

        await FillFormLookupsAsync(vm);
        vm.ExpectedAssetsCount = await ScopeQuery(a.LocationId).CountAsync();
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(AuditFormViewModel vm)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status == AuditStatus.Completed)
        {
            ToastError("لا يمكن تعديل عملية جرد مكتملة");
            return RedirectToAction(nameof(Details), new { id = a.Id });
        }

        if (!ModelState.IsValid)
        {
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var before = new { a.Title, a.LocationId, a.ScheduledDate };

        a.Title = vm.Title.Trim();
        a.Description = Clean(vm.Description);
        a.ResponsibleUserId = string.IsNullOrWhiteSpace(vm.ResponsibleUserId) ? null : vm.ResponsibleUserId;
        a.ScheduledDate = vm.ScheduledDate.Date;
        a.Notes = Clean(vm.Notes);
        // الموقع لا يُغيَّر بعد بدء الجرد لأن البنود مُعبَّأة بالفعل
        if (a.Status == AuditStatus.Draft) a.LocationId = vm.LocationId;
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", nameof(InventoryAudit), a.Id.ToString(),
            before, new { a.Title, a.LocationId, a.ScheduledDate });

        ToastSuccess("تم تحديث بيانات عملية الجرد");
        return RedirectToAction(nameof(Details), new { id = a.Id });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task FillFormLookupsAsync(AuditFormViewModel vm)
    {
        vm.Locations = await LocationsAsync();
        vm.Users = await ResponsiblesAsync();

        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick)
            vm.Companies = await _db.Companies.AsNoTracking().Where(c => c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr })
                .ToListAsync();
    }

    /// <summary>الأصول الداخلة في نطاق الجرد — تستبعد المُستبعدة</summary>
    private IQueryable<Asset> ScopeQuery(int? locationId)
    {
        var q = _db.Assets.AsNoTracking().Where(x => x.Status != AssetStatus.Disposed);
        if (locationId.HasValue) q = q.Where(x => x.LocationId == locationId);
        return q;
    }

    // ────────────────────── بدء الجرد (تعبئة البنود) ──────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Start(int id)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status != AuditStatus.Draft)
        {
            ToastError("لا يمكن بدء عملية جرد إلا وهي في حالة مسودة");
            return RedirectToAction(nameof(Details), new { id });
        }

        var assets = await ScopeQuery(a.LocationId)
            .Select(x => new { x.Id, x.LocationId })
            .ToListAsync();

        if (assets.Count == 0)
        {
            ToastError("لا توجد أصول داخل نطاق الجرد المحدَّد");
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;

        foreach (var asset in assets)
        {
            _db.InventoryAuditItems.Add(new InventoryAuditItem
            {
                CompanyId = a.CompanyId,
                AuditId = a.Id,
                AssetId = asset.Id,
                Result = AuditItemResult.Pending,
                ExpectedLocationId = asset.LocationId
            });
        }

        a.Status = AuditStatus.InProgress;
        a.StartedAt = now;
        a.TotalExpected = assets.Count;
        a.TotalScanned = 0;
        a.TotalMissing = 0;
        a.UpdatedAt = now;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("StartAudit", nameof(InventoryAudit), a.Id.ToString(),
            null, new { a.Code, a.TotalExpected });

        if (a.ResponsibleUserId != null)
            await _notify.NotifyAsync(a.ResponsibleUserId, NotificationType.General,
                $"بدء عملية الجرد {a.Code}",
                $"{a.Title} — {a.TotalExpected} أصل بانتظار الجرد",
                $"/InventoryAudits/Details/{a.Id}", a.CompanyId);

        ToastSuccess($"بدأ الجرد — تمت تعبئة {assets.Count} بند");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────── تسجيل نتيجة بند ──────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanItem(int id, int itemId, AuditItemResult result,
        int? actualLocationId, string? notes)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status != AuditStatus.InProgress)
        {
            ToastError("لا يمكن تسجيل نتائج إلا في جرد جارٍ");
            return RedirectToAction(nameof(Details), new { id });
        }

        var item = await _db.InventoryAuditItems.FirstOrDefaultAsync(i => i.Id == itemId && i.AuditId == id);
        if (item == null) return NotFoundOrForbidden();

        if (result == AuditItemResult.Pending)
        {
            ToastError("اختر نتيجة صحيحة للبند");
            return RedirectToAction(nameof(Details), new { id });
        }

        item.Result = result;
        item.ActualLocationId = result == AuditItemResult.Misplaced ? actualLocationId : null;
        item.Notes = Clean(notes);
        item.ScannedAt = DateTime.UtcNow;
        item.ScannedByUserId = Me.UserId;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await RecalcCountersAsync(a);

        ToastSuccess($"تم تسجيل البند: {DisplayHelper.Name(result)}");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>مسح سريع بوسم الأصل — يسجّل «موجود بمكانه» مباشرة</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickScan(int id, string tag)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status != AuditStatus.InProgress)
        {
            ToastError("لا يمكن المسح إلا في جرد جارٍ");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            ToastError("أدخل وسم الأصل");
            return RedirectToAction(nameof(Details), new { id });
        }

        var term = tag.Trim();
        var item = await _db.InventoryAuditItems
            .Include(i => i.Asset)
            .FirstOrDefaultAsync(i => i.AuditId == id && i.Asset!.AssetTag == term);

        if (item == null)
        {
            ToastError($"الوسم «{term}» غير موجود في بنود هذا الجرد");
            return RedirectToAction(nameof(Details), new { id });
        }

        var already = item.Result != AuditItemResult.Pending;

        // إذا اختلف موقع الأصل الحالي عن الموقع المتوقع نُسجّله «بمكان مختلف»
        var misplaced = item.ExpectedLocationId != null
                        && item.Asset?.LocationId != null
                        && item.Asset!.LocationId != item.ExpectedLocationId;

        item.Result = misplaced ? AuditItemResult.Misplaced : AuditItemResult.Found;
        item.ActualLocationId = misplaced ? item.Asset!.LocationId : null;
        item.ScannedAt = DateTime.UtcNow;
        item.ScannedByUserId = Me.UserId;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await RecalcCountersAsync(a);

        if (already)
            ToastInfo($"{term} — كان مجروداً بالفعل، تم تحديث النتيجة إلى {DisplayHelper.Name(item.Result)}");
        else
            ToastSuccess($"{term} — {DisplayHelper.Name(item.Result)} ({item.Asset?.NameAr})");

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>تسجيل كل البنود المتبقية كـ «مفقود» — لإغلاق الجرد</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> MarkRemainingMissing(int id)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status != AuditStatus.InProgress)
        {
            ToastError("لا يمكن تنفيذ ذلك إلا في جرد جارٍ");
            return RedirectToAction(nameof(Details), new { id });
        }

        var pending = await _db.InventoryAuditItems
            .Where(i => i.AuditId == id && i.Result == AuditItemResult.Pending)
            .ToListAsync();

        if (pending.Count == 0)
        {
            ToastInfo("لا توجد بنود متبقية");
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;
        foreach (var i in pending)
        {
            i.Result = AuditItemResult.Missing;
            i.ScannedAt = now;
            i.ScannedByUserId = Me.UserId;
            i.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        await RecalcCountersAsync(a);

        ToastSuccess($"تم تسجيل {pending.Count} بند كمفقود");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────── إتمام / إلغاء ──────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Complete(int id, string? notes)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status != AuditStatus.InProgress)
        {
            ToastError("لا يمكن إتمام إلا جرد جارٍ");
            return RedirectToAction(nameof(Details), new { id });
        }

        var pending = await _db.InventoryAuditItems
            .CountAsync(i => i.AuditId == id && i.Result == AuditItemResult.Pending);

        if (pending > 0)
        {
            ToastError($"لا يزال هناك {pending} بند لم يُجرد — سجّلها أو اعتبرها مفقودة أولاً");
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;
        a.Status = AuditStatus.Completed;
        a.CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(notes)) a.Notes = notes.Trim();
        a.UpdatedAt = now;

        await _db.SaveChangesAsync();
        await RecalcCountersAsync(a);

        await _audit.LogAsync("CompleteAudit", nameof(InventoryAudit), a.Id.ToString(),
            null, new { a.Code, a.TotalScanned, a.TotalMissing });

        ToastSuccess($"تم إتمام الجرد {a.Code} — مفقود: {a.TotalMissing}");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var a = await _db.InventoryAudits.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.Status == AuditStatus.Completed)
        {
            ToastError("لا يمكن إلغاء جرد مكتمل");
            return RedirectToAction(nameof(Details), new { id });
        }

        a.Status = AuditStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason)) a.Notes = reason.Trim();
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("CancelAudit", nameof(InventoryAudit), a.Id.ToString(),
            null, new { a.Code, Reason = reason });

        ToastSuccess("تم إلغاء عملية الجرد");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>يحدّث عدّادات الجرد المخزّنة على السجل الرئيسي</summary>
    private async Task RecalcCountersAsync(InventoryAudit a)
    {
        var counts = await _db.InventoryAuditItems.AsNoTracking()
            .Where(i => i.AuditId == a.Id)
            .GroupBy(i => i.Result)
            .Select(g => new { Result = g.Key, N = g.Count() })
            .ToListAsync();

        a.TotalExpected = counts.Sum(c => c.N);
        a.TotalScanned = counts.Where(c => c.Result != AuditItemResult.Pending).Sum(c => c.N);
        a.TotalMissing = counts.Where(c => c.Result == AuditItemResult.Missing).Sum(c => c.N);
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
