using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

public class HomeController : BaseController
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    /// <summary>لوحة المعلومات — المحتوى يتغيّر حسب الدور</summary>
    public async Task<IActionResult> Index()
    {
        var me = Me;
        var vm = new DashboardViewModel
        {
            UserFullName = me.FullName ?? me.UserName ?? "",
            RoleName = me.IsAdmin ? Roles.ArabicName(Roles.Admin)
                     : me.IsInRole(Roles.CompanyManager) ? Roles.ArabicName(Roles.CompanyManager)
                     : me.IsInRole(Roles.Technician) ? Roles.ArabicName(Roles.Technician)
                     : Roles.ArabicName(Roles.Employee)
        };

        var isEmployee = !me.IsAdmin && !me.IsInRole(Roles.CompanyManager) && !me.IsInRole(Roles.Technician);

        // ── الموظف: يرى عهده وتذاكره فقط ────────────────────
        if (isEmployee)
        {
            vm.MyCustodyCount = await _db.Assets
                .CountAsync(a => a.CurrentCustodyUserId == me.UserId);

            vm.PendingCustodyCount = await _db.CustodyLogs
                .CountAsync(c => c.NewUserId == me.UserId && c.Status == CustodyStatus.Pending);

            vm.MyTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.RequestedByUserId == me.UserId);

            vm.OpenTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.RequestedByUserId == me.UserId
                                 && t.Status != TicketStatus.Closed
                                 && t.Status != TicketStatus.Cancelled);

            vm.RecentTickets = await _db.MaintenanceTickets
                .Where(t => t.RequestedByUserId == me.UserId)
                .OrderByDescending(t => t.ReportedAt).Take(8)
                .Select(t => new RecentTicketRow
                {
                    Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                    AssetName = t.Asset!.NameAr, Status = t.Status,
                    Priority = t.Priority, ReportedAt = t.ReportedAt, IsSlaBreached = t.IsSlaBreached
                })
                .ToListAsync();

            return View(vm);
        }

        // ── الفني: يركّز على تذاكره ──────────────────────────
        if (me.IsInRole(Roles.Technician))
        {
            vm.MyTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.AssignedTechnicianId == me.UserId);

            vm.InProgressTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.AssignedTechnicianId == me.UserId
                                 && t.Status == TicketStatus.InProgress);

            vm.OpenTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.AssignedTechnicianId == me.UserId
                                 && (t.Status == TicketStatus.Assigned
                                     || t.Status == TicketStatus.WaitingParts));

            vm.SlaBreachedTickets = await _db.MaintenanceTickets
                .CountAsync(t => t.AssignedTechnicianId == me.UserId && t.IsSlaBreached
                                 && t.Status != TicketStatus.Closed);

            vm.ResolvedThisMonth = await _db.MaintenanceTickets
                .CountAsync(t => t.AssignedTechnicianId == me.UserId
                                 && t.ResolvedAt != null
                                 && t.ResolvedAt.Value.Month == DateTime.UtcNow.Month
                                 && t.ResolvedAt.Value.Year == DateTime.UtcNow.Year);

            vm.MaintenanceDueSoon = await _db.MaintenanceSchedules
                .CountAsync(s => s.Status == ScheduleStatus.Active
                                 && s.NextDueDate <= DateTime.UtcNow.AddDays(7));

            vm.RecentTickets = await _db.MaintenanceTickets
                .Where(t => t.AssignedTechnicianId == me.UserId
                            && t.Status != TicketStatus.Closed
                            && t.Status != TicketStatus.Cancelled)
                .OrderBy(t => t.ResolutionDueAt).Take(10)
                .Select(t => new RecentTicketRow
                {
                    Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                    AssetName = t.Asset!.NameAr, Status = t.Status,
                    Priority = t.Priority, ReportedAt = t.ReportedAt, IsSlaBreached = t.IsSlaBreached
                })
                .ToListAsync();

            vm.TicketsByPriority = await TicketsByPriorityAsync(me.UserId);
            return View(vm);
        }

        // ── Admin / مدير الشركة: الصورة الكاملة ──────────────
        vm.TotalAssets = await _db.Assets.CountAsync();
        vm.ActiveAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.Active);
        vm.InStoreAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.InStore);
        vm.UnderMaintenanceAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.UnderMaintenance);
        vm.DamagedAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.Damaged);
        vm.DisposedAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.Disposed);
        vm.LostAssets = await _db.Assets.CountAsync(a => a.Status == AssetStatus.Lost);

        // ملاحظة: SQLite لا يدعم SUM على decimal، لذا نجمع على العميل.
        // استعلام واحد يجلب القيمتين معاً — لا فرق ملموس في الأداء.
        var assetValues = await _db.Assets
            .Where(a => a.Status != AssetStatus.Disposed)
            .Select(a => new { a.PurchaseValue, a.BookValue })
            .ToListAsync();

        vm.TotalPurchaseValue = assetValues.Sum(x => x.PurchaseValue ?? 0m);
        vm.TotalBookValue = assetValues.Sum(x => x.BookValue ?? 0m);

        vm.OpenTickets = await _db.MaintenanceTickets
            .CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.Assigned);

        vm.InProgressTickets = await _db.MaintenanceTickets
            .CountAsync(t => t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingParts);

        vm.SlaBreachedTickets = await _db.MaintenanceTickets
            .CountAsync(t => t.IsSlaBreached && t.Status != TicketStatus.Closed
                             && t.Status != TicketStatus.Cancelled);

        vm.ResolvedThisMonth = await _db.MaintenanceTickets
            .CountAsync(t => t.ResolvedAt != null
                             && t.ResolvedAt.Value.Month == DateTime.UtcNow.Month
                             && t.ResolvedAt.Value.Year == DateTime.UtcNow.Year);

        vm.PendingCustodyCount = await _db.CustodyLogs
            .CountAsync(c => c.Status == CustodyStatus.Pending);

        vm.WarrantyExpiringSoon = await _db.Assets
            .CountAsync(a => a.WarrantyEndDate != null
                             && a.WarrantyEndDate >= DateTime.UtcNow
                             && a.WarrantyEndDate <= DateTime.UtcNow.AddDays(30)
                             && a.Status != AssetStatus.Disposed);

        vm.MaintenanceDueSoon = await _db.MaintenanceSchedules
            .CountAsync(s => s.Status == ScheduleStatus.Active
                             && s.NextDueDate <= DateTime.UtcNow.AddDays(7));

        // نُرتّب ونقتطع على العميل — SQLite لا يدعم ORDER BY على decimal
        var byCategory = await _db.Assets
            .GroupBy(a => a.Category!.NameAr)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.AssetsByCategory = byCategory
            .OrderByDescending(x => x.Count).Take(8)
            .Select(x => new ChartPoint { Label = x.Label, Value = x.Count })
            .ToList();

        vm.AssetsByStatus = new List<ChartPoint>
        {
            new() { Label = "نشط", Value = vm.ActiveAssets },
            new() { Label = "في المخزن", Value = vm.InStoreAssets },
            new() { Label = "تحت الصيانة", Value = vm.UnderMaintenanceAssets },
            new() { Label = "تالف", Value = vm.DamagedAssets },
            new() { Label = "مستبعد", Value = vm.DisposedAssets },
            new() { Label = "مفقود", Value = vm.LostAssets }
        };

        vm.TicketsByPriority = await TicketsByPriorityAsync(null);

        vm.RecentTickets = await _db.MaintenanceTickets
            .OrderByDescending(t => t.ReportedAt).Take(10)
            .Select(t => new RecentTicketRow
            {
                Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                AssetName = t.Asset!.NameAr, Status = t.Status,
                Priority = t.Priority, ReportedAt = t.ReportedAt, IsSlaBreached = t.IsSlaBreached
            })
            .ToListAsync();

        return View(vm);
    }

    private async Task<List<ChartPoint>> TicketsByPriorityAsync(string? technicianId)
    {
        var q = _db.MaintenanceTickets.AsQueryable();

        if (technicianId != null)
            q = q.Where(t => t.AssignedTechnicianId == technicianId);

        var raw = await q
            .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled)
            .GroupBy(t => t.Priority)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        return raw.Select(x => new ChartPoint
        {
            Label = x.Key switch
            {
                TicketPriority.Critical => "حرجة",
                TicketPriority.High => "عالية",
                TicketPriority.Medium => "متوسطة",
                _ => "منخفضة"
            },
            Value = x.Count
        }).ToList();
    }

    [AllowAnonymous]
    public IActionResult Error() => View();

    [AllowAnonymous]
    public IActionResult StatusCode(int? code)
    {
        ViewData["Code"] = code ?? 500;
        return View("StatusCodePage");
    }
}
