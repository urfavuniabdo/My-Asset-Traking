using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Controllers;

/// <summary>
/// إدارة الأصول — الشاشة المحورية في النظام.
/// كل الاستعلامات تمر عبر Global Query Filters فتُقصَر تلقائياً على شركة المستخدم.
/// </summary>
public class AssetsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IQrCodeService _qr;
    private readonly IAuditService _audit;

    public AssetsController(AppDbContext db, UserManager<ApplicationUser> users,
        IQrCodeService qr, IAuditService audit)
    {
        _db = db;
        _users = users;
        _qr = qr;
        _audit = audit;
    }

    // ────────────────────────────── الفهرس ──────────────────────────────
    public async Task<IActionResult> Index(string? q, int? categoryId, int? locationId,
        int? departmentId, AssetStatus? status, string? sort, int page = 1)
    {
        var vm = new AssetIndexViewModel
        {
            Q = q, CategoryId = categoryId, LocationId = locationId,
            DepartmentId = departmentId, Status = status, Sort = sort,
            Page = page < 1 ? 1 : page,
            CanEdit = IsManager
        };

        var query = _db.Assets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.AssetTag.Contains(term)
                                     || a.NameAr.Contains(term)
                                     || (a.NameEn != null && a.NameEn.Contains(term))
                                     || (a.SerialNumber != null && a.SerialNumber.Contains(term))
                                     || (a.Brand != null && a.Brand.Contains(term))
                                     || (a.Model != null && a.Model.Contains(term)));
        }

        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId);
        if (locationId.HasValue) query = query.Where(a => a.LocationId == locationId);
        if (departmentId.HasValue) query = query.Where(a => a.DepartmentId == departmentId);
        if (status.HasValue) query = query.Where(a => a.Status == status);

        vm.TotalCount = await query.CountAsync();

        // ملاحظة: SQLite لا يدعم ORDER BY على decimal، لذا الترتيب المالي يُنفَّذ
        // على العميل بعد الجلب. باقي الترتيبات تُنفَّذ في قاعدة البيانات.
        var isMoneySort = sort is "value" or "value_desc";

        if (!isMoneySort)
        {
            query = sort switch
            {
                "name" => query.OrderBy(a => a.NameAr),
                "name_desc" => query.OrderByDescending(a => a.NameAr),
                "tag" => query.OrderBy(a => a.AssetTag),
                "status" => query.OrderBy(a => a.Status).ThenBy(a => a.AssetTag),
                "date" => query.OrderBy(a => a.PurchaseDate),
                "date_desc" => query.OrderByDescending(a => a.PurchaseDate),
                _ => query.OrderByDescending(a => a.Id)
            };
            query = query.Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize);
        }

        var rows = await query
            .Select(a => new AssetRow
            {
                Id = a.Id,
                AssetTag = a.AssetTag,
                NameAr = a.NameAr,
                Brand = a.Brand,
                Model = a.Model,
                SerialNumber = a.SerialNumber,
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

        if (isMoneySort)
        {
            rows = sort == "value"
                ? rows.OrderBy(r => r.PurchaseValue ?? 0m).ToList()
                : rows.OrderByDescending(r => r.PurchaseValue ?? 0m).ToList();
            rows = rows.Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize).ToList();
        }

        // أسماء حائزي العهدة — استعلام واحد بدل N+1
        var custodyIds = rows.Select(r => r.Id).ToList();
        var holders = await _db.Assets.AsNoTracking()
            .Where(a => custodyIds.Contains(a.Id) && a.CurrentCustodyUserId != null)
            .Select(a => new { a.Id, a.CurrentCustodyUserId })
            .ToListAsync();

        if (holders.Count > 0)
        {
            var uids = holders.Select(h => h.CurrentCustodyUserId!).Distinct().ToList();
            var names = await _db.Users.AsNoTracking()
                .Where(u => uids.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            foreach (var r in rows)
            {
                var h = holders.FirstOrDefault(x => x.Id == r.Id);
                if (h?.CurrentCustodyUserId != null && names.TryGetValue(h.CurrentCustodyUserId, out var nm))
                    r.CustodyUserName = nm;
            }
        }

        vm.Items = rows;

        // إجماليات الفلتر الحالي — تُحسب على العميل (قيود SQLite على decimal)
        var totals = await _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed)
            .Select(a => new { a.PurchaseValue, a.BookValue })
            .ToListAsync();
        vm.SumPurchaseValue = totals.Sum(x => x.PurchaseValue ?? 0m);
        vm.SumBookValue = totals.Sum(x => x.BookValue ?? 0m);

        await FillLookupsAsync(vm);

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;
        return View(vm);
    }

    private async Task FillLookupsAsync(AssetIndexViewModel vm)
    {
        vm.Categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();

        vm.Locations = await _db.Locations.AsNoTracking()
            .OrderBy(l => l.NameAr)
            .Select(l => new LookupItem { Id = l.Id, Name = l.NameAr }).ToListAsync();

        vm.Departments = await _db.Departments.AsNoTracking()
            .OrderBy(d => d.NameAr)
            .Select(d => new LookupItem { Id = d.Id, Name = d.NameAr }).ToListAsync();
    }

    // ────────────────────────────── التفاصيل ──────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var a = await _db.Assets.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Department)
            .Include(x => x.Location).Include(x => x.Vendor).Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a == null) return NotFoundOrForbidden();

        var vm = new AssetDetailsViewModel
        {
            Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr, NameEn = a.NameEn,
            Description = a.Description, Status = a.Status,
            CategoryName = a.Category?.NameAr, DepartmentName = a.Department?.NameAr,
            LocationName = a.Location?.NameAr, VendorName = a.Vendor?.NameAr,
            CompanyName = a.Company?.NameAr,
            Brand = a.Brand, Model = a.Model, SerialNumber = a.SerialNumber,
            Specifications = a.Specifications, Color = a.Color,
            PurchaseDate = a.PurchaseDate, PurchaseValue = a.PurchaseValue,
            SalvageValue = a.SalvageValue, BookValue = a.BookValue,
            UsefulLifeYears = a.UsefulLifeYears, DepreciationMethod = a.DepreciationMethod,
            InvoiceNumber = a.InvoiceNumber,
            WarrantyStartDate = a.WarrantyStartDate, WarrantyEndDate = a.WarrantyEndDate,
            WarrantyProvider = a.WarrantyProvider,
            CustodySince = a.CustodySince, Notes = a.Notes, CreatedAt = a.CreatedAt,
            CanEdit = IsManager
        };

        if (a.CurrentCustodyUserId != null)
        {
            var holder = await _db.Users.AsNoTracking()
                .Where(u => u.Id == a.CurrentCustodyUserId)
                .Select(u => new { u.FullName, u.Email }).FirstOrDefaultAsync();
            vm.CustodyUserName = holder?.FullName;
            vm.CustodyUserEmail = holder?.Email;
        }

        vm.Tickets = await _db.MaintenanceTickets.AsNoTracking()
            .Where(t => t.AssetId == id)
            .OrderByDescending(t => t.ReportedAt).Take(20)
            .Select(t => new TicketRow
            {
                Id = t.Id, TicketNumber = t.TicketNumber, Title = t.Title,
                Type = t.Type, Priority = t.Priority, Status = t.Status,
                ReportedAt = t.ReportedAt, ResolvedAt = t.ResolvedAt,
                IsSlaBreached = t.IsSlaBreached
            }).ToListAsync();

        vm.CustodyHistory = await _db.CustodyLogs.AsNoTracking()
            .Where(c => c.AssetId == id)
            .OrderByDescending(c => c.ActionDate).Take(20)
            .Select(c => new CustodyRow
            {
                Id = c.Id, AssetId = c.AssetId, Action = c.Action, Status = c.Status,
                ActionDate = c.ActionDate, RespondedAt = c.RespondedAt,
                Reason = c.Reason, ResponseNote = c.ResponseNote,
                NewUserId = c.NewUserId, PreviousUserId = c.PreviousUserId,
                AssignedById = c.AssignedByUserId
            }).ToListAsync();

        await ResolveCustodyNamesAsync(vm.CustodyHistory);

        vm.Schedules = await _db.MaintenanceSchedules.AsNoTracking()
            .Where(s => s.AssetId == id)
            .OrderBy(s => s.NextDueDate)
            .Select(s => new ScheduleRow
            {
                Id = s.Id, Title = s.Title, Frequency = s.Frequency,
                Status = s.Status, NextDueDate = s.NextDueDate, LastRunDate = s.LastGeneratedAt
            }).ToListAsync();

        // الإهلاك — نجلب ثم نرتّب/نجمع على العميل (SQLite + decimal)
        var dep = await _db.DepreciationEntries.AsNoTracking()
            .Where(d => d.AssetId == id)
            .Select(d => new { d.Year, d.Month, d.DepreciationAmount, d.ClosingValue })
            .ToListAsync();

        vm.TotalDepreciated = dep.Sum(d => d.DepreciationAmount);
        vm.Depreciation = dep
            .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month).Take(12)
            .Select(d => new DepreciationRow
            {
                Year = d.Year, Month = d.Month,
                Amount = d.DepreciationAmount, BookValueAfter = d.ClosingValue
            }).ToList();

        var costs = await _db.MaintenanceTickets.AsNoTracking()
            .Where(t => t.AssetId == id)
            .Select(t => new { t.LaborCost, t.PartsCost }).ToListAsync();
        vm.TotalMaintenanceCost = costs.Sum(c => (c.LaborCost ?? 0m) + (c.PartsCost ?? 0m));

        return View(vm);
    }

    /// <summary>يحوّل معرّفات المستخدمين في سجل العهد إلى أسماء — استعلام واحد</summary>
    private async Task ResolveCustodyNamesAsync(List<CustodyRow> rows)
    {
        var ids = rows.SelectMany(r => new[] { r.NewUserId, r.PreviousUserId, r.AssignedById })
                      .Where(x => x != null).Select(x => x!).Distinct().ToList();
        if (ids.Count == 0) return;

        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var r in rows)
        {
            if (r.NewUserId != null && names.TryGetValue(r.NewUserId, out var n1)) r.NewUserName = n1;
            if (r.PreviousUserId != null && names.TryGetValue(r.PreviousUserId, out var n2)) r.PreviousUserName = n2;
            if (r.AssignedById != null && names.TryGetValue(r.AssignedById, out var n3)) r.AssignedByName = n3;
        }
    }

    // ────────────────────────────── QR ──────────────────────────────

    /// <summary>صفحة الهبوط بعد مسح كود QR — تحوّل للتفاصيل</summary>
    [Route("Assets/ByTag/{tag}")]
    public async Task<IActionResult> ByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return NotFoundOrForbidden();

        var id = await _db.Assets.AsNoTracking()
            .Where(a => a.AssetTag == tag.Trim())
            .Select(a => a.Id).FirstOrDefaultAsync();

        if (id == 0) return NotFoundOrForbidden();
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>واجهة مسح QR بالكاميرا (تحتاج HTTPS في الإنتاج)</summary>
    public IActionResult Scan() => View();

    /// <summary>يعيد صورة QR كـ PNG مباشرة (لا تُحفظ على القرص)</summary>
    public async Task<IActionResult> Qr(int id)
    {
        var tag = await _db.Assets.AsNoTracking()
            .Where(a => a.Id == id).Select(a => a.AssetTag).FirstOrDefaultAsync();

        if (tag == null) return NotFoundOrForbidden();

        var url = $"{Request.Scheme}://{Request.Host}/Assets/ByTag/{tag}";
        var png = _qr.GeneratePng(url, 8);
        return File(png, "image/png", $"{tag}.png");
    }

    /// <summary>صفحة طباعة ملصقات QR لمجموعة أصول</summary>
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Labels(string? ids)
    {
        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
            .Where(v => v > 0).Distinct().Take(60).ToList();

        var query = _db.Assets.AsNoTracking().AsQueryable();
        query = idList.Count > 0
            ? query.Where(a => idList.Contains(a.Id))
            : query.OrderByDescending(a => a.Id).Take(24);

        var rows = await query
            .Select(a => new AssetRow
            {
                Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr,
                CategoryName = a.Category!.NameAr,
                LocationName = a.Location != null ? a.Location.NameAr : null,
                CompanyName = a.Company!.NameAr
            }).ToListAsync();

        return View(rows);
    }

    // ────────────────────────────── إضافة ──────────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create()
    {
        var vm = new AssetFormViewModel
        {
            Status = AssetStatus.InStore,
            DepreciationMethod = DepreciationMethod.StraightLine,
            UsefulLifeYears = 5,
            IsAdmin = IsAdmin,
            CompanyId = Me.CompanyId
        };
        await FillFormLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Create(AssetFormViewModel vm)
    {
        var companyId = ResolveCompanyId(vm.CompanyId);
        if (companyId == null)
        {
            ModelState.AddModelError(nameof(vm.CompanyId), "يجب اختيار الشركة.");
        }

        await ValidateFormAsync(vm, companyId, null);

        if (!ModelState.IsValid)
        {
            vm.IsAdmin = IsAdmin;
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var seq = HttpContext.RequestServices.GetRequiredService<ISequenceService>();
        var tag = await seq.NextAssetTagAsync(companyId!.Value);

        var a = new Asset
        {
            CompanyId = companyId.Value,
            AssetTag = tag,
            NameAr = vm.NameAr.Trim(),
            NameEn = vm.NameEn?.Trim(),
            Description = vm.Description?.Trim(),
            CategoryId = vm.CategoryId,
            DepartmentId = vm.DepartmentId,
            LocationId = vm.LocationId,
            VendorId = vm.VendorId,
            Brand = vm.Brand?.Trim(),
            Model = vm.Model?.Trim(),
            SerialNumber = vm.SerialNumber?.Trim(),
            Specifications = vm.Specifications?.Trim(),
            Color = vm.Color?.Trim(),
            PurchaseDate = vm.PurchaseDate,
            PurchaseValue = vm.PurchaseValue,
            SalvageValue = vm.SalvageValue,
            BookValue = vm.PurchaseValue,       // القيمة الدفترية تبدأ = قيمة الشراء
            UsefulLifeYears = vm.UsefulLifeYears,
            DepreciationMethod = vm.DepreciationMethod,
            InvoiceNumber = vm.InvoiceNumber?.Trim(),
            WarrantyStartDate = vm.WarrantyStartDate,
            WarrantyEndDate = vm.WarrantyEndDate,
            WarrantyProvider = vm.WarrantyProvider?.Trim(),
            Status = vm.Status,
            Notes = vm.Notes?.Trim()
        };

        _db.Assets.Add(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", nameof(Asset), a.Id.ToString(), null, new { a.AssetTag, a.NameAr });

        ToastSuccess($"تم إضافة الأصل بنجاح — الوسم: {a.AssetTag}");
        return RedirectToAction(nameof(Details), new { id = a.Id });
    }

    // ────────────────────────────── تعديل ──────────────────────────────
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        var vm = new AssetFormViewModel
        {
            Id = a.Id, AssetTag = a.AssetTag, NameAr = a.NameAr, NameEn = a.NameEn,
            Description = a.Description, CategoryId = a.CategoryId,
            DepartmentId = a.DepartmentId, LocationId = a.LocationId, VendorId = a.VendorId,
            CompanyId = a.CompanyId,
            Brand = a.Brand, Model = a.Model, SerialNumber = a.SerialNumber,
            Specifications = a.Specifications, Color = a.Color,
            PurchaseDate = a.PurchaseDate, PurchaseValue = a.PurchaseValue,
            SalvageValue = a.SalvageValue, UsefulLifeYears = a.UsefulLifeYears,
            DepreciationMethod = a.DepreciationMethod, InvoiceNumber = a.InvoiceNumber,
            WarrantyStartDate = a.WarrantyStartDate, WarrantyEndDate = a.WarrantyEndDate,
            WarrantyProvider = a.WarrantyProvider, Status = a.Status, Notes = a.Notes,
            RowVersion = a.RowVersion != null ? Convert.ToBase64String(a.RowVersion) : null,
            IsAdmin = IsAdmin
        };

        await FillFormLookupsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(AssetFormViewModel vm)
    {
        var a = await _db.Assets.FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (a == null) return NotFoundOrForbidden();

        await ValidateFormAsync(vm, a.CompanyId, a.Id);

        if (!ModelState.IsValid)
        {
            vm.IsAdmin = IsAdmin;
            vm.AssetTag = a.AssetTag;
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        var before = new { a.NameAr, a.Status, a.LocationId, a.PurchaseValue };

        // التزامن المتفائل — إن عُدِّل السجل من مستخدم آخر نرفض الحفظ
        if (!string.IsNullOrEmpty(vm.RowVersion))
        {
            try { _db.Entry(a).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(vm.RowVersion); }
            catch (FormatException) { /* قيمة تالفة — نتجاهلها ونعتمد على الفحص التالي */ }
        }

        var oldLocation = a.LocationId;

        a.NameAr = vm.NameAr.Trim();
        a.NameEn = vm.NameEn?.Trim();
        a.Description = vm.Description?.Trim();
        a.CategoryId = vm.CategoryId;
        a.DepartmentId = vm.DepartmentId;
        a.LocationId = vm.LocationId;
        a.VendorId = vm.VendorId;
        a.Brand = vm.Brand?.Trim();
        a.Model = vm.Model?.Trim();
        a.SerialNumber = vm.SerialNumber?.Trim();
        a.Specifications = vm.Specifications?.Trim();
        a.Color = vm.Color?.Trim();
        a.PurchaseDate = vm.PurchaseDate;
        a.PurchaseValue = vm.PurchaseValue;
        a.SalvageValue = vm.SalvageValue;
        a.UsefulLifeYears = vm.UsefulLifeYears;
        a.DepreciationMethod = vm.DepreciationMethod;
        a.InvoiceNumber = vm.InvoiceNumber?.Trim();
        a.WarrantyStartDate = vm.WarrantyStartDate;
        a.WarrantyEndDate = vm.WarrantyEndDate;
        a.WarrantyProvider = vm.WarrantyProvider?.Trim();
        a.Status = vm.Status;
        a.Notes = vm.Notes?.Trim();

        // تسجيل النقل المكاني إن تغيّر الموقع
        if (oldLocation != a.LocationId)
        {
            _db.LocationLogs.Add(new LocationLog
            {
                CompanyId = a.CompanyId, AssetId = a.Id,
                FromLocationId = oldLocation, ToLocationId = a.LocationId,
                MovedByUserId = Me.UserId, Reason = "تعديل بيانات الأصل"
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty,
                "تم تعديل هذا الأصل بواسطة مستخدم آخر. أعد تحميل الصفحة وحاول مرة أخرى.");
            vm.IsAdmin = IsAdmin;
            vm.AssetTag = a.AssetTag;
            await FillFormLookupsAsync(vm);
            return View("Form", vm);
        }

        await _audit.LogAsync("Update", nameof(Asset), a.Id.ToString(), before,
            new { a.NameAr, a.Status, a.LocationId, a.PurchaseValue });

        ToastSuccess("تم حفظ التعديلات بنجاح.");
        return RedirectToAction(nameof(Details), new { id = a.Id });
    }

    // ────────────────────────────── استبعاد ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<IActionResult> Dispose(int id, string? reason)
    {
        var a = await _db.Assets.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFoundOrForbidden();

        if (a.CurrentCustodyUserId != null)
        {
            ToastError("لا يمكن استبعاد أصل في عهدة موظف — استرجع العهدة أولاً.");
            return RedirectToAction(nameof(Details), new { id });
        }

        a.Status = AssetStatus.Disposed;
        a.DisposalDate = DateTime.UtcNow;
        a.DisposalReason = string.IsNullOrWhiteSpace(reason) ? "استبعاد إداري" : reason.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Dispose", nameof(Asset), a.Id.ToString(), null, new { a.DisposalReason });

        ToastSuccess($"تم استبعاد الأصل {a.AssetTag}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ────────────────────────────── مساعدات ──────────────────────────────

    /// <summary>يحدد الشركة: المدير يعمل داخل شركته، ومدير النظام يختار</summary>
    private int? ResolveCompanyId(int? posted)
    {
        if (!IsAdmin) return Me.CompanyId;
        return posted;
    }

    /// <summary>تحقّقات لا تُغطّيها DataAnnotations (تفرد الرقم التسلسلي، منطق التواريخ)</summary>
    private async Task ValidateFormAsync(AssetFormViewModel vm, int? companyId, int? excludeId)
    {
        if (vm.WarrantyStartDate.HasValue && vm.WarrantyEndDate.HasValue
            && vm.WarrantyEndDate < vm.WarrantyStartDate)
        {
            ModelState.AddModelError(nameof(vm.WarrantyEndDate),
                "نهاية الضمان يجب أن تكون بعد بدايته.");
        }

        if (vm.PurchaseDate.HasValue && vm.PurchaseDate > DateTime.UtcNow.Date.AddDays(1))
        {
            ModelState.AddModelError(nameof(vm.PurchaseDate), "تاريخ الشراء لا يكون في المستقبل.");
        }

        if (vm.SalvageValue.HasValue && vm.PurchaseValue.HasValue
            && vm.SalvageValue > vm.PurchaseValue)
        {
            ModelState.AddModelError(nameof(vm.SalvageValue),
                "القيمة التخريدية لا تتجاوز قيمة الشراء.");
        }

        if (!string.IsNullOrWhiteSpace(vm.SerialNumber))
        {
            var sn = vm.SerialNumber.Trim();
            var dup = await _db.Assets.AsNoTracking()
                .AnyAsync(a => a.SerialNumber == sn && (excludeId == null || a.Id != excludeId));
            if (dup)
                ModelState.AddModelError(nameof(vm.SerialNumber),
                    "هذا الرقم التسلسلي مستخدم بالفعل في أصل آخر.");
        }

        // التصنيف يجب أن يكون من نفس الشركة (حاجز ضد تمرير معرّف من شركة أخرى)
        if (companyId.HasValue && vm.CategoryId > 0)
        {
            var okCat = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == vm.CategoryId);
            if (!okCat) ModelState.AddModelError(nameof(vm.CategoryId), "التصنيف غير صحيح.");
        }
    }

    private async Task FillFormLookupsAsync(AssetFormViewModel vm)
    {
        vm.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();

        vm.Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.NameAr)
            .Select(d => new LookupItem { Id = d.Id, Name = d.NameAr }).ToListAsync();

        vm.Locations = await _db.Locations.AsNoTracking().OrderBy(l => l.NameAr)
            .Select(l => new LookupItem { Id = l.Id, Name = l.NameAr }).ToListAsync();

        vm.Vendors = await _db.Vendors.AsNoTracking().OrderBy(v => v.NameAr)
            .Select(v => new LookupItem { Id = v.Id, Name = v.NameAr }).ToListAsync();

        if (IsAdmin)
        {
            vm.Companies = await _db.Companies.AsNoTracking().OrderBy(c => c.NameAr)
                .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();
        }
    }
}
