using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.Configuration;
using AssetTracking.Web.Controllers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssetTracking.Tests;

public sealed class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = new();

    /// <summary>يفشل سجل التدقيق باستثناء حقيقي بعد تسجيل المسمّى — لمحاكاة عطل الكتابة.</summary>
    public Func<string, Exception?>? ThrowOn { get; set; }

    public Task LogAsync(string action, string entityName, string? entityId,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        Actions.Add(action);
        var boom = ThrowOn?.Invoke(action);
        return boom == null ? Task.CompletedTask : Task.FromException(boom);
    }

    public Task LogTextAsync(string action, string entityName, string? entityId,
        string? oldText, string? newText, CancellationToken ct = default)
        => LogAsync(action, entityName, entityId, oldText, newText, ct);
}

public sealed class FakeQrService : IQrCodeService
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 10) => Array.Empty<byte>();

    public Task<string> GenerateForAssetAsync(string assetTag, string baseUrl, CancellationToken ct = default)
        => Task.FromResult($"/qrcodes/{assetTag}.png");
}

/// <summary>
/// اختبارات عزل الشركات على مسار الكتابة الحقيقي: الـController يعمل بـSQLite
/// وبمستخدم لشركة واحدة، فيُقاس رفض الربط العابر على الفعل نفسه لا على دالة مساعدة.
/// </summary>
public class TenantIsolationTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser ManagerA => new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager]);
    private WebTestKit.FakeUser Admin => new(CompanyId: null, IsAdmin: true, UserId: "u-admin");

    private AssetsController Assets(AppDbContext db, FakeAuditService audit, ICurrentUser me)
        => new AssetsController(db, users: null!, new FakeQrService(), audit, Microsoft.Extensions.Options.Options.Create(new FeatureFlags()))
            .Attach(db, me);

    private static AssetFormViewModel Form(int categoryId, int? departmentId = null, int? warehouseId = null,
        int? vendorId = null) => new()
    {
        NameAr = "ماكينة اختبار",
        CategoryId = categoryId,
        DepartmentId = departmentId,
        WarehouseId = warehouseId,
        VendorId = vendorId,
        Status = AssetStatus.InStore,
    };

    [Fact]
    public async Task Create_rejects_department_of_another_company_and_writes_nothing()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var controller = Assets(db, audit, ManagerA);

        var result = await controller.Create(Form(CatA, departmentId: DeptB));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.DepartmentId)));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
        Assert.Empty(audit.Actions);
    }

    [Fact]
    public async Task Create_rejects_warehouse_of_another_company()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var result = await controller.Create(Form(CatA, warehouseId: WhB));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.WarehouseId)));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Create_rejects_vendor_of_another_company()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var result = await controller.Create(Form(CatA, vendorId: VendorB));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.VendorId)));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Create_rejects_category_of_another_company()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var result = await controller.Create(Form(CatB, departmentId: DeptA));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.CategoryId)));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Create_rejects_a_link_that_does_not_exist_at_all()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var result = await controller.Create(Form(CatA, warehouseId: 4_242));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.WarehouseId)));
    }

    [Fact]
    public async Task Create_rejects_a_soft_deleted_link()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var result = await controller.Create(Form(CatA, departmentId: DeletedDeptA));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Create_accepts_the_same_company_links_and_persists_them()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();

        var result = await Assets(db, audit, ManagerA).Create(Form(CatA, DeptA, WhA, VendorA));

        Assert.True(WebTestKit.ReturnedRedirect(result));
        var saved = await db.Assets.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyAId, saved.CompanyId);
        Assert.Equal(DeptA, saved.DepartmentId);
        Assert.Equal(WhA, saved.WarehouseId);
        Assert.Equal(VendorA, saved.VendorId);
        Assert.Equal(new[] { "Create" }, audit.Actions);
    }

    [Fact]
    public async Task Edit_rejects_foreign_warehouse_and_leaves_the_stored_link_untouched()
    {
        await using (var seed = NewUnscopedDb())
        {
            seed.Assets.Add(new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-KEEP-1", NameAr = "أصل للمخزن",
                CategoryId = CatA, WarehouseId = WhA, Status = AssetStatus.InStore
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb(ManagerA);
        var stored = await db.Assets.SingleAsync(a => a.AssetTag == "AST-KEEP-1");
        var audit = new FakeAuditService();
        var controller = Assets(db, audit, ManagerA);

        var vm = Form(CatA, DeptA, WhB, VendorA);
        vm.Id = stored.Id;
        var result = await controller.Edit(vm);

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.WarehouseId)));

        await using var check = NewUnscopedDb();
        var after = await check.Assets.AsNoTracking().SingleAsync(a => a.Id == stored.Id);
        Assert.Equal(WhA, after.WarehouseId);
        Assert.Equal(0, await check.LocationLogs.CountAsync());
        Assert.Empty(audit.Actions);
    }

    [Fact]
    public async Task Edit_accepts_a_legacy_link_to_a_deleted_department_when_it_is_unchanged()
    {
        await using (var seed = NewUnscopedDb())
        {
            seed.Assets.Add(new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-LEG-1", NameAr = "أصل بقسم محذوف",
                CategoryId = CatA, DepartmentId = DeletedDeptA, Status = AssetStatus.InStore
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb(ManagerA);
        var stored = await db.Assets.SingleAsync(a => a.AssetTag == "AST-LEG-1");
        var controller = Assets(db, new FakeAuditService(), ManagerA);

        var vm = Form(CatA, DeletedDeptA);
        vm.Id = stored.Id;
        vm.NameAr = "اسم جديد بعد الترخيص";

        var result = await controller.Edit(vm);

        Assert.True(WebTestKit.ReturnedRedirect(result));
        await using var check = NewUnscopedDb();
        Assert.Equal("اسم جديد بعد الترخيص",
            (await check.Assets.SingleAsync(a => a.Id == stored.Id)).NameAr);
    }

    [Fact]
    public async Task Admin_create_validates_links_against_the_company_it_chose()
    {
        await using var db = NewDb(Admin);
        var controller = Assets(db, new FakeAuditService(), Admin);

        var vm = Form(CatA, DeptB);
        vm.CompanyId = CompanyAId;

        var result = await controller.Create(vm);

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.DepartmentId)));
        Assert.Equal(0, await db.Assets.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Details_of_another_company_asset_is_refused_like_a_missing_one()
    {
        await using (var seed = NewUnscopedDb())
        {
            seed.Assets.Add(new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-A-1", NameAr = "أصل شركة ألف",
                CategoryId = CatA, Status = AssetStatus.InStore
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb(ManagerB());
        var foreign = await db.Assets.IgnoreQueryFilters().SingleAsync(a => a.AssetTag == "AST-A-1");

        var result = await Assets(db, new FakeAuditService(), ManagerB()).Details(foreign.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task ByTag_of_another_company_asset_is_refused_like_a_missing_one()
    {
        await using (var seed = NewUnscopedDb())
        {
            seed.Assets.Add(new Asset
            {
                CompanyId = CompanyBId, AssetTag = "AST-BYTAG-1", NameAr = "أصل باء للوسم",
                CategoryId = CatB, Status = AssetStatus.InStore
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb(ManagerA);
        var result = await Assets(db, new FakeAuditService(), ManagerA).ByTag("AST-BYTAG-1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    private WebTestKit.FakeUser ManagerB() => new(CompanyId: CompanyBId, Roles: [Roles.CompanyManager]);
}
