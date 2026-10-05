using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using System.Text.Json;

namespace AssetTracking.Infrastructure.Services;

/// <summary>محرّك SLA — يحسب مواعيد الاستجابة والحل من سياسة الأولوية</summary>
public class SlaService : ISlaService
{
    private readonly AppDbContext _db;
    public SlaService(AppDbContext db) => _db = db;

    public async Task<(DateTime? responseDue, DateTime? resolutionDue)> ComputeDueDatesAsync(
        int companyId, TicketPriority priority, DateTime reportedAt, CancellationToken ct = default)
    {
        var policy = await _db.SlaPolicies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Priority == priority
                                      && p.IsActive && !p.IsDeleted, ct);

        // قيم افتراضية إن لم توجد سياسة معرّفة
        var (resp, res) = policy != null
            ? (policy.ResponseHours, policy.ResolutionHours)
            : priority switch
            {
                TicketPriority.Critical => (1, 4),
                TicketPriority.High => (4, 24),
                TicketPriority.Medium => (8, 72),
                _ => (24, 168)
            };

        return (reportedAt.AddHours(resp), reportedAt.AddHours(res));
    }
}

/// <summary>
/// خدمة الإهلاك — القسط الثابت أو المتناقص، مع حد أدنى إلزامي عند القيمة التخريدية.
/// </summary>
public class DepreciationService : IDepreciationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DepreciationService> _log;

    public DepreciationService(AppDbContext db, ILogger<DepreciationService> log)
    {
        _db = db; _log = log;
    }

    public async Task<decimal> AccrueMonthlyAsync(int assetId, int year, int month, CancellationToken ct = default)
    {
        _db.IgnoreCompanyFilter = true;

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset == null) return 0m;

        // الأصول المستبعدة أو المفقودة لا تُهلك
        if (asset.Status is AssetStatus.Disposed or AssetStatus.Lost) return 0m;
        if (asset.PurchaseDate == null || asset.PurchaseValue is null or <= 0) return 0m;

        // منع التكرار لنفس الشهر
        var exists = await _db.DepreciationEntries
            .AnyAsync(d => d.AssetId == assetId && d.Year == year && d.Month == month, ct);
        if (exists) return 0m;

        var purchase = asset.PurchaseValue.Value;
        var salvage = asset.SalvageValue ?? 0m;
        var lifeYears = asset.UsefulLifeYears ?? asset.Category?.UsefulLifeYears ?? 5;
        if (lifeYears <= 0) lifeYears = 5;

        var opening = asset.BookValue ?? purchase;

        // وصل للقيمة التخريدية — لا مزيد من الإهلاك
        if (opening <= salvage) return 0m;

        decimal monthly = asset.DepreciationMethod == DepreciationMethod.DecliningBalance
            ? opening * (2m / (lifeYears * 12m))
            : (purchase - salvage) / (lifeYears * 12m);

        // ⚠️ الحد الأدنى الإلزامي: لا تنزل القيمة الدفترية عن القيمة التخريدية أبداً
        if (opening - monthly < salvage) monthly = opening - salvage;
        if (monthly <= 0) return 0m;

        monthly = Math.Round(monthly, 2);
        var closing = Math.Round(opening - monthly, 2);

        _db.DepreciationEntries.Add(new DepreciationEntry
        {
            CompanyId = asset.CompanyId,
            AssetId = asset.Id,
            Year = year,
            Month = month,
            OpeningValue = opening,
            DepreciationAmount = monthly,
            ClosingValue = closing,
            Method = asset.DepreciationMethod
        });

        asset.BookValue = closing;
        await _db.SaveChangesAsync(ct);

        return monthly;
    }

    public async Task<int> RunMonthlyForAllAsync(int year, int month, CancellationToken ct = default)
    {
        _db.IgnoreCompanyFilter = true;

        var ids = await _db.Assets
            .Where(a => a.PurchaseDate != null
                        && a.PurchaseValue > 0
                        && a.Status != AssetStatus.Disposed
                        && a.Status != AssetStatus.Lost)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var count = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await AccrueMonthlyAsync(id, year, month, ct) > 0) count++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "فشل احتساب إهلاك الأصل {AssetId}", id);
            }
        }

        _log.LogInformation("مهمة الإهلاك: تم احتساب {Count} قيد لشهر {Month}/{Year}", count, month, year);
        return count;
    }
}

/// <summary>توليد صور QR بمكتبة QRCoder (لا اعتماديات خارجية)</summary>
public class QrCodeService : IQrCodeService
{
    private readonly string _rootPath;

    public QrCodeService(string webRootPath) => _rootPath = webRootPath;

    public byte[] GeneratePng(string content, int pixelsPerModule = 10)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public async Task<string> GenerateForAssetAsync(string assetTag, string baseUrl, CancellationToken ct = default)
    {
        // الرابط المختصر الذي يفتح صفحة الأصل بعد التحقق من الهوية
        var url = $"{baseUrl.TrimEnd('/')}/a/{assetTag}";
        var bytes = GeneratePng(url, 10);

        var dir = Path.Combine(_rootPath, "qrcodes");
        Directory.CreateDirectory(dir);

        var fileName = $"{assetTag}.png";
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, ct);

        return $"/qrcodes/{fileName}";
    }
}

/// <summary>سجل التدقيق الشامل لكل العمليات الحساسة</summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;

    public AuditService(AppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    /// <summary>
    /// إعدادات التسلسل: UnsafeRelaxedJsonEscaping تمنع تحويل الحروف العربية
    /// إلى صيغ \uXXXX غير مقروءة داخل سجل التدقيق.
    /// </summary>
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public async Task LogAsync(string action, string entityName, string? entityId,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            CompanyId = _me.CompanyId,
            UserId = _me.UserId,
            UserName = _me.FullName ?? _me.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues, AuditJson),
            NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues, AuditJson),
            IpAddress = _me.IpAddress,
            UserAgent = _me.UserAgent
        });

        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>إعدادات النظام من قاعدة البيانات (SMTP، الشعار، السياسات)</summary>
public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    public SettingsService(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key, int? companyId = null, CancellationToken ct = default)
    {
        var s = await _db.SystemSettings
            .FirstOrDefaultAsync(x => x.Key == key && x.CompanyId == companyId, ct);

        // ارتداد للإعداد العام إن لم يوجد إعداد خاص بالشركة
        if (s == null && companyId != null)
            s = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key && x.CompanyId == null, ct);

        return s?.Value;
    }

    public async Task SetAsync(string key, string? value, int? companyId = null, CancellationToken ct = default)
    {
        var s = await _db.SystemSettings
            .FirstOrDefaultAsync(x => x.Key == key && x.CompanyId == companyId, ct);

        if (s == null)
            _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, CompanyId = companyId });
        else
            s.Value = value;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string?>> GetCategoryAsync(string category,
        int? companyId = null, CancellationToken ct = default)
    {
        var list = await _db.SystemSettings
            .Where(x => x.Category == category && (x.CompanyId == companyId || x.CompanyId == null))
            .ToListAsync(ct);

        return list
            .GroupBy(x => x.Key)
            // الإعداد الخاص بالشركة يتقدم على العام
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CompanyId ?? 0).First().Value);
    }
}
