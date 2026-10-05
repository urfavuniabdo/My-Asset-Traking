using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssetTracking.Infrastructure.Data;

/// <summary>
/// سياق قاعدة البيانات الرئيسي.
///
/// ⚠️ حرج — عزل بيانات الشركات:
/// يُطبَّق <b>EF Core Global Query Filter</b> على كل كيان يورّث ICompanyOwned، بحيث
/// لا يستطيع أي مستخدم غير Admin رؤية بيانات شركة أخرى — على مستوى قاعدة البيانات
/// وليس الواجهة فقط. هذا هو الخط الدفاعي الأول، ويُدعَّم بفحص ملكية السجل في
/// الـ Controllers (Defense in Depth).
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    private readonly ICurrentUser? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // ── الجداول ───────────────────────────────
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<CustodyLog> CustodyLogs => Set<CustodyLog>();
    public DbSet<LocationLog> LocationLogs => Set<LocationLog>();
    public DbSet<DepreciationEntry> DepreciationEntries => Set<DepreciationEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketLog> TicketLogs => Set<TicketLog>();
    public DbSet<TicketPart> TicketParts => Set<TicketPart>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<InventoryAudit> InventoryAudits => Set<InventoryAudit>();
    public DbSet<InventoryAuditItem> InventoryAuditItems => Set<InventoryAuditItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    /// <summary>
    /// يسمح بتعطيل فلتر الشركة مؤقتاً للمهام الخلفية (Hangfire) التي تعمل بلا مستخدم.
    /// </summary>
    public bool IgnoreCompanyFilter { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // تحميل كل إعدادات الكيانات من مجلد Configurations
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── تطبيق الفلاتر العامة تلقائياً على كل الكيانات ──────────────
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var isSoftDelete = typeof(BaseEntity).IsAssignableFrom(clrType);
            var isCompanyOwned = typeof(ICompanyOwned).IsAssignableFrom(clrType);

            if (!isSoftDelete && !isCompanyOwned) continue;

            var param = Expression.Parameter(clrType, "e");
            Expression? body = null;

            // 1) فلتر الحذف الناعم: !e.IsDeleted
            if (isSoftDelete)
            {
                body = Expression.Not(Expression.Property(param, nameof(BaseEntity.IsDeleted)));
            }

            // 2) فلتر عزل الشركة: IgnoreCompanyFilter || IsAdmin || e.CompanyId == CurrentCompanyId
            if (isCompanyOwned)
            {
                var companyProp = Expression.Property(param, nameof(ICompanyOwned.CompanyId));

                // استدعاء دوال على this يجعل الفلتر ديناميكياً (يُعاد تقييمه لكل استعلام)
                var ignoreCall = Expression.Call(
                    Expression.Constant(this),
                    typeof(AppDbContext).GetMethod(nameof(GetIgnoreCompanyFilter))!);

                var currentCompanyCall = Expression.Call(
                    Expression.Constant(this),
                    typeof(AppDbContext).GetMethod(nameof(GetCurrentCompanyId))!);

                // e.CompanyId == (currentCompanyId ?? -1)
                var companyMatch = Expression.Equal(
                    companyProp,
                    Expression.Coalesce(currentCompanyCall, Expression.Constant(-1)));

                var companyFilter = Expression.OrElse(ignoreCall, companyMatch);

                body = body == null ? companyFilter : Expression.AndAlso(body, companyFilter);
            }

            builder.Entity(clrType).HasQueryFilter(Expression.Lambda(body!, param));
        }
    }

    /// <summary>يُستدعى من داخل الفلتر العام — لا تستخدمه مباشرة.</summary>
    public bool GetIgnoreCompanyFilter()
        => IgnoreCompanyFilter || _currentUser == null || _currentUser.IsAdmin || !_currentUser.IsAuthenticated;

    /// <summary>يُستدعى من داخل الفلتر العام — لا تستخدمه مباشرة.</summary>
    public int? GetCurrentCompanyId() => _currentUser?.CompanyId;

    // ── حفظ تلقائي لحقول التتبع + الحذف الناعم ────────────────
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
        var userId = _currentUser?.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                // تحويل الحذف الفعلي إلى حذف ناعم — لا نفقد أي بيانات أبداً
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.DeletedBy = userId;
                    break;
            }
        }
    }
}
