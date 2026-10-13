using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// قوائم الاختيار المشتركة بين النماذج.
///
/// قائمات ثلاث لا واحدة: المخزن يجيب «أين يُحفظ الأصل؟»، وموقع الشركة يجيب
/// «أي فرع هذا؟»، وخط الإنتاج يجيب «على أي خط يعمل؟». القائمة الموحَّدة الواحدة
/// كانت هي نفسها موضع اللبس — كل سطح يقرأها كان يجب أن يستنتج النوع. الآن كل قائمة
/// تقرأ جدولها، فلا يوجد استنتاج أصلاً، ولا تختلط وجهات الإرجاع بالخطوط.
///
/// المعطَّل يبقى في القوائم (ولا يُستثنى بشرط WHERE): أصلٌ مقيم في مخزن موقوف يجب
/// ألا يفقد قيمته عند فتح نموذج التعديل، والحارس في مسار الكتابة هو من يرفضه كوجهة نقل.
/// </summary>
public static class ReferencePickers
{
    /// <summary>المخازن — وجهات التحفظ والإرجاع.</summary>
    public static async Task<List<LookupItem>> WarehousesAsync(AppDbContext db,
        CancellationToken ct = default)
        => await db.Warehouses.AsNoTracking()
            .OrderBy(w => w.NameAr)
            .Select(w => new LookupItem
            {
                Id = w.Id, Name = w.NameAr, CompanyId = w.CompanyId,
                IsActive = w.IsActive, ParentName = w.CompanySite != null ? w.CompanySite.NameAr : null
            })
            .ToListAsync(ct);

    /// <summary>مواقع / فروع الشركة — مستقلة عن المخازن، وتُعرض حتى بلا مخزن واحد تحتها.</summary>
    public static async Task<List<LookupItem>> CompanySitesAsync(AppDbContext db,
        CancellationToken ct = default)
        => await db.CompanySites.AsNoTracking()
            .OrderBy(s => s.NameAr)
            .Select(s => new LookupItem
            {
                Id = s.Id, Name = s.NameAr, CompanyId = s.CompanyId, IsActive = s.IsActive
            })
            .ToListAsync(ct);

    /// <summary>خطوط الإنتاج — مفهوم تشغيلي: لا تدخل قائمة المخازن ولا قائمة الفروع.</summary>
    public static async Task<List<LookupItem>> ProductionLinesAsync(AppDbContext db,
        CancellationToken ct = default)
        => await db.ProductionLines.AsNoTracking()
            .OrderBy(l => l.NameAr)
            .Select(l => new LookupItem
            {
                Id = l.Id, Name = l.NameAr, CompanyId = l.CompanyId, IsActive = l.IsActive
            })
            .ToListAsync(ct);

    public static async Task<List<LookupItem>> CompaniesAsync(AppDbContext db,
        CancellationToken ct = default)
        => await db.Companies.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new LookupItem { Id = c.Id, Name = c.NameAr })
            .ToListAsync(ct);
}
