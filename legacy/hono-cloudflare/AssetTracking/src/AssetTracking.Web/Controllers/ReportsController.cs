using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// التقارير التحليلية — أصول، تذاكر، عهد، إهلاك — مع تصدير Excel لكل تقرير.
/// ملاحظة مهمة: SQLite لا يدعم SUM/ORDER BY على decimal، لذا تُسحَب القيم
/// إلى الذاكرة ثم تُجمَع/تُرتَّب في C#.
/// </summary>
[Authorize(Policy = Policies.ManagerOrAdmin)]
public class ReportsController : BaseController
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    // ────────────────────────── الرئيسية ──────────────────────────
    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var soon = today.AddDays(60);

        var assetMoney = await _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed)
            .Select(a => new { a.PurchaseValue, a.BookValue })
            .ToListAsync();

        var ticketCosts = await _db.MaintenanceTickets.AsNoTracking()
            .Select(t => new { t.TotalCost, t.Status })
            .ToListAsync();

        var vm = new ReportsHomeViewModel
        {
            TotalAssets = assetMoney.Count,
            TotalValue = assetMoney.Sum(a => a.PurchaseValue ?? 0),
            TotalBookValue = assetMoney.Sum(a => a.BookValue ?? 0),
            TotalTickets = ticketCosts.Count,
            OpenTickets = ticketCosts.Count(t => t.Status != TicketStatus.Closed
                                                 && t.Status != TicketStatus.Cancelled),
            MaintenanceCost = ticketCosts.Sum(t => t.TotalCost ?? 0),
            CustodyActive = await _db.Assets.AsNoTracking()
                .CountAsync(a => a.CurrentCustodyUserId != null && a.Status != AssetStatus.Disposed),
            WarrantyExpiringSoon = await _db.Assets.AsNoTracking()
                .CountAsync(a => a.WarrantyEndDate != null
                                 && a.WarrantyEndDate >= today && a.WarrantyEndDate <= soon),
            AuditsCompleted = await _db.InventoryAudits.AsNoTracking()
                .CountAsync(a => a.Status == AuditStatus.Completed)
        };

        if (Me.CompanyId != null)
            vm.CompanyName = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == Me.CompanyId)
                .Select(c => c.NameAr).FirstOrDefaultAsync();

        return View(vm);
    }

    // ═════════════════════ تقرير الأصول ═════════════════════
    public async Task<IActionResult> Assets(AssetStatus? status, int? categoryId, int? locationId,
        DateTime? from, DateTime? to, string? export)
    {
        var vm = new AssetsReportViewModel
        {
            Status = status, CategoryId = categoryId, LocationId = locationId,
            From = from, To = to
        };

        vm.Rows = await AssetRowsAsync(status, categoryId, locationId, from, to);
        await ResolveHolderNamesAsync(vm.Rows);

        vm.SumPurchase = vm.Rows.Sum(r => r.PurchaseValue ?? 0);
        vm.SumBook = vm.Rows.Sum(r => r.BookValue ?? 0);

        vm.ByCategory = vm.Rows.GroupBy(r => r.CategoryName ?? "غير مصنَّف")
            .Select(g => new ChartSlice
            {
                Label = g.Key, Count = g.Count(),
                Value = g.Sum(x => x.PurchaseValue ?? 0)
            })
            .OrderByDescending(s => s.Value).Take(10).ToList();

        vm.ByStatus = vm.Rows.GroupBy(r => r.Status)
            .Select(g => new ChartSlice
            {
                Label = DisplayHelper.Name(g.Key), Count = g.Count(),
                Value = g.Sum(x => x.PurchaseValue ?? 0)
            })
            .OrderByDescending(s => s.Count).ToList();

        if (export == "xlsx")
        {
            var cols = new List<ExcelExporter.Column<AssetReportRow>>
            {
                new("وسم الأصل", r => r.AssetTag, Width: 20),
                new("الاسم", r => r.Name, Width: 30),
                new("التصنيف", r => r.CategoryName),
                new("الموقع", r => r.LocationName),
                new("الإدارة", r => r.DepartmentName),
                new("الحالة", r => DisplayHelper.Name(r.Status), Width: 14),
                new("الماركة", r => r.Brand, Width: 14),
                new("الرقم التسلسلي", r => r.SerialNumber, Width: 20),
                new("تاريخ الشراء", r => r.PurchaseDate, Width: 14),
                new("قيمة الشراء", r => r.PurchaseValue, "#,##0.00", 16),
                new("القيمة الدفترية", r => r.BookValue, "#,##0.00", 16),
                new("نهاية الضمان", r => r.WarrantyEndDate, Width: 14),
                new("حائز العهدة", r => r.HolderName, Width: 22),
                new("الشركة", r => r.CompanyName, Width: 24)
            };

            var summary = new List<(string, string)>
            {
                ("عدد الأصول", vm.Count.ToString("N0")),
                ("إجمالي قيمة الشراء", vm.SumPurchase.ToString("N2") + " ج.م"),
                ("إجمالي القيمة الدفترية", vm.SumBook.ToString("N2") + " ج.م"),
                ("مجمّع الإهلاك", vm.SumDepreciation.ToString("N2") + " ج.م")
            };

            var bytes = ExcelExporter.Build("الأصول", "تقرير الأصول", vm.Rows, cols, summary);
            return File(bytes, ExcelExporter.ContentType, FileName("تقرير-الأصول"));
        }

        vm.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();
        vm.Locations = await _db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.NameAr)
            .Select(l => new LookupItem { Id = l.Id, Name = l.NameAr }).ToListAsync();

        return View(vm);
    }

    private async Task<List<AssetReportRow>> AssetRowsAsync(AssetStatus? status, int? categoryId,
        int? locationId, DateTime? from, DateTime? to)
    {
        var q = _db.Assets.AsNoTracking().AsQueryable();

        if (status.HasValue) q = q.Where(a => a.Status == status);
        if (categoryId.HasValue) q = q.Where(a => a.CategoryId == categoryId);
        if (locationId.HasValue) q = q.Where(a => a.LocationId == locationId);
        if (from.HasValue) q = q.Where(a => a.PurchaseDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.PurchaseDate <= to.Value.Date);

        var rows = await q.OrderBy(a => a.AssetTag).Take(5000)
            .Select(a => new AssetReportRow
            {
                Id = a.Id, AssetTag = a.AssetTag, Name = a.NameAr,
                CategoryName = a.Category != null ? a.Category.NameAr : null,
                LocationName = a.Location != null ? a.Location.NameAr : null,
                DepartmentName = a.Department != null ? a.Department.NameAr : null,
                Status = a.Status, Brand = a.Brand, SerialNumber = a.SerialNumber,
                PurchaseDate = a.PurchaseDate, PurchaseValue = a.PurchaseValue,
                BookValue = a.BookValue, WarrantyEndDate = a.WarrantyEndDate,
                HolderName = a.CurrentCustodyUserId,
                CompanyName = a.Company != null ? a.Company.NameAr : null
            }).ToListAsync();

        return rows;
    }

    private async Task ResolveHolderNamesAsync(List<AssetReportRow> rows)
    {
        var ids = rows.Select(r => r.HolderName)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
            r.HolderName = r.HolderName != null && names.TryGetValue(r.HolderName, out var n) ? n : null;
    }

    // ═════════════════════ تقرير التذاكر ═════════════════════
    public async Task<IActionResult> Tickets(TicketStatus? status, TicketType? type,
        string? technicianId, DateTime? from, DateTime? to, string? export)
    {
        var vm = new TicketsReportViewModel
        {
            Status = status, Type = type, TechnicianId = technicianId, From = from, To = to
        };

        var q = _db.MaintenanceTickets.AsNoTracking().AsQueryable();

        if (status.HasValue) q = q.Where(t => t.Status == status);
        if (type.HasValue) q = q.Where(t => t.Type == type);
        if (!string.IsNullOrWhiteSpace(technicianId))
            q = q.Where(t => t.AssignedTechnicianId == technicianId);
        if (from.HasValue) q = q.Where(t => t.ReportedAt >= from.Value.Date);
        if (to.HasValue) q = q.Where(t => t.ReportedAt <= to.Value.Date.AddDays(1));

        vm.Rows = await q.OrderByDescending(t => t.ReportedAt).Take(5000)
            .Select(t => new TicketReportRow
            {
                Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                AssetTag = t.Asset != null ? t.Asset.AssetTag : null,
                AssetName = t.Asset != null ? t.Asset.NameAr : null,
                Type = t.Type, Priority = t.Priority, Status = t.Status,
                TechnicianId = t.AssignedTechnicianId,
                RequesterId = t.RequestedByUserId,
                ReportedAt = t.ReportedAt, ResolvedAt = t.ResolvedAt,
                IsSlaBreached = t.IsSlaBreached,
                LaborCost = t.LaborCost, PartsCost = t.PartsCost, TotalCost = t.TotalCost,
                CompanyName = t.Company != null ? t.Company.NameAr : null
            }).ToListAsync();

        await ResolveTicketNamesAsync(vm.Rows);

        vm.Breached = vm.Rows.Count(r => r.IsSlaBreached);
        vm.SumCost = vm.Rows.Sum(r => r.TotalCost ?? 0);
        var resolved = vm.Rows.Where(r => r.ResolutionHours.HasValue).ToList();
        vm.AvgResolutionHours = resolved.Count == 0
            ? 0
            : Math.Round(resolved.Average(r => r.ResolutionHours!.Value), 1);

        vm.ByStatus = vm.Rows.GroupBy(r => r.Status)
            .Select(g => new ChartSlice { Label = DisplayHelper.Name(g.Key), Count = g.Count() })
            .OrderByDescending(s => s.Count).ToList();

        vm.ByType = vm.Rows.GroupBy(r => r.Type)
            .Select(g => new ChartSlice { Label = DisplayHelper.Name(g.Key), Count = g.Count() })
            .OrderByDescending(s => s.Count).ToList();

        vm.ByTechnician = vm.Rows.Where(r => r.TechnicianName != null)
            .GroupBy(r => r.TechnicianName!)
            .Select(g => new ChartSlice
            {
                Label = g.Key, Count = g.Count(),
                Value = g.Sum(x => x.TotalCost ?? 0)
            })
            .OrderByDescending(s => s.Count).Take(10).ToList();

        if (export == "xlsx")
        {
            var cols = new List<ExcelExporter.Column<TicketReportRow>>
            {
                new("رقم التذكرة", r => r.TicketNumber, Width: 18),
                new("الموضوع", r => r.Title, Width: 34),
                new("وسم الأصل", r => r.AssetTag, Width: 18),
                new("اسم الأصل", r => r.AssetName, Width: 26),
                new("النوع", r => DisplayHelper.Name(r.Type), Width: 14),
                new("الأولوية", r => DisplayHelper.Name(r.Priority), Width: 12),
                new("الحالة", r => DisplayHelper.Name(r.Status), Width: 14),
                new("الفني", r => r.TechnicianName, Width: 22),
                new("المُبلِّغ", r => r.RequesterName, Width: 22),
                new("تاريخ الفتح", r => r.ReportedAt, Width: 14),
                new("تاريخ الحل", r => r.ResolvedAt, Width: 14),
                new("ساعات الحل", r => r.ResolutionHours, "#,##0.0", 12),
                new("خرق SLA", r => r.IsSlaBreached, Width: 10),
                new("أجر العمل", r => r.LaborCost, "#,##0.00", 14),
                new("قطع الغيار", r => r.PartsCost, "#,##0.00", 14),
                new("الإجمالي", r => r.TotalCost, "#,##0.00", 14),
                new("الشركة", r => r.CompanyName, Width: 24)
            };

            var summary = new List<(string, string)>
            {
                ("عدد التذاكر", vm.Count.ToString("N0")),
                ("خارقة لـ SLA", vm.Breached.ToString("N0")),
                ("نسبة الالتزام", vm.SlaCompliancePercent.ToString("N1") + "%"),
                ("متوسط زمن الحل", vm.AvgResolutionHours.ToString("N1") + " ساعة"),
                ("إجمالي التكلفة", vm.SumCost.ToString("N2") + " ج.م")
            };

            var bytes = ExcelExporter.Build("التذاكر", "تقرير تذاكر الصيانة", vm.Rows, cols, summary);
            return File(bytes, ExcelExporter.ContentType, FileName("تقرير-التذاكر"));
        }

        vm.Technicians = await TechniciansAsync();
        return View(vm);
    }

    private async Task ResolveTicketNamesAsync(List<TicketReportRow> rows)
    {
        var ids = rows.Select(r => r.TechnicianId)
            .Concat(rows.Select(r => r.RequesterId))
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
        {
            if (r.TechnicianId != null && names.TryGetValue(r.TechnicianId, out var t)) r.TechnicianName = t;
            if (r.RequesterId != null && names.TryGetValue(r.RequesterId, out var q)) r.RequesterName = q;
        }
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
            .Select(u => new UserLookupItem { Id = u.Id, Name = u.FullName }).ToListAsync();
    }

    // ═════════════════════ تقرير العهد ═════════════════════
    public async Task<IActionResult> Custody(string? userId, CustodyStatus? status,
        DateTime? from, DateTime? to, string? export)
    {
        var vm = new CustodyReportViewModel
        {
            UserId = userId, Status = status, From = from, To = to
        };

        var q = _db.CustodyLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            q = q.Where(c => c.NewUserId == userId || c.PreviousUserId == userId);
        if (status.HasValue) q = q.Where(c => c.Status == status);
        if (from.HasValue) q = q.Where(c => c.ActionDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(c => c.ActionDate <= to.Value.Date.AddDays(1));

        vm.Rows = await q.OrderByDescending(c => c.ActionDate).Take(5000)
            .Select(c => new CustodyReportRow
            {
                Id = c.Id, AssetId = c.AssetId,
                AssetTag = c.Asset != null ? c.Asset.AssetTag : null,
                AssetName = c.Asset != null ? c.Asset.NameAr : null,
                Action = c.Action, Status = c.Status,
                PreviousUserId = c.PreviousUserId, NewUserId = c.NewUserId,
                AssignedByUserId = c.AssignedByUserId,
                ActionDate = c.ActionDate, RespondedAt = c.RespondedAt,
                Reason = c.Reason,
                AssetValue = c.Asset != null ? c.Asset.PurchaseValue : null
            }).ToListAsync();

        await ResolveCustodyNamesAsync(vm.Rows);
        vm.SumValue = vm.Rows.Sum(r => r.AssetValue ?? 0);

        // ملخّص العهد الحالية (الأصول التي بحوزة موظفين الآن)
        var current = await _db.Assets.AsNoTracking()
            .Where(a => a.CurrentCustodyUserId != null && a.Status != AssetStatus.Disposed)
            .Select(a => new { a.CurrentCustodyUserId, a.PurchaseValue })
            .ToListAsync();

        var grouped = current.GroupBy(a => a.CurrentCustodyUserId!)
            .Select(g => new CustodySummaryRow
            {
                UserId = g.Key, AssetsCount = g.Count(),
                TotalValue = g.Sum(x => x.PurchaseValue ?? 0)
            })
            .OrderByDescending(s => s.TotalValue).ToList();

        if (grouped.Count > 0)
        {
            var ids = grouped.Select(g => g.UserId).ToList();
            var info = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new
                {
                    u.Id, u.FullName,
                    Dept = u.DepartmentId != null
                        ? _db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => d.NameAr).FirstOrDefault()
                        : null
                })
                .ToListAsync();

            var map = info.ToDictionary(x => x.Id);
            foreach (var g in grouped)
                if (map.TryGetValue(g.UserId, out var u))
                {
                    g.UserName = u.FullName;
                    g.DepartmentName = u.Dept;
                }
        }

        vm.Summary = grouped;

        if (export == "xlsx")
        {
            var cols = new List<ExcelExporter.Column<CustodyReportRow>>
            {
                new("وسم الأصل", r => r.AssetTag, Width: 20),
                new("اسم الأصل", r => r.AssetName, Width: 30),
                new("قيمة الأصل", r => r.AssetValue, "#,##0.00", 16),
                new("الإجراء", r => DisplayHelper.Name(r.Action), Width: 16),
                new("الحالة", r => DisplayHelper.Name(r.Status), Width: 14),
                new("الحائز السابق", r => r.PreviousUserName, Width: 22),
                new("الحائز الجديد", r => r.NewUserName, Width: 22),
                new("سلّمها", r => r.AssignedByName, Width: 22),
                new("تاريخ الإجراء", r => r.ActionDate, Width: 14),
                new("تاريخ الرد", r => r.RespondedAt, Width: 14),
                new("السبب", r => r.Reason, Width: 34)
            };

            var summary = new List<(string, string)>
            {
                ("عدد الحركات", vm.Count.ToString("N0")),
                ("إجمالي قيمة الأصول", vm.SumValue.ToString("N2") + " ج.م"),
                ("موظفون لديهم عهد حالياً", vm.Summary.Count.ToString("N0"))
            };

            var bytes = ExcelExporter.Build("العهد", "تقرير العهد", vm.Rows, cols, summary);
            return File(bytes, ExcelExporter.ContentType, FileName("تقرير-العهد"));
        }

        vm.Users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && (IsAdmin || u.CompanyId == Me.CompanyId))
            .OrderBy(u => u.FullName)
            .Select(u => new UserLookupItem { Id = u.Id, Name = u.FullName }).ToListAsync();

        return View(vm);
    }

    private async Task ResolveCustodyNamesAsync(List<CustodyReportRow> rows)
    {
        var ids = rows.Select(r => r.PreviousUserId)
            .Concat(rows.Select(r => r.NewUserId))
            .Concat(rows.Select(r => r.AssignedByUserId))
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
        {
            if (r.PreviousUserId != null && names.TryGetValue(r.PreviousUserId, out var p)) r.PreviousUserName = p;
            if (r.NewUserId != null && names.TryGetValue(r.NewUserId, out var n)) r.NewUserName = n;
            if (r.AssignedByUserId != null && names.TryGetValue(r.AssignedByUserId, out var b)) r.AssignedByName = b;
        }
    }

    // ═════════════════════ تقرير الإهلاك ═════════════════════
    public async Task<IActionResult> Depreciation(int? categoryId, string? export)
    {
        var vm = new DepreciationReportViewModel { CategoryId = categoryId };

        var q = _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed && a.PurchaseValue != null);

        if (categoryId.HasValue) q = q.Where(a => a.CategoryId == categoryId);

        var raw = await q.Take(5000)
            .Select(a => new
            {
                a.Id, a.AssetTag, a.NameAr,
                CategoryName = a.Category != null ? a.Category.NameAr : null,
                a.PurchaseDate, a.PurchaseValue, a.SalvageValue, a.BookValue,
                a.UsefulLifeYears, a.DepreciationMethod
            }).ToListAsync();

        vm.Rows = raw.Select(a => new DepreciationReportRow
            {
                Id = a.Id, AssetTag = a.AssetTag, Name = a.NameAr,
                CategoryName = a.CategoryName,
                PurchaseDate = a.PurchaseDate,
                PurchaseValue = a.PurchaseValue ?? 0,
                SalvageValue = a.SalvageValue ?? 0,
                BookValue = a.BookValue ?? a.PurchaseValue ?? 0,
                UsefulLifeYears = a.UsefulLifeYears ?? 0,
                Method = a.DepreciationMethod
            })
            .OrderByDescending(r => r.Accumulated)
            .ToList();

        vm.SumPurchase = vm.Rows.Sum(r => r.PurchaseValue);
        vm.SumBook = vm.Rows.Sum(r => r.BookValue);
        vm.SumAnnual = vm.Rows.Sum(r => r.AnnualDepreciation);

        vm.ByCategory = vm.Rows.GroupBy(r => r.CategoryName ?? "غير مصنَّف")
            .Select(g => new ChartSlice
            {
                Label = g.Key, Count = g.Count(),
                Value = g.Sum(x => x.Accumulated)
            })
            .OrderByDescending(s => s.Value).Take(10).ToList();

        if (export == "xlsx")
        {
            var cols = new List<ExcelExporter.Column<DepreciationReportRow>>
            {
                new("وسم الأصل", r => r.AssetTag, Width: 20),
                new("الاسم", r => r.Name, Width: 30),
                new("التصنيف", r => r.CategoryName),
                new("تاريخ الشراء", r => r.PurchaseDate, Width: 14),
                new("العمر (شهر)", r => r.AgeMonths, Width: 12),
                new("قيمة الشراء", r => r.PurchaseValue, "#,##0.00", 16),
                new("القيمة التخريدية", r => r.SalvageValue, "#,##0.00", 16),
                new("القيمة الدفترية", r => r.BookValue, "#,##0.00", 16),
                new("مجمّع الإهلاك", r => r.Accumulated, "#,##0.00", 16),
                new("نسبة الإهلاك %", r => r.DepreciationPercent, "#,##0.0", 14),
                new("العمر الإنتاجي (سنة)", r => r.UsefulLifeYears, Width: 16),
                new("قسط سنوي", r => r.AnnualDepreciation, "#,##0.00", 16),
                new("الطريقة", r => DisplayHelper.Name(r.Method), Width: 18),
                new("مُهلَك بالكامل", r => r.IsFullyDepreciated, Width: 14)
            };

            var summary = new List<(string, string)>
            {
                ("عدد الأصول", vm.Count.ToString("N0")),
                ("إجمالي قيمة الشراء", vm.SumPurchase.ToString("N2") + " ج.م"),
                ("إجمالي القيمة الدفترية", vm.SumBook.ToString("N2") + " ج.م"),
                ("مجمّع الإهلاك", vm.SumAccumulated.ToString("N2") + " ج.م"),
                ("إجمالي القسط السنوي", vm.SumAnnual.ToString("N2") + " ج.م")
            };

            var bytes = ExcelExporter.Build("الإهلاك", "تقرير الإهلاك", vm.Rows, cols, summary);
            return File(bytes, ExcelExporter.ContentType, FileName("تقرير-الإهلاك"));
        }

        vm.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();

        return View(vm);
    }

    private static string FileName(string baseName) =>
        $"{baseName}-{DateTime.Now:yyyy-MM-dd}.xlsx";
}
