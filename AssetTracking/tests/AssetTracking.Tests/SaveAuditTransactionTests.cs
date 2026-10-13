using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Services;
using AssetTracking.Web.Configuration;
using AssetTracking.Web.Controllers;
using AssetTracking.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetTracking.Tests;

/// <summary>
/// سلامة «الحفظ + التدقيق»: يُثبت أن فشل كتابة سطر التدقيق يرجع الأصل نفسه،
/// وأن فشل حفظ الأصل لا يترك سطر تدقيق مضللاً. القياس على SQLite حقيقية بصفقات،
/// لا على mocks: بعد الاستثناء تُقرأ القاعدة من سياق جديد تماماً.
/// </summary>
public class SaveAuditTransactionTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser ManagerA => new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager]);

    private AssetsController Assets(AppDbContext db, IAuditService audit,
        IServiceProvider? provider = null)
        => new AssetsController(db, users: null!, new FakeQrService(), audit,
                Microsoft.Extensions.Options.Options.Create(new FeatureFlags()))
            .Attach(db, ManagerA, provider);

    private AssetFormViewModel Form(int id, int? warehouseId = null) => new()
    {
        Id = id,
        NameAr = "ماكينة اختبار",
        CategoryId = CatA,
        WarehouseId = warehouseId,
        Status = AssetStatus.InStore,
    };

    /// <summary>يُرجع وسماً ثابتاً لتوليد تعارض فهرس فريد عند الحفظ.</summary>
    private sealed class FixedSequence : ISequenceService
    {
        public FixedSequence(string tag) => _tag = tag;
        private readonly string _tag;
        public Task<string> NextAssetTagAsync(int companyId, CancellationToken ct = default) => Task.FromResult(_tag);
        public Task<string> NextTicketNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult(_tag);
        public Task<string> NextAuditCodeAsync(int companyId, CancellationToken ct = default) => Task.FromResult(_tag);
    }

    private async Task<int> SeedAssetAsync(string tag = "AST-TX-1", int? warehouseId = null)
    {
        await using var seed = NewUnscopedDb();
        var a = new Asset
        {
            CompanyId = CompanyAId, AssetTag = tag, NameAr = "أصل محفوظ",
            CategoryId = CatA, WarehouseId = warehouseId, Status = AssetStatus.Active
        };
        seed.Assets.Add(a);
        await seed.SaveChangesAsync();
        return a.Id;
    }

    private async Task<(int Assets, int Audits)> CountsAsync()
    {
        await using var check = NewUnscopedDb();
        return (await check.Assets.CountAsync(), await check.AuditLogs.CountAsync());
    }

    // ───────────────Scenario A: نجاح كامل — الأصل والسجل معاً ───────────────
    [Fact]
    public async Task Create_persists_the_asset_and_its_audit_row()
    {
        await using var db = NewDb(ManagerA);
        var controller = Assets(db, new AuditService(db, ManagerA));

        var result = await controller.Create(Form(0));

        Assert.True(WebTestKit.ReturnedRedirect(result));
        Assert.Equal(1, (await CountsAsync()).Assets);

        await using var check = NewUnscopedDb();
        var audit = await check.AuditLogs.SingleAsync();
        Assert.Equal("Create", audit.Action);
        Assert.Equal(nameof(Asset), audit.EntityName);
        Assert.Equal(ManagerA.UserId, audit.UserId);
    }

    // ─────────── Scenario B: فشل التدقيق → الأصل نفسه غير محفوظ ───────────
    [Fact]
    public async Task Create_rolls_the_asset_back_when_the_audit_write_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService
        {
            ThrowOn = _ => new InvalidOperationException("سجلات التدقيق متوقفة")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Assets(db, audit).Create(Form(0)));

        Assert.Equal(0, (await CountsAsync()).Assets);
    }

    // ─────── Scenario C: فشل حفظ الأصل → لا سطر تدقيق مضلل ───────
    [Fact]
    public async Task Create_writes_no_audit_row_when_the_entity_save_fails()
    {
        var existingId = await SeedAssetAsync("AST-DUP");
        Assert.NotEqual(0, existingId);

        await using var db = NewDb(ManagerA);
        var provider = WebTestKit.BuildProvider(db, ManagerA,
            s => s.AddSingleton<ISequenceService>(new FixedSequence("AST-DUP")));
        var audit = new FakeAuditService();

        // الفهرس الفريد للوسم ينفجر في SaveChanges نفسه
        await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => Assets(db, audit, provider).Create(Form(0)));

        Assert.Equal(0, (await CountsAsync()).Audits);
        Assert.Equal(1, (await CountsAsync()).Assets);
    }

    // ──── Scenario D: استثناء في منتصف التدفق → رجوع كامل للتعديل واللوغ ────
    [Fact]
    public async Task Edit_rolls_back_both_the_update_and_the_move_log_when_the_audit_fails()
    {
        var id = await SeedAssetAsync("AST-TX-2", warehouseId: WhA);

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService
        {
            ThrowOn = _ => new InvalidOperationException("تعذّر كتابة السجل")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Assets(db, audit).Edit(Form(id, warehouseId: WhA2)));

        await using var check = NewUnscopedDb();
        var stored = await check.Assets.SingleAsync(x => x.Id == id);
        Assert.Equal(WhA, stored.WarehouseId);
        Assert.Equal("أصل محفوظ", stored.NameAr);
        Assert.Equal(0, await check.LocationLogs.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Dispose_rolls_the_status_change_back_when_the_audit_fails()
    {
        var id = await SeedAssetAsync("AST-TX-3");

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService
        {
            ThrowOn = _ => new InvalidOperationException("تعذّر كتابة السجل")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Assets(db, audit).Dispose(id, "نهاية العمر"));

        await using var check = NewUnscopedDb();
        Assert.Equal(AssetStatus.Active, (await check.Assets.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    // ─── Scenario E: Service الحقيقي ينضم إلى التعهّد المفتوح حوله ───
    [Fact]
    public async Task AuditService_writes_inside_an_ambient_transaction_and_rolls_back_with_it()
    {
        await using var db = NewDb(ManagerA);
        var before = await CountsAsync();
        Assert.Equal(0, before.Assets);

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Assets.Add(new Asset
            {
                CompanyId = CompanyAId, AssetTag = "AST-AMB", NameAr = "أصل داخل تعهّد",
                CategoryId = CatA, Status = AssetStatus.Active
            });
            await db.SaveChangesAsync();
            await new AuditService(db, ManagerA).LogAsync("Create", nameof(Asset), "1", null, "x");

            // الكتابة مرئية داخل التعهّد نفسه...
            Assert.Equal(1, await db.Assets.CountAsync());
            Assert.Equal(1, await db.AuditLogs.CountAsync());

            await tx.RollbackAsync();
        }

        // ...وليست مرئية بعده: سطر التدقيق لم يُحفظ مستقلاً عن الأصل
        var after = await CountsAsync();
        Assert.Equal(0, after.Assets);
        Assert.Equal(0, after.Audits);
    }

    // ─── حماية السلوك القائم: تعارض النسخة لا يزال يرجع النموذج لا استثناء ───
    [Fact]
    public async Task Edit_still_returns_the_form_on_a_concurrent_update()
    {
        var id = await SeedAssetAsync("AST-TX-4");

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var controller = Assets(db, audit);

        var vm = Form(id);
        // نسخة محمولة من نسخة أخرى — لا يطابقها أي صف في القاعدة
        vm.RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });

        var result = await controller.Edit(vm);

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.Empty(audit.Actions);
        Assert.Equal("أصل محفوظ", (await NewUnscopedDb().Assets.SingleAsync(x => x.Id == id)).NameAr);
    }
}
