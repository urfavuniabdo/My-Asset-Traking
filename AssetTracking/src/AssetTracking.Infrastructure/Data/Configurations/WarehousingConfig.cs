using AssetTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetTracking.Infrastructure.Data.Configurations;

/// <summary>
/// موقع / فرع الشركة. الترتيب والحدود مطابقة لباقي جداول البيانات الأساسية
/// (Companies / Departments / Vendors) حتى تتصرّف الوحدتان كعائلتين متجانستين
/// لا كشاذوذ في المخطط.
/// </summary>
public class CompanySiteConfig : IEntityTypeConfiguration<CompanySite>
{
    public void Configure(EntityTypeBuilder<CompanySite> b)
    {
        b.ToTable("CompanySites");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Governorate).HasMaxLength(100);
        b.Property(x => x.Latitude).HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasPrecision(10, 7);
        b.Property(x => x.ContactPerson).HasMaxLength(150);
        b.Property(x => x.ContactPhone).HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Company).WithMany(c => c.CompanySites)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        // الكود فريد داخل نطاق الشركة. NULLs متمايزة في SQLite وSQL Server
        // وPostgres معًا، فتبقى بلا كود عدة مواقع بلا تصادم — وبلا HasFilter
        // بصيغة مزوّد بعينه.
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.IsDeleted);

        // مفتاح أصلي مركّب يستند إليه مرجع المخزن: (Id, CompanyId). الغرض أن
        // تجبر القاعدة نفسها — لا الكاتب وحده — على أن يكون الفرع المُشار إليه
        // فرعَ الشركة ذاتها. لا يُقرأ ولا يُكتب من أي مسار، لذا يبقى صامتاً.
        //
        // هذا الفهرس ليس تكراراً لقيود HasPrincipalKey وحذفه يُسقط الترحيل:
        // بقياسٍ على نسخة من قاعدة تطوير SQLite، بلا هذا الفهرس يولّد EF الترتيب
        // نفسه من SQL لكن يفشل التطبيق بـ
        // «foreign key mismatch - "ef_temp_Warehouses" referencing "CompanySites"»
        // لأن SQLite يتحقق من أب المفتاح الأجنبي وقت CREATE TABLE، والفهرس الفريد
        // هنا هو ما يُصدر قبل إعادة البناء بينما يصل AK_ بعدها.
        // اختبارات EnsureCreated لا ترى هذا العطب، فالتحقق من الترحيل نفسه ضروري.
        b.HasIndex(x => new { x.Id, x.CompanyId }).IsUnique();
    }
}

/// <summary>
/// المخزن. المرجع إلى موقع الشركة اختياري بـ null صالح، وحذف موقع لديه مخازن
/// مرفوض صراحةً (Restrict) — لا يُيتَّم المخزن بصمت كما كان ليحدث بـ SetNull.
/// </summary>
public class WarehouseConfig : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("Warehouses");
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Company).WithMany(c => c.Warehouses)
            .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        // الرابط الاختياري بفرع الشركة مُقيَّد مركّباً: (CompanySiteId, CompanyId)
        // يجب أن يطابق (Id, CompanyId) لفرع واحد. النتيجة أن الربط العابر للشركة
        // مستحيل حتى بكتابة مباشرة خارج التطبيق، لأن عمود الشركة في المخزن نفسه
        // يدخل في القيد. وnull في CompanySiteId يُسقط المفتاح الأجنبي بقاعدة
        // MATCH SIMPLE في المزوّدين، فيبقى «مخزن مستقل» قيمة صالحة محمية.
        b.HasOne(x => x.CompanySite).WithMany(s => s.Warehouses)
            .HasForeignKey(x => new { x.CompanySiteId, x.CompanyId })
            .HasPrincipalKey(p => new { p.Id, p.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        b.HasIndex(x => x.CompanyId);
        b.HasIndex(x => x.CompanySiteId);
        b.HasIndex(x => x.IsDeleted);
    }
}
