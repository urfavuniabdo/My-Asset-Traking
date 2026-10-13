using AssetTracking.Domain.Common;

namespace AssetTracking.Domain.Entities;

/// <summary>
/// خط الإنتاج / منطقة التشغيل — مفهوم تشغيلي تستهلكه الصيانة (MaintenanceTicket)
/// والأصل القائم عليه. ليس موقع شركة ولا مخزنًا: لا يدخل قائمة الفروع ولا قائمة
/// المخازن، ولا يُستخدم أبدًا للإجابة «أين يُحفظ الأصل؟».
///
/// كان خط الإنتاج صفًا في جدول Locations بعمود نوع، فأي استعلام عن «موضع الأصل»
/// كان يخلطه بالفرع وبالمخزن. الجدول المستقل هو ما يجعل الخلط مستحيلًا.
/// </summary>
public class ProductionLine : BaseEntity, ICompanyOwned, ICompanyScopedCode
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    /// <summary>كود الخط — فريد داخل الشركة، مثل الموقع والمخزن.</summary>
    public string? Code { get; set; }

    /// <summary>وصف اختياري: ما الذي يُنتَج على هذا الخط.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
