using AssetTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

/// <summary>
/// خط الإنتاج — جدول مستقل. كان صفًا في Locations بعمود نوع، فصار المرجع إليه
/// غامضًا: «ProductionLineId» في تذاكر الصيانة كان يشير جدول مواقع يحمل الفروع
/// والمخازن والخطوط معًا. الجدول المنفصل يجعل الإشارة نوعًا واحدًا لا ثلاثة.
/// </summary>
public class ProductionLineConfig : IEntityTypeConfiguration<ProductionLine>
{
    public void Configure(EntityTypeBuilder<ProductionLine> b)
    {
        b.ToTable("ProductionLines");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Company).WithMany(c => c.ProductionLines)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.IsDeleted);
    }
}
