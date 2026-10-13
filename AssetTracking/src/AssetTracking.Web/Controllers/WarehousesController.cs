using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.Configuration;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// وحدة المخازن — كيان Warehouse وجدول Warehouses، بلا أي مرجع إلى مواقع الشركة
/// في الهوية: الموقع اختياري يُعرض إن وُجد.
///
/// لا يوجد فعل «حفظ مشترك» مع مواقع الشركة هنا، ولا عمود نوع في النموذج:
/// هذان كانا جذر عيب «إضافة مخزن تفتح إضافة موقع».
/// </summary>
[Authorize(Policy = Policies.ManagerOrAdmin)]
public class WarehousesController : BaseController
{
    /// <summary>حد قائمة الأصول في التفاصيل — الزيادة عبر رابط «كل أصول المخزن».</summary>
    private const int AssetsOnDetails = 50;

    private readonly AppDbContext _db;
    private readonly IWarehouseService _warehouses;

    public WarehousesController(AppDbContext db, IWarehouseService warehouses)
    {
        _db = db; _warehouses = warehouses;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new WarehousesIndexViewModel();

        var rows = await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.NameAr)
            .Select(w => new
            {
                w.Id, w.NameAr, w.Code, w.IsActive, w.Address,
                SiteName = w.CompanySite != null ? w.CompanySite.NameAr : null
            })
            .ToListAsync();

        var ids = rows.Select(w => w.Id).ToList();

        // التجميعات على العميل — SQLite لا يجمع decimal بأمان، ونفس نمط شاشة الأصول.
        var pairs = await _db.Assets.AsNoTracking()
            .Where(a => a.WarehouseId != null && ids.Contains(a.WarehouseId.Value))
            .Select(a => new { a.WarehouseId, a.Status, a.PurchaseValue, a.CurrentCustodyUserId })
            .ToListAsync();

        vm.Items = rows.Select(w =>
        {
            var at = pairs.Where(p => p.WarehouseId == w.Id).ToList();
            return new WarehouseRow
            {
                Id = w.Id, NameAr = w.NameAr, Code = w.Code, IsActive = w.IsActive,
                Address = w.Address, SiteName = w.SiteName,
                AssetCount = at.Count,
                TotalValue = at.Sum(r => r.PurchaseValue ?? 0m),
                UnderMaintenance = at.Count(r => r.Status == AssetStatus.UnderMaintenance),
                InCustodyCount = at.Count(r => r.CurrentCustodyUserId != null)
            };
        }).ToList();

        vm.TopWarehouses = vm.Items.OrderByDescending(w => w.AssetCount).Take(5)
            .Select(w => new LookupItem { Id = w.Id, Name = w.NameAr }).ToList();

        vm.UnplacedAssets = await _db.Assets.CountAsync(
            a => a.WarehouseId == null && a.CompanySiteId == null);

        return View("IndexModern", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new WarehouseFormViewModel { IsActive = true };
        await FillFormAsync(vm);
        return View("Form", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new WarehouseFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            Notes = e.Notes, Address = e.Address, CompanySiteId = e.CompanySiteId,
            CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillFormAsync(vm);
        return View("Form", vm);
    }

    /// <summary>
    /// حفظ المخزن: فعل لا يعرف إلا المخازن. لا parameter نوع ولا توجيه بحسب نوع
    /// محفوظ — الإخراج دائمًا إلى قائمة المخازن.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WarehouseFormViewModel vm)
    {
        var isNew = vm.Id == 0;
        if (!isNew && !await _db.Warehouses.AnyAsync(w => w.Id == vm.Id))
            return NotFoundOrForbidden();

        if (isNew && Me.CompanyId == null && vm.CompanyId == null)
            ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillFormAsync(vm);
            return View("Form", vm);
        }

        var result = isNew
            ? await _warehouses.CreateAsync(vm.ToInput())
            : await _warehouses.UpdateAsync(vm.ToInput());

        if (!result.Succeeded)
        {
            ToastError(result.Error!);
            await FillFormAsync(vm);
            return View("Form", vm);
        }

        ToastSuccess(result.SuccessMessage!);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var e = await _db.Warehouses.AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id, w.NameAr, w.NameEn, w.Code, w.IsActive, w.Notes, w.Address,
                w.CompanySiteId, SiteName = w.CompanySite != null ? w.CompanySite.NameAr : null
            })
            .FirstOrDefaultAsync();
        if (e == null) return NotFoundOrForbidden();

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.WarehouseId == id)
            .OrderBy(a => a.NameAr)
            .Select(a => new { a.Id, a.AssetTag, a.NameAr, a.Status, a.PurchaseValue, a.CurrentCustodyUserId })
            .ToListAsync();

        var vm = new WarehouseDetailsViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            IsActive = e.IsActive, Notes = e.Notes, Address = e.Address,
            SiteId = e.CompanySiteId, SiteName = e.SiteName,
            AssetCount = assets.Count,
            TotalValue = assets.Sum(a => a.PurchaseValue ?? 0m),
            UnderMaintenance = assets.Count(a => a.Status == AssetStatus.UnderMaintenance),
            InCustodyCount = assets.Count(a => a.CurrentCustodyUserId != null),
            Assets = assets.Take(AssetsOnDetails)
                .Select(a => new WarehouseAssetRow
                { Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr, Status = a.Status })
                .ToList()
        };

        vm.Moves = await PlaceMoves.RecentAsync(_db,
            m => m.FromWarehouseId == id || m.ToWarehouseId == id);
        return View("Details", vm);
    }

    private async Task FillFormAsync(WarehouseFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await ReferencePickers.CompaniesAsync(_db);
        vm.Sites = await ReferencePickers.CompanySitesAsync(_db);
    }
}
