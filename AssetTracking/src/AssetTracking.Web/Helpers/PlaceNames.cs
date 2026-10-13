using AssetTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Web.Helpers;

/// <summary>
/// قاموس أسماء المواضع الثلاثة — حلّ المرجع الرقمي إلى اسمه في شاشة تعرض سجلات
/// تاريخية (نقلات، بنود جرد) تحمل مراجع عددية من ثلاثة جداول.
///
/// لماذا يُحمَّل مرة واحدة: شاشة التفاصيل كان لها أن تُجري استعلامًا مترابطًا لكل
/// سطر ولكل عمود موضع (ستة استعلامات × مئات البنود). القاموس يحمّل الجداول الثلاثة
/// مرة واحدة، وهي جداول بيانات أساسية صغيرة.
///
/// لا يميّز «نوع» السجل: كل عمود يُقرأ من قاموس جدولِه بالاسم، فلا يُسأل «هل الرقم ٤
/// مخزن أم موقع؟» — وهو السؤال الذي جئنا نزيله.
/// </summary>
public sealed class PlaceNames
{
    private readonly Dictionary<int, string> _warehouses;
    private readonly Dictionary<int, string> _sites;
    private readonly Dictionary<int, string> _lines;

    private PlaceNames(Dictionary<int, string> warehouses, Dictionary<int, string> sites,
        Dictionary<int, string> lines)
    {
        _warehouses = warehouses; _sites = sites; _lines = lines;
    }

    public static async Task<PlaceNames> LoadAsync(AppDbContext db, CancellationToken ct = default)
        => new(
            await db.Warehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.NameAr, ct),
            await db.CompanySites.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.NameAr, ct),
            await db.ProductionLines.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.NameAr, ct));

    /// <summary>اسم الموضع — المخزن أولاً لأنه إجابة «أين يُحفظ؟»، ثم الفرع، ثم الخط.</summary>
    public string? Of(int? warehouseId, int? siteId, int? lineId)
        => Named(_warehouses, warehouseId) ?? Named(_sites, siteId) ?? Named(_lines, lineId);

    private static string? Named(Dictionary<int, string> map, int? id)
        => id != null && map.TryGetValue(id.Value, out var name) ? name : null;
}
