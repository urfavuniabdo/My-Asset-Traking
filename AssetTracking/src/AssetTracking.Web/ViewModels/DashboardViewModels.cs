using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>لوحة المعلومات — تتغيّر محتوياتها حسب دور المستخدم</summary>
public class DashboardViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;

    // بطاقات الأصول
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int InStoreAssets { get; set; }
    public int UnderMaintenanceAssets { get; set; }
    public int DamagedAssets { get; set; }
    public int DisposedAssets { get; set; }
    public int LostAssets { get; set; }

    // القيم المالية
    public decimal TotalPurchaseValue { get; set; }
    public decimal TotalBookValue { get; set; }

    // التذاكر
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int SlaBreachedTickets { get; set; }
    public int ResolvedThisMonth { get; set; }
    public int MyTickets { get; set; }

    // العهد
    public int MyCustodyCount { get; set; }
    public int PendingCustodyCount { get; set; }

    // تنبيهات
    public int WarrantyExpiringSoon { get; set; }
    public int MaintenanceDueSoon { get; set; }

    // بيانات الرسوم البيانية
    public List<ChartPoint> AssetsByCategory { get; set; } = new();
    public List<ChartPoint> AssetsByStatus { get; set; } = new();
    public List<ChartPoint> TicketsByPriority { get; set; } = new();
    public List<ChartPoint> TicketsTrend { get; set; } = new();

    public List<RecentTicketRow> RecentTickets { get; set; } = new();
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class RecentTicketRow
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public DateTime ReportedAt { get; set; }
    public bool IsSlaBreached { get; set; }
}
