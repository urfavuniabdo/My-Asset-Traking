using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Entities;

/// <summary>الأصل — الكيان المحوري في النظام</summary>
public class Asset : BaseEntity, ICompanyOwned, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>الوسم الفريد AST-YYYY-NNNNN</summary>
    public string AssetTag { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>
    /// المخزن الذي يحفظ الأصل الآن — الإجابة على «أين يُحفظ؟».
    /// لا يحل محله مرجع الموقع: موقع الشركة يجيب على سؤال آخر (أي فرع هذا؟).
    /// </summary>
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>
    /// موقع / فرع الشركة الذي يقف فيه الأصل عندما لا يكون في مخزن — الإجابة على
    /// «أي منشأة هذه؟». قاعدة الوضع الواحد: المخزن أو الموقع، لا الاثنان معًا،
    /// وإلا صار لنفس الأصل موضعان يتباعدان بصمت (PlaceRules).
    /// </summary>
    public int? CompanySiteId { get; set; }
    public CompanySite? CompanySite { get; set; }

    /// <summary>
    /// خط الإنتاج القائم عليه الأصل — مفهوم تشغيلي لا موضع حفظ: يجيب «على أي خط
    /// يعمل؟» لا «أين يُحفظ؟»، لذلك لا يُدمج مع المخزن ولا مع موقع الشركة.
    /// </summary>
    public int? ProductionLineId { get; set; }
    public ProductionLine? ProductionLine { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // ── المواصفات ─────────────────────────────
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specifications { get; set; }
    public string? Color { get; set; }

    // ── المالية ───────────────────────────────
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseValue { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? BookValue { get; set; }
    public int? UsefulLifeYears { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public string? InvoiceNumber { get; set; }
    public string? PurchaseOrderNumber { get; set; }

    // ── الضمان ────────────────────────────────
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? WarrantyProvider { get; set; }
    public string? WarrantyTerms { get; set; }

    // ── الحالة والعهدة ────────────────────────
    public AssetStatus Status { get; set; } = AssetStatus.InStore;

    /// <summary>الموظف الحائز على العهدة حالياً (إن وُجد)</summary>
    public string? CurrentCustodyUserId { get; set; }
    public DateTime? CustodySince { get; set; }

    // ── QR والمرفقات ──────────────────────────
    public string? QrCodePath { get; set; }
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }

    public DateTime? DisposalDate { get; set; }
    public string? DisposalReason { get; set; }

    /// <summary>التزامن المتفائل — يمنع الكتابة فوق تعديل مستخدم آخر</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<CustodyLog> CustodyLogs { get; set; } = new List<CustodyLog>();
    public ICollection<LocationLog> LocationLogs { get; set; } = new List<LocationLog>();
    public ICollection<MaintenanceTicket> Tickets { get; set; } = new List<MaintenanceTicket>();
    public ICollection<DepreciationEntry> DepreciationEntries { get; set; } = new List<DepreciationEntry>();
    public ICollection<MaintenanceSchedule> Schedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

/// <summary>سجل حركات العهدة (تسليم / نقل / إرجاع)</summary>
public class CustodyLog : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public CustodyAction Action { get; set; }
    public CustodyStatus Status { get; set; } = CustodyStatus.Pending;

    /// <summary>الحائز السابق</summary>
    public string? PreviousUserId { get; set; }

    /// <summary>الحائز الجديد (هو الوحيد المصرَّح له بالرد على الطلب)</summary>
    public string? NewUserId { get; set; }

    public string? AssignedByUserId { get; set; }

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public string? Reason { get; set; }
    public string? ConditionNote { get; set; }
    public string? ResponseNote { get; set; }

    /// <summary>مسار صورة الإيصال الموقَّع</summary>
    public string? ReceiptPath { get; set; }
}

/// <summary>
/// سجل تغيّر موضع الأصل. النقلات تُخزَّن بمراجع عددية صريحة لكل كيان (مخزن /
/// موقع شركة / خط إنتاج) لا بمرجع واحد مشترك: تاريخ يجيب «انتقل من مخزن إلى
/// مخزن» يجب ألا يختلف تفسيره عن «انتقل من فرع إلى مخزن» بفعل عمود واحد.
/// المراجع أعمدة بلا FK ولا مِلَاكات تنقّل — نفس ما كان عليه
/// FromLocationId/ToLocationId: السجل تاريخ، وتاريخ الأصل لا يمنع حذف سجل مرجعي.
/// </summary>
public class LocationLog : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public int? FromWarehouseId { get; set; }
    public int? FromSiteId { get; set; }
    public int? FromLineId { get; set; }

    public int? ToWarehouseId { get; set; }
    public int? ToSiteId { get; set; }
    public int? ToLineId { get; set; }

    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public string? MovedByUserId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    /// <summary>هل هذه النقلة متعلقة بخط إنتاج؟ للعرض فقط — لا تُخزَّن.</summary>
    public bool InvolvesLine => FromLineId.HasValue || ToLineId.HasValue;
}

/// <summary>قيد إهلاك شهري</summary>
public class DepreciationEntry : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal OpeningValue { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal ClosingValue { get; set; }

    public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

/// <summary>مرفق (صورة / مستند) مرتبط بأصل أو تذكرة</summary>
public class Attachment : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public int? TicketId { get; set; }
    public MaintenanceTicket? Ticket { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? UploadedByUserId { get; set; }
    public string? Description { get; set; }

    /// <summary>مرحلة الصورة: قبل الإصلاح / بعده — لتوثيق حالة الماكينة</summary>
    public PhotoStage Stage { get; set; } = PhotoStage.Other;
}
