using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetTracking.Tests;

/// <summary>
/// عدّة اختبارات طبقة العرض: تبني الـController الحقيقي على SQLite في الذاكرة،
/// فتُنفَّذ مسارات الكتابة/القراءة الفعلية (وليست دوال مساعدة معزولة) بنفس
/// العزل الذي يفرضه الفلتر العام في الإنتاج.
/// </summary>
public static class WebTestKit
{
    public sealed record FakeUser(
        int? CompanyId = null,
        bool IsAdmin = false,
        string UserId = "u-1",
        string[]? Roles = null) : ICurrentUser
    {
        public string? UserName => "test.user";
        public string? FullName => "مستخدم اختبار";
        public bool IsAuthenticated => true;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "xunit";
        public bool IsInRole(string role) => IsAdmin || (Roles ?? []).Contains(role);
    }

    /// <summary>
    /// TempData بلا تخزين: مسارات الـController تكتب رسائل Toast، والهدف أن ينجح
    /// المسار لا أن نُثبت شيء عن الرسائل.
    /// </summary>
    private sealed class NullTempData : Dictionary<string, object?>, ITempDataDictionary
    {
        public bool IsReadonly => false;
        public void Load() { }
        public void Save() { }
        public void Keep() { }
        public void Keep(string key) { }
        public void MarkUsed(string key) { }
        public object? Peek(string key) => TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>
    /// يُلحق الـController بـHttpContext حقيقي: ICurrentUser + TempData + ISequenceService
    /// على نفس السياق، فيُنفَّذ مسار الطلب كما في الإنتاج بلا Host كامل.
    /// </summary>
    public static T Attach<T>(this T controller, AppDbContext db, ICurrentUser me,
        IServiceProvider? provider = null) where T : Controller
    {
        provider ??= BuildProvider(db, me);

        var http = new DefaultHttpContext { RequestServices = provider };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = http,
            // UrlHelper يقرأ RouteData في البناء — بلا مسارات يفشل بـNullReference
            RouteData = new RouteData(),
        };
        controller.TempData = new NullTempData();
        return controller;
    }

    /// <summary>
    /// حاوية خدمات الطلب بنفس تسجيلات Attach — تُستخدم حين يحتاج الاختبار مصادرة
    /// خدمة من الحاوية نفسها (UserManager مثلاً) ثم تمريرها للـController.
    /// </summary>
    public static ServiceProvider BuildProvider(AppDbContext db, ICurrentUser me,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(me);
        services.AddSingleton(db);
        services.AddSingleton<ISequenceService, SequenceService>();
        // RedirectToAction يبني الرابط فوراً — بلا MVC لا يوجد IUrlHelperFactory
        services.AddRouting();
        services.AddMvcCore();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    /// <summary>رسالة خطأ ModelState لحقل معيّن (null إن لم يوجد).</summary>
    public static string? FieldError(this ControllerBase controller, string field)
        => controller.ModelState.TryGetValue(field, out var entry) && entry?.Errors.Count > 0
            ? entry.Errors[0].ErrorMessage
            : null;

    /// <summary>هل أعاد الـAction صفحة نموذج (فشل تحقق) أم تحويلًا (نجاح)؟</summary>
    public static bool ReturnedForm(IActionResult result) => result is ViewResult;
    public static bool ReturnedRedirect(IActionResult result) => result is RedirectToActionResult;

    /// <summary>Identity حقيقي على نفس قاعدة SQLite — لمسارات تتطلب UserManager.</summary>
    public static IServiceCollection AddIdentityOnSqlite(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        return services;
    }
}

/// <summary>إشعارات بلا بريد ولا قاعدة: تسجّل ما طُلب إرساله فقط.</summary>
public sealed class FakeNotificationService : INotificationService
{
    public List<string> Titles { get; } = new();

    public Task NotifyAsync(string userId, NotificationType type, string title, string body,
        string? actionUrl = null, int? companyId = null,
        NotificationChannel channel = NotificationChannel.InApp, CancellationToken ct = default)
    {
        Titles.Add(title);
        return Task.CompletedTask;
    }

    public Task NotifyRoleAsync(string role, int? companyId, NotificationType type, string title,
        string body, string? actionUrl = null, CancellationToken ct = default)
    {
        Titles.Add(title);
        return Task.CompletedTask;
    }

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default) => Task.FromResult(0);
    public Task MarkReadAsync(int notificationId, string userId, CancellationToken ct = default) => Task.CompletedTask;
    public Task MarkAllReadAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>قاعدة SQLite في الذاكرة على اتصال مفتوح واحد، مع شركتين وبيانهما المرجعية.</summary>
public abstract class TwoCompanyFixture : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnectionHolder _holder = new();
    protected DbContextOptions<AppDbContext> Options => _holder.Options;

    protected int CompanyAId { get; private set; }
    protected int CompanyBId { get; private set; }
    protected int DeptA { get; private set; }
    protected int DeptB { get; private set; }
    protected int SiteA { get; private set; }
    protected int SiteA2 { get; private set; }
    protected int SiteB { get; private set; }
    protected int WhA { get; private set; }
    protected int WhA2 { get; private set; }
    protected int WhB { get; private set; }
    protected int LineA { get; private set; }
    protected int LineB { get; private set; }
    protected int VendorA { get; private set; }
    protected int VendorB { get; private set; }
    protected int CatA { get; private set; }
    protected int CatB { get; private set; }
    protected int DeletedDeptA { get; private set; }
    protected string UserA { get; private set; } = "tech-a";
    protected string UserB { get; private set; } = "tech-b";

    public virtual async Task InitializeAsync()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();

        var a = new Company { NameAr = "شركة ألف", Code = "ALF", City = "القاهرة" };
        var b = new Company { NameAr = "شركة باء", Code = "BAA", City = "الجيزة" };
        db.Companies.AddRange(a, b);
        await db.SaveChangesAsync();
        CompanyAId = a.Id;
        CompanyBId = b.Id;

        var deptA = NewDept(a.Id, "قسم ألف");
        var deptB = NewDept(b.Id, "قسم باء");
        var deletedA = NewDept(a.Id, "قسم ألف محذوف");
        deletedA.IsDeleted = true;
        var siteA = new CompanySite { CompanyId = a.Id, NameAr = "موقع ألف", Kind = SiteKind.Factory, IsActive = true };
        var siteA2 = new CompanySite { CompanyId = a.Id, NameAr = "موقع ألف الثاني", Kind = SiteKind.Office, IsActive = true };
        var siteB = new CompanySite { CompanyId = b.Id, NameAr = "موقع باء", Kind = SiteKind.Branch, IsActive = true };
        // المخازن بلا موقع شركة: «مخزن مستقل» حالة كاملة لا نقص، فالربط يقيسه
        // الاختبار صراحةً ولا تبنيه البذرة فيخفي المسار المقصوص.
        var whA = new Warehouse { CompanyId = a.Id, NameAr = "مخزن ألف", IsActive = true };
        var whA2 = new Warehouse { CompanyId = a.Id, NameAr = "مخزن ألف الثاني", IsActive = true };
        var whB = new Warehouse { CompanyId = b.Id, NameAr = "مخزن باء", IsActive = true };
        var lineA = new ProductionLine { CompanyId = a.Id, NameAr = "خط ألف", IsActive = true };
        var lineB = new ProductionLine { CompanyId = b.Id, NameAr = "خط باء", IsActive = true };
        var venA = new Vendor { CompanyId = a.Id, NameAr = "مورّد ألف" };
        var venB = new Vendor { CompanyId = b.Id, NameAr = "مورّد باء" };
        var catA = new Category { CompanyId = a.Id, NameAr = "تصنيف ألف", IsActive = true };
        var catB = new Category { CompanyId = b.Id, NameAr = "تصنيف باء", IsActive = true };
        db.Departments.AddRange(deptA, deptB, deletedA);
        db.CompanySites.AddRange(siteA, siteA2, siteB);
        db.Warehouses.AddRange(whA, whA2, whB);
        db.ProductionLines.AddRange(lineA, lineB);
        db.Vendors.AddRange(venA, venB);
        db.Categories.AddRange(catA, catB);

        db.Users.Add(new ApplicationUser
        {
            Id = UserA, UserName = "tech@a.eg", Email = "tech@a.eg",
            FullName = "فني ألف", CompanyId = a.Id, IsActive = true, EmailConfirmed = true
        });
        db.Users.Add(new ApplicationUser
        {
            Id = UserB, UserName = "tech@b.eg", Email = "tech@b.eg",
            FullName = "فني باء", CompanyId = b.Id, IsActive = true, EmailConfirmed = true
        });

        // الأدوار موجودة في الإنتاج من البذرة — AddToRoleAsync يرفض دوراً غير موجود
        foreach (var roleName in Roles.All)
            db.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });

        await db.SaveChangesAsync();

        DeptA = deptA.Id; DeptB = deptB.Id;
        SiteA = siteA.Id; SiteA2 = siteA2.Id; SiteB = siteB.Id;
        WhA = whA.Id; WhA2 = whA2.Id; WhB = whB.Id;
        LineA = lineA.Id; LineB = lineB.Id;
        VendorA = venA.Id; VendorB = venB.Id;
        CatA = catA.Id; CatB = catB.Id;
        DeletedDeptA = deletedA.Id;
    }

    private static Department NewDept(int companyId, string name) => new()
    {
        CompanyId = companyId,
        NameAr = name,
        IsActive = true
    };

    protected AppDbContext NewDb(ICurrentUser? me = null)
        => _holder.NewDb(me);

    /// <summary>سياق بلا مستخدم — لكل الإعدادات التي تسبق تحديد هوية الطلب.</summary>
    protected AppDbContext NewUnscopedDb() => _holder.NewDb(null, ignoreCompanyFilter: true);

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _holder.Dispose();
}

internal sealed class SqliteConnectionHolder : IDisposable
{
    // المفاتيح الأجنبية مفعّلة صراحةً: اختبار القيد المركّب على Warehouses
    // (CompanySiteId, CompanyId) لا يعني شيئاً إن لم تكن القاعدة تفرضه. القياس هنا
    // هو الضابط: بكتابة المفتاح وحده مرّ الاختبار، وبـ «Foreign Keys=False» انقلب
    // إلى Failed: 1 — أي أن الرفض يأتي من القيد فعلاً لا من سبب آخر.
    // ملاحظة مقاسة: Microsoft.Data.Sqlite يفرض المفاتيح الأجنبية افتراضياً (عكس
    // سلوك sqlite الخام)، فحذف المفتاح لا يُسقط الاختبار؛ تُثبَّت القيمة صراحةً حتى
    // لا يتغيّر المعنى بتغيّر الإعداد أو المزوّد.
    private readonly SqliteConnection _connection = new("Filename=:memory:;Foreign Keys=True");

    public SqliteConnectionHolder()
    {
        _connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .RelationalEventId.AmbientTransactionWarning))
            .Options;
    }

    public DbContextOptions<AppDbContext> Options { get; }

    public AppDbContext NewDb(ICurrentUser? me, bool ignoreCompanyFilter = false)
    {
        var db = new AppDbContext(Options, me);
        if (ignoreCompanyFilter) db.IgnoreCompanyFilter = true;
        return db;
    }

    public void Dispose() => _connection.Dispose();
}
