using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Infrastructure.Services;
using AssetTracking.Web.Configuration;
using AssetTracking.Web.Controllers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetTracking.Tests;

/// <summary>
/// مسارات الكتابة الأخرى التي تستقبل معرّفاً لكيان مملوك لشركة: نفس الحاجز،
/// ونفس الإثبات — رفض عابر للشركة على الفعل الحقيقي، وقبول الربط الصحيح.
/// </summary>
public class CrossTenantWriteTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser ManagerA => new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager]);

    private static Microsoft.Extensions.Options.IOptions<FeatureFlags> Flags
        => Microsoft.Extensions.Options.Options.Create(new FeatureFlags());

    private (AdminController Controller, ServiceProvider Provider) Admin(AppDbContext db)
    {
        var provider = WebTestKit.BuildProvider(db, ManagerA, s => s.AddIdentityOnSqlite());
        var audit = new FakeAuditService();
        var controller = new AdminController(db,
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                provider.GetRequiredService<RoleManager<ApplicationRole>>(), Flags,
                audit, new ProductionLineService(db, ManagerA, audit))
            .Attach(db, ManagerA, provider);
        return (controller, provider);
    }

    // ────────────────────────── مستخدم: قسم شركة أخرى ──────────────────────────
    [Fact]
    public async Task CreateUser_rejects_department_of_another_company()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db);

        var vm = new UserFormViewModel
        {
            FullName = "موظف جديد", Email = "new@a.eg", Password = "Test@1234",
            Role = Roles.Employee, DepartmentId = DeptB
        };

        var result = await controller.CreateUser(vm);

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(UserFormViewModel.DepartmentId)));
        Assert.Equal(0, await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "new@a.eg"));
    }

    [Fact]
    public async Task CreateUser_accepts_a_department_from_his_own_company()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db);

        var vm = new UserFormViewModel
        {
            FullName = "موظف صحيح", Email = "ok@a.eg", Password = "Test@1234",
            Role = Roles.Employee, DepartmentId = DeptA
        };

        var result = await controller.CreateUser(vm);

        Assert.True(WebTestKit.ReturnedRedirect(result));
        var created = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "ok@a.eg");
        Assert.Equal(DeptA, created.DepartmentId);
        Assert.Equal(CompanyAId, created.CompanyId);
    }

    [Fact]
    public async Task EditUser_rejects_foreign_department_and_keeps_the_stored_one()
    {
        await using var db = NewDb(ManagerA);
        var stored = await db.Users.SingleAsync(u => u.Id == UserA);
        Assert.Equal(DeptA, stored.DepartmentId);

        var (controller, _) = Admin(db);
        var vm = new UserFormViewModel
        {
            Id = UserA, FullName = "فني ألف", Email = "tech@a.eg",
            Role = Roles.Technician, DepartmentId = DeptB
        };

        var result = await controller.EditUser(vm);

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(UserFormViewModel.DepartmentId)));

        await using var check = NewUnscopedDb();
        Assert.Equal(DeptA, (await check.Users.SingleAsync(u => u.Id == UserA)).DepartmentId);
    }

    // ────────────────────────── تصنيف: أب من شركة أخرى ──────────────────────────
    [Fact]
    public async Task CreateCategory_rejects_parent_from_another_company()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db);

        var result = await controller.CreateCategory(new CategoryFormViewModel
        {
            NameAr = "تصنيف جديد", ParentCategoryId = CatB
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.Equal(0, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.NameAr == "تصنيف جديد"));
    }

    [Fact]
    public async Task CreateCategory_accepts_a_parent_from_the_same_company()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db);

        var result = await controller.CreateCategory(new CategoryFormViewModel
        {
            NameAr = "تصنيف فرعي", ParentCategoryId = CatA
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        var created = await db.Categories.IgnoreQueryFilters().SingleAsync(c => c.NameAr == "تصنيف فرعي");
        Assert.Equal(CatA, created.ParentCategoryId);
    }

    // ────────────────────────── الجرد: مخزن ومسؤول ──────────────────────────
    private InventoryAuditsController Audits(AppDbContext db, FakeAuditService audit)
        => new InventoryAuditsController(db, new SequenceService(db), audit,
                new FakeNotificationService())
            .Attach(db, ManagerA);

    [Fact]
    public async Task AuditCreate_rejects_warehouse_of_another_company_and_writes_nothing()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var controller = Audits(db, audit);

        var result = await controller.Create(new AuditFormViewModel
        {
            Title = "جرد مرحَّل", WarehouseId = WhB, ScheduledDate = DateTime.UtcNow.Date
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AuditFormViewModel.WarehouseId)));
        Assert.Equal(0, await db.InventoryAudits.IgnoreQueryFilters().CountAsync());
        Assert.Empty(audit.Actions);
    }

    [Fact]
    public async Task AuditCreate_rejects_responsible_user_of_another_company()
    {
        await using var db = NewDb(ManagerA);
        var controller = Audits(db, new FakeAuditService());

        var result = await controller.Create(new AuditFormViewModel
        {
            Title = "جرد مسؤول", WarehouseId = WhA, ResponsibleUserId = UserB,
            ScheduledDate = DateTime.UtcNow.Date
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AuditFormViewModel.ResponsibleUserId)));
        Assert.Equal(0, await db.InventoryAudits.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task AuditCreate_accepts_a_same_company_warehouse_and_responsible()
    {
        await using var db = NewDb(ManagerA);
        var controller = Audits(db, new FakeAuditService());

        var result = await controller.Create(new AuditFormViewModel
        {
            Title = "جرد سليم", WarehouseId = WhA, ResponsibleUserId = UserA,
            ScheduledDate = DateTime.UtcNow.Date
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        var saved = await db.InventoryAudits.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyAId, saved.CompanyId);
        Assert.Equal(WhA, saved.WarehouseId);
        Assert.Equal(UserA, saved.ResponsibleUserId);
    }

    [Fact]
    public async Task ScanItem_rejects_foreign_actual_warehouse_and_leaves_the_item_pending()
    {
        var (auditId, itemId) = await SeedInProgressAuditAsync("AST-ISO-1");

        await using var db = NewDb(ManagerA);
        var result = await Audits(db, new FakeAuditService())
            .ScanItem(auditId, itemId, AuditItemResult.Misplaced, WhB, null, null, "نقل إلى غير مكانه");

        // التحويل إلى Details لا إلى Home يُثبت أن الحاجز هو الذي أوقف المسار
        // لا أن السجل لم يُوجد أصلاً (NotFoundOrForbidden يحوّل إلى Home).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(InventoryAuditsController.Details), redirect.ActionName);

        await using var check = NewUnscopedDb();
        var item = await check.InventoryAuditItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        Assert.Equal(AuditItemResult.Pending, item.Result);
        Assert.Null(item.ActualWarehouseId);
    }

    /// <summary>ضابط إيجابي: نفس المسار يكتب فعلاً حين يكون المخزن من شركة الجرد.</summary>
    [Fact]
    public async Task ScanItem_accepts_a_warehouse_from_the_audit_company()
    {
        var (auditId, itemId) = await SeedInProgressAuditAsync("AST-ISO-2");

        await using var db = NewDb(ManagerA);
        var result = await Audits(db, new FakeAuditService())
            .ScanItem(auditId, itemId, AuditItemResult.Misplaced, WhA, null, null, "نقل داخل الشركة");

        Assert.IsType<RedirectToActionResult>(result);

        await using var check = NewUnscopedDb();
        var item = await check.InventoryAuditItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        Assert.Equal(AuditItemResult.Misplaced, item.Result);
        Assert.Equal(WhA, item.ActualWarehouseId);
    }

    private async Task<(int AuditId, int ItemId)> SeedInProgressAuditAsync(string assetTag)
    {
        await using var seed = NewUnscopedDb();
        var a = new InventoryAudit
        {
            CompanyId = CompanyAId, Code = $"AUD-{assetTag}", Title = "جرد معزول",
            ScheduledDate = DateTime.UtcNow.Date, Status = AuditStatus.InProgress
        };
        seed.InventoryAudits.Add(a);
        var asset = new Asset
        {
            CompanyId = CompanyAId, AssetTag = assetTag, NameAr = "أصل مجرود",
            CategoryId = CatA, WarehouseId = WhA, Status = AssetStatus.Active
        };
        seed.Assets.Add(asset);
        await seed.SaveChangesAsync();

        var item = new InventoryAuditItem
        {
            CompanyId = CompanyAId,
            AuditId = a.Id, AssetId = asset.Id, Result = AuditItemResult.Pending,
            ExpectedWarehouseId = WhA
        };
        seed.InventoryAuditItems.Add(item);
        await seed.SaveChangesAsync();
        return (a.Id, item.Id);
    }

    // ────────────────────────── صيانة دورية: فني شركة أخرى ──────────────────────────
    [Fact]
    public async Task ScheduleCreate_rejects_technician_of_another_company()
    {
        int assetId;
        await using (var seed = NewUnscopedDb())
        {
            var asset = new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-SCH-1", NameAr = "ماكينة مجدولة",
                CategoryId = CatA, Status = AssetStatus.Active
            };
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
            assetId = asset.Id;
        }

        await using var db = NewDb(ManagerA);
        var controller = new MaintenanceSchedulesController(db, new SequenceService(db),
                new SlaService(db), new FakeNotificationService(), new FakeAuditService())
            .Attach(db, ManagerA);

        var result = await controller.Create(new ScheduleFormViewModel
        {
            AssetId = assetId, Title = "جدول صيانة", DefaultTechnicianId = UserB
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(ScheduleFormViewModel.DefaultTechnicianId)));
        Assert.Equal(0, await db.MaintenanceSchedules.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ScheduleCreate_accepts_a_technician_from_the_asset_company()
    {
        int assetId;
        await using (var seed = NewUnscopedDb())
        {
            var asset = new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-SCH-2", NameAr = "ماكينة مجدولة 2",
                CategoryId = CatA, Status = AssetStatus.Active
            };
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
            assetId = asset.Id;
        }

        await using var db = NewDb(ManagerA);
        var controller = new MaintenanceSchedulesController(db, new SequenceService(db),
                new SlaService(db), new FakeNotificationService(), new FakeAuditService())
            .Attach(db, ManagerA);

        var result = await controller.Create(new ScheduleFormViewModel
        {
            AssetId = assetId, Title = "جدول سليم", DefaultTechnicianId = UserA
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        Assert.Equal(UserA, (await db.MaintenanceSchedules.AsNoTracking().SingleAsync()).DefaultTechnicianId);
    }

    // ────────────────────────── تذكرة: خط إنتاج شركة أخرى ──────────────────────────
    [Fact]
    public async Task TicketCreate_rejects_production_line_of_another_company()
    {
        int assetId;
        await using (var seed = NewUnscopedDb())
        {
            var asset = new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-TKT-1", NameAr = "ماكينة معطلة",
                CategoryId = CatA, WarehouseId = WhA, Status = AssetStatus.Active
            };
            seed.Assets.Add(asset);
            await seed.SaveChangesAsync();
            assetId = asset.Id;
        }

        await using var db = NewDb(ManagerA);
        var provider = WebTestKit.BuildProvider(db, ManagerA, s => s.AddIdentityOnSqlite());
        var controller = new TicketsController(db,
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                new SequenceService(db), new SlaService(db),
                new FakeNotificationService(), new FakeAuditService())
            .Attach(db, ManagerA, provider);

        var result = await controller.Create(new TicketCreateViewModel
        {
            AssetId = assetId, Title = "توقف الخط", Description = "تفصيل العطل",
            Priority = TicketPriority.High, ProductionLineId = LineB
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(TicketCreateViewModel.ProductionLineId)));
        Assert.Equal(0, await db.MaintenanceTickets.IgnoreQueryFilters().CountAsync());
    }

    // ────────────────────────── بذرة الاختبار ──────────────────────────
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // مستخدمو الاختبار بلا قسم مبدئياً — اختبار EditUser يحتاج قسماً محفوظاً لألف
        await using var db = NewUnscopedDb();
        await db.Users.Where(u => u.CompanyId == CompanyAId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, DeptA));
    }
}
