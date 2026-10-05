using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Email;
using AssetTracking.Infrastructure.Identity;
using AssetTracking.Infrastructure.Jobs;
using AssetTracking.Infrastructure.Services;
using AssetTracking.Web.Hubs;
using AssetTracking.Web.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog: ملفات يومية + الأخطاء الحرجة ─────────────────
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/ats-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

// ── قاعدة البيانات: SQL Server للإنتاج | SQLite للتطوير ────
var provider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=assettracking.db";

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        opt.UseSqlite(connectionString, b => b.MigrationsAssembly("AssetTracking.Infrastructure"));
    else
        opt.UseSqlServer(connectionString, b =>
        {
            b.MigrationsAssembly("AssetTracking.Infrastructure");
            b.EnableRetryOnFailure(3);
        });
});

// ── الهوية والصلاحيات (ASP.NET Core Identity) ─────────────
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireNonAlphanumeric = true;

        // قفل الحساب بعد ٥ محاولات فاشلة
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AssetTracking.Web.Controllers.AppClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
    o.Cookie.Name = "ats_auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// ── سياسات التصريح (Role Policies) ────────────────────────
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
    o.AddPolicy(Policies.ManagerOrAdmin, p => p.RequireRole(Roles.Admin, Roles.CompanyManager));
    o.AddPolicy(Policies.TechnicianOrAbove, p =>
        p.RequireRole(Roles.Admin, Roles.CompanyManager, Roles.Technician));
    o.AddPolicy(Policies.AuthenticatedUser, p => p.RequireAuthenticatedUser());
});

// ── الخدمات ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ISequenceService, SequenceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<IDepreciationService, DepreciationService>();
builder.Services.AddScoped<ICustodyService, CustodyService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<MaintenanceJobs>();

builder.Services.AddScoped<IQrCodeService>(sp =>
    new QrCodeService(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath));

// ── Hangfire: SQL Storage للإنتاج | Memory للتطوير ────────
var useHangfireSql = !provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings();

    if (useHangfireSql) cfg.UseSqlServerStorage(connectionString);
    else cfg.UseMemoryStorage();
});

// خادم المهام الخلفية — يمكن تعطيله في البيئات محدودة الذاكرة
// (كل عامل Worker يستهلك خيطاً؛ الافتراضي = مُفعَّل)
var enableJobs = builder.Configuration.GetValue("EnableBackgroundJobs", true);
if (enableJobs)
{
    builder.Services.AddHangfireServer(o =>
    {
        // في التطوير نقلّل عدد العاملين لتوفير الذاكرة
        o.WorkerCount = useHangfireSql ? Math.Min(20, Environment.ProcessorCount * 5) : 2;
    });
}

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// ── خط أنابيب الطلبات ─────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    // لا Stack Trace للمستخدم أبداً — تذهب لـ Serilog
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/health");

// لوحة Hangfire — للـ Admin فقط
app.MapHangfireDashboard("/jobs", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminFilter() }
});

// الرابط المختصر لمسح QR: /a/AST-2026-00001
app.MapGet("/a/{tag}", (string tag) => Results.Redirect($"/Assets/ByTag/{tag}"));

// ── تهيئة قاعدة البيانات والبيانات التجريبية ──────────────
// فشل التهيئة لا يجب أن يمنع إقلاع التطبيق — يُسجَّل ويُكمَل
try
{
    await DbSeeder.SeedAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "فشلت تهيئة قاعدة البيانات — التطبيق سيقلع بدون بيانات تجريبية");
}

// ── جدولة المهام الخلفية ──────────────────────────────────
if (enableJobs)
{
    RecurringJob.AddOrUpdate<MaintenanceJobs>("sla-escalation",
        j => j.SlaEscalationJob(), "*/15 * * * *");
    RecurringJob.AddOrUpdate<MaintenanceJobs>("depreciation-monthly",
        j => j.DepreciationJob(), Cron.Monthly(1, 2));
    RecurringJob.AddOrUpdate<MaintenanceJobs>("preventive-maintenance",
        j => j.PreventiveMaintenanceJob(), Cron.Daily(6));
    RecurringJob.AddOrUpdate<MaintenanceJobs>("warranty-expiry",
        j => j.WarrantyExpiryAlertJob(), Cron.Daily(7));
    RecurringJob.AddOrUpdate<MaintenanceJobs>("ticket-reminder",
        j => j.TicketReminderJob(), Cron.Daily(8));
    RecurringJob.AddOrUpdate<MaintenanceJobs>("data-cleanup",
        j => j.DataCleanupJob(), Cron.Weekly(DayOfWeek.Sunday, 3));
}

app.Run();


/// <summary>يقصر لوحة Hangfire على مدير النظام</summary>
public class HangfireAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var user = http.User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Admin);
    }
}
