using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Infrastructure.Services;
using AssetTracking.Web.Controllers;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetTracking.Tests;

/// <summary>
/// اختبارات قبول «المخازن كيانات مستقلة عن مواقع الشركة» (§7) على الأفعال الحقيقية:
/// الـController والخدمة وقاعدة SQLite نفسها. لا دوال مساعدة معزولة — الحراسة التي
/// يجب أن تُرفض هي حراسة الكتابة، فتُقاس من مسار الكتابة لا من قائمة العرض.
///
/// كل اختبار موسوم بالبند الذي يُغلقه من نصّ الميزة، فالتقرير لا يطلب بادّعاء.
/// </summary>
public class WarehouseSiteSeparationTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser ManagerA => new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager]);
    private WebTestKit.FakeUser ManagerB => new(CompanyId: CompanyBId, Roles: [Roles.CompanyManager]);

    /// <summary>مستلم العهدة: مستخدم البذرة «فني ألف» بهويته لا بهوية المدير.</summary>
    private WebTestKit.FakeUser HolderA => new(CompanyId: CompanyAId, UserId: UserA, Roles: [Roles.Technician]);

    private static FakeAuditService Failing() =>
        new() { ThrowOn = _ => new InvalidOperationException("تعذّر كتابة سجل التدقيق") };

    // ══════════════════ بنية الاختبار ══════════════════

    /// <summary>الكاتب هو الخدمة الحقيقية: لا مسار كتابة في هذه الصفحات سواه.</summary>
    private WarehousesController Warehouses(AppDbContext db, ICurrentUser me, IAuditService? audit = null)
        => new WarehousesController(db, new WarehouseService(db, me, audit ?? new FakeAuditService())).Attach(db, me);

    private CompanySitesController Sites(AppDbContext db, ICurrentUser me, IAuditService? audit = null)
        => new CompanySitesController(db, new CompanySiteService(db, me, audit ?? new FakeAuditService())).Attach(db, me);

    /// <summary>
    /// UserManager حقيقي عند التسليم فقط؛ null! مقصود لبقية المسارات لأن
    /// RespondAsync/ReturnAsync لا يلمسانه — المقصوص هو العهدة لا الهوية.
    /// </summary>
    private CustodyService Custody(AppDbContext db, ICurrentUser me, IAuditService audit,
        UserManager<ApplicationUser>? users = null)
        => new(db, me, new FakeNotificationService(), audit, users ?? null!);

    private AssetsController Assets(AppDbContext db, ICurrentUser me, IAuditService? audit = null)
        => new AssetsController(db, users: null!, new FakeQrService(), audit ?? new FakeAuditService(),
            Microsoft.Extensions.Options.Options.Create(new AssetTracking.Web.Configuration.FeatureFlags()))
            .Attach(db, me);

    private static WarehouseFormViewModel WhForm(string name, int id = 0, int? site = null,
        string? code = null, string? notes = null, string? address = null, bool active = true) => new()
    {
        Id = id, NameAr = name, CompanySiteId = site, Code = code,
        Notes = notes, Address = address, IsActive = active
    };

    private static CompanySiteFormViewModel SiteVm(string name, int id = 0,
        SiteKind kind = SiteKind.Branch, string? code = null, string? city = null,
        string? notes = null, bool active = true) => new()
    {
        Id = id, NameAr = name, Kind = kind, Code = code, City = city,
        Notes = notes, IsActive = active
    };

    /// <summary>رفض Writer موحَّد: يخرج رسالة Toast، فلا يُنسب الرفض لسبب آخر.</summary>
    private static string? Toast(Controller controller) => controller.TempData["ToastError"] as string;

    private async Task<int> SeedWarehouseAsync(string name, int? companyId = null, int? siteId = null,
        bool active = true, string? code = null)
    {
        await using var seed = NewUnscopedDb();
        var w = new Warehouse
        {
            CompanyId = companyId ?? CompanyAId, NameAr = name, Code = code,
            CompanySiteId = siteId, IsActive = active
        };
        seed.Warehouses.Add(w);
        await seed.SaveChangesAsync();
        return w.Id;
    }

    private async Task<int> SeedSiteAsync(string name, int? companyId = null,
        SiteKind kind = SiteKind.Branch, bool active = true)
    {
        await using var seed = NewUnscopedDb();
        var s = new CompanySite
        {
            CompanyId = companyId ?? CompanyAId, NameAr = name, Kind = kind, IsActive = active
        };
        seed.CompanySites.Add(s);
        await seed.SaveChangesAsync();
        return s.Id;
    }

    private async Task<int> SeedAssetAsync(string tag, int? warehouseId = null, int? siteId = null,
        string? holderId = null, AssetStatus status = AssetStatus.Active)
    {
        await using var seed = NewUnscopedDb();
        var a = new Asset
        {
            CompanyId = CompanyAId, AssetTag = tag, NameAr = "أصل اختبار",
            CategoryId = CatA, WarehouseId = warehouseId, CompanySiteId = siteId,
            Status = status, PurchaseValue = 1200m,
            CurrentCustodyUserId = holderId,
            CustodySince = holderId == null ? null : DateTime.UtcNow.AddDays(-3)
        };
        seed.Assets.Add(a);
        await seed.SaveChangesAsync();
        return a.Id;
    }

    // ══════════════ §7-1 و §7-6: موقع الشركة يوجد بلا أي مخزن ══════════════
    [Fact]
    public async Task Site_is_created_with_zero_warehouses_and_is_a_complete_record()
    {
        await using var db = NewDb(ManagerA);

        // الكاتب حقيقي: عدّ السطر دليلٌ على التزامه لا على مجرد استدعاء الكتابة
        var redirect = Assert.IsType<RedirectToActionResult>(await Sites(db, ManagerA,
                new AuditService(db, ManagerA))
            .Save(SiteVm("فرع التجمع الخامس", code: "BR-TJM", city: "القاهرة الجديدة",
                notes: "مقر مؤقت للعمليات.")));

        Assert.Equal(nameof(CompanySitesController.Index), redirect.ActionName);

        await using var check = NewUnscopedDb();
        var site = await check.CompanySites.SingleAsync(s => s.NameAr == "فرع التجمع الخامس");
        Assert.Equal(CompanyAId, site.CompanyId);
        Assert.Equal(SiteKind.Branch, site.Kind);
        Assert.Equal("BR-TJM", site.Code);
        Assert.Empty(site.Warehouses);        // صفر حالة كاملة لا نقص

        var row = await check.AuditLogs.SingleAsync(a => a.Action == "CreateCompanySite");
        Assert.Equal(nameof(CompanySite), row.EntityName);
        Assert.Equal("فرع التجمع الخامس", row.NewValues);
        Assert.Equal(ManagerA.UserId, row.UserId);
    }

    [Fact]
    public async Task Site_index_lists_a_site_that_holds_no_warehouse()
    {
        await using var db = NewDb(ManagerA);
        var vm = Assert.IsType<CompanySitesIndexViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Index()).Model);

        var site = vm.Items.Single(s => s.Id == SiteA);
        Assert.Empty(site.Warehouses);
        Assert.Equal(0, site.WarehouseCount);
        Assert.Equal(0, site.DirectAssetCount);
    }

    /// <summary>
    /// تفاصيل موقع بلا مخازن تفتح وتُعرض: لا مسار في الوحدة يفترض أن لكل موقع مخزناً،
    /// فذلك كان يكسر الشاشة التي يصفها §1.
    /// </summary>
    [Fact]
    public async Task Site_details_open_for_a_site_that_holds_no_warehouse()
    {
        var site = await SeedSiteAsync("موقع بلا مخازن");

        await using var db = NewDb(ManagerA);
        var vm = Assert.IsType<CompanySiteDetailsViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Details(site)).Model);

        Assert.Empty(vm.Warehouses);
        Assert.Empty(vm.Assets);
        Assert.Equal(0, vm.DirectAssetCount);
    }

    // ══════════════ §7-2 و §7-3: مخزن بلا موقع شركة يعمل كاملاً ══════════════
    [Fact]
    public async Task Warehouse_is_created_with_no_site_and_keeps_its_own_identity()
    {
        await using var db = NewDb(ManagerA);

        var redirect = Assert.IsType<RedirectToActionResult>(await Warehouses(db, ManagerA,
                new AuditService(db, ManagerA))
            .Save(WhForm("مخزن Spare Parts", code: "WH-SPARE", address: "العاشر من رمضان",
                notes: "قطع غيار مكيّفة.")));

        Assert.Equal(nameof(WarehousesController.Index), redirect.ActionName);

        await using var check = NewUnscopedDb();
        var wh = await check.Warehouses.SingleAsync(w => w.NameAr == "مخزن Spare Parts");
        Assert.Equal(CompanyAId, wh.CompanyId);
        Assert.Null(wh.CompanySiteId);         // null قيمة صالحة لا صفرة ناقصة
        Assert.Equal("قطع غيار مكيّفة.", wh.Notes);
        Assert.Equal("العاشر من رمضان", wh.Address);

        var auditRows = await check.AuditLogs
            .Where(a => a.EntityName == nameof(Warehouse))
            .Select(a => a.Action).ToListAsync();
        Assert.Equal(new[] { "CreateWarehouse" }, auditRows);
        Assert.Equal(wh.Id.ToString(),
            (await check.AuditLogs.SingleAsync(a => a.Action == "CreateWarehouse")).EntityId);
    }

    [Fact]
    public async Task Warehouse_with_no_site_still_holds_assets_and_counts_them()
    {
        var wh = await SeedWarehouseAsync("مخزن نائي");
        await SeedAssetAsync("AST-FAR-1", warehouseId: wh);
        await SeedAssetAsync("AST-FAR-2", warehouseId: wh, status: AssetStatus.UnderMaintenance);

        await using var db = NewDb(ManagerA);
        var vm = Assert.IsType<WarehouseDetailsViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Details(wh)).Model);

        Assert.Null(vm.SiteId);
        Assert.Null(vm.SiteName);
        Assert.Equal(2, vm.AssetCount);
        Assert.Equal(2400m, vm.TotalValue);
        Assert.Equal(1, vm.UnderMaintenance);
        Assert.Equal(2, vm.Assets.Count);
    }

    /// <summary>
    /// قائمة المخازن تقرأ جدول المخازن وحده: مواقع الشركة وخطوط الإنتاج ليست أسطراً
    /// فيه بعمود نوع، فلا «مخزن» مقنَّع باسم فرع.
    /// </summary>
    [Fact]
    public async Task Warehouse_index_lists_only_warehouses_with_their_optional_site()
    {
        var site = await SeedSiteAsync("مصنع بلبيس");
        var linked = await SeedWarehouseAsync("مخزن تابع", siteId: site);
        var alone = await SeedWarehouseAsync("مخزن مستقل");

        await using var db = NewDb(ManagerA);
        var vm = Assert.IsType<WarehousesIndexViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Index()).Model);

        Assert.Equal("مصنع بلبيس", vm.Items.Single(w => w.Id == linked).SiteName);
        Assert.Null(vm.Items.Single(w => w.Id == alone).SiteName);
        Assert.DoesNotContain("مصنع بلبيس", vm.Items.Select(w => w.NameAr));
        Assert.DoesNotContain("خط ألف", vm.Items.Select(w => w.NameAr));
    }

    // ══════════════ §7-4: الربط الاختياري بموقع من الشركة نفسها ══════════════
    [Fact]
    public async Task Warehouse_links_to_a_site_of_its_own_company()
    {
        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Warehouses(db, ManagerA)
            .Save(WhForm("مخزن مدينة نصر", site: SiteA))));

        await using var check = NewUnscopedDb();
        var wh = await check.Warehouses.SingleAsync(w => w.NameAr == "مخزن مدينة نصر");
        Assert.Equal(SiteA, wh.CompanySiteId);
        Assert.Equal(CompanyAId, wh.CompanyId);
        Assert.Equal(CompanyAId, (await check.CompanySites.SingleAsync(s => s.Id == SiteA)).CompanyId);
    }

    /// <summary>الربط اختياري في الاتجاهين: مسح الموقع يُبقي المخزن مخزناً عاملاً.</summary>
    [Fact]
    public async Task Editing_a_warehouse_unlinks_its_site_without_changing_what_the_row_is()
    {
        var wh = await SeedWarehouseAsync("مخزن وفيّ", siteId: SiteA);

        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Warehouses(db, ManagerA)
            .Save(WhForm("مخزن وفيّ", id: wh))));

        await using var check = NewUnscopedDb();
        var stored = await check.Warehouses.SingleAsync(w => w.Id == wh);
        Assert.Null(stored.CompanySiteId);
        Assert.Equal(CompanyAId, stored.CompanyId);
        Assert.True(await check.CompanySites.AnyAsync(s => s.Id == SiteA));   // الموقع لم يُمَس
    }

    // ══════════════ §7-5: رابط عابر للشركة مرفوض ══════════════
    [Fact]
    public async Task Warehouse_cannot_link_a_site_of_another_company_and_writes_nothing()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var controller = Warehouses(db, ManagerA, audit);

        var result = await controller.Save(WhForm("مخزن عابر", site: SiteB));

        Assert.True(WebTestKit.ReturnedForm(result));
        // معرّف شركة أخرى يُعامَل كغير موجود: لا تسريب لاسم موقع باء ولا رسالة مختلفة
        Assert.Equal(Messages.HiddenRecord, Toast(controller));
        Assert.Equal("مخزن عابر",
            Assert.IsType<WarehouseFormViewModel>(Assert.IsType<ViewResult>(result).Model).NameAr);
        Assert.Empty(audit.Actions);

        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Warehouses.CountAsync(w => w.NameAr == "مخزن عابر"));
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>التعديل لا يفتح ثغرة الربط: نفس الحارس على المسارين والمخزن يبقى على حالته.</summary>
    [Fact]
    public async Task Editing_a_warehouse_to_another_companys_site_is_refused_and_keeps_the_link()
    {
        var wh = await SeedWarehouseAsync("مخزن وفيّ", siteId: SiteA2);

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        Assert.True(WebTestKit.ReturnedForm(await Warehouses(db, ManagerA, audit)
            .Save(WhForm("مخزن وفيّ", id: wh, site: SiteB))));

        Assert.Empty(audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(SiteA2, (await check.Warehouses.SingleAsync(w => w.Id == wh)).CompanySiteId);
    }

    /// <summary>
    /// §6 مغلقة على مستوى **القاعدة** لا الكاتب: القيد المركّب
    /// <c>(CompanySiteId, CompanyId) → CompanySites(Id, CompanyId)</c> يرفض الكتابة
    /// المباشرة نفسها، فحتى سطر SQL خارج التطبيق لا يمر. الضابطان الموجبان ضروريان:
    /// بلاهما قد يُنسب الرفض لأي عطل في البنية بدل أن يكون القيد هو الذي عمل.
    /// </summary>
    [Fact]
    public async Task The_database_itself_refuses_a_cross_company_site_link()
    {
        await using (var db = NewUnscopedDb())
        {
            db.Warehouses.Add(new Warehouse
            {
                CompanyId = CompanyAId, NameAr = "مخزن كتابة مباشرة",
                CompanySiteId = SiteB, IsActive = true
            });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            // الإدانة بنوع الانتهاك نفسه، لا بأي استثناء: شدّلت المفاتيح الأجنبية فبقي
            // الفشل قائماً لسبب آخر، فكان الاختبار «يرفض» ادّعاءً كاذباً.
            var msg = ex.InnerException?.Message ?? ex.Message;
            Assert.True(msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase), msg);
        }

        await using (var db = NewUnscopedDb())
        {
            var sameCompany = new Warehouse
            { CompanyId = CompanyAId, NameAr = "مخزن رابط سليم", CompanySiteId = SiteA };
            var standalone = new Warehouse { CompanyId = CompanyAId, NameAr = "مخزن بلا فرع" };
            db.Warehouses.AddRange(sameCompany, standalone);
            await db.SaveChangesAsync();

            Assert.Equal(SiteA, (await db.Warehouses.SingleAsync(w => w.Id == sameCompany.Id)).CompanySiteId);
            // null في العمود الاختياري قيمة صالحة محمية بالقيد نفسه، لا ثغرة فيه
            Assert.Null((await db.Warehouses.SingleAsync(w => w.Id == standalone.Id)).CompanySiteId);
        }

        // ضابط ضدّ نسبة الرفض لسبب خاطئ: نفس الرابط الحرفي مع نفس الشركة الأخرى يُقبل،
        // فالمرفوض هو **اختلاف الشركتين** لا وجود الرابط ولا قيمته.
        await using (var db = NewUnscopedDb())
        {
            var mirrored = new Warehouse
            { CompanyId = CompanyBId, NameAr = "مخزن باء لفرع باء", CompanySiteId = SiteB };
            db.Warehouses.Add(mirrored);
            await db.SaveChangesAsync();
            Assert.Equal(SiteB, mirrored.CompanySiteId);
        }

        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Warehouses.CountAsync(w => w.NameAr == "مخزن كتابة مباشرة"));
    }

    // ══════════════ §7-7: مخازن كثيرة تحت موقع واحد ══════════════
    [Fact]
    public async Task One_site_holds_many_warehouses_and_lists_them_all()
    {
        var site = await SeedSiteAsync("مقر القاهرة");
        var a = await SeedWarehouseAsync("مخزن أ", siteId: site);
        var b = await SeedWarehouseAsync("مخزن ب", siteId: site);
        var c = await SeedWarehouseAsync("مخزن ج", siteId: site);

        await using var db = NewDb(ManagerA);
        var index = Assert.IsType<CompanySitesIndexViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Index()).Model);
        Assert.Equal(new[] { a, b, c },
            index.Items.Single(s => s.Id == site).Warehouses.Select(w => w.Id).OrderBy(i => i).ToArray());

        var details = Assert.IsType<CompanySiteDetailsViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Details(site)).Model);
        Assert.Equal(3, details.Warehouses.Count);
        Assert.Equal(new[] { "مخزن أ", "مخزن ب", "مخزن ج" }, details.Warehouses.Select(w => w.Name));
    }

    // ══════════════ §7-8 و §5: «إضافة مخزن» لا تُنشئ موقعاً، ولا العكس ══════════════
    [Fact]
    public async Task Add_warehouse_saves_a_warehouse_row_and_creates_no_site()
    {
        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Warehouses(db, ManagerA)
            .Save(WhForm("مخزن لا موقع", site: SiteA))));

        await using var check = NewUnscopedDb();
        Assert.Equal(1, await check.Warehouses.CountAsync(w => w.NameAr == "مخزن لا موقع"));
        Assert.Equal(0, await check.CompanySites.CountAsync(s => s.NameAr == "مخزن لا موقع"));
        Assert.Equal(3, await check.CompanySites.CountAsync());   // لم يزد موقع واحد
    }

    [Fact]
    public async Task Add_site_saves_a_site_row_and_creates_no_warehouse()
    {
        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Sites(db, ManagerA)
            .Save(SiteVm("فرع الإسماعيلية"))));

        await using var check = NewUnscopedDb();
        Assert.Equal(1, await check.CompanySites.CountAsync(s => s.NameAr == "فرع الإسماعيلية"));
        Assert.Equal(0, await check.Warehouses.CountAsync(w => w.NameAr == "فرع الإسماعيلية"));
        Assert.Equal(3, await check.Warehouses.CountAsync());
    }

    /// <summary>
    /// شكل المسارين مقياس لا افتراض: لكل وحدة نموذج مستقل وفعل حفظ واحد بلا بارامتر
    /// «نوع»، فالحفظ في جدول واحد لا يجعل السجل الآخر — وهو جذر عيب الشاشة القديمة.
    /// </summary>
    [Fact]
    public async Task Each_unit_has_its_own_form_and_its_own_edit_page()
    {
        await using var db = NewDb(ManagerA);

        var warehouseCreate = Assert.IsType<WarehouseFormViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Create()).Model);
        Assert.True(warehouseCreate.IsActive);
        Assert.NotEmpty(warehouseCreate.Sites);           // الموقع اختيارٌ معروض لا مفروض

        var siteCreate = Assert.IsType<CompanySiteFormViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Create()).Model);
        Assert.Empty(siteCreate.GetType().GetProperties()
            .Where(p => p.Name.Contains("Warehouse", StringComparison.Ordinal)));

        // لا عمود نوع ولا أب مشترك: الجدول الذي يُحفظ فيه السجل هو هويته
        Assert.Null(typeof(WarehouseFormViewModel).GetProperty("Type"));
        Assert.Null(typeof(WarehouseFormViewModel).GetProperty("ParentLocationId"));
        Assert.Null(typeof(CompanySiteFormViewModel).GetProperty("ParentLocationId"));

        Assert.Equal(WhA, Assert.IsType<WarehouseFormViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Edit(WhA)).Model).Id);
        Assert.Equal(SiteA, Assert.IsType<CompanySiteFormViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Edit(SiteA)).Model).Id);
    }

    // ══════════════ §7-9: تتبّع الأصول يحفظ في المخازن ══════════════
    [Fact]
    public async Task Asset_form_stores_the_asset_in_a_warehouse_and_the_warehouse_answers()
    {
        var wh = await SeedWarehouseAsync("مخزن الاستلام");

        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Assets(db, ManagerA)
            .Create(new AssetFormViewModel
            {
                NameAr = "مولّد اختبار", CategoryId = CatA, WarehouseId = wh,
                Status = AssetStatus.InStore
            })));

        await using var check = NewUnscopedDb();
        var asset = await check.Assets.SingleAsync(a => a.NameAr == "مولّد اختبار");
        Assert.Equal(wh, asset.WarehouseId);
        Assert.Null(asset.CompanySiteId);
        Assert.Null(asset.ProductionLineId);

        var details = Assert.IsType<WarehouseDetailsViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Details(wh)).Model);
        Assert.Contains(asset.Id, details.Assets.Select(a => a.Id));
    }

    /// <summary>
    /// الأصل الواقف في فرع ليس في مخزن: للسؤالين عمودان وشاشتان، فعدّ الأصل
    /// «محفوظاً» في الفرع كان يعني جوابين متباعدين على «أين؟».
    /// </summary>
    [Fact]
    public async Task Asset_at_a_site_appears_on_the_site_screen_not_as_warehouse_stock()
    {
        var assetId = await SeedAssetAsync("AST-SITE-1", siteId: SiteA);
        // ضابط موجب على عدّاد «بلا موضع»: يقيس من لا مخزن ولا موقع له، فالأصل
        // المسمَّر لفرع له موضع واحد ولا يُعدّ تائهاً.
        await SeedAssetAsync("AST-NOWHERE-1");

        await using var db = NewDb(ManagerA);
        var site = Assert.IsType<CompanySiteDetailsViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Details(SiteA)).Model);
        Assert.Contains(assetId, site.Assets.Select(a => a.Id));
        Assert.Equal(1, site.DirectAssetCount);

        var warehouseIndex = Assert.IsType<WarehousesIndexViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Index()).Model);
        Assert.Equal(0, warehouseIndex.Items.Sum(w => w.AssetCount));
        Assert.Equal(1, warehouseIndex.UnplacedAssets);
    }

    // ══════════════ §7-10: التسليم والإرجاع يعملان مع بنية التاريخ ══════════════
    [Fact]
    public async Task Assign_accept_and_return_keep_working_and_leave_dated_history()
    {
        var assetId = await SeedAssetAsync("AST-CYC-1", warehouseId: WhA);

        int logId;
        await using (var db = NewDb(ManagerA))
        {
            var provider = WebTestKit.BuildProvider(db, ManagerA, s => s.AddIdentityOnSqlite());
            var custody = Custody(db, ManagerA, new FakeAuditService(),
                provider.GetRequiredService<UserManager<ApplicationUser>>());
            var assigned = await custody.AssignAsync(assetId, UserA, "استخدام ميداني");
            Assert.True(assigned.Succeeded, assigned.Error);
            logId = assigned.Data!.Id;
        }

        await using (var db = NewDb(HolderA))
            Assert.True((await Custody(db, HolderA, new FakeAuditService())
                .RespondAsync(logId, true, "استُلم")).Succeeded);

        await using (var db = NewDb(ManagerA))
        {
            var returned = await Custody(db, ManagerA, new FakeAuditService())
                .ReturnAsync(assetId, WhA2, "انتهاء الاستخدام", "سليم");
            Assert.True(returned.Succeeded, returned.Error);
            Assert.Contains("مخزن ألف الثاني", returned.SuccessMessage);
        }

        await using var check = NewUnscopedDb();
        var asset = await check.Assets.SingleAsync(a => a.Id == assetId);
        Assert.Null(asset.CurrentCustodyUserId);
        Assert.Null(asset.CustodySince);
        Assert.Equal(AssetStatus.InStore, asset.Status);
        Assert.Equal(WhA2, asset.WarehouseId);
        Assert.Null(asset.CompanySiteId);

        var logs = await check.CustodyLogs.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal(CustodyAction.Assign, logs[0].Action);
        Assert.Equal(CustodyStatus.Accepted, logs[0].Status);
        Assert.Equal(UserA, logs[0].NewUserId);
        Assert.Equal(CustodyAction.Return, logs[1].Action);
        Assert.Equal(CustodyStatus.Returned, logs[1].Status);
        Assert.Equal(UserA, logs[1].PreviousUserId);

        // التاريخ المكاني محفوظ كما هو: نقلة واحدة مؤرَّخة من المخزن القديم إلى الجديد
        var move = await check.LocationLogs.SingleAsync(m => m.AssetId == assetId);
        Assert.Equal(WhA, move.FromWarehouseId);
        Assert.Equal(WhA2, move.ToWarehouseId);
        Assert.Equal(ManagerA.UserId, move.MovedByUserId);
        Assert.Equal("إرجاع عهدة", move.Reason);
    }

    /// <summary>وجهة الإرجاع قوائم المخازن وحدها، والمعطَّل ليس خياراً صالحاً.</summary>
    [Fact]
    public async Task Return_page_offers_active_warehouses_and_never_a_site()
    {
        var stopped = await SeedWarehouseAsync("مخزن موقوف", active: false);
        var assetId = await SeedAssetAsync("AST-RET-1", warehouseId: WhA, holderId: UserA);

        await using var db = NewDb(ManagerA);
        var controller = new CustodyController(db, Custody(db, ManagerA, new FakeAuditService()))
            .Attach(db, ManagerA);
        var vm = Assert.IsType<CustodyReturnViewModel>(
            Assert.IsType<ViewResult>(await controller.Return(assetId)).Model);

        Assert.Contains("مخزن ألف", vm.Warehouses.Select(w => w.Name));
        Assert.DoesNotContain("مخزن موقوف", vm.Warehouses.Select(w => w.Name));
        Assert.DoesNotContain("موقع ألف", vm.Warehouses.Select(w => w.Name));
        Assert.DoesNotContain(stopped, vm.Warehouses.Select(w => w.Id));
    }

    /// <summary>
    /// موقع شركة ليس وجهة تحفظ: الخدمة تقرأ جدول Warehouses وحده، فمعرّف موقع —
    /// ولو من الشركة نفسها — لا exists كمخزن.
    /// </summary>
    [Fact]
    public async Task Returning_to_a_company_site_is_refused_and_keeps_the_asset_where_it_was()
    {
        var assetId = await SeedAssetAsync("AST-RET-2", warehouseId: WhA, holderId: UserA);
        var siteOnly = await SeedSiteAsync("موقع لا مخزن له");

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var result = await Custody(db, ManagerA, audit).ReturnAsync(assetId, siteOnly, "إرجاع", null);

        Assert.False(result.Succeeded);
        Assert.Equal(Messages.HiddenRecord, result.Error);
        Assert.Empty(audit.Actions);

        await using var check = NewUnscopedDb();
        var asset = await check.Assets.SingleAsync(a => a.Id == assetId);
        Assert.Equal(WhA, asset.WarehouseId);
        Assert.Equal(UserA, asset.CurrentCustodyUserId);
        Assert.Equal(0, await check.LocationLogs.CountAsync());
    }

    [Fact]
    public async Task Return_to_another_companys_warehouse_is_refused_and_writes_nothing()
    {
        var assetId = await SeedAssetAsync("AST-RET-3", warehouseId: WhA, holderId: UserA);

        await using var db = NewDb(ManagerA);
        var result = await Custody(db, ManagerA, new FakeAuditService())
            .ReturnAsync(assetId, WhB, "اختبار عزل", null);

        Assert.False(result.Succeeded);
        await using var check = NewUnscopedDb();
        Assert.Equal(WhA, (await check.Assets.SingleAsync(a => a.Id == assetId)).WarehouseId);
        Assert.Equal(0, await check.CustodyLogs.CountAsync());
        Assert.Equal(0, await check.LocationLogs.CountAsync());
    }

    // ══════════════ §7-11: لا قراءة ولا كتابة عبر الشركة ══════════════
    [Fact]
    public async Task Another_company_can_neither_read_nor_write_either_entity()
    {
        await using var db = NewDb(ManagerB);

        // 404 مقصود لا 403: لا تعداد معرّفات للمهاجم
        Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(
            await Warehouses(db, ManagerB).Edit(WhA)).ControllerName);
        Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(
            await Sites(db, ManagerB).Edit(SiteA)).ControllerName);
        Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(
            await Warehouses(db, ManagerB).Details(WhA)).ControllerName);

        // الكتابة على الخدمة مباشرة (النموذج يُرسل مباشرة): لا تعديل لمخزن ألف من باء
        var warehouseUpdate = await new WarehouseService(db, ManagerB, new FakeAuditService())
            .UpdateAsync(WhForm("مخزن ألف مُنتحل", id: WhA).ToInput());
        Assert.False(warehouseUpdate.Succeeded);
        Assert.Equal(Messages.HiddenRecord, warehouseUpdate.Error);

        var siteUpdate = await new CompanySiteService(db, ManagerB, new FakeAuditService())
            .UpdateAsync(SiteVm("موقع ألف مُنتحل", id: SiteA).ToInput());
        Assert.False(siteUpdate.Succeeded);
        Assert.Equal(Messages.HiddenRecord, siteUpdate.Error);

        // قوائم باء لا تتضمّن شيئاً من ألف
        var warehouseIndex = Assert.IsType<WarehousesIndexViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerB).Index()).Model);
        Assert.Equal(new[] { WhB }, warehouseIndex.Items.Select(w => w.Id).ToArray());

        var siteIndex = Assert.IsType<CompanySitesIndexViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerB).Index()).Model);
        Assert.Equal(new[] { SiteB }, siteIndex.Items.Select(s => s.Id).ToArray());

        await using var check = NewUnscopedDb();
        Assert.Equal("مخزن ألف", (await check.Warehouses.SingleAsync(w => w.Id == WhA)).NameAr);
        Assert.Equal("موقع ألف", (await check.CompanySites.SingleAsync(s => s.Id == SiteA)).NameAr);
    }

    [Fact]
    public async Task Admin_of_one_company_cannot_move_an_asset_to_another_companys_warehouse()
    {
        var assetId = await SeedAssetAsync("AST-X-1", warehouseId: WhA);

        await using var db = NewDb(ManagerA);
        var controller = Assets(db, ManagerA);
        var vm = new AssetFormViewModel
        {
            Id = assetId, NameAr = "أصل اختبار", CategoryId = CatA,
            WarehouseId = WhB, Status = AssetStatus.Active
        };

        Assert.True(WebTestKit.ReturnedForm(await controller.Edit(vm)));
        Assert.NotNull(WebTestKit.FieldError(controller, nameof(AssetFormViewModel.WarehouseId)));

        await using var check = NewUnscopedDb();
        Assert.Equal(WhA, (await check.Assets.SingleAsync(a => a.Id == assetId)).WarehouseId);
        Assert.Equal(0, await check.LocationLogs.CountAsync());
    }

    // ══════════════ حواجز إضافية على الكيانين ══════════════
    [Fact]
    public async Task Code_is_unique_inside_the_company_only()
    {
        await SeedWarehouseAsync("مخزن بكود", code: "WH-01");

        await using var db = NewDb(ManagerA);
        var controller = Warehouses(db, ManagerA);
        Assert.True(WebTestKit.ReturnedForm(await controller.Save(WhForm("مخزن مكرر", code: "WH-01"))));
        Assert.Contains("WH-01", Toast(controller));

        // نفس الكود في شركة أخرى مقبول: التفرد داخل نطاق الشركة لا عالمي
        await using var dbB = NewDb(ManagerB);
        Assert.True(WebTestKit.ReturnedRedirect(await Warehouses(dbB, ManagerB)
            .Save(WhForm("مخزن باء الثاني", code: "WH-01"))));
    }

    [Fact]
    public async Task Saving_a_new_warehouse_rolls_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Warehouses(db, ManagerA, audit).Save(WhForm("مخزن لا يجب أن يُحفظ")));

        Assert.Contains("CreateWarehouse", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Warehouses.CountAsync(w => w.NameAr == "مخزن لا يجب أن يُحفظ"));
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Editing_a_site_rolls_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sites(db, ManagerA, audit).Save(SiteVm("موقع ألف", id: SiteA, notes: "وصف جديد")));

        Assert.Contains("EditCompanySite", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Null((await check.CompanySites.SingleAsync(s => s.Id == SiteA)).Notes);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>
    /// خط الإنتاج ليس فرعاً ولا مخزناً: قائمة ثالثة بجدولها، فلا يدخل قوائم المواضع
    /// ولا يُقرأ موضعه من عمود موقع.
    /// </summary>
    [Fact]
    public async Task Production_line_is_neither_a_site_nor_a_warehouse()
    {
        await using var db = NewDb(ManagerA);

        var warehouses = await ReferencePickers.WarehousesAsync(db);
        var sites = await ReferencePickers.CompanySitesAsync(db);
        var lines = await ReferencePickers.ProductionLinesAsync(db);

        Assert.Contains("خط ألف", lines.Select(l => l.Name));
        Assert.DoesNotContain("خط ألف", warehouses.Select(w => w.Name));
        Assert.DoesNotContain("خط ألف", sites.Select(s => s.Name));
        Assert.DoesNotContain("مخزن ألف", lines.Select(l => l.Name));
        Assert.DoesNotContain("موقع ألف", lines.Select(l => l.Name));

        var warehouseIndex = Assert.IsType<WarehousesIndexViewModel>(
            Assert.IsType<ViewResult>(await Warehouses(db, ManagerA).Index()).Model);
        Assert.DoesNotContain("خط ألف", warehouseIndex.Items.Select(w => w.NameAr));

        var siteIndex = Assert.IsType<CompanySitesIndexViewModel>(
            Assert.IsType<ViewResult>(await Sites(db, ManagerA).Index()).Model);
        Assert.DoesNotContain("خط ألف", siteIndex.Items.Select(s => s.NameAr));
    }

    /// <summary>
    /// الجدول الموحَّد القديم باقٍ في النموذج حتى المرحلة الثانية، لكن لا كاتب له:
    /// أي عودة إلى «Locations بعمود نوع» تكسر هذا الاختبار والحاجز المعماري معه.
    /// </summary>
    [Fact]
    public async Task Unified_location_table_has_no_new_rows_after_both_entities_are_written()
    {
        await SeedWarehouseAsync("مخزن بعد الفصل");

        await using var db = NewDb(ManagerA);
        Assert.True(WebTestKit.ReturnedRedirect(await Sites(db, ManagerA).Save(SiteVm("فرع بعد الفصل"))));
        Assert.True(WebTestKit.ReturnedRedirect(await Warehouses(db, ManagerA).Save(WhForm("مخزن بعد الفصل"))));

        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Locations.CountAsync());
        Assert.Equal(2, await check.Warehouses.CountAsync(w => w.NameAr.Contains("بعد الفصل")));
        Assert.Equal(1, await check.CompanySites.CountAsync(s => s.NameAr.Contains("بعد الفصل")));
    }
}
