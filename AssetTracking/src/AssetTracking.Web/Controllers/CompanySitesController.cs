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
/// وحدة مواقع / فروع الشركة — كيان CompanySite مستقل تمامًا: لا نوع، ولا مرجع
/// إجباري لمخزن، ولا فعل حفظ مشترك مع المخازن.
///
/// شركة تنشئ ثلاثة فروع وبلا أي مخزن — وهذا المسار هو ما يجعل ذلك ممكنًا.
/// </summary>
[Authorize(Policy = Policies.ManagerOrAdmin)]
public class CompanySitesController : BaseController
{
    private const int AssetsOnDetails = 50;

    private readonly AppDbContext _db;
    private readonly ICompanySiteService _sites;

    public CompanySitesController(AppDbContext db, ICompanySiteService sites)
    {
        _db = db; _sites = sites;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new CompanySitesIndexViewModel();

        var rows = await _db.CompanySites.AsNoTracking()
            .OrderBy(s => s.NameAr)
            .Select(s => new
            {
                s.Id, s.NameAr, s.Code, s.Kind, s.IsActive, s.City, s.Governorate, s.Notes,
                Warehouses = s.Warehouses.OrderBy(w => w.NameAr)
                    .Select(w => new LookupItem { Id = w.Id, Name = w.NameAr }).ToList()
            })
            .ToListAsync();

        // أصول الموقع نفسه — ما يقف في الفرع مباشرة لا في مخزن تحته؛ التسلسل
        // «مخزن ← موقعه» يُقرأ في شاشة المخازن ولا يُجمع هنا.
        var counts = await _db.Assets.AsNoTracking()
            .Where(a => a.CompanySiteId != null)
            .GroupBy(a => a.CompanySiteId!.Value)
            .Select(g => new { SiteId = g.Key, Count = g.Count() })
            .ToListAsync();
        var bySite = counts.ToDictionary(c => c.SiteId, c => c.Count);

        vm.Items = rows.Select(s => new SiteRow
        {
            Id = s.Id, NameAr = s.NameAr, Code = s.Code, Kind = s.Kind, IsActive = s.IsActive,
            City = s.City, Governorate = s.Governorate, Notes = s.Notes,
            Warehouses = s.Warehouses,
            DirectAssetCount = bySite.TryGetValue(s.Id, out var n) ? n : 0
        }).ToList();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CompanySiteFormViewModel { IsActive = true };
        await FillFormAsync(vm);
        return View("Form", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.CompanySites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new CompanySiteFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code, Kind = e.Kind,
            Address = e.Address, City = e.City, Governorate = e.Governorate,
            ContactPerson = e.ContactPerson, ContactPhone = e.ContactPhone,
            Notes = e.Notes, CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillFormAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CompanySiteFormViewModel vm)
    {
        var isNew = vm.Id == 0;
        if (!isNew && !await _db.CompanySites.AnyAsync(s => s.Id == vm.Id))
            return NotFoundOrForbidden();

        if (isNew && Me.CompanyId == null && vm.CompanyId == null)
            ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillFormAsync(vm);
            return View("Form", vm);
        }

        var result = isNew
            ? await _sites.CreateAsync(vm.ToInput())
            : await _sites.UpdateAsync(vm.ToInput());

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
        var e = await _db.CompanySites.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.NameAr, s.NameEn, s.Code, s.Kind, s.IsActive, s.Notes, s.Address,
                s.City, s.Governorate, s.ContactPerson, s.ContactPhone
            })
            .FirstOrDefaultAsync();
        if (e == null) return NotFoundOrForbidden();

        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(w => w.CompanySiteId == id)
            .OrderBy(w => w.NameAr)
            .Select(w => new LookupItem { Id = w.Id, Name = w.NameAr })
            .ToListAsync();

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => a.CompanySiteId == id)
            .OrderBy(a => a.NameAr)
            .Select(a => new { a.Id, a.AssetTag, a.NameAr, a.Status })
            .ToListAsync();

        var moves = await PlaceMoves.RecentAsync(_db, m => m.FromSiteId == id || m.ToSiteId == id);

        var vm = new CompanySiteDetailsViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code, Kind = e.Kind,
            IsActive = e.IsActive, Notes = e.Notes, Address = e.Address, City = e.City,
            Governorate = e.Governorate, ContactPerson = e.ContactPerson, ContactPhone = e.ContactPhone,
            Warehouses = warehouses,
            DirectAssetCount = assets.Count,
            Assets = assets.Take(AssetsOnDetails)
                .Select(a => new WarehouseAssetRow
                { Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr, Status = a.Status })
                .ToList(),
            Moves = moves
        };

        return View(vm);
    }

    private async Task FillFormAsync(CompanySiteFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await ReferencePickers.CompaniesAsync(_db);
    }
}
