using AssetTracking.Application.Interfaces;
using AssetTracking.Domain.Common;
using AssetTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Infrastructure.Services;

/// <summary>
/// توليد الأرقام التسلسلية بصيغة PREFIX-YYYY-NNNNN.
/// يقرأ آخر رقم مستخدم في السنة الحالية ويزيده — مع تجاهل فلتر الشركة
/// لضمان التفرد على مستوى النظام كله.
/// </summary>
public class SequenceService : ISequenceService
{
    private readonly AppDbContext _db;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>آخر رقم حُجز لكل نمط: طلبان متزامنان يقرآن نفس MAX قبل التزام أيهما، فينتج كود مكرر يسقط على الفهرس الفريد.</summary>
    private static readonly Dictionary<string, int> _reserved = new();

    public SequenceService(AppDbContext db) => _db = db;

    public Task<string> NextAssetTagAsync(int companyId, CancellationToken ct = default)
        => NextAsync(AppConstants.AssetTagPrefix, ct);

    public Task<string> NextTicketNumberAsync(int companyId, CancellationToken ct = default)
        => NextAsync(AppConstants.TicketNumberPrefix, ct);

    public Task<string> NextAuditCodeAsync(int companyId, CancellationToken ct = default)
        => NextAsync(AppConstants.AuditCodePrefix, ct);

    private async Task<string> NextAsync(string prefix, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var year = DateTime.UtcNow.Year;
            var pattern = $"{prefix}-{year}-";

            int lastNumber = 0;

            if (prefix == AppConstants.AssetTagPrefix)
            {
                var tags = await _db.Assets.IgnoreQueryFilters()
                    .Where(a => a.AssetTag.StartsWith(pattern))
                    .Select(a => a.AssetTag).ToListAsync(ct);
                lastNumber = MaxSuffix(tags, pattern);
            }
            else if (prefix == AppConstants.TicketNumberPrefix)
            {
                var nums = await _db.MaintenanceTickets.IgnoreQueryFilters()
                    .Where(t => t.TicketNumber.StartsWith(pattern))
                    .Select(t => t.TicketNumber).ToListAsync(ct);
                lastNumber = MaxSuffix(nums, pattern);
            }
            else
            {
                var codes = await _db.InventoryAudits.IgnoreQueryFilters()
                    .Where(a => a.Code.StartsWith(pattern))
                    .Select(a => a.Code).ToListAsync(ct);
                lastNumber = MaxSuffix(codes, pattern);
            }

            return Reserve(pattern, lastNumber + 1);
        }
        finally
        {
            _gate.Release();
        }
    }

    // الحجز على مستوى العملية لا مستوى الطلب: الخدمة scoped فلكل طلب نسخة جديدة منها
    private static string Reserve(string pattern, int candidate)
    {
        var number = Math.Max(candidate, _reserved.TryGetValue(pattern, out var already) ? already + 1 : candidate);
        _reserved[pattern] = number;
        return $"{pattern}{number:D5}";
    }

    private static int MaxSuffix(IEnumerable<string> values, string pattern)
    {
        var max = 0;
        foreach (var v in values)
        {
            if (v.Length <= pattern.Length) continue;
            if (int.TryParse(v.AsSpan(pattern.Length), out var n) && n > max) max = n;
        }
        return max;
    }
}
