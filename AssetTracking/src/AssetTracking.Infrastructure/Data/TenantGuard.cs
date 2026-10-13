using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Infrastructure.Data;

/// <summary>
/// حواجز ملكية العلاقات.
///
/// لماذا لا يكفي الفلتر العام: الفلتر يُخفي صفوف الشركات الأخرى عن القراءة، لكنه
/// (أ) مُعطَّل عمداً لمدير النظام، و(ب) لا يفحص القيمة التي يرسلها العميل في نموذج،
/// و(ج) مفاتيحنا الأجنبية أحادية العمود فالقاعدة تقبل ربط أصل بقسم من شركة أخرى ما دام
/// القسم موجوداً. لذا كل مسار ربط يجب أن يقارن CompanyId صراحةً قبل الحفظ.
/// </summary>
public static class TenantGuard
{
    /// <summary>
    /// هل المعرّف يشير لسجل حيّ مملوك لنفس الشركة؟ null/0 يعني «غير مرتبط» فيُقبل.
    /// مقارنة CompanyId صريحة مع IgnoreQueryFilters لأن فلتر العزل معطَّل لمدير النظام،
    /// فالاعتماد عليه وحده يقبل معرّفاً من شركة أخرى في طلب يُرسل مباشرة.
    /// </summary>
    public static async Task<bool> OwnedByCompanyAsync<TEntity>(
        this AppDbContext db, int? id, int companyId, CancellationToken ct = default)
        where TEntity : BaseEntity, ICompanyOwned
        => id is null or 0
           || await db.Set<TEntity>().IgnoreQueryFilters()
                .AnyAsync(e => e.Id == id.Value && e.CompanyId == companyId && !e.IsDeleted, ct);

    /// <summary>هل المعرّف يشير لمستخدم حيّ ضمن الشركة نفسها؟ null/فارغ = «غير مُسند» فيُقبل.</summary>
    public static async Task<bool> UserInCompanyAsync(
        this AppDbContext db, string? userId, int companyId, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(userId)
           || await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Id == userId && u.CompanyId == companyId && !u.IsDeleted, ct);

    /// <summary>
    /// حصر كشف بيانات المستخدمين في شركة الطالب نفسه.
    ///
    /// لماذا لا يكفي الفلتر العام هنا: ApplicationUser يورّث IdentityUser ولا يورّث
    /// ICompanyOwned، فلا يُطبَّق عليه فلتر عزل الشركة إطلاقاً. النتيجة أن أي مسار قراءة
    /// يُسمّي معرّفات مستخدمين (اسم حائز، مُسنِد، فني) يقرأ من كل الشركات بصمت، ويكشف
    /// اسماً وبريداً عابرين للشركات لو بقي ربط عابر من بيانات سابقة للحاجز.
    ///
    /// الاستثناء المقصود: مدير النظام شركته null وهو يرى كل الشركات بالسياسة، فيبقى
    /// بلا تضييق. موظف بلا شركة (بيانات معطوبة) لا يكشف أحداً — نفس دلالة ?? -1 في الفلتر العام.
    /// </summary>
    public static IQueryable<ApplicationUser> ReadableBy(
        this IQueryable<ApplicationUser> users, ICurrentUser me)
        => me.IsAdmin ? users : users.Where(u => u.CompanyId == (me.CompanyId ?? -1));
}
