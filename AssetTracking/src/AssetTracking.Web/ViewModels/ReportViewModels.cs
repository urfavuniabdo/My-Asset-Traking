using AssetTracking.Domain.Enums;

namespace AssetTracking.Web.ViewModels;

/// <summary>صفحة التقارير الرئيسية — قائمة التقارير المتاحة</summary>
public class ReportsHomeViewModel
{
    public int TotalAssets { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalBookValue { get; set; }
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public decimal MaintenanceCost { get; set; }
    public int CustodyActive { get; set; }
    public int WarrantyExpiringSoon { get; set; }
    public int AuditsCompleted { get; set; }
    public string? CompanyName { get; set; }
}

// ─────────────────── تقرير الأصول ───────────────────
public class AssetsReportViewModel
{
    public List<AssetReportRow> Rows { get; set; } = new();

    public AssetStatus? Status { get; set; }
    public int? CategoryId { get; set; }
    public int? LocationId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Locations { get; set; } = new();

    public int Count => Rows.Count;
    public decimal SumPurchase { get; set; }
    public decimal SumBook { get; set; }
    public decimal SumDepreciation => SumPurchase - SumBook;

    /// <summary>توزيع حسب التصنيف — للرسم البياني</summary>
    public List<ChartSlice> ByCategory { get; set; } = new();
    public List<ChartSlice> ByStatus { get; set; } = new();
}

public class AssetReportRow
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? LocationName { get; set; }
    public string? DepartmentName { get; set; }
    public AssetStatus Status { get; set; }
    public string? Brand { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseValue { get; set; }
    public decimal? BookValue { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? HolderName { get; set; }
    public string? CompanyName { get; set; }
}

// ─────────────────── تقرير التذاكر ───────────────────
public class TicketsReportViewModel
{
    public List<TicketReportRow> Rows { get; set; } = new();

    public TicketStatus? Status { get; set; }
    public TicketType? Type { get; set; }
    public string? TechnicianId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<UserLookupItem> Technicians { get; set; } = new();

    public int Count => Rows.Count;
    public int Breached { get; set; }
    public decimal SumCost { get; set; }
    public double AvgResolutionHours { get; set; }
    public double SlaCompliancePercent => Count == 0 ? 100 : Math.Round((Count - Breached) * 100.0 / Count, 1);

    public List<ChartSlice> ByStatus { get; set; } = new();
    public List<ChartSlice> ByType { get; set; } = new();
    public List<ChartSlice> ByTechnician { get; set; } = new();
}

public class TicketReportRow
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public TicketType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public string? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public string? RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public decimal? LaborCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? TotalCost { get; set; }
    public string? CompanyName { get; set; }

    public double? ResolutionHours => ResolvedAt.HasValue
        ? Math.Round((ResolvedAt.Value - ReportedAt).TotalHours, 1)
        : null;
}

// ─────────────────── تقرير العهد ───────────────────
public class CustodyReportViewModel
{
    public List<CustodyReportRow> Rows { get; set; } = new();

    public string? UserId { get; set; }
    public CustodyStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<UserLookupItem> Users { get; set; } = new();

    public int Count => Rows.Count;
    public decimal SumValue { get; set; }

    /// <summary>ملخّص العهد الحالية لكل موظف</summary>
    public List<CustodySummaryRow> Summary { get; set; } = new();
}

public class CustodyReportRow
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string? AssetTag { get; set; }
    public string? AssetName { get; set; }
    public CustodyAction Action { get; set; }
    public CustodyStatus Status { get; set; }
    public string? PreviousUserId { get; set; }
    public string? PreviousUserName { get; set; }
    public string? NewUserId { get; set; }
    public string? NewUserName { get; set; }
    public string? AssignedByUserId { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime ActionDate { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Reason { get; set; }
    public decimal? AssetValue { get; set; }
}

public class CustodySummaryRow
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int AssetsCount { get; set; }
    public decimal TotalValue { get; set; }
}

// ─────────────────── تقرير الإهلاك ───────────────────
public class DepreciationReportViewModel
{
    public List<DepreciationReportRow> Rows { get; set; } = new();

    public int? CategoryId { get; set; }
    public List<LookupItem> Categories { get; set; } = new();

    public int Count => Rows.Count;
    public decimal SumPurchase { get; set; }
    public decimal SumBook { get; set; }
    public decimal SumAccumulated => SumPurchase - SumBook;
    public decimal SumAnnual { get; set; }

    public List<ChartSlice> ByCategory { get; set; } = new();
}

public class DepreciationReportRow
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal PurchaseValue { get; set; }
    public decimal SalvageValue { get; set; }
    public decimal BookValue { get; set; }
    public int UsefulLifeYears { get; set; }
    public DepreciationMethod Method { get; set; }

    public decimal Accumulated => PurchaseValue - BookValue;
    public decimal AnnualDepreciation => UsefulLifeYears <= 0
        ? 0
        : Math.Round((PurchaseValue - SalvageValue) / UsefulLifeYears, 2);

    public decimal DepreciationPercent => PurchaseValue <= 0
        ? 0
        : Math.Round(Accumulated * 100 / PurchaseValue, 1);

    public int AgeMonths => PurchaseDate.HasValue
        ? Math.Max(0, (int)((DateTime.UtcNow - PurchaseDate.Value).TotalDays / 30.44))
        : 0;

    public bool IsFullyDepreciated => BookValue <= SalvageValue;
}

/// <summary>شريحة في رسم بياني</summary>
public class ChartSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}
