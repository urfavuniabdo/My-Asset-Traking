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
/// لوحة الإدارة — المستخدمون والبيانات المرجعية (شركات، إدارات، مواقع،
/// تصنيفات، مورّدون، سياسات SLA) وسجل التدقيق.
/// عزل الشركات: مدير الشركة يرى/يعدّل بيانات شركته فقط؛ مدير النظام يرى الكل.
/// </summary>
[Authorize(Policy = Policies.ManagerOrAdmin)]
public class AdminController : BaseController
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public AdminController(AppDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles)
    {
        _db = db;
        _users = users;
        _roles = roles;
    }

    // ═════════════════════ لوحة الإدارة ═════════════════════
    public async Task<IActionResult> Index()
    {
        var uq = ScopedUsers();

        var vm = new AdminHomeViewModel
        {
            IsAdmin = IsAdmin,
            Users = await uq.CountAsync(),
            ActiveUsers = await uq.CountAsync(u => u.IsActive),
            Companies = IsAdmin ? await _db.Companies.CountAsync() : 1,
            Departments = await _db.Departments.CountAsync(),
            Locations = await _db.Locations.CountAsync(),
            Categories = await _db.Categories.CountAsync(),
            Vendors = await _db.Vendors.CountAsync(),
            SlaPolicies = await _db.SlaPolicies.CountAsync(),
            AuditLogs = await _db.AuditLogs.CountAsync()
        };

        if (Me.CompanyId != null)
            vm.CompanyName = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == Me.CompanyId)
                .Select(c => c.NameAr).FirstOrDefaultAsync();

        return View(vm);
    }

    // ═════════════════════ المستخدمون ═════════════════════
    public async Task<IActionResult> Users(string? q, string? role, int? companyId,
        bool? active, int page = 1)
    {
        if (page < 1) page = 1;

        var vm = new UserListViewModel
        {
            Q = Clean(q), Role = Clean(role), CompanyId = companyId,
            Active = active, Page = page, IsAdmin = IsAdmin
        };

        var query = ScopedUsers();

        if (!string.IsNullOrEmpty(vm.Q))
        {
            var term = vm.Q;
            query = query.Where(u => u.FullName.Contains(term)
                                     || (u.Email != null && u.Email.Contains(term))
                                     || (u.EmployeeNumber != null && u.EmployeeNumber.Contains(term))
                                     || (u.JobTitle != null && u.JobTitle.Contains(term)));
        }

        if (IsAdmin && companyId.HasValue)
            query = query.Where(u => u.CompanyId == companyId);

        if (active.HasValue)
            query = query.Where(u => u.IsActive == active.Value);

        // فلترة الدور تحتاج جدول UserRoles
        if (!string.IsNullOrEmpty(vm.Role))
        {
            var roleId = await _db.Roles.Where(r => r.Name == vm.Role)
                .Select(r => r.Id).FirstOrDefaultAsync();
            if (roleId != null)
            {
                var idsInRole = _db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId);
                query = query.Where(u => idsInRole.Contains(u.Id));
            }
        }

        vm.TotalCount = await query.CountAsync();

        var rows = await query.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * vm.PageSize).Take(vm.PageSize)
            .Select(u => new UserRow
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email,
                PhoneNumber = u.PhoneNumber, JobTitle = u.JobTitle,
                EmployeeNumber = u.EmployeeNumber,
                CompanyName = u.Company != null ? u.Company.NameAr : null,
                DepartmentName = u.Department != null ? u.Department.NameAr : null,
                IsActive = u.IsActive,
                LockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                MustChangePassword = u.MustChangePassword,
                CreatedAt = u.CreatedAt, LastLoginAt = u.LastLoginAt
            }).ToListAsync();

        await AttachRolesAndCustodyAsync(rows);
        vm.Items = rows;

        // عدّادات الأدوار
        var counts = await RoleCountsAsync();
        vm.CountAdmin = counts.GetValueOrDefault(Roles.Admin);
        vm.CountManager = counts.GetValueOrDefault(Roles.CompanyManager);
        vm.CountTechnician = counts.GetValueOrDefault(Roles.Technician);
        vm.CountEmployee = counts.GetValueOrDefault(Roles.Employee);

        if (IsAdmin) vm.Companies = await CompaniesAsync();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        var vm = new UserFormViewModel { CompanyId = Me.CompanyId };
        await FillUserFormAsync(vm);
        return View("UserForm", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserFormViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "كلمة المرور مطلوبة عند إنشاء مستخدم جديد");

        // مدير الشركة لا يستطيع إنشاء مدير نظام
        if (!IsAdmin && vm.Role == Roles.Admin)
            ModelState.AddModelError(nameof(vm.Role), "لا تملك صلاحية إنشاء مدير نظام");

        var companyId = IsAdmin ? vm.CompanyId : Me.CompanyId;
        if (vm.Role != Roles.Admin && companyId == null)
            ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة لهذا الدور");

        if (await _users.FindByEmailAsync(vm.Email ?? "") != null)
            ModelState.AddModelError(nameof(vm.Email), "هذا البريد مستخدم بالفعل");

        if (!ModelState.IsValid)
        {
            await FillUserFormAsync(vm);
            return View("UserForm", vm);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName.Trim(),
            PhoneNumber = Clean(vm.PhoneNumber),
            JobTitle = Clean(vm.JobTitle),
            EmployeeNumber = Clean(vm.EmployeeNumber),
            NationalId = Clean(vm.NationalId),
            CompanyId = vm.Role == Roles.Admin ? null : companyId,
            DepartmentId = vm.DepartmentId,
            IsActive = vm.IsActive,
            MustChangePassword = vm.MustChangePassword,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, vm.Password!);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", Translate(e));
            await FillUserFormAsync(vm);
            return View("UserForm", vm);
        }

        await _users.AddToRoleAsync(user, vm.Role);
        await LogAsync("CreateUser", nameof(ApplicationUser), user.Id,
            null, $"{user.FullName} / {user.Email} / {vm.Role}");

        ToastSuccess($"تم إنشاء المستخدم «{user.FullName}» بنجاح");
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFoundOrForbidden();

        var roles = await _users.GetRolesAsync(user);

        var vm = new UserFormViewModel
        {
            Id = user.Id, FullName = user.FullName, Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber, JobTitle = user.JobTitle,
            EmployeeNumber = user.EmployeeNumber, NationalId = user.NationalId,
            Role = roles.FirstOrDefault() ?? Roles.Employee,
            CompanyId = user.CompanyId, DepartmentId = user.DepartmentId,
            IsActive = user.IsActive, MustChangePassword = user.MustChangePassword
        };

        await FillUserFormAsync(vm);
        return View("UserForm", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(UserFormViewModel vm)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == vm.Id);
        if (user == null) return NotFoundOrForbidden();

        if (!IsAdmin && vm.Role == Roles.Admin)
            ModelState.AddModelError(nameof(vm.Role), "لا تملك صلاحية ترقية مستخدم لمدير نظام");

        // منع المستخدم من تعطيل حسابه أو تخفيض دوره بنفسه
        var isSelf = user.Id == Me.UserId;
        if (isSelf && !vm.IsActive)
            ModelState.AddModelError(nameof(vm.IsActive), "لا يمكنك تعطيل حسابك الحالي");

        var dupEmail = await _db.Users.AnyAsync(u => u.Email == vm.Email && u.Id != user.Id);
        if (dupEmail) ModelState.AddModelError(nameof(vm.Email), "هذا البريد مستخدم بحساب آخر");

        // كلمة المرور اختيارية في التعديل — تُغيَّر فقط إن أُدخلت
        if (!string.IsNullOrWhiteSpace(vm.Password) && vm.Password.Length < 8)
            ModelState.AddModelError(nameof(vm.Password), "كلمة المرور 8 أحرف على الأقل");

        if (!ModelState.IsValid)
        {
            await FillUserFormAsync(vm);
            return View("UserForm", vm);
        }

        var before = $"{user.FullName} / {user.Email} / active={user.IsActive}";

        user.FullName = vm.FullName.Trim();
        user.Email = vm.Email;
        user.UserName = vm.Email;
        user.PhoneNumber = Clean(vm.PhoneNumber);
        user.JobTitle = Clean(vm.JobTitle);
        user.EmployeeNumber = Clean(vm.EmployeeNumber);
        user.NationalId = Clean(vm.NationalId);
        user.DepartmentId = vm.DepartmentId;
        user.IsActive = vm.IsActive;
        user.MustChangePassword = vm.MustChangePassword;

        if (IsAdmin)
            user.CompanyId = vm.Role == Roles.Admin ? null : vm.CompanyId;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
        {
            foreach (var e in upd.Errors) ModelState.AddModelError("", Translate(e));
            await FillUserFormAsync(vm);
            return View("UserForm", vm);
        }

        // تحديث الدور
        var current = await _users.GetRolesAsync(user);
        if (!current.Contains(vm.Role) && !isSelf)
        {
            if (current.Count > 0) await _users.RemoveFromRolesAsync(user, current);
            await _users.AddToRoleAsync(user, vm.Role);
        }

        // تغيير كلمة المرور إن طُلب
        if (!string.IsNullOrWhiteSpace(vm.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var pr = await _users.ResetPasswordAsync(user, token, vm.Password);
            if (!pr.Succeeded)
            {
                foreach (var e in pr.Errors) ModelState.AddModelError("", Translate(e));
                await FillUserFormAsync(vm);
                return View("UserForm", vm);
            }
            ToastInfo("تم تغيير كلمة المرور");
        }

        await LogAsync("EditUser", nameof(ApplicationUser), user.Id, before,
            $"{user.FullName} / {user.Email} / active={user.IsActive}");

        ToastSuccess($"تم تحديث بيانات «{user.FullName}»");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFoundOrForbidden();

        if (user.Id == Me.UserId)
        {
            ToastError("لا يمكنك تعطيل حسابك الحالي");
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        await LogAsync("ToggleUser", nameof(ApplicationUser), user.Id, null,
            $"active={user.IsActive}");

        ToastSuccess(user.IsActive
            ? $"تم تفعيل حساب «{user.FullName}»"
            : $"تم تعطيل حساب «{user.FullName}»");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(string id)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFoundOrForbidden();

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await LogAsync("UnlockUser", nameof(ApplicationUser), user.Id, null, "unlocked");

        ToastSuccess($"تم فتح قفل حساب «{user.FullName}»");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await ScopedUsers().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFoundOrForbidden();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            ToastError("كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل");
            return RedirectToAction(nameof(Users));
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var res = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!res.Succeeded)
        {
            ToastError(string.Join(" · ", res.Errors.Select(Translate)));
            return RedirectToAction(nameof(Users));
        }

        user.MustChangePassword = true;
        await _users.UpdateAsync(user);
        await LogAsync("ResetPassword", nameof(ApplicationUser), user.Id, null, "password reset");

        ToastSuccess($"تم تعيين كلمة مرور جديدة للمستخدم «{user.FullName}»");
        return RedirectToAction(nameof(Users));
    }

    // ═════════════════════ الشركات ═════════════════════
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Companies(string? q, bool? active)
    {
        var query = _db.Companies.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null)
            query = query.Where(c => c.NameAr.Contains(term) || c.Code.Contains(term)
                                     || (c.City != null && c.City.Contains(term)));
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var rows = await query.OrderBy(c => c.NameAr)
            .Select(c => new RefRow
            {
                Id = c.Id, NameAr = c.NameAr, NameEn = c.NameEn, Code = c.Code,
                IsActive = c.IsActive,
                AssetsCount = c.Assets.Count,
                Extra = c.City, Extra2 = c.Phone
            }).ToListAsync();

        return View("RefList", new RefListViewModel
        {
            Kind = "companies", TitleAr = "الشركات", Icon = "bi-buildings",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "المدينة", Extra2Header = "الهاتف"
        });
    }

    [HttpGet, Authorize(Policy = Policies.AdminOnly)]
    public IActionResult CreateCompany() => View("CompanyForm", new CompanyFormViewModel());

    [HttpPost, Authorize(Policy = Policies.AdminOnly), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompany(CompanyFormViewModel vm)
    {
        if (await _db.Companies.AnyAsync(c => c.Code == vm.Code))
            ModelState.AddModelError(nameof(vm.Code), "هذا الكود مستخدم بالفعل");

        if (!ModelState.IsValid) return View("CompanyForm", vm);

        var e = new Company();
        MapCompany(vm, e);
        _db.Companies.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateCompany", nameof(Company), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة الشركة «{e.NameAr}»");
        return RedirectToAction(nameof(Companies));
    }

    [HttpGet, Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> EditCompany(int id)
    {
        var e = await _db.Companies.FindAsync(id);
        if (e == null) return NotFoundOrForbidden();

        return View("CompanyForm", new CompanyFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            TaxNumber = e.TaxNumber, CommercialRegister = e.CommercialRegister,
            Address = e.Address, City = e.City, Phone = e.Phone, Email = e.Email,
            Website = e.Website, IsActive = e.IsActive
        });
    }

    [HttpPost, Authorize(Policy = Policies.AdminOnly), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCompany(CompanyFormViewModel vm)
    {
        var e = await _db.Companies.FindAsync(vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (await _db.Companies.AnyAsync(c => c.Code == vm.Code && c.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.Code), "هذا الكود مستخدم بشركة أخرى");

        if (!ModelState.IsValid) return View("CompanyForm", vm);

        var before = e.NameAr;
        MapCompany(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditCompany", nameof(Company), e.Id.ToString(), before, e.NameAr);

        ToastSuccess($"تم تحديث الشركة «{e.NameAr}»");
        return RedirectToAction(nameof(Companies));
    }

    private static void MapCompany(CompanyFormViewModel vm, Company e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.NameEn = vm.NameEn?.Trim();
        e.Code = vm.Code.Trim().ToUpperInvariant();
        e.TaxNumber = vm.TaxNumber?.Trim();
        e.CommercialRegister = vm.CommercialRegister?.Trim();
        e.Address = vm.Address?.Trim();
        e.City = vm.City?.Trim();
        e.Phone = vm.Phone?.Trim();
        e.Email = vm.Email?.Trim();
        e.Website = vm.Website?.Trim();
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ الإدارات ═════════════════════
    public async Task<IActionResult> Departments(string? q, bool? active)
    {
        var query = _db.Departments.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null)
            query = query.Where(d => d.NameAr.Contains(term)
                                     || (d.Code != null && d.Code.Contains(term)));
        if (active.HasValue) query = query.Where(d => d.IsActive == active.Value);

        var rows = await query.OrderBy(d => d.NameAr)
            .Select(d => new RefRow
            {
                Id = d.Id, NameAr = d.NameAr, NameEn = d.NameEn, Code = d.Code,
                CompanyName = d.Company != null ? d.Company.NameAr : null,
                IsActive = d.IsActive, AssetsCount = d.Assets.Count,
                Extra = d.CostCenter
            }).ToListAsync();

        return View("RefList", new RefListViewModel
        {
            Kind = "departments", TitleAr = "الإدارات والأقسام", Icon = "bi-diagram-3",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "مركز التكلفة"
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateDepartment()
    {
        var vm = new DepartmentFormViewModel { CompanyId = Me.CompanyId };
        await FillDepartmentFormAsync(vm);
        return View("DepartmentForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartment(DepartmentFormViewModel vm)
    {
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null) ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillDepartmentFormAsync(vm);
            return View("DepartmentForm", vm);
        }

        var e = new Department { CompanyId = companyId!.Value };
        MapDepartment(vm, e);
        _db.Departments.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateDepartment", nameof(Department), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة الإدارة «{e.NameAr}»");
        return RedirectToAction(nameof(Departments));
    }

    [HttpGet]
    public async Task<IActionResult> EditDepartment(int id)
    {
        var e = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new DepartmentFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            ManagerUserId = e.ManagerUserId, CostCenter = e.CostCenter,
            CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillDepartmentFormAsync(vm);
        return View("DepartmentForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDepartment(DepartmentFormViewModel vm)
    {
        var e = await _db.Departments.FirstOrDefaultAsync(d => d.Id == vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (!ModelState.IsValid)
        {
            await FillDepartmentFormAsync(vm);
            return View("DepartmentForm", vm);
        }

        var before = e.NameAr;
        MapDepartment(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditDepartment", nameof(Department), e.Id.ToString(), before, e.NameAr);

        ToastSuccess($"تم تحديث الإدارة «{e.NameAr}»");
        return RedirectToAction(nameof(Departments));
    }

    private static void MapDepartment(DepartmentFormViewModel vm, Department e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.NameEn = vm.NameEn?.Trim();
        e.Code = vm.Code?.Trim();
        e.ManagerUserId = string.IsNullOrWhiteSpace(vm.ManagerUserId) ? null : vm.ManagerUserId;
        e.CostCenter = vm.CostCenter?.Trim();
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ المواقع ═════════════════════
    public async Task<IActionResult> Locations(string? q, bool? active)
    {
        var query = _db.Locations.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null)
            query = query.Where(l => l.NameAr.Contains(term)
                                     || (l.Code != null && l.Code.Contains(term))
                                     || (l.City != null && l.City.Contains(term)));
        if (active.HasValue) query = query.Where(l => l.IsActive == active.Value);

        var raw = await query.OrderBy(l => l.NameAr)
            .Select(l => new
            {
                l.Id, l.NameAr, l.NameEn, l.Code, l.Type, l.City, l.Governorate,
                CompanyName = l.Company != null ? l.Company.NameAr : null,
                l.IsActive, AssetsCount = l.Assets.Count
            }).ToListAsync();

        var rows = raw.Select(l => new RefRow
        {
            Id = l.Id, NameAr = l.NameAr, NameEn = l.NameEn, Code = l.Code,
            CompanyName = l.CompanyName, IsActive = l.IsActive, AssetsCount = l.AssetsCount,
            Extra = Helpers.DisplayHelper.Name(l.Type),
            Extra2 = string.Join(" — ", new[] { l.City, l.Governorate }
                .Where(x => !string.IsNullOrEmpty(x)))
        }).ToList();

        return View("RefList", new RefListViewModel
        {
            Kind = "locations", TitleAr = "المواقع", Icon = "bi-geo-alt",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "النوع", Extra2Header = "المدينة / المحافظة"
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateLocation()
    {
        var vm = new LocationFormViewModel { CompanyId = Me.CompanyId };
        await FillLocationFormAsync(vm);
        return View("LocationForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLocation(LocationFormViewModel vm)
    {
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null) ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillLocationFormAsync(vm);
            return View("LocationForm", vm);
        }

        var e = new Location { CompanyId = companyId!.Value };
        MapLocation(vm, e);
        _db.Locations.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateLocation", nameof(Location), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة الموقع «{e.NameAr}»");
        return RedirectToAction(nameof(Locations));
    }

    [HttpGet]
    public async Task<IActionResult> EditLocation(int id)
    {
        var e = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new LocationFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            Type = e.Type, Address = e.Address, City = e.City, Governorate = e.Governorate,
            ContactPerson = e.ContactPerson, ContactPhone = e.ContactPhone,
            CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillLocationFormAsync(vm);
        return View("LocationForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLocation(LocationFormViewModel vm)
    {
        var e = await _db.Locations.FirstOrDefaultAsync(l => l.Id == vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (!ModelState.IsValid)
        {
            await FillLocationFormAsync(vm);
            return View("LocationForm", vm);
        }

        var before = e.NameAr;
        MapLocation(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditLocation", nameof(Location), e.Id.ToString(), before, e.NameAr);

        ToastSuccess($"تم تحديث الموقع «{e.NameAr}»");
        return RedirectToAction(nameof(Locations));
    }

    private static void MapLocation(LocationFormViewModel vm, Location e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.NameEn = vm.NameEn?.Trim();
        e.Code = vm.Code?.Trim();
        e.Type = vm.Type;
        e.Address = vm.Address?.Trim();
        e.City = vm.City?.Trim();
        e.Governorate = vm.Governorate?.Trim();
        e.ContactPerson = vm.ContactPerson?.Trim();
        e.ContactPhone = vm.ContactPhone?.Trim();
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ التصنيفات ═════════════════════
    public async Task<IActionResult> Categories(string? q, bool? active)
    {
        var query = _db.Categories.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null)
            query = query.Where(c => c.NameAr.Contains(term)
                                     || (c.Code != null && c.Code.Contains(term)));
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var raw = await query.OrderBy(c => c.NameAr)
            .Select(c => new
            {
                c.Id, c.NameAr, c.NameEn, c.Code, c.UsefulLifeYears, c.DepreciationMethod,
                ParentName = c.ParentCategory != null ? c.ParentCategory.NameAr : null,
                CompanyName = c.Company != null ? c.Company.NameAr : null,
                c.IsActive, AssetsCount = c.Assets.Count
            }).ToListAsync();

        var rows = raw.Select(c => new RefRow
        {
            Id = c.Id, NameAr = c.NameAr, NameEn = c.NameEn, Code = c.Code,
            CompanyName = c.CompanyName, IsActive = c.IsActive, AssetsCount = c.AssetsCount,
            Extra = c.UsefulLifeYears.HasValue ? $"{c.UsefulLifeYears} سنة" : null,
            Extra2 = Helpers.DisplayHelper.Name(c.DepreciationMethod)
                     + (c.ParentName != null ? $" · تحت: {c.ParentName}" : "")
        }).ToList();

        return View("RefList", new RefListViewModel
        {
            Kind = "categories", TitleAr = "تصنيفات الأصول", Icon = "bi-tags",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "العمر الإنتاجي", Extra2Header = "طريقة الإهلاك"
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateCategory()
    {
        var vm = new CategoryFormViewModel { CompanyId = Me.CompanyId };
        await FillCategoryFormAsync(vm);
        return View("CategoryForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryFormViewModel vm)
    {
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null) ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillCategoryFormAsync(vm);
            return View("CategoryForm", vm);
        }

        var e = new Category { CompanyId = companyId!.Value };
        MapCategory(vm, e);
        _db.Categories.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateCategory", nameof(Category), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة التصنيف «{e.NameAr}»");
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var e = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new CategoryFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            ParentCategoryId = e.ParentCategoryId, UsefulLifeYears = e.UsefulLifeYears,
            SalvageRate = e.SalvageRate, DepreciationMethod = e.DepreciationMethod,
            Icon = e.Icon, CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillCategoryFormAsync(vm);
        return View("CategoryForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel vm)
    {
        var e = await _db.Categories.FirstOrDefaultAsync(c => c.Id == vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (vm.ParentCategoryId == vm.Id)
            ModelState.AddModelError(nameof(vm.ParentCategoryId), "لا يمكن أن يكون التصنيف أباً لنفسه");

        if (!ModelState.IsValid)
        {
            await FillCategoryFormAsync(vm);
            return View("CategoryForm", vm);
        }

        var before = e.NameAr;
        MapCategory(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditCategory", nameof(Category), e.Id.ToString(), before, e.NameAr);

        ToastSuccess($"تم تحديث التصنيف «{e.NameAr}»");
        return RedirectToAction(nameof(Categories));
    }

    private static void MapCategory(CategoryFormViewModel vm, Category e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.NameEn = vm.NameEn?.Trim();
        e.Code = vm.Code?.Trim();
        e.ParentCategoryId = vm.ParentCategoryId == 0 ? null : vm.ParentCategoryId;
        e.UsefulLifeYears = vm.UsefulLifeYears;
        e.SalvageRate = vm.SalvageRate;
        e.DepreciationMethod = vm.DepreciationMethod;
        e.Icon = vm.Icon?.Trim();
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ المورّدون ═════════════════════
    public async Task<IActionResult> Vendors(string? q, bool? active)
    {
        var query = _db.Vendors.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null)
            query = query.Where(v => v.NameAr.Contains(term)
                                     || (v.Code != null && v.Code.Contains(term))
                                     || (v.ContactPerson != null && v.ContactPerson.Contains(term)));
        if (active.HasValue) query = query.Where(v => v.IsActive == active.Value);

        var rows = await query.OrderBy(v => v.NameAr)
            .Select(v => new RefRow
            {
                Id = v.Id, NameAr = v.NameAr, NameEn = v.NameEn, Code = v.Code,
                CompanyName = v.Company != null ? v.Company.NameAr : null,
                IsActive = v.IsActive, AssetsCount = v.Assets.Count,
                Extra = v.ContactPerson, Extra2 = v.Phone
            }).ToListAsync();

        return View("RefList", new RefListViewModel
        {
            Kind = "vendors", TitleAr = "المورّدون", Icon = "bi-truck",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "الشخص المسؤول", Extra2Header = "الهاتف"
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateVendor()
    {
        var vm = new VendorFormViewModel { CompanyId = Me.CompanyId };
        await FillVendorFormAsync(vm);
        return View("VendorForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVendor(VendorFormViewModel vm)
    {
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null) ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (!ModelState.IsValid)
        {
            await FillVendorFormAsync(vm);
            return View("VendorForm", vm);
        }

        var e = new Vendor { CompanyId = companyId!.Value };
        MapVendor(vm, e);
        _db.Vendors.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateVendor", nameof(Vendor), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة المورّد «{e.NameAr}»");
        return RedirectToAction(nameof(Vendors));
    }

    [HttpGet]
    public async Task<IActionResult> EditVendor(int id)
    {
        var e = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new VendorFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, NameEn = e.NameEn, Code = e.Code,
            ContactPerson = e.ContactPerson, Phone = e.Phone, Email = e.Email,
            Address = e.Address, TaxNumber = e.TaxNumber, Rating = e.Rating,
            Notes = e.Notes, CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillVendorFormAsync(vm);
        return View("VendorForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVendor(VendorFormViewModel vm)
    {
        var e = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (!ModelState.IsValid)
        {
            await FillVendorFormAsync(vm);
            return View("VendorForm", vm);
        }

        var before = e.NameAr;
        MapVendor(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditVendor", nameof(Vendor), e.Id.ToString(), before, e.NameAr);

        ToastSuccess($"تم تحديث المورّد «{e.NameAr}»");
        return RedirectToAction(nameof(Vendors));
    }

    private static void MapVendor(VendorFormViewModel vm, Vendor e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.NameEn = vm.NameEn?.Trim();
        e.Code = vm.Code?.Trim();
        e.ContactPerson = vm.ContactPerson?.Trim();
        e.Phone = vm.Phone?.Trim();
        e.Email = vm.Email?.Trim();
        e.Address = vm.Address?.Trim();
        e.TaxNumber = vm.TaxNumber?.Trim();
        e.Rating = vm.Rating;
        e.Notes = vm.Notes?.Trim();
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ سياسات SLA ═════════════════════
    public async Task<IActionResult> Sla(string? q, bool? active)
    {
        var query = _db.SlaPolicies.AsNoTracking().AsQueryable();
        var term = Clean(q);
        if (term != null) query = query.Where(s => s.NameAr.Contains(term));
        if (active.HasValue) query = query.Where(s => s.IsActive == active.Value);

        var raw = await query.OrderBy(s => s.Priority).ThenBy(s => s.NameAr)
            .Select(s => new
            {
                s.Id, s.NameAr, s.Priority, s.ResponseHours, s.ResolutionHours,
                s.EscalationWarningHours,
                CompanyName = s.Company != null ? s.Company.NameAr : null,
                s.IsActive
            }).ToListAsync();

        var rows = raw.Select(s => new RefRow
        {
            Id = s.Id, NameAr = s.NameAr,
            Code = Helpers.DisplayHelper.Name(s.Priority),
            CompanyName = s.CompanyName, IsActive = s.IsActive,
            Extra = $"استجابة {s.ResponseHours} س · حل {s.ResolutionHours} س",
            Extra2 = $"تنبيه قبل {s.EscalationWarningHours} س"
        }).ToList();

        return View("RefList", new RefListViewModel
        {
            Kind = "sla", TitleAr = "سياسات مستوى الخدمة (SLA)", Icon = "bi-stopwatch",
            Items = rows, Q = term, Active = active, IsAdmin = IsAdmin,
            ExtraHeader = "المدد المستهدفة", Extra2Header = "التنبيه"
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateSla()
    {
        var vm = new SlaFormViewModel { CompanyId = Me.CompanyId };
        await FillSlaFormAsync(vm);
        return View("SlaForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSla(SlaFormViewModel vm)
    {
        var companyId = Me.CompanyId ?? vm.CompanyId;
        if (companyId == null) ModelState.AddModelError(nameof(vm.CompanyId), "يجب تحديد الشركة");

        if (vm.ResolutionHours < vm.ResponseHours)
            ModelState.AddModelError(nameof(vm.ResolutionHours),
                "مدة الحل يجب أن تكون أكبر من أو تساوي مدة الاستجابة");

        if (!ModelState.IsValid)
        {
            await FillSlaFormAsync(vm);
            return View("SlaForm", vm);
        }

        var e = new SlaPolicy { CompanyId = companyId!.Value };
        MapSla(vm, e);
        _db.SlaPolicies.Add(e);
        await _db.SaveChangesAsync();
        await LogAsync("CreateSla", nameof(SlaPolicy), e.Id.ToString(), null, e.NameAr);

        ToastSuccess($"تمت إضافة سياسة «{e.NameAr}»");
        return RedirectToAction(nameof(Sla));
    }

    [HttpGet]
    public async Task<IActionResult> EditSla(int id)
    {
        var e = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Id == id);
        if (e == null) return NotFoundOrForbidden();

        var vm = new SlaFormViewModel
        {
            Id = e.Id, NameAr = e.NameAr, Priority = e.Priority,
            ResponseHours = e.ResponseHours, ResolutionHours = e.ResolutionHours,
            EscalationWarningHours = e.EscalationWarningHours,
            CompanyId = e.CompanyId, IsActive = e.IsActive
        };
        await FillSlaFormAsync(vm);
        return View("SlaForm", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSla(SlaFormViewModel vm)
    {
        var e = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Id == vm.Id);
        if (e == null) return NotFoundOrForbidden();

        if (vm.ResolutionHours < vm.ResponseHours)
            ModelState.AddModelError(nameof(vm.ResolutionHours),
                "مدة الحل يجب أن تكون أكبر من أو تساوي مدة الاستجابة");

        if (!ModelState.IsValid)
        {
            await FillSlaFormAsync(vm);
            return View("SlaForm", vm);
        }

        var before = $"{e.NameAr} / {e.ResponseHours}h / {e.ResolutionHours}h";
        MapSla(vm, e);
        await _db.SaveChangesAsync();
        await LogAsync("EditSla", nameof(SlaPolicy), e.Id.ToString(), before,
            $"{e.NameAr} / {e.ResponseHours}h / {e.ResolutionHours}h");

        ToastSuccess($"تم تحديث سياسة «{e.NameAr}»");
        return RedirectToAction(nameof(Sla));
    }

    private static void MapSla(SlaFormViewModel vm, SlaPolicy e)
    {
        e.NameAr = vm.NameAr.Trim();
        e.Priority = vm.Priority;
        e.ResponseHours = vm.ResponseHours;
        e.ResolutionHours = vm.ResolutionHours;
        e.EscalationWarningHours = vm.EscalationWarningHours;
        e.IsActive = vm.IsActive;
    }

    // ═════════════════════ تفعيل/تعطيل عام ═════════════════════
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRef(string kind, int id)
    {
        string? name = null;

        switch (kind)
        {
            case "companies":
                if (!IsAdmin) return NotFoundOrForbidden();
                var c = await _db.Companies.FindAsync(id);
                if (c == null) return NotFoundOrForbidden();
                c.IsActive = !c.IsActive; name = c.NameAr;
                break;
            case "departments":
                var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
                if (d == null) return NotFoundOrForbidden();
                d.IsActive = !d.IsActive; name = d.NameAr;
                break;
            case "locations":
                var l = await _db.Locations.FirstOrDefaultAsync(x => x.Id == id);
                if (l == null) return NotFoundOrForbidden();
                l.IsActive = !l.IsActive; name = l.NameAr;
                break;
            case "categories":
                var g = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id);
                if (g == null) return NotFoundOrForbidden();
                g.IsActive = !g.IsActive; name = g.NameAr;
                break;
            case "vendors":
                var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Id == id);
                if (v == null) return NotFoundOrForbidden();
                v.IsActive = !v.IsActive; name = v.NameAr;
                break;
            case "sla":
                var s = await _db.SlaPolicies.FirstOrDefaultAsync(x => x.Id == id);
                if (s == null) return NotFoundOrForbidden();
                s.IsActive = !s.IsActive; name = s.NameAr;
                break;
            default:
                return NotFoundOrForbidden();
        }

        await _db.SaveChangesAsync();
        await LogAsync("ToggleRef", kind, id.ToString(), null, name);
        ToastSuccess($"تم تغيير حالة «{name}»");

        return RedirectToAction(ActionForKind(kind));
    }

    private static string ActionForKind(string kind) => kind switch
    {
        "companies" => nameof(Companies),
        "departments" => nameof(Departments),
        "locations" => nameof(Locations),
        "categories" => nameof(Categories),
        "vendors" => nameof(Vendors),
        "sla" => nameof(Sla),
        _ => nameof(Index)
    };

    // ═════════════════════ سجل التدقيق ═════════════════════
    // ملاحظة: لا نسمي المعامل "action" لأنه يتعارض مع قيمة المسار (Route Value) الخاصة بالأكشن
    // فيُربَط تلقائياً بالقيمة "AuditLog" ويُفرغ النتائج. نستخدم "act" بدلاً منه.
    public async Task<IActionResult> AuditLog(
        [FromQuery] string? q,
        [FromQuery(Name = "act")] string? act,
        [FromQuery] string? entityName,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1)
    {
        if (page < 1) page = 1;

        var vm = new AuditLogViewModel
        {
            Q = Clean(q), Action = Clean(act), EntityName = Clean(entityName),
            From = from, To = to, Page = page
        };

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!IsAdmin)
        {
            var cid = Me.CompanyId;
            query = query.Where(a => a.CompanyId == cid);
        }

        if (!string.IsNullOrEmpty(vm.Q))
        {
            var term = vm.Q;
            query = query.Where(a => (a.UserName != null && a.UserName.Contains(term))
                                     || a.Action.Contains(term)
                                     || a.EntityName.Contains(term)
                                     || (a.EntityId != null && a.EntityId.Contains(term)));
        }

        if (!string.IsNullOrEmpty(vm.Action)) query = query.Where(a => a.Action == vm.Action);
        if (!string.IsNullOrEmpty(vm.EntityName)) query = query.Where(a => a.EntityName == vm.EntityName);
        if (from.HasValue) query = query.Where(a => a.OccurredAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(a => a.OccurredAt <= to.Value.Date.AddDays(1));

        vm.TotalCount = await query.CountAsync();

        vm.Items = await query.OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * vm.PageSize).Take(vm.PageSize)
            .Select(a => new AuditLogRow
            {
                Id = a.Id, UserName = a.UserName, Action = a.Action,
                EntityName = a.EntityName, EntityId = a.EntityId,
                OldValues = a.OldValues, NewValues = a.NewValues,
                IpAddress = a.IpAddress, OccurredAt = a.OccurredAt
            }).ToListAsync();

        vm.Actions = await _db.AuditLogs.AsNoTracking()
            .Select(a => a.Action).Distinct().ToListAsync();
        vm.Actions = vm.Actions.OrderBy(x => x, StringComparer.Ordinal).Take(60).ToList();

        vm.Entities = await _db.AuditLogs.AsNoTracking()
            .Select(a => a.EntityName).Distinct().ToListAsync();
        vm.Entities = vm.Entities.OrderBy(x => x, StringComparer.Ordinal).Take(60).ToList();

        ViewData["Page"] = vm.Page;
        ViewData["TotalPages"] = vm.TotalPages;
        ViewData["TotalCount"] = vm.TotalCount;

        return View(vm);
    }

    // ═════════════════════ مساعدات ═════════════════════
    /// <summary>
    /// المستخدمون ليسوا ICompanyOwned فلا تُطبَّق عليهم فلاتر EF العامة،
    /// لذا نفلتر الشركة يدوياً هنا — حاجز أمني إلزامي.
    /// </summary>
    private IQueryable<ApplicationUser> ScopedUsers()
    {
        var q = _db.Users.Where(u => !u.IsDeleted);
        if (!IsAdmin)
        {
            var cid = Me.CompanyId;
            q = q.Where(u => u.CompanyId == cid);
        }
        return q;
    }

    private async Task<Dictionary<string, int>> RoleCountsAsync()
    {
        var scoped = await ScopedUsers().Select(u => u.Id).ToListAsync();

        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where scoped.Contains(ur.UserId)
                           select new { r.Name, ur.UserId }).ToListAsync();

        return pairs.Where(p => p.Name != null)
            .GroupBy(p => p.Name!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task AttachRolesAndCustodyAsync(List<UserRow> rows)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.Id).ToList();

        var roleMap = await (from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             where ids.Contains(ur.UserId)
                             select new { ur.UserId, RoleName = r.Name, r.NameAr })
            .ToListAsync();

        var custody = await _db.Assets.AsNoTracking()
            .Where(a => a.CurrentCustodyUserId != null && ids.Contains(a.CurrentCustodyUserId)
                        && a.Status != AssetStatus.Disposed)
            .GroupBy(a => a.CurrentCustodyUserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var custodyMap = custody.ToDictionary(x => x.UserId, x => x.Count);

        foreach (var row in rows)
        {
            row.Roles = roleMap.Where(m => m.UserId == row.Id)
                .Select(m => m.NameAr ?? RoleNameAr(m.RoleName)).ToList();
            row.AssetsHeld = custodyMap.GetValueOrDefault(row.Id);
        }
    }

    private static string RoleNameAr(string? role) => role switch
    {
        Roles.Admin => "مدير النظام",
        Roles.CompanyManager => "مدير شركة",
        Roles.Technician => "فني دعم",
        Roles.Employee => "موظف",
        _ => role ?? "—"
    };

    private async Task<List<LookupItem>> CompaniesAsync() =>
        await _db.Companies.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();

    private async Task FillUserFormAsync(UserFormViewModel vm)
    {
        vm.NeedsCompanyPick = IsAdmin;
        if (IsAdmin) vm.Companies = await CompaniesAsync();

        var cid = vm.CompanyId ?? Me.CompanyId;
        vm.Departments = cid == null
            ? new List<LookupItem>()
            : await _db.Departments.AsNoTracking()
                .Where(d => d.CompanyId == cid && d.IsActive)
                .OrderBy(d => d.NameAr)
                .Select(d => new LookupItem { Id = d.Id, Name = d.NameAr }).ToListAsync();

        var allowed = IsAdmin
            ? new[] { Roles.Admin, Roles.CompanyManager, Roles.Technician, Roles.Employee }
            : new[] { Roles.CompanyManager, Roles.Technician, Roles.Employee };

        vm.RoleOptions = allowed.Select(r => new RoleOption
        {
            Name = r, NameAr = RoleNameAr(r), Description = RoleDescription(r)
        }).ToList();
    }

    private static string RoleDescription(string role) => role switch
    {
        Roles.Admin => "صلاحية كاملة على كل الشركات والإعدادات",
        Roles.CompanyManager => "إدارة كاملة لبيانات شركته وتقاريرها",
        Roles.Technician => "استلام تذاكر الصيانة وتنفيذ الجرد",
        Roles.Employee => "استلام العهد وفتح تذاكر الدعم فقط",
        _ => ""
    };

    private async Task FillDepartmentFormAsync(DepartmentFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await CompaniesAsync();

        var cid = vm.CompanyId ?? Me.CompanyId;
        vm.Managers = cid == null
            ? new List<UserLookupItem>()
            : await _db.Users.AsNoTracking()
                .Where(u => u.CompanyId == cid && u.IsActive && !u.IsDeleted)
                .OrderBy(u => u.FullName)
                .Select(u => new UserLookupItem { Id = u.Id, Name = u.FullName }).ToListAsync();
    }

    private async Task FillLocationFormAsync(LocationFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await CompaniesAsync();
    }

    private async Task FillCategoryFormAsync(CategoryFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await CompaniesAsync();

        vm.Parents = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.Id != vm.Id)
            .OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr }).ToListAsync();
    }

    private async Task FillVendorFormAsync(VendorFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await CompaniesAsync();
    }

    private async Task FillSlaFormAsync(SlaFormViewModel vm)
    {
        vm.NeedsCompanyPick = Me.CompanyId == null;
        if (vm.NeedsCompanyPick) vm.Companies = await CompaniesAsync();
    }

    private async Task LogAsync(string action, string entity, string? entityId,
        string? oldValues, string? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            CompanyId = Me.CompanyId,
            UserId = Me.UserId,
            UserName = Me.FullName ?? Me.UserName,
            Action = action,
            EntityName = entity,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static string Translate(IdentityError e) => e.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "هذا البريد الإلكتروني مستخدم بالفعل",
        "PasswordTooShort" => "كلمة المرور قصيرة جداً",
        "PasswordRequiresDigit" => "كلمة المرور يجب أن تحتوي رقماً",
        "PasswordRequiresUpper" => "كلمة المرور يجب أن تحتوي حرفاً كبيراً",
        "PasswordRequiresLower" => "كلمة المرور يجب أن تحتوي حرفاً صغيراً",
        "PasswordRequiresNonAlphanumeric" => "كلمة المرور يجب أن تحتوي رمزاً خاصاً",
        "InvalidEmail" => "صيغة البريد الإلكتروني غير صحيحة",
        _ => e.Description
    };

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
