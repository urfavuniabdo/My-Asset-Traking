using AssetTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

public class CompanyConfig : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Companies");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.TaxNumber).HasMaxLength(50);
        b.Property(x => x.CommercialRegister).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Website).HasMaxLength(200);
        b.Property(x => x.LogoPath).HasMaxLength(400);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.IsDeleted);
    }
}

public class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.ManagerUserId).HasMaxLength(450);
        b.Property(x => x.CostCenter).HasMaxLength(50);

        b.HasOne(x => x.Company).WithMany(c => c.Departments)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CompanyId);
    }
}

public class LocationConfig : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("Locations");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Governorate).HasMaxLength(100);
        b.Property(x => x.Latitude).HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasPrecision(10, 7);
        b.Property(x => x.ContactPerson).HasMaxLength(150);
        b.Property(x => x.ContactPhone).HasMaxLength(30);
        b.Property(x => x.Type).HasConversion<int>();

        b.HasOne(x => x.Company).WithMany(c => c.Locations)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CompanyId);
    }
}

public class VendorConfig : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> b)
    {
        b.ToTable("Vendors");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.ContactPerson).HasMaxLength(150);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.TaxNumber).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CompanyId);
    }
}

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Icon).HasMaxLength(80);
        b.Property(x => x.SalvageRate).HasPrecision(5, 4);
        b.Property(x => x.DepreciationMethod).HasConversion<int>();

        b.HasOne(x => x.Company).WithMany()
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ParentCategory).WithMany(x => x.SubCategories)
            .HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.ParentCategoryId);
    }
}
