using AssetTracking.Domain.Common;
using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Entities;

/// <summary>سياسة SLA لكل أولوية</summary>
public class SlaPolicy : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public TicketPriority Priority { get; set; }
    public string NameAr { get; set; } = string.Empty;

    /// <summary>مدة الاستجابة المستهدفة بالساعات</summary>
    public int ResponseHours { get; set; }

    /// <summary>مدة الحل المستهدفة بالساعات</summary>
    public int ResolutionHours { get; set; }

    /// <summary>ساعات قبل الاستحقاق يتم فيها التنبيه</summary>
    public int EscalationWarningHours { get; set; } = 2;

    public bool IsActive { get; set; } = true;
}

/// <summary>تذكرة صيانة / دعم فني</summary>
public class MaintenanceTicket : BaseEntity, ICompanyOwned, IConcurrencyAware
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>الرقم الفريد TKT-YYYY-NNNNN</summary>
    public string TicketNumber { get; set; } = string.Empty;

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TicketType Type { get; set; } = TicketType.Corrective;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public string? RequestedByUserId { get; set; }
    public string? AssignedTechnicianId { get; set; }
    public string? ClosedByUserId { get; set; }

    // ── مواعيد SLA ────────────────────────────
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>هل تم خرق SLA؟ (تحدّده مهمة SlaEscalationJob)</summary>
    public bool IsSlaBreached { get; set; }
    public DateTime? SlaBreachedAt { get; set; }
    public bool IsEscalated { get; set; }

    // ── التكاليف والحل ────────────────────────
    public decimal? LaborCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? TotalCost { get; set; }
    public string? Resolution { get; set; }
    public string? RootCause { get; set; }

    /// <summary>تقييم الموظف لجودة الخدمة 1..5</summary>
    public int? SatisfactionRating { get; set; }
    public string? SatisfactionComment { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>التذكرة مولّدة تلقائياً من جدول صيانة وقائية</summary>
    public int? ScheduleId { get; set; }
    public MaintenanceSchedule? Schedule { get; set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketLog> Logs { get; set; } = new List<TicketLog>();
    public ICollection<TicketPart> Parts { get; set; } = new List<TicketPart>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

/// <summary>تعليق على التذكرة</summary>
public class TicketComment : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int TicketId { get; set; }
    public MaintenanceTicket? Ticket { get; set; }

    public string? AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>تعليق داخلي لا يظهر للموظف مقدّم الطلب</summary>
    public bool IsInternal { get; set; }
}

/// <summary>سجل تدقيق حركات التذكرة (تغيير حالة / تكليف / ...)</summary>
public class TicketLog : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int TicketId { get; set; }
    public MaintenanceTicket? Ticket { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? ByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>قطعة غيار مستخدمة في التذكرة</summary>
public class TicketPart : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int TicketId { get; set; }
    public MaintenanceTicket? Ticket { get; set; }

    public string PartName { get; set; } = string.Empty;
    public string? PartNumber { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public int? VendorId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>جدول صيانة وقائية</summary>
public class MaintenanceSchedule : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }

    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Checklist { get; set; }

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Monthly;
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime NextDueDate { get; set; }

    /// <summary>عدد الأيام قبل الاستحقاق لتوليد التذكرة</summary>
    public int LeadTimeDays { get; set; } = 3;

    public string? DefaultTechnicianId { get; set; }
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;
    public decimal? EstimatedCost { get; set; }

    public ICollection<MaintenanceTicket> GeneratedTickets { get; set; } = new List<MaintenanceTicket>();
}
