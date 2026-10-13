namespace AssetTracking.Web.ViewModels;

/// <summary>
/// ترقيم الصفحات للصفحات الحديثة (ذات قشرة Tailwind).
/// <see cref="PageUrl"/> دالة تمرّرها كل صفحة لأنها وحدها التي تملك فلاتر رابطها،
/// فتبقى علامة الترقيم نفسها في مكان واحد.
/// </summary>
public class PagerModernModel
{
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 20;

    /// <summary>يبني رابط الصفحة — يجب أن يحافظ على كل الفلاتر الحالية.</summary>
    public Func<int, string> PageUrl { get; set; } = p => $"?page={p}";

    /// <summary>إظهار «عرض س–ص من ن» إلى جوار الأرقام.</summary>
    public bool ShowCounter { get; set; }

    public int WindowStart => Math.Max(1, Page - 2);
    public int WindowEnd => Math.Min(TotalPages, Page + 2);
    public int CounterFrom => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int CounterTo => Math.Min(Page * PageSize, TotalCount);
}
