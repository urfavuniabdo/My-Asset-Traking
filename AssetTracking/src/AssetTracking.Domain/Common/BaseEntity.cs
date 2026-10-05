namespace AssetTracking.Domain.Common;

/// <summary>
/// الكيان الأساسي — يحمل حقول التتبع والحذف الناعم المشتركة بين كل الجداول.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>تاريخ الإنشاء (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>مُعرِّف المستخدم المُنشئ</summary>
    public string? CreatedBy { get; set; }

    /// <summary>تاريخ آخر تعديل (UTC)</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>مُعرِّف آخر مستخدم عدّل السجل</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>الحذف الناعم — لا نحذف أي سجل فعلياً أبداً</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// كيان مملوك لشركة — يُطبَّق عليه EF Core Global Query Filter لعزل بيانات الشركات.
/// </summary>
public interface ICompanyOwned
{
    int CompanyId { get; set; }
}

/// <summary>
/// كيان يدعم التزامن المتفائل (Optimistic Concurrency) عبر RowVersion.
/// </summary>
public interface IConcurrencyAware
{
    byte[]? RowVersion { get; set; }
}
