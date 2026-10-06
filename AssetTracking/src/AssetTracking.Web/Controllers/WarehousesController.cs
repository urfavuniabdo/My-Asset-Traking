using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.Configuration;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// إدارة المخازن — عرض المواقع/المخازن مع الأصول داخلها.
/// المخزن هنا = Location (من نوع مخزن أو أي موقع)، والإضافة تعيد
/// استخدام نفس شاشة إنشاء المواقع الموجودة في الإدارة.
/// </summary>
[Authorize(Policy = Policies.ManagerOrAdmin)]
public class WarehousesController : BaseController
{
    private readonly AppDbContext _db;

    public WarehousesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new WarehousesIndexViewModel();

        var locations = await _db.Locations.AsNoTracking()
            .OrderBy(l => l.Type == LocationType.Warehouse ? 0 : 1)
            .ThenBy(l => l.NameAr)
            .Select(l => new
            {
                l.Id, l.NameAr, l.Code, l.Type, l.IsActive, l.ContactPerson
            })
            .ToListAsync();

        // تجميعات الأصول — القيم تُجمع على العميل (قيود SQLite على decimal)
        var counts = await _db.Assets.AsNoTracking()
            .Where(a => a.LocationId != null)
            .GroupBy(a => a.LocationId)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToListAsync();

        var valuePairs = await _db.Assets.AsNoTracking()
            .Where(a => a.LocationId != null && a.Status != AssetStatus.Disposed)
            .Select(a => new { a.LocationId, a.PurchaseValue })
            .ToListAsync();

        var maintPairs = await _db.Assets.AsNoTracking()
            .Where(a => a.LocationId != null && a.Status == AssetStatus.UnderMaintenance)
            .Select(a => new { a.LocationId })
            .ToListAsync();

        vm.Items = locations.Select(l => new WarehouseRow
        {
            Id = l.Id,
            NameAr = l.NameAr,
            Code = l.Code,
            Type = l.Type,
            IsActive = l.IsActive,
            ContactPerson = l.ContactPerson,
            AssetCount = counts.FirstOrDefault(x => x.LocationId == l.Id)?.Count ?? 0,
            TotalValue = valuePairs.Where(x => x.LocationId == l.Id).Sum(x => x.PurchaseValue ?? 0m),
            UnderMaintenance = maintPairs.Count(x => x.LocationId == l.Id)
        }).ToList();

        vm.TopWarehouses = vm.Items
            .OrderByDescending(w => w.AssetCount).Take(5)
            .Select(w => new LookupItem { Id = w.Id, Name = w.NameAr })
            .ToList();

        vm.UnlocatedAssets = await _db.Assets.CountAsync(a => a.LocationId == null);

        return View("IndexModern", vm);
    }
}
