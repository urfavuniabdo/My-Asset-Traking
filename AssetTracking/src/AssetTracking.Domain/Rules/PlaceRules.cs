namespace AssetTracking.Domain.Rules;

/// <summary>
/// قاعدة الموضع الواحد: السجل يقف في مخزن **أو** في موقع شركة **أو** على خط إنتاج
/// — واحد منها فقط، أو بلا موضع.
///
/// تسري على الأصل (أين هو؟) وعلى نطاق الجرد (أين نجرد؟) لأن السؤالين واحد:
/// «أي موضع؟». كانت المواضع الثلاثة عمودًا واحدًا (LocationId) يجيب ثلاثة أسئلة
/// مختلفة، فكانت الإجابة تعتمد على نوع السجل المُشار إليه. الفصل إلى ثلاثة أعمدة
/// بلا حارس كان يسمح بحالة لا معنى لها — سجل في مخزن وفي فرع وعلى خط معا.
///
/// خط الإنتاج موضعٌ لا تصنيف تشغيلي هنا: الماكينة القائمة على الخط لا تكون في
/// مخزن، وتاريخ نقلاتها («خرجت من الخط إلى المخزن») يسجّلها النظام حركة موضع.
/// </summary>
public static class PlaceRules
{
    public static int PlaceCount(int? warehouseId, int? siteId, int? lineId)
        => (warehouseId.HasValue ? 1 : 0) + (siteId.HasValue ? 1 : 0) + (lineId.HasValue ? 1 : 0);

    /// <summary>لا مخزن ولا موقع ولا خط معًا — بلا موضع مقصود مسموح.</summary>
    public static bool HasOnePlace(int? warehouseId, int? siteId, int? lineId)
        => PlaceCount(warehouseId, siteId, lineId) <= 1;

    /// <summary>هل للسجل موضعٌ ما؟ المقارنة بين موضعين لا تجوز إذا كان أحدهما فارغًا.</summary>
    public static bool HasPlace(int? warehouseId, int? siteId, int? lineId)
        => PlaceCount(warehouseId, siteId, lineId) > 0;

    /// <summary>
    /// تساوي موضعين — بثلاثة أعمدة لا بواحد: رقم المخزن يطابق رقم الموقع بالقيمة
    /// لا بالمعنى، ومقارنة رقم واحد كانت تعدّهما متطابقين.
    /// </summary>
    public static bool SamePlace(int? warehouseA, int? siteA, int? lineA,
        int? warehouseB, int? siteB, int? lineB)
        => warehouseA == warehouseB && siteA == siteB && lineA == lineB;

    /// <summary>نفس القاعدة بصيغة رسالة — فلا يكتب كل سطح تفسيره الخاص للاختراق.</summary>
    public const string OnePlaceError =
        "موضع واحد فقط: مخزن أو موقع شركة أو خط إنتاج — اختر واحدًا منها";
}
