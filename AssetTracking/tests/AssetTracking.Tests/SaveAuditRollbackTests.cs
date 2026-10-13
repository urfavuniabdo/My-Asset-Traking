using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Infrastructure.Services;
using AssetTracking.Web.Controllers;
using AssetTracking.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetTracking.Tests;

/// <summary>
/// بقية مسارات «حفظ + تدقيق»: الجداول الدورية والجرد والتذاكر.
/// كل اختبار يؤكد أن محاولة التدقيق وقعت فعلاً (Actions يسجّل قبل الرمي) حتى لا
/// ينجح الاختبار لمجرد أن المسار خرج مبكراً قبل الحاجز.
/// </summary>
public class SaveAuditRollbackTests : TwoCompanyFixture
{
    private WebTestKit.FakeUser ManagerA => new(CompanyId: CompanyAId, Roles: [Roles.CompanyManager]);

    private static FakeAuditService Failing() =>
        new() { ThrowOn = _ => new InvalidOperationException("تعذّر كتابة سجل التدقيق") };

    private async Task<int> SeedAssetAsync(string tag)
    {
        await using var seed = NewUnscopedDb();
        var a = new Asset
        {
            CompanyId = CompanyAId, AssetTag = tag, NameAr = "أصل",
            CategoryId = CatA, WarehouseId = WhA, Status = AssetStatus.Active
        };
        seed.Assets.Add(a);
        await seed.SaveChangesAsync();
        return a.Id;
    }

    private MaintenanceSchedulesController Schedules(AppDbContext db, IAuditService audit)
        => new MaintenanceSchedulesController(db, new SequenceService(db), new SlaService(db),
                new FakeNotificationService(), audit)
            .Attach(db, ManagerA);

    private InventoryAuditsController Audits(AppDbContext db, IAuditService audit)
        => new InventoryAuditsController(db, new SequenceService(db), audit,
                new FakeNotificationService())
            .Attach(db, ManagerA);

    private (TicketsController Controller, ServiceProvider Provider) Tickets(
        AppDbContext db, IAuditService audit)
    {
        var provider = WebTestKit.BuildProvider(db, ManagerA, s => s.AddIdentityOnSqlite());
        var controller = new TicketsController(db,
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                new SequenceService(db), new SlaService(db),
                new FakeNotificationService(), audit)
            .Attach(db, ManagerA, provider);
        return (controller, provider);
    }

    // ──────────────── جدول الصيانة الدورية ────────────────
    [Fact]
    public async Task ScheduleCreate_rolls_back_when_the_audit_fails()
    {
        var assetId = await SeedAssetAsync("AST-RB-S1");
        await using var db = NewDb(ManagerA);
        var audit = Failing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Schedules(db, audit).Create(new ScheduleFormViewModel
            {
                AssetId = assetId, Title = "جدول غير قابل للحفظ", DefaultTechnicianId = UserA
            }));

        Assert.Contains("Create", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.MaintenanceSchedules.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ScheduleCreate_saves_the_schedule_and_its_audit_together()
    {
        var assetId = await SeedAssetAsync("AST-RB-S2");
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();

        var result = await Schedules(db, audit).Create(new ScheduleFormViewModel
        {
            AssetId = assetId, Title = "جدول سليم", DefaultTechnicianId = UserA
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        await using var check = NewUnscopedDb();
        Assert.Equal(1, await check.MaintenanceSchedules.CountAsync());
    }

    // ──────────────── عملية الجرد ────────────────
    [Fact]
    public async Task AuditCreate_rolls_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Audits(db, audit).Create(new AuditFormViewModel
            {
                Title = "جرد يفشل تدقيقه", WarehouseId = WhA, ResponsibleUserId = UserA,
                ScheduledDate = DateTime.UtcNow.Date
            }));

        Assert.Contains("Create", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.InventoryAudits.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task AuditEdit_rolls_back_when_the_audit_fails()
    {
        int auditId;
        await using (var seed = NewUnscopedDb())
        {
            var a = new InventoryAudit
            {
                CompanyId = CompanyAId, Code = "AUD-RB-1", Title = "جرد أصلي",
                WarehouseId = WhA, ScheduledDate = DateTime.UtcNow.Date, Status = AuditStatus.Draft
            };
            seed.InventoryAudits.Add(a);
            await seed.SaveChangesAsync();
            auditId = a.Id;
        }

        await using var db = NewDb(ManagerA);
        var audit = Failing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Audits(db, audit).Edit(new AuditFormViewModel
            {
                Id = auditId, Title = "اسم معدَّل", WarehouseId = WhA,
                ResponsibleUserId = UserA, ScheduledDate = DateTime.UtcNow.Date
            }));

        Assert.Contains("Update", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal("جرد أصلي", (await check.InventoryAudits.SingleAsync(x => x.Id == auditId)).Title);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    // ──────────────── إدارة المراجع (كاتب التدقيق الموحَّد) ────────────────
    private (AdminController Controller, ServiceProvider Provider) Admin(
        AppDbContext db, IAuditService audit, Action<IServiceCollection>? extraServices = null)
    {
        var provider = WebTestKit.BuildProvider(db, ManagerA, s =>
        {
            s.AddIdentityOnSqlite();
            extraServices?.Invoke(s);
        });
        var controller = new AdminController(db,
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                provider.GetRequiredService<RoleManager<ApplicationRole>>(),
                Microsoft.Extensions.Options.Options.Create(new AssetTracking.Web.Configuration.FeatureFlags()),
                audit, new ProductionLineService(db, ManagerA, audit))
            .Attach(db, ManagerA, provider);
        return (controller, provider);
    }

    [Fact]
    public async Task DepartmentCreate_rolls_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.CreateDepartment(new DepartmentFormViewModel
            {
                NameAr = "قسم لا يجب أن يُحفظ", CompanyId = CompanyAId
            }));

        Assert.Contains("CreateDepartment", audit.Actions);
        await using var check = NewUnscopedDb();
        // قسمان حيّان من البذرة فقط — القسم الجديد رُجع مع فشل التدقيق
        Assert.Equal(2, await check.Departments.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ToggleRef_rolls_the_flag_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.ToggleRef("departments", DeptA));

        Assert.Contains("ToggleRef", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.True((await check.Departments.SingleAsync(x => x.Id == DeptA)).IsActive);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>
    /// إثبات التوحيد: الكاتب الموحَّد يحفظ نفس شكل النص القديم الذي تعرضه صفحات
    /// الإدارة (قيمة خام لا JSON) ونفس الأعمدة، فلا تنكسر صفحة سجل التدقيق.
    /// </summary>
    [Fact]
    public async Task Admin_audit_row_keeps_the_legacy_raw_text_shape()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db, new AuditService(db, ManagerA));

        var result = await controller.CreateDepartment(new DepartmentFormViewModel
        {
            NameAr = "قسم مقبول", CompanyId = CompanyAId
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        await using var check = NewUnscopedDb();
        var row = await check.AuditLogs.SingleAsync(x => x.Action == "CreateDepartment");
        var created = await check.Departments.SingleAsync(x => x.NameAr == "قسم مقبول");

        Assert.Equal(nameof(Department), row.EntityName);
        Assert.Equal(created.Id.ToString(), row.EntityId);
        Assert.Equal("قسم مقبول", row.NewValues);
        Assert.Null(row.OldValues);
        Assert.Equal(ManagerA.UserId, row.UserId);
        Assert.Equal(ManagerA.FullName, row.UserName);
        Assert.Equal(CompanyAId, row.CompanyId);
        Assert.Equal("127.0.0.1", row.IpAddress);
    }

    // ──────────────── حالة التذكرة ────────────────
    private async Task<int> SeedTicketAsync()
    {
        var assetId = await SeedAssetAsync("AST-RB-T1");
        await using var seed = NewUnscopedDb();
        var t = new MaintenanceTicket
        {
            CompanyId = CompanyAId, AssetId = assetId, TicketNumber = "TKT-RB-1",
            Title = "تذكرة اختبار", Status = TicketStatus.Open
        };
        seed.MaintenanceTickets.Add(t);
        await seed.SaveChangesAsync();
        return t.Id;
    }

    [Fact]
    public async Task ChangeStatus_rolls_back_ticket_and_history_when_the_audit_fails()
    {
        var id = await SeedTicketAsync();
        await using var db = NewDb(ManagerA);
        var audit = Failing();

        var (controller, _) = Tickets(db, audit);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.ChangeStatus(id, TicketStatus.InProgress, "بدأ الفني"));

        Assert.Contains("ChangeStatus", audit.Actions);
        await using var check = NewUnscopedDb();
        var t = await check.MaintenanceTickets.SingleAsync(x => x.Id == id);
        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Equal(0, await check.TicketLogs.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ChangeStatus_persists_ticket_history_and_audit_together()
    {
        var id = await SeedTicketAsync();
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();

        var (controller, _) = Tickets(db, audit);
        await controller.ChangeStatus(id, TicketStatus.InProgress, "بدأ الفني");

        await using var check = NewUnscopedDb();
        Assert.Equal(TicketStatus.InProgress, (await check.MaintenanceTickets.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(1, await check.TicketLogs.CountAsync());
    }

    // ──────────────── مسارات الهوية: التغيير + التدقيق ذرّة واحدة ────────────────
    [Fact]
    public async Task ToggleUser_rolls_the_active_flag_back_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ToggleUser(UserA));

        Assert.Contains("ToggleUser", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.True((await check.Users.SingleAsync(u => u.Id == UserA)).IsActive);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ToggleUser_saves_the_flag_and_its_audit_together()
    {
        await using var db = NewDb(ManagerA);
        // الكاتب الحقيقي هنا: إثبات أن سطر التدقيق يُلتزم فعلاً مع التغيير لا أن يسجّله فقط
        var (controller, _) = Admin(db, new AuditService(db, ManagerA));

        Assert.True(WebTestKit.ReturnedRedirect(await controller.ToggleUser(UserA)));

        await using var check = NewUnscopedDb();
        Assert.False((await check.Users.SingleAsync(u => u.Id == UserA)).IsActive);
        Assert.Equal(1, await check.AuditLogs.CountAsync(x => x.Action == "ToggleUser"));
    }

    [Fact]
    public async Task UnlockUser_rolls_the_lockout_back_when_the_audit_fails()
    {
        var until = DateTime.UtcNow.AddDays(1);
        await using (var seed = NewUnscopedDb())
        {
            await seed.Users.Where(u => u.Id == UserA)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LockoutEnabled, true)
                    .SetProperty(u => u.LockoutEnd, until)
                    .SetProperty(u => u.AccessFailedCount, 7));
        }

        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.UnlockUser(UserA));

        Assert.Contains("UnlockUser", audit.Actions);
        await using var check = NewUnscopedDb();
        var u = await check.Users.SingleAsync(x => x.Id == UserA);
        Assert.NotNull(u.LockoutEnd);
        Assert.Equal(7, u.AccessFailedCount);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>
    /// مزوّد توكن ثابت: مشروع الاختبار لا يسند Microsoft.AspNetCore.App فلا يصل
    /// AddDefaultTokenProviders، والتوكن هنا وسيلةٌ لبلوغ الذيل المقصوص لا موضوع القياس.
    /// </summary>
    private sealed class StubResetTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager,
            ApplicationUser user) => Task.FromResult("reset-token");

        public Task<bool> ValidateAsync(string purpose, string token,
            UserManager<ApplicationUser> manager, ApplicationUser user)
            => Task.FromResult(token == "reset-token");

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager,
            ApplicationUser user) => Task.FromResult(true);

        public Task<ApplicationUser?> GetUserAsync(string token, UserManager<ApplicationUser> manager)
            => Task.FromResult<ApplicationUser?>(null);
    }

    private static void WithResetTokenProvider(IServiceCollection services)
    {
        new IdentityBuilder(typeof(ApplicationUser), typeof(ApplicationRole), services)
            .AddTokenProvider(TokenOptions.DefaultProvider, typeof(StubResetTokenProvider));
    }

    [Fact]
    public async Task ResetPassword_rolls_the_hash_flag_and_audit_back_when_the_audit_fails()
    {
        string originalHash;
        await using (var seed = NewUnscopedDb())
        {
            var u = await seed.Users.SingleAsync(x => x.Id == UserA);
            // لا بذرة تحمل كلمة مرور — الترميز مطلوب حتى ينجح مسار إعادة التعيين فعلاً
            u.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(u, "Old@12345");
            await seed.SaveChangesAsync();
            originalHash = u.PasswordHash!;
        }

        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit, WithResetTokenProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.ResetPassword(UserA, "New@12345"));

        Assert.Contains("ResetPassword", audit.Actions);
        await using var check = NewUnscopedDb();
        var stored = await check.Users.SingleAsync(u => u.Id == UserA);
        Assert.False(stored.MustChangePassword);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
        // الذرّية الكاملة بعد نقل إعادة التعيين داخل التعهّد: الـhash نفسه يرجع،
        // فلا يبقى «كلمة مرور جديدة بلا راية وبلا أثر تدقيقي».
        Assert.Equal(originalHash, stored.PasswordHash);
    }

    /// <summary>
    /// ضابط موجب على نفس المسار: الترميز الجديد والراية وسطر التدقيق تُلتزم معاً.
    /// الفرق عن الاختبار السابق هو كاتب التدقيق وحده، فالتراجع لا يُنسب لكتابة
    /// تدقيق معطوبة.
    /// </summary>
    [Fact]
    public async Task ResetPassword_persists_the_hash_flag_and_audit_together()
    {
        string originalHash;
        await using (var seed = NewUnscopedDb())
        {
            var u = await seed.Users.SingleAsync(x => x.Id == UserA);
            u.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(u, "Old@12345");
            await seed.SaveChangesAsync();
            originalHash = u.PasswordHash!;
        }

        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db, new AuditService(db, ManagerA), WithResetTokenProvider);

        Assert.True(WebTestKit.ReturnedRedirect(await controller.ResetPassword(UserA, "New@12345")));

        await using var check = NewUnscopedDb();
        var stored = await check.Users.SingleAsync(u => u.Id == UserA);
        Assert.NotEqual(originalHash, stored.PasswordHash);
        Assert.True(stored.MustChangePassword);
        Assert.Equal(1, await check.AuditLogs.CountAsync(a => a.Action == "ResetPassword"));
    }

    /// <summary>
    /// رفض سياسة كلمة المرور نتيجة لا استثناء: الشكل المحفوظ هو رسالة الـToast
    /// والتحويل نفسه اللذان كانا قبل النقل، والحالة المخزّنة لا تتغيّر إطلاقاً.
    /// </summary>
    [Fact]
    public async Task ResetPassword_with_a_policy_rejected_password_keeps_the_Identity_message_and_writes_nothing()
    {
        string originalHash;
        await using (var seed = NewUnscopedDb())
        {
            var u = await seed.Users.SingleAsync(x => x.Id == UserA);
            u.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(u, "Old@12345");
            await seed.SaveChangesAsync();
            originalHash = u.PasswordHash!;
        }

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var (controller, _) = Admin(db, audit, WithResetTokenProvider);

        // ١٢ حرفاً صغيراً: يمرّ من فحص المتجر (≥٨) ويرفضه Identity
        var result = await controller.ResetPassword(UserA, "abcdefghijkl");

        Assert.True(WebTestKit.ReturnedRedirect(result));
        Assert.Contains("كلمة المرور يجب أن تحتوي رقماً",
            controller.TempData["ToastError"] as string);
        Assert.Empty(audit.Actions);

        await using var check = NewUnscopedDb();
        var stored = await check.Users.SingleAsync(u => u.Id == UserA);
        Assert.Equal(originalHash, stored.PasswordHash);
        Assert.False(stored.MustChangePassword);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// ضابط موجب على نفس المسار: الحساب والدور وسطر التدقيق تُلتزم معاً عند النجاح.
    /// الكاتب حقيقي (AuditService) حتى يكون عدّ السطر دليلاً على الالتزام لا على استدعاء.
    [Fact]
    public async Task CreateUser_persists_account_role_and_audit_together()
    {
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db, new AuditService(db, ManagerA));

        var result = await controller.CreateUser(new UserFormViewModel
        {
            FullName = "موظف سليم", Email = "ok@a.eg", Password = "Test@1234",
            Role = Roles.Employee, DepartmentId = DeptA
        });

        Assert.True(WebTestKit.ReturnedRedirect(result));
        await using var check = NewUnscopedDb();
        var created = await check.Users.SingleAsync(u => u.Email == "ok@a.eg");
        Assert.Equal(DeptA, created.DepartmentId);
        Assert.Equal(1, await check.UserRoles.CountAsync(r => r.UserId == created.Id));
        Assert.Equal(1, await check.AuditLogs.CountAsync(a => a.Action == "CreateUser"));
    }

    /// <summary>
    /// عقد منح الدور مُقاس لا مُفترض: <c>UserStore.AddToRoleAsync</c> يرمي
    /// <c>InvalidOperationException : Role X does not exist.</c> ولا يُرجع IdentityResult فاشلاً،
    /// فشكل الاستجابة (500) كان كذلك قبل النقل وبقاه — المقيس هنا هو الحالة المخزّنة:
    /// الحساب والدور وسطر التدقيق كلها تتراجع معاً، فلا يبقى موظف بلا دور.
    /// </summary>
    [Fact]
    public async Task CreateUser_rolls_the_account_back_when_the_role_grant_throws()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateUser(new UserFormViewModel
        {
            FullName = "موظف بلا دور", Email = "norole@a.eg", Password = "Test@1234",
            Role = "RoleDoesNotExist", DepartmentId = DeptA
        }));

        Assert.Empty(audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Users.CountAsync(u => u.Email == "norole@a.eg"));
        Assert.Equal(0, await check.UserRoles.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task CreateUser_with_a_rejected_password_keeps_Identity_errors_and_writes_nothing()
    {
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var (controller, _) = Admin(db, audit);

        var result = await controller.CreateUser(new UserFormViewModel
        {
            FullName = "موظف بكلمة سر ضعيفة", Email = "weak@a.eg", Password = "a1",
            Role = Roles.Employee, DepartmentId = DeptA
        });

        Assert.True(WebTestKit.ReturnedForm(result));
        // رسالة Identity المعروضة محفوظة كما كانت (لا رسالة عامة مكانها)
        Assert.Contains("كلمة المرور قصيرة جداً",
            controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        Assert.Empty(audit.Actions);

        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Users.CountAsync(u => u.Email == "weak@a.eg"));
        Assert.Equal(0, await check.AuditLogs.CountAsync());
        // البيانات المدخلة تعود للنموذج ليُصحّحها المستخدم
        Assert.Equal("موظف بكلمة سر ضعيفة",
            Assert.IsType<UserFormViewModel>(Assert.IsType<ViewResult>(result).Model).FullName);
    }

    /// <summary>
    /// الذرّية الكاملة: فشل كتابة التدقيق يرجع الحساب والدور معاً. كان الحدّ الموثّق
    /// أن CreateAsync يلتزم قبل التعهّد فيبقى الحساب — نُفِّذ نقله داخل التعهّد لأن
    /// Identity يكتب على نفس السياق، فحفظه يُلتزم مع التعهّد لا قبله.
    /// </summary>
    [Fact]
    public async Task CreateUser_rolls_back_the_account_role_and_audit_when_the_audit_fails()
    {
        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateUser(new UserFormViewModel
        {
            FullName = "موظف معلّق", Email = "hang@a.eg", Password = "Test@1234",
            Role = Roles.Employee, DepartmentId = DeptA
        }));

        Assert.Contains("CreateUser", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(0, await check.Users.CountAsync(u => u.Email == "hang@a.eg"));
        Assert.Equal(0, await check.UserRoles.CountAsync());
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    // ──────────────── مسار التعديل: ملف + دور + كلمة مرور + تدقيق ────────────────

    /// <summary>
    /// بذرة دور كلمة مرور لمستخدم الاختبار: مسارات EditUser تحتاج كليهما حتى تبلغ
    /// ذيلها المقصوص بدل أن تفشل قبله بسبب مستخدم بلا دور ولا قفل.
    /// </summary>
    private async Task SeedRoleAsync(string roleName)
    {
        await using var seed = NewUnscopedDb();
        var u = await seed.Users.SingleAsync(x => x.Id == UserA);
        u.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(u, "Old@12345");
        var roleId = (await seed.Roles.SingleAsync(r => r.Name == roleName)).Id;
        seed.UserRoles.Add(new IdentityUserRole<string> { UserId = UserA, RoleId = roleId });
        await seed.SaveChangesAsync();
    }

    private Task<List<string?>> RolesOfAsync(AppDbContext db)
        => db.UserRoles.Where(ur => ur.UserId == UserA)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();

    private UserFormViewModel EditVm(string role, string name, string? password = null) => new()
    {
        Id = UserA, FullName = name, Email = "tech@a.eg", Role = role,
        DepartmentId = DeptA, IsActive = true, Password = password
    };

    [Fact]
    public async Task EditUser_persists_profile_role_and_audit_together()
    {
        await SeedRoleAsync(Roles.Employee);
        await using var db = NewDb(ManagerA);
        var (controller, _) = Admin(db, new AuditService(db, ManagerA));

        Assert.True(WebTestKit.ReturnedRedirect(
            await controller.EditUser(EditVm(Roles.Technician, "فني ألف محذَّل"))));

        await using var check = NewUnscopedDb();
        var stored = await check.Users.SingleAsync(u => u.Id == UserA);
        Assert.Equal("فني ألف محذَّل", stored.FullName);
        Assert.Equal(Roles.Technician, Assert.Single(await RolesOfAsync(check)));
        Assert.Equal(1, await check.AuditLogs.CountAsync(a => a.Action == "EditUser"));
    }

    [Fact]
    public async Task EditUser_rolls_the_profile_and_role_back_when_the_audit_fails()
    {
        await SeedRoleAsync(Roles.Employee);
        await using var db = NewDb(ManagerA);
        var audit = Failing();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.EditUser(EditVm(Roles.Technician, "فني ألف محذَّل")));

        Assert.Contains("EditUser", audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal("فني ألف", (await check.Users.SingleAsync(u => u.Id == UserA)).FullName);
        Assert.Equal(Roles.Employee, Assert.Single(await RolesOfAsync(check)));
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>
    /// الدور غير الموجود يرمي من UserStore (مقيس) بعد حذف الدور السابق: التعهّد يرجع
    /// الحذف فلا يبقى موظف بلا دور — وهو الشكل الذي كان يُترك قبل النقل.
    /// </summary>
    [Fact]
    public async Task EditUser_rolls_the_role_removal_back_when_the_grant_throws()
    {
        await SeedRoleAsync(Roles.Employee);
        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var (controller, _) = Admin(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.EditUser(EditVm("RoleDoesNotExist", "فني ألف محذَّل")));

        Assert.Empty(audit.Actions);
        await using var check = NewUnscopedDb();
        Assert.Equal(Roles.Employee, Assert.Single(await RolesOfAsync(check)));
        Assert.Equal("فني ألف", (await check.Users.SingleAsync(u => u.Id == UserA)).FullName);
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }

    /// <summary>
    /// كلمة مرور ترفضها السياسة: نتيجة لا استثناء، فيبقى عقد إعادة عرض النموذج
    /// (View + رسائل Identity في ModelState) كما كان، وترجع معه كل الكتابة السابقة.
    /// </summary>
    [Fact]
    public async Task EditUser_with_a_policy_rejected_password_rolls_back_profile_role_and_hash()
    {
        await SeedRoleAsync(Roles.Employee);
        string originalHash;
        await using (var probe = NewUnscopedDb())
            originalHash = (await probe.Users.SingleAsync(u => u.Id == UserA)).PasswordHash!;

        await using var db = NewDb(ManagerA);
        var audit = new FakeAuditService();
        var (controller, _) = Admin(db, audit, WithResetTokenProvider);

        // ١٢ حرفاً صغيراً: يمرّ من فحص المتجر (≥٨) ويرفضه Identity
        var result = await controller.EditUser(EditVm(Roles.Technician, "فني ألف محذَّل", "abcdefghijkl"));

        Assert.True(WebTestKit.ReturnedForm(result));
        Assert.Contains("كلمة المرور يجب أن تحتوي رقماً",
            controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        Assert.Empty(audit.Actions);

        await using var check = NewUnscopedDb();
        var stored = await check.Users.SingleAsync(u => u.Id == UserA);
        Assert.Equal("فني ألف", stored.FullName);
        Assert.Equal(originalHash, stored.PasswordHash);
        Assert.Equal(Roles.Employee, Assert.Single(await RolesOfAsync(check)));
        Assert.Equal(0, await check.AuditLogs.CountAsync());
    }
}
