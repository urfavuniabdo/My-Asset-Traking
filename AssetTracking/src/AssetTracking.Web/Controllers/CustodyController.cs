using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// إدارة العهد — تسليم الأصول للموظفين ومتابعة الموافقات والإرجاع.
///
/// ⚠️ قاعدة أمنية محورية: الرد على طلب العهدة (قبول/رفض) مسموح
/// للموظف المستلِم فقط، ويُنفَّذ داخل CustodyService.RespondAsync.
/// </summary>
public class CustodyController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ICustodyService _custody;

    public CustodyController(AppDbContext db, ICustodyService custody)
    {
        _db = db;
        _custody = custody;
    }

    // ────────────────────── عهدي (كل المستخدمين) ──────────────────────
    public async Task<IActionResult> MyCustody()
    {
        var me = Me.UserId!;
        var vm = new MyCustodyViewModel { UserName = Me.FullName ?? Me.UserName ?? "" };

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.CurrentCustodyUserId == me)
            .Select(a => new AssetRow
            {
                Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr,
                Brand = a.Brand, Model = a.Model, SerialNumber = a.SerialNumber,
                CategoryName = a.Category!.NameAr,
                LocationName = a.Location != null ? a.Location.NameAr : null,
                DepartmentName = a.Department != null ? a.Department.NameAr : null,
                CompanyName = a.Company!.NameAr,
                Status = a.Status,
                PurchaseValue = a.PurchaseValue,
                BookValue = a.BookValue,
                WarrantyEndDate = a.WarrantyEndDate,
                OpenTickets = a.Tickets.Count(t => t.Status != TicketStatus.Closed
                                                   && t.Status != TicketStatus.Cancelled)
            })
            .ToListAsync();

        // الترتيب والجمع على العميل — SQLite لا يدعم ORDER BY/SUM على decimal
        vm.MyAssets = assets.OrderByDescending(a => a.PurchaseValue ?? 0m).ToList();
        vm.TotalValue = assets.Sum(a => a.PurchaseValue ?? 0m);

        // الطلبات المعلّقة التي تنتظر ردّي
        vm.Pending = await RowsAsync(_db.CustodyLogs.AsNoTracking()
            .Where(c => c.NewUserId == me && c.Status == CustodyStatus.Pending)
            .OrderByDescending(c => c.ActionDate));

        foreach (var p in vm.Pending) p.AwaitingMyResponse = true;

        // سجل حركاتي (تسليم/إرجاع)
        vm.History = await RowsAsync(_db.CustodyLogs.AsNoTracking()
            .Where(c => (c.NewUserId == me || c.PreviousUserId == me)
                        && c.Status != CustodyStatus.Pending)
            .OrderByDescending(c => c.ActionDate).Take(30));

        return View(vm);
    }

    // ────────────────────── فهرس الإدارة (المدير) ──────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Index(CustodyStatus? status, string? userId, string? q, int page = 1)
    {
        var vm = new CustodyIndexViewModel
        {
            Status = status, UserId = userId, Q = q,
            Page = page < 1 ? 1 : page,
            CanAssign = true
        };

        var query = _db.CustodyLogs.AsNoTracking().AsQueryable();

        vm.CountPending = await query.CountAsync(c => c.Status == CustodyStatus.Pending);
        vm.CountAccepted = await query.CountAsync(c => c.Status == CustodyStatus.Accepted);
        vm.CountRejected = await query.CountAsync(c => c.Status == CustodyStatus.Rejected);

        if (status.HasValue) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(c => c.NewUserId == userId || c.PreviousUserId == userId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Asset!.AssetTag.Contains(term)
                                     || c.Asset!.NameAr.Contains(term)
                                     || (c.Reason != null && c.Reason.Contains(term)));
        }

        vm.TotalCount = await query.CountAsync();

        vm.Items = await RowsAsync(query.OrderByDescending(c => c.ActionDate)
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize));

        vm.Users = await EmployeesAsync();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;
        return View(vm);
    }

    /// <summary>يحوّل استعلام CustodyLogs إلى صفوف عرض ويحلّ أسماء المستخدمين في استعلام واحد</summary>
    private async Task<List<CustodyRow>> RowsAsync(IQueryable<Domain.Entities.CustodyLog> query)
    {
        var rows = await query.Select(c => new CustodyRow
        {
            Id = c.Id,
            AssetId = c.AssetId,
            AssetTag = c.Asset!.AssetTag,
            AssetName = c.Asset!.NameAr,
            CategoryName = c.Asset!.Category != null ? c.Asset!.Category!.NameAr : null,
            CompanyName = c.Asset!.Company != null ? c.Asset!.Company!.NameAr : null,
            Action = c.Action,
            Status = c.Status,
            PreviousUserId = c.PreviousUserId,
            NewUserId = c.NewUserId,
            AssignedById = c.AssignedByUserId,
            ActionDate = c.ActionDate,
            RespondedAt = c.RespondedAt,
            Reason = c.Reason,
            ResponseNote = c.ResponseNote
        }).ToListAsync();

        await ResolveNamesAsync(rows);
        return rows;
    }

    private async Task ResolveNamesAsync(List<CustodyRow> rows)
    {
        var ids = rows.SelectMany(r => new[] { r.PreviousUserId, r.NewUserId, r.AssignedById })
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;

        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string? Look(string? id) => id != null && names.TryGetValue(id, out var n) ? n : null;

        foreach (var r in rows)
        {
            r.PreviousUserName = Look(r.PreviousUserId);
            r.NewUserName = Look(r.NewUserId);
            r.AssignedByName = Look(r.AssignedById);
        }
    }

    private async Task<List<UserLookupItem>> EmployeesAsync()
    {
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive && !u.IsDeleted);
        if (!IsAdmin)
        {
            var cid = Me.CompanyId;
            q = q.Where(u => u.CompanyId == cid);
        }
        return await q.OrderBy(u => u.FullName)
            .Select(u => new UserLookupItem { Id = u.Id, Name = u.FullName })
            .ToListAsync();
    }

    // ────────────────────── تسليم عهدة ──────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Assign(int? assetId)
    {
        var vm = new CustodyAssignViewModel();

        if (assetId.HasValue)
        {
            var a = await _db.Assets.AsNoTracking()
                .Where(x => x.Id == assetId)
                .Select(x => new { x.Id, x.AssetTag, x.NameAr, x.CurrentCustodyUserId })
                .FirstOrDefaultAsync();

            if (a != null)
            {
                vm.AssetId = a.Id;
                vm.AssetLabel = $"{a.AssetTag} — {a.NameAr}";
                if (a.CurrentCustodyUserId != null)
                    vm.CurrentHolderName = await _db.Users.AsNoTracking()
                        .Where(u => u.Id == a.CurrentCustodyUserId)
                        .Select(u => u.FullName).FirstOrDefaultAsync();
            }
        }

        await FillAssignLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Assign(CustodyAssignViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await FillAssignLookupsAsync(vm);
            return View(vm);
        }

        var result = await _custody.AssignAsync(vm.AssetId, vm.NewUserId, vm.Reason);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await FillAssignLookupsAsync(vm);
            return View(vm);
        }

        ToastSuccess(result.SuccessMessage ?? "تم إرسال طلب العهدة");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillAssignLookupsAsync(CustodyAssignViewModel vm)
    {
        vm.Assets = await _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Lost)
            .OrderBy(a => a.AssetTag).Take(500)
            .Select(a => new AssetPickerItem { Id = a.Id, AssetTag = a.AssetTag, Name = a.NameAr })
            .ToListAsync();

        vm.Users = await EmployeesAsync();
    }

    // ────────────────────── الرد على الطلب (المستلِم فقط) ──────────────────────
    public async Task<IActionResult> Respond(int id)
    {
        var me = Me.UserId;
        var log = await _db.CustodyLogs.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id, c.NewUserId, c.Status, c.ActionDate, c.Reason,
                c.AssignedByUserId,
                AssetTag = c.Asset!.AssetTag,
                AssetName = c.Asset!.NameAr
            }).FirstOrDefaultAsync();

        // ⚠️ نفس رسالة "غير موجود" للحالتين — منع تعداد المعرّفات
        if (log == null || log.NewUserId != me) return NotFoundOrForbidden();

        if (log.Status != CustodyStatus.Pending)
        {
            ToastInfo("تم الرد على هذا الطلب مسبقاً");
            return RedirectToAction(nameof(MyCustody));
        }

        var byName = log.AssignedByUserId == null ? null : await _db.Users.AsNoTracking()
            .Where(u => u.Id == log.AssignedByUserId).Select(u => u.FullName).FirstOrDefaultAsync();

        return View(new CustodyRespondViewModel
        {
            Id = log.Id, AssetTag = log.AssetTag, AssetName = log.AssetName,
            AssignedByName = byName, ActionDate = log.ActionDate, Reason = log.Reason
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, bool accept, string? note)
    {
        var result = await _custody.RespondAsync(id, accept, note);

        if (!result.Succeeded)
        {
            ToastError(result.Error!);
            return RedirectToAction(nameof(MyCustody));
        }

        ToastSuccess(result.SuccessMessage!);
        return RedirectToAction(nameof(MyCustody));
    }

    // ────────────────────── الإرجاع ──────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Return(int assetId)
    {
        var a = await _db.Assets.AsNoTracking()
            .Where(x => x.Id == assetId)
            .Select(x => new { x.Id, x.AssetTag, x.NameAr, x.CurrentCustodyUserId, x.CustodySince })
            .FirstOrDefaultAsync();

        if (a == null) return NotFoundOrForbidden();

        if (a.CurrentCustodyUserId == null)
        {
            ToastError("هذا الأصل ليس في عهدة أي موظف حالياً");
            return RedirectToAction("Details", "Assets", new { id = assetId });
        }

        var holder = await _db.Users.AsNoTracking()
            .Where(u => u.Id == a.CurrentCustodyUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync();

        return View(new CustodyReturnViewModel
        {
            AssetId = a.Id, AssetTag = a.AssetTag, AssetName = a.NameAr,
            HolderName = holder, CustodySince = a.CustodySince
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Return(CustodyReturnViewModel vm)
    {
        var result = await _custody.ReturnAsync(vm.AssetId, vm.Reason, vm.Condition);

        if (!result.Succeeded)
        {
            ToastError(result.Error!);
            return RedirectToAction("Details", "Assets", new { id = vm.AssetId });
        }

        ToastSuccess(result.SuccessMessage!);
        return RedirectToAction("Details", "Assets", new { id = vm.AssetId });
    }
}
