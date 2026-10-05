using AssetTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

public class AssetConfig : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("Assets");

        b.Property(x => x.AssetTag).HasMaxLength(30).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(250).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(250);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Brand).HasMaxLength(100);
        b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.SerialNumber).HasMaxLength(120);
        b.Property(x => x.Specifications).HasMaxLength(2000);
        b.Property(x => x.Color).HasMaxLength(50);
        b.Property(x => x.InvoiceNumber).HasMaxLength(60);
        b.Property(x => x.PurchaseOrderNumber).HasMaxLength(60);
        b.Property(x => x.WarrantyProvider).HasMaxLength(200);
        b.Property(x => x.WarrantyTerms).HasMaxLength(1000);
        b.Property(x => x.QrCodePath).HasMaxLength(400);
        b.Property(x => x.ImagePath).HasMaxLength(400);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.DisposalReason).HasMaxLength(1000);
        b.Property(x => x.CurrentCustodyUserId).HasMaxLength(450);

        b.Property(x => x.PurchaseValue).HasPrecision(18, 2);
        b.Property(x => x.SalvageValue).HasPrecision(18, 2);
        b.Property(x => x.BookValue).HasPrecision(18, 2);

        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.DepreciationMethod).HasConversion<int>();

        // التزامن المتفائل — SQL Server: rowversion ، SQLite: يُتجاهل عبر Migration
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

        b.HasOne(x => x.Company).WithMany(c => c.Assets)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany(c => c.Assets)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany(d => d.Assets)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Location).WithMany(l => l.Assets)
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendor).WithMany(v => v.Assets)
            .HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.AssetTag).IsUnique();
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.CurrentCustodyUserId);
        b.HasIndex(x => x.SerialNumber);
        b.HasIndex(x => x.WarrantyEndDate);
    }
}

public class CustodyLogConfig : IEntityTypeConfiguration<CustodyLog>
{
    public void Configure(EntityTypeBuilder<CustodyLog> b)
    {
        b.ToTable("CustodyLogs");
        b.Property(x => x.PreviousUserId).HasMaxLength(450);
        b.Property(x => x.NewUserId).HasMaxLength(450);
        b.Property(x => x.AssignedByUserId).HasMaxLength(450);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ConditionNote).HasMaxLength(1000);
        b.Property(x => x.ResponseNote).HasMaxLength(1000);
        b.Property(x => x.ReceiptPath).HasMaxLength(400);
        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Asset).WithMany(a => a.CustodyLogs)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.NewUserId);
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.Status);
    }
}

public class LocationLogConfig : IEntityTypeConfiguration<LocationLog>
{
    public void Configure(EntityTypeBuilder<LocationLog> b)
    {
        b.ToTable("LocationLogs");
        b.Property(x => x.MovedByUserId).HasMaxLength(450);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Asset).WithMany(a => a.LocationLogs)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.CompanyId);
    }
}

public class DepreciationEntryConfig : IEntityTypeConfiguration<DepreciationEntry>
{
    public void Configure(EntityTypeBuilder<DepreciationEntry> b)
    {
        b.ToTable("DepreciationEntries");
        b.Property(x => x.OpeningValue).HasPrecision(18, 2);
        b.Property(x => x.DepreciationAmount).HasPrecision(18, 2);
        b.Property(x => x.ClosingValue).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Method).HasConversion<int>();

        b.HasOne(x => x.Asset).WithMany(a => a.DepreciationEntries)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.AssetId, x.Year, x.Month }).IsUnique();
        b.HasIndex(x => x.CompanyId);
    }
}

public class AttachmentConfig : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("Attachments");
        b.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        b.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(150);
        b.Property(x => x.UploadedByUserId).HasMaxLength(450);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.Asset).WithMany(a => a.Attachments)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Ticket).WithMany(t => t.Attachments)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.TicketId);
        b.HasIndex(x => x.CompanyId);
    }
}
