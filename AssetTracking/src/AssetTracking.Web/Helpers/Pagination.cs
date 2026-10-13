using AssetTracking.Domain.Common;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// حساب الترقيم في مكان واحد. كانت صيغة عدد الصفحات مكتوبة سبع مرات بثلاثة أشكال
/// مختلفة (<c>TotalCount == 0 ? 1</c> و<c>PageSize == 0 ? 1</c> و<c>PageSize &lt;= 0 ? 1</c>)،
/// فأي تصحيح لها كان يجب أن يتكرر سبع مرات.
/// </summary>
public static class Pagination
{
    /// <summary>عدد الصفحات: لا يقل عن واحد أبداً — الصفر يكسر حلقات الترقيم والروابط.</summary>
    public static int TotalPages(int totalCount, int pageSize)
        => pageSize <= 0 || totalCount <= 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

    /// <summary>حجم الصفحة القادم من المستدعي: صفر/سالب يرجع الافتراضي، وأكبر من الحد لا يحوّل الفهرس إلى جلب جدول كامل.</summary>
    public static int ClampSize(int requested)
        => requested <= 0
            ? AppConstants.DefaultPageSize
            : Math.Min(requested, AppConstants.MaxPageSize);
}
