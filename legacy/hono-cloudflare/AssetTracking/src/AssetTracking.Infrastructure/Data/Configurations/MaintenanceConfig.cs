using AssetTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

public class SlaPolicyConfig : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> b)
    {
        b.ToTable("SlaPolicies");
        b.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        b.Property(x => x.Priority).HasConversion<int>();

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CompanyId, x.Priority });
    }
}

public class MaintenanceTicketConfig : IEntityTypeConfiguration<MaintenanceTicket>
{
    public void Configure(EntityTypeBuilder<MaintenanceTicket> b)
    {
        b.ToTable("MaintenanceTickets");

        b.Property(x => x.TicketNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Resolution).HasMaxLength(4000);
        b.Property(x => x.RootCause).HasMaxLength(2000);
        b.Property(x => x.SatisfactionComment).HasMaxLength(1000);
        b.Property(x => x.RequestedByUserId).HasMaxLength(450);
        b.Property(x => x.AssignedTechnicianId).HasMaxLength(450);
        b.Property(x => x.ClosedByUserId).HasMaxLength(450);

        b.Property(x => x.LaborCost).HasPrecision(18, 2);
        b.Property(x => x.PartsCost).HasPrecision(18, 2);
        b.Property(x => x.TotalCost).HasPrecision(18, 2);

        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Priority).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();

        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Asset).WithMany(a => a.Tickets)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendor).WithMany()
            .HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Schedule).WithMany(s => s.GeneratedTickets)
            .HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TicketNumber).IsUnique();
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.AssignedTechnicianId);
        b.HasIndex(x => x.RequestedByUserId);
        b.HasIndex(x => x.ResolutionDueAt);
    }
}

public class TicketCommentConfig : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> b)
    {
        b.ToTable("TicketComments");
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.AuthorUserId).HasMaxLength(450);

        b.HasOne(x => x.Ticket).WithMany(t => t.Comments)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => x.CompanyId);
    }
}

public class TicketLogConfig : IEntityTypeConfiguration<TicketLog>
{
    public void Configure(EntityTypeBuilder<TicketLog> b)
    {
        b.ToTable("TicketLogs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.FromValue).HasMaxLength(200);
        b.Property(x => x.ToValue).HasMaxLength(200);
        b.Property(x => x.ByUserId).HasMaxLength(450);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Ticket).WithMany(t => t.Logs)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => x.CompanyId);
    }
}

public class TicketPartConfig : IEntityTypeConfiguration<TicketPart>
{
    public void Configure(EntityTypeBuilder<TicketPart> b)
    {
        b.ToTable("TicketParts");
        b.Property(x => x.PartName).HasMaxLength(250).IsRequired();
        b.Property(x => x.PartNumber).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.TotalPrice).HasPrecision(18, 2);

        b.HasOne(x => x.Ticket).WithMany(t => t.Parts)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => x.CompanyId);
    }
}

public class MaintenanceScheduleConfig : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> b)
    {
        b.ToTable("MaintenanceSchedules");
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Checklist).HasMaxLength(4000);
        b.Property(x => x.DefaultTechnicianId).HasMaxLength(450);
        b.Property(x => x.EstimatedCost).HasPrecision(18, 2);
        b.Property(x => x.Frequency).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.DefaultPriority).HasConversion<int>();

        b.HasOne(x => x.Asset).WithMany(a => a.Schedules)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.NextDueDate);
    }
}
