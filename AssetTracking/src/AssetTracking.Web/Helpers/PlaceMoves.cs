using System.Linq.Expressions;
using AssetTracking.Domain.Entities;
using AssetTracking.Infrastructure.Data;
using AssetTracking.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// قارئ النقلات المؤرَّخة لشاشة موضع: المخزن وموقع الشركة يسألان نفس السؤال على
/// نفس الجدول بمرساة مختلفة، فكانت كل شاشة تملك نسختها من الاستجواب ومن حلّ
/// الأسماء. النسخة الثانية كانت أنقص: تتجاهل عمود خط الإنتاج فلا تُسمّي نقلة
/// خرجت من خطّ إلى مخزن.
/// </summary>
public static class PlaceMoves
{
    /// <summary>آخر <paramref name="take"/> نقلة تمسّ الموضع الذي يصفه الحارس.</summary>
    public static async Task<List<WarehouseMoveRow>> RecentAsync(AppDbContext db,
        Expression<Func<LocationLog, bool>> anchor, int take = 12,
        CancellationToken ct = default)
    {
        var moves = await db.LocationLogs.AsNoTracking().Where(anchor)
            .OrderByDescending(m => m.MovedAt).Take(take)
            .Select(m => new
            {
                m.AssetId, AssetName = m.Asset!.NameAr,
                m.FromWarehouseId, m.FromSiteId, m.FromLineId,
                m.ToWarehouseId, m.ToSiteId, m.ToLineId,
                m.MovedAt, m.Reason
            })
            .ToListAsync(ct);

        var places = await PlaceNames.LoadAsync(db, ct);

        return moves.Select(m => new WarehouseMoveRow
        {
            AssetId = m.AssetId,
            AssetName = m.AssetName,
            FromName = places.Of(m.FromWarehouseId, m.FromSiteId, m.FromLineId),
            ToName = places.Of(m.ToWarehouseId, m.ToSiteId, m.ToLineId),
            MovedAt = m.MovedAt,
            Reason = m.Reason
        }).ToList();
    }
}
