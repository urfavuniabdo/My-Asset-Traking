using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Entities;

/// <summary>
/// موقع / فرع الشركة — منشأة فعلية للشركة (مقر، فرع، مصنع، مكتب إداري، موقع مشروع).
///
/// يجيب على «أي فرع أو منشأة هذا؟» ولا يجيب على «أين تُحفظ الأصول؟»: تلك وظيفة
/// Warehouse. الكيانان منفصلان تمامًا، ولا يوجد أيهما نوع من الآخر.
///
/// يوجد الموقع مستقلًا عن المخازن: شركة تنشئ ثلاثة فروع وبلا أي مخزن — وهذا السبب
/// أن المخزن هو من يحمل مرجع الموقع (اختياريًا)، لا العكس.
/// </summary>
public class CompanySite : BaseEntity, ICompanyOwned, ICompanyScopedCode
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    /// <summary>كود الموقع — فريد داخل الشركة (يُحرس في الكاتب وفريدًا في القاعدة).</summary>
    public string? Code { get; set; }

    public SiteKind Kind { get; set; } = SiteKind.Office;

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>وصف اختياري للموقع.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// المخازن المرتبطة بهذا الموقع — قد تكون صفرًا، والموقع يوجد بدونها.
    /// لا Assets هنا عمدًا: أغلب ما «في» الموقع يحفظه مخزن تحته، فمجموعة أصول
    /// مباشرة على الموقع توحي بأنها موضع الحفظ وتُغري باستعمالها بدلاً من Warehouse.
    /// الأصول الواقفة في الموقع نفسه تُقرأ باستعلام صريح على Asset.CompanySiteId.
    /// </summary>
    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}

/// <summary>
/// المخزن — وحدة تحفظ مستقلة: تجيب على «أي مخزن يحفظ هذا الأصل الآن؟».
///
/// ليس فرعًا للشركة: غرضه الجرد والتحفظ لا تعريف منشأة. يعمل بلا أي موقع شركة،
/// ويرتبط بموقع واحد اختياريًا (null قيمة صالحة محترمة في النموذج والقاعدة).
/// الربط اختياري أحادي الاتجاه: الموقع لا يحتاج مخزنًا ليوجد.
/// </summary>
public class Warehouse : BaseEntity, ICompanyOwned, ICompanyScopedCode
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    /// <summary>كود المخزن — فريد داخل الشركة (WH-SPARES-01 مثلًا).</summary>
    public string? Code { get; set; }

    /// <summary>وصف اختياري: ما يُخزَّن فيه وشرطه.</summary>
    public string? Notes { get; set; }

    /// <summary>عنوان أو مكان التخزين — اختياري، ولا يُشترط لإنشاء المخزن أو لاستخدامه.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// موقع الشركة الذي يحوي هذا المخزن — اختياري تمامًا. null = مخزن مستقل.
    /// إن وُجد فيجب أن يكون موقعًا من نفس الشركة (يُحرس في الكاتب).
    /// </summary>
    public int? CompanySiteId { get; set; }
    public CompanySite? CompanySite { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
