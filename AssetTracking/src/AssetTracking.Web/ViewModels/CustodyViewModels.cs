using System.ComponentModel.DataAnnotations;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>سطر في سجل العهد</summary>
public class CustodyRow
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public string? CategoryName { get; set; }
    public CustodyAction Action { get; set; }
    public CustodyStatus Status { get; set; }

    // المعرّفات تُجلب من القاعدة، والأسماء تُحلّ في استعلام واحد لاحقاً
    public string? PreviousUserId { get; set; }
    public string? NewUserId { get; set; }
    public string? AssignedById { get; set; }

    public string? PreviousUserName { get; set; }
    public string? NewUserName { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime ActionDate { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Reason { get; set; }
    public string? ResponseNote { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>هل هذا الطلب في انتظار رد المستخدم الحالي؟</summary>
    public bool AwaitingMyResponse { get; set; }
}

/// <summary>صفحة «عهدي» — الأصول المسجّلة باسم المستخدم</summary>
public class MyCustodyViewModel
{
    public List<AssetRow> MyAssets { get; set; } = new();
    public List<CustodyRow> Pending { get; set; } = new();
    public List<CustodyRow> History { get; set; } = new();
    public decimal TotalValue { get; set; }
    public string UserName { get; set; } = string.Empty;
}

/// <summary>فهرس إدارة العهد (للمدير)</summary>
public class CustodyIndexViewModel
{
    public List<CustodyRow> Items { get; set; } = new();

    public CustodyStatus? Status { get; set; }
    public string? UserId { get; set; }
    public string? Q { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<UserLookupItem> Users { get; set; } = new();

    public int CountPending { get; set; }
    public int CountAccepted { get; set; }
    public int CountRejected { get; set; }
    public bool CanAssign { get; set; }
}

/// <summary>نموذج تسليم عهدة</summary>
public class CustodyAssignViewModel
{
    [Required(ErrorMessage = "يجب اختيار الأصل")]
    [Display(Name = "الأصل")]
    public int AssetId { get; set; }

    [Required(ErrorMessage = "يجب اختيار الموظف")]
    [Display(Name = "الموظف المستلِم")]
    public string NewUserId { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "سبب التسليم")]
    public string? Reason { get; set; }

    public List<AssetPickerItem> Assets { get; set; } = new();
    public List<UserLookupItem> Users { get; set; } = new();
    public string? AssetLabel { get; set; }
    public string? CurrentHolderName { get; set; }
}

/// <summary>نموذج الرد على طلب عهدة</summary>
public class CustodyRespondViewModel
{
    public int Id { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime ActionDate { get; set; }
    public string? Reason { get; set; }

    [Display(Name = "ملاحظة الاستلام")]
    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>نموذج استرجاع عهدة</summary>
public class CustodyReturnViewModel
{
    public int AssetId { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public string? HolderName { get; set; }
    public DateTime? CustodySince { get; set; }

    [StringLength(500)]
    [Display(Name = "سبب الاسترجاع")]
    public string? Reason { get; set; }

    [StringLength(500)]
    [Display(Name = "حالة الأصل عند الاسترجاع")]
    public string? Condition { get; set; }
}
