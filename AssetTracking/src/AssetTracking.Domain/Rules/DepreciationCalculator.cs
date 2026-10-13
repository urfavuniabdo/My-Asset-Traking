using AssetTracking.Domain.Enums;

namespace AssetTracking.Domain.Rules;

/// <summary>
/// محرك حساب قسط الإهلاك — المصدر الوحيد للحساب.
///
/// كان القسط يُحتسب بطريقتين: هنا (شهري، يحترم طريقة الإهلاك وأرضية القيمة
/// التخريدية) وفي شاشة تقرير الإهلاك (سنوي، القسط الثابت فقط، بلا أرضية) —
/// فكان التقرير يعرض قسطاً لا يُنتجه دفتر القيود أبداً.
/// </summary>
public static class DepreciationCalculator
{
    /// <summary>
    /// القسط الشهري من قيمة دفترية افتتاحية، مع الأرضية الإلزامية:
    /// لا تنزل القيمة الدفترية تحت القيمة التخريدية أبداً.
    /// </summary>
    /// <returns>القسط، أو 0 إذا بلغ الأصل قيمته التخريدية أو كانت المعطيات غير صالحة.</returns>
    public static decimal MonthlyCharge(decimal openingValue, decimal purchaseValue,
        decimal salvageValue, int usefulLifeYears, DepreciationMethod method)
    {
        if (usefulLifeYears <= 0) return 0m;
        if (openingValue <= salvageValue) return 0m;

        var months = usefulLifeYears * 12m;

        var charge = method == DepreciationMethod.DecliningBalance
            ? openingValue * (2m / months)
            : (purchaseValue - salvageValue) / months;

        // الأرضية التخريدية: نقصّ القسط بدل أن نكسر القيمة الدفترية
        if (openingValue - charge < salvageValue) charge = openingValue - salvageValue;
        if (charge <= 0m) return 0m;

        return Math.Round(charge, 2);
    }

    /// <summary>القسط السنوي المقدر = 12 × القسط الشهري على القيمة الافتتاحية نفسها.</summary>
    public static decimal AnnualCharge(decimal openingValue, decimal purchaseValue,
        decimal salvageValue, int usefulLifeYears, DepreciationMethod method)
        => Math.Round(MonthlyCharge(openingValue, purchaseValue, salvageValue, usefulLifeYears, method) * 12m, 2);

    /// <summary>الإهلاك المتراكم حتى اليوم.</summary>
    public static decimal Accumulated(decimal purchaseValue, decimal bookValue)
        => Math.Max(0m, purchaseValue - bookValue);

    /// <summary>نسبة ما هُلك من أصل القيمة، بلا قسمة على صفر.</summary>
    public static decimal DepreciationPercent(decimal purchaseValue, decimal bookValue)
        => purchaseValue <= 0m
            ? 0m
            : Math.Round(Accumulated(purchaseValue, bookValue) * 100m / purchaseValue, 1);

    /// <summary>هل استهلك الأصل بالكامل (بلغ القيمة التخريدية أو أقل)؟</summary>
    public static bool IsFullyDepreciated(decimal bookValue, decimal salvageValue)
        => bookValue <= salvageValue;
}
