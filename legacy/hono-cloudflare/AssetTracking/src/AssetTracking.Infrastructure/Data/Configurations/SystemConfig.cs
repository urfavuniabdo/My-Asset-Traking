using AssetTracking.Domain.Entities;
using AssetTracking.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

public class InventoryAuditConfig : IEntityTypeConfiguration<InventoryAudit>
{
    public void Configure(EntityTypeBuilder<InventoryAudit> b)
    {
        b.ToTable("InventoryAudits");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.ResponsibleUserId).HasMaxLength(450);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.CompanyId);
    }
}

public class InventoryAuditItemConfig : IEntityTypeConfiguration<InventoryAuditItem>
{
    public void Configure(EntityTypeBuilder<InventoryAuditItem> b)
    {
        b.ToTable("InventoryAuditItems");
        b.Property(x => x.ScannedByUserId).HasMaxLength(450);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Result).HasConversion<int>();

        b.HasOne(x => x.Audit).WithMany(a => a.Items)
            .HasForeignKey(x => x.AuditId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Asset).WithMany()
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.AuditId, x.AssetId }).IsUnique();
        b.HasIndex(x => x.CompanyId);
    }
}

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ActionUrl).HasMaxLength(500);
        b.Property(x => x.Icon).HasMaxLength(80);
        b.Property(x => x.Type).HasConversion<int>();

        b.HasIndex(x => new { x.UserId, x.IsRead });
        b.HasIndex(x => x.CreatedAt);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.UserName).HasMaxLength(200);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(150).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(60);
        b.Property(x => x.UserAgent).HasMaxLength(500);

        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.HasIndex(x => x.UserId);
    }
}

public class SystemSettingConfig : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("SystemSettings");
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.Value).HasMaxLength(2000);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Category).HasMaxLength(100);

        b.HasIndex(x => new { x.CompanyId, x.Key }).IsUnique();
    }
}

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(250).IsRequired();
        b.Property(x => x.JobTitle).HasMaxLength(150);
        b.Property(x => x.EmployeeNumber).HasMaxLength(50);
        b.Property(x => x.NationalId).HasMaxLength(30);
        b.Property(x => x.AvatarPath).HasMaxLength(400);
        b.Property(x => x.LastLoginIp).HasMaxLength(60);

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.IsDeleted);
    }
}
