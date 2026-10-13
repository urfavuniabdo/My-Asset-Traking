using AssetTracking.Domain.Enums;
using AssetTracking.Domain.Rules;

namespace AssetTracking.Tests;

/// <summary>
/// قسط الإهلاك — المصدر الوحيد. هذه الاختبارات هي الضد المباشر للخطأ الذي كان
/// موجوداً: تقرير يعرض قسطاً ثابتاً لا يحترم طريقة الإهلاك ولا الأرضية التخريدية.
/// </summary>
public class DepreciationCalculatorTests
{
    private const decimal Purchase = 120_000m;
    private const decimal Salvage = 20_000m;
    private const int Life5 = 5;

    [Fact]
    public void StraightLine_monthly_is_depreciable_base_over_months()
    {
        var charge = DepreciationCalculator.MonthlyCharge(
            Purchase, Purchase, Salvage, Life5, DepreciationMethod.StraightLine);

        // (120000 - 20000) / 60 شهر
        Assert.Equal(1666.67m, charge);
    }

    [Fact]
    public void StraightLine_annual_is_twelve_times_monthly()
    {
        var monthly = DepreciationCalculator.MonthlyCharge(
            Purchase, Purchase, Salvage, Life5, DepreciationMethod.StraightLine);

        Assert.Equal(Math.Round(monthly * 12m, 2),
            DepreciationCalculator.AnnualCharge(
                Purchase, Purchase, Salvage, Life5, DepreciationMethod.StraightLine));
    }

    [Fact]
    public void DecliningBalance_uses_opening_value_not_purchase()
    {
        var fromPurchase = DepreciationCalculator.MonthlyCharge(
            Purchase, Purchase, Salvage, Life5, DepreciationMethod.DecliningBalance);
        var fromHalf = DepreciationCalculator.MonthlyCharge(
            60_000m, Purchase, Salvage, Life5, DepreciationMethod.DecliningBalance);

        Assert.Equal(4000m, fromPurchase);   // 120000 × (2/60)
        Assert.Equal(2000m, fromHalf);       // نصف القيمة ⇒ نصف القسط
    }

    [Fact]
    public void Salvage_floor_never_breaks_the_book_value()
    {
        // أصل اقترب من قيمته التخريدية: القسط يُقصّ بدل أن ينزل تحتها
        var opening = 21_000m;
        var charge = DepreciationCalculator.MonthlyCharge(
            opening, Purchase, Salvage, Life5, DepreciationMethod.StraightLine);

        Assert.Equal(1_000m, charge);
        Assert.True(opening - charge >= Salvage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Zero_or_negative_useful_life_produces_no_charge(int life)
    {
        Assert.Equal(0m, DepreciationCalculator.MonthlyCharge(
            Purchase, Purchase, Salvage, life, DepreciationMethod.StraightLine));
    }

    [Fact]
    public void Asset_already_at_salvage_is_not_depreciated()
    {
        Assert.Equal(0m, DepreciationCalculator.MonthlyCharge(
            Salvage, Purchase, Salvage, Life5, DepreciationMethod.StraightLine));
    }

    [Fact]
    public void Accumulated_never_goes_negative_when_book_exceeds_purchase()
    {
        Assert.Equal(0m, DepreciationCalculator.Accumulated(Purchase, Purchase + 5_000m));
    }

    [Fact]
    public void Percent_handles_zero_purchase_value_without_division_by_zero()
    {
        Assert.Equal(0m, DepreciationCalculator.DepreciationPercent(0m, 0m));
        Assert.Equal(50m, DepreciationCalculator.DepreciationPercent(100m, 50m));
    }

    [Fact]
    public void Fully_depreciated_flag_matches_salvage_floor()
    {
        Assert.True(DepreciationCalculator.IsFullyDepreciated(Salvage, Salvage));
        Assert.False(DepreciationCalculator.IsFullyDepreciated(Salvage + 0.01m, Salvage));
    }
}
