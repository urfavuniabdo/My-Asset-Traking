using System.Reflection;
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

namespace AssetTracking.Tests;

/// <summary>
/// سياسة قراءة الأصول (R9) على الكونترولر الحقيقي: الموظف يقرأ أصول شركته فقط،
/// ولا يقرأ — ولا يسمّي — شيء من شركة أخرى.
///
/// كل اختبار هنا له ضابط موجب على نفس المسار: لو عُطّل التضييق صار الاختبار
/// السالب يفشل لأن «لا شيء يُعرض أصلاً» (قائمة فارغة)، لا لأن الاسم محجوب.
/// </summary>
public class AssetReadPolicyTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser EmployeeA =>
        new(CompanyId: CompanyAId, Roles: [Roles.Employee], UserId: "emp-a");

    private WebTestKit.FakeUser ManagerA =>
        new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager], UserId: "mgr-a");

    private WebTestKit.FakeUser Admin =>
        new(CompanyId: null, IsAdmin: true, UserId: "u-admin");

    private AssetsController Assets(AppDbContext db, ICurrentUser me)
        => new AssetsController(db, users: null!, new FakeQrService(), new FakeAuditService(),
                Microsoft.Extensions.Options.Options.Create(new FeatureFlags()))
            .Attach(db, me);

    /// <summary>
    /// قائمة الأصول بلا فلاتر. التوقيع لا يحمل قيماً افتراضية (المُعرِّفات تأتي من
    /// routing في الإنتاج)، فالنداء المباشر يحتاج كل الوسائط.
    /// </summary>
    private static Task<IActionResult> List(AssetsController c, string? q = null,
        AssetStatus? status = null) => c.Index(q, null, null, null, null, null, status, null);

    /// <summary>بذر بلا فلتر شركة — الأصل قد يكون لشركة ألف أو باء.</summary>
    private async Task<int> SeedAsset(string tag, int companyId, int categoryId, string nameAr,
        string? holderId = null)
    {
        await using var seed = NewUnscopedDb();
        var a = new Asset
        {
            CompanyId = companyId, AssetTag = tag, NameAr = nameAr,
            CategoryId = categoryId, Status = AssetStatus.InStore,
            CurrentCustodyUserId = holderId
        };
        seed.Assets.Add(a);
        await seed.SaveChangesAsync();
        return a.Id;
    }

    // ─────────────── القراءة المسموحة: أصول الشركة نفسها ───────────────

    [Fact]
    public async Task Employee_sees_his_company_assets_in_the_list_and_not_other_companies()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "حاسب ألف");
        await SeedAsset("R9-A2", CompanyAId, CatA, "طابعة ألف");
        await SeedAsset("R9-B1", CompanyBId, CatB, "حاسب باء");

        await using var db = NewDb(EmployeeA);
        var vm = Assert.IsType<AssetIndexViewModel>(
            Assert.IsType<ViewResult>(await List(Assets(db, EmployeeA))).Model);

        Assert.Equal(new[] { "R9-A1", "R9-A2" }, vm.Items.Select(r => r.AssetTag).OrderBy(t => t));
        Assert.Equal(2, vm.TotalCount);
        Assert.DoesNotContain("R9-B1", vm.Items.Select(r => r.AssetTag));
    }

    [Fact]
    public async Task Employee_search_and_counters_exclude_other_companies()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "حاسب مكتب ألف");
        await SeedAsset("R9-A2", CompanyAId, CatA, "كرسي ألف");
        await SeedAsset("R9-B1", CompanyBId, CatB, "حاسب مكتب باء");

        await using var db = NewDb(EmployeeA);

        var searched = Assert.IsType<AssetIndexViewModel>(Assert.IsType<ViewResult>(
            await List(Assets(db, EmployeeA), q: "حاسب مكتب")).Model);
        Assert.Single(searched.Items);
        Assert.Equal("R9-A1", Assert.Single(searched.Items).AssetTag);
        Assert.Equal(1, searched.TotalCount);

        var byStatus = Assert.IsType<AssetIndexViewModel>(Assert.IsType<ViewResult>(
            await List(Assets(db, EmployeeA), status: AssetStatus.InStore)).Model);
        // ثلاثة أصول InStore في القاعدة (ألفان وباءٌ)، والعداد لا يرى إلا أصلَي شركة ألف
        Assert.Equal(3, await db.Assets.IgnoreQueryFilters()
            .CountAsync(a => a.Status == AssetStatus.InStore));
        Assert.Equal(2, byStatus.TotalCount);
    }

    [Fact]
    public async Task Employee_list_lookups_carry_no_other_company_names()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف");

        await using var db = NewDb(EmployeeA);
        var vm = Assert.IsType<AssetIndexViewModel>(
            Assert.IsType<ViewResult>(await List(Assets(db, EmployeeA))).Model);

        Assert.Contains("تصنيف ألف", vm.Categories.Select(c => c.Name));
        Assert.DoesNotContain("تصنيف باء", vm.Categories.Select(c => c.Name));
        Assert.DoesNotContain("مخزن باء", vm.Warehouses.Select(w => w.Name));
        Assert.DoesNotContain("موقع باء", vm.CompanySites.Select(s => s.Name));
        Assert.DoesNotContain("قسم باء", vm.Departments.Select(d => d.Name));
    }

    [Fact]
    public async Task Employee_reads_details_of_a_same_company_asset_without_write_rights()
    {
        var id = await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف للقراءة");

        await using var db = NewDb(EmployeeA);
        var vm = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, EmployeeA).Details(id)).Model);

        Assert.Equal("أصل ألف للقراءة", vm.NameAr);
        Assert.Equal("R9-A1", vm.AssetTag);
        // القراءة مسموحة والموظف لا يملك زر التحرير — سياسة الكتابة لم تتوسّع
        Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task Manager_reads_own_company_asset_and_keeps_write_rights()
    {
        var own = await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف");
        var foreign = await SeedAsset("R9-B1", CompanyBId, CatB, "أصل باء");

        await using var db = NewDb(ManagerA);

        var vm = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, ManagerA).Details(own)).Model);
        Assert.True(vm.CanEdit);

        var refused = await Assets(db, ManagerA).Details(foreign);
        Assert.IsType<RedirectToActionResult>(refused);
    }

    // ─────────────── القراءة الممنوعة: أصل شركة أخرى ───────────────

    [Fact]
    public async Task Employee_cannot_read_another_company_asset_by_id()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف");
        var foreign = await SeedAsset("R9-B1", CompanyBId, CatB, "أصل باء سري");

        await using var db = NewDb(EmployeeA);
        var result = await Assets(db, EmployeeA).Details(foreign);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Employee_by_tag_resolves_own_asset_and_refuses_another_company_silently()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف");
        await SeedAsset("R9-B1", CompanyBId, CatB, "أصل باء");

        await using var db = NewDb(EmployeeA);

        var own = Assert.IsType<RedirectToActionResult>(await Assets(db, EmployeeA).ByTag("R9-A1"));
        Assert.Equal(nameof(AssetsController.Details), own.ActionName);

        var foreign = Assert.IsType<RedirectToActionResult>(await Assets(db, EmployeeA).ByTag("R9-B1"));
        Assert.Equal("Home", foreign.ControllerName);
        Assert.Null(foreign.RouteValues);
    }

    [Fact]
    public async Task Employee_cannot_download_the_qr_image_of_another_company_asset()
    {
        var own = await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف");
        var foreign = await SeedAsset("R9-B1", CompanyBId, CatB, "أصل باء");

        await using var db = NewDb(EmployeeA);

        // الضابط الموجب: أصل شركته يُعيد صورة فعلية
        var png = Assert.IsType<FileContentResult>(await Assets(db, EmployeeA).Qr(own));
        Assert.Equal("image/png", png.ContentType);

        var refused = Assert.IsType<RedirectToActionResult>(await Assets(db, EmployeeA).Qr(foreign));
        Assert.Equal("Home", refused.ControllerName);
    }

    // ─────────── كشف أسماء المستخدمين (لا فلتر company عليهم) ───────────

    [Fact]
    public async Task Employee_details_resolve_same_company_holder_but_not_a_foreign_one()
    {
        var heldByOwn = await SeedAsset("R9-A1", CompanyAId, CatA, "أصل بعهدة ألف", holderId: UserA);
        var heldByForeign = await SeedAsset("R9-A2", CompanyAId, CatA, "أصل بربط عابر",
            holderId: UserB);

        await using var db = NewDb(EmployeeA);

        var own = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, EmployeeA).Details(heldByOwn)).Model);
        Assert.Equal("فني ألف", own.CustodyUserName);
        Assert.Equal("tech@a.eg", own.CustodyUserEmail);

        var leak = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, EmployeeA).Details(heldByForeign)).Model);
        // الأصل مملوك لشركته فيُقرأ، لكن الاسم والبريد المملوكين لشركة أخرى لا يُكشفان
        Assert.Null(leak.CustodyUserName);
        Assert.Null(leak.CustodyUserEmail);
    }

    [Fact]
    public async Task Employee_list_resolves_same_company_holders_but_not_foreign_ones()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "أصل بعهدة ألف", holderId: UserA);
        await SeedAsset("R9-A2", CompanyAId, CatA, "أصل بربط عابر", holderId: UserB);

        await using var db = NewDb(EmployeeA);
        var vm = Assert.IsType<AssetIndexViewModel>(
            Assert.IsType<ViewResult>(await List(Assets(db, EmployeeA))).Model);

        Assert.Equal("فني ألف", vm.Items.Single(r => r.AssetTag == "R9-A1").CustodyUserName);
        Assert.Null(vm.Items.Single(r => r.AssetTag == "R9-A2").CustodyUserName);
    }

    [Fact]
    public async Task Employee_custody_history_hides_foreign_actor_names_but_keeps_own()
    {
        var asset = await SeedAsset("R9-A1", CompanyAId, CatA, "أصل بتاريخ عهدة");
        await using (var seed = NewUnscopedDb())
        {
            seed.CustodyLogs.Add(new CustodyLog
            {
                CompanyId = CompanyAId, AssetId = asset,
                Action = CustodyAction.Assign, Status = CustodyStatus.Accepted,
                NewUserId = UserA, AssignedByUserId = UserB,
                ActionDate = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var db = NewDb(EmployeeA);
        var vm = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, EmployeeA).Details(asset)).Model);

        var row = Assert.Single(vm.CustodyHistory);
        Assert.Equal("فني ألف", row.NewUserName);
        Assert.Null(row.AssignedByName);
    }

    // ─────────────── مدير النظام: لا تراجع عن سلوكه ───────────────

    [Fact]
    public async Task Admin_still_lists_every_company_and_resolves_any_holder_name()
    {
        await SeedAsset("R9-A1", CompanyAId, CatA, "أصل ألف", holderId: UserA);
        var foreign = await SeedAsset("R9-B1", CompanyBId, CatB, "أصل باء", holderId: UserB);

        await using var db = NewDb(Admin);

        var list = Assert.IsType<AssetIndexViewModel>(
            Assert.IsType<ViewResult>(await List(Assets(db, Admin))).Model);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal(2, list.TotalCount);

        var vm = Assert.IsType<AssetDetailsViewModel>(
            Assert.IsType<ViewResult>(await Assets(db, Admin).Details(foreign)).Model);
        Assert.Equal("فني باء", vm.CustodyUserName);
    }

    // ─────────── ربط السياسة: القراءة بلا سياسة دور، الكتابة بقيدها ───────────

    [Fact]
    public void Read_actions_have_no_role_policy_while_write_actions_keep_ManagerOrAdmin()
    {
        // الأساس [Authorize]: بلا هوية لا تُنفَّذ أي Action على هذا الكونترولر
        Assert.True(HasAuthorize(typeof(BaseController)), "BaseController بلا [Authorize]");

        // مسارات القراءة: لا سياسة دور — القرار هو عزل الشركة في الخادم لا الدور
        foreach (var action in new[] { "Index", "Details", "ByTag", "Qr", "Scan" })
            Assert.Equal(new string?[] { null }, PoliciesOf(action));

        // مسارات الكتابة: بقيت ManagerOrAdmin — لم تُوسَّع نتيجة إضافة حق القراءة
        foreach (var action in new[] { "Create", "Edit", "Dispose", "Labels" })
            Assert.Equal(new[] { Policies.ManagerOrAdmin }, PoliciesOf(action));
    }

    // السمة تُقرأ بالاسم لا بالنوع: نوع [Authorize] يعيش في وحدة لا يراها مشروع
    // الاختبار مباشرة، والمطلوب هنا وجود السمة وقيمة Policy التي سيراها الوسيط.
    private static bool HasAuthorize(MemberInfo m)
        => m.GetCustomAttributesData().Any(a => a.AttributeType.Name == "AuthorizeAttribute");

    /// <summary>سياسة كل overload للفعل — التوحيد يفشل لو ضاع القيد من نسخة واحدة.</summary>
    private static string?[] PoliciesOf(string action)
    {
        var methods = typeof(AssetsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == action).ToList();

        Assert.NotEmpty(methods);

        return methods.Select(m => m.GetCustomAttributesData()
                        .Where(a => a.AttributeType.Name == "AuthorizeAttribute")
                        .Select(a => a.NamedArguments
                            .FirstOrDefault(n => n.MemberInfo?.Name == "Policy")
                            .TypedValue.Value as string)
                        .FirstOrDefault())
                      .Distinct().OrderBy(p => p).ToArray();
    }
}
