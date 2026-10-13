using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Domain.Rules;

namespace AssetTracking.Tests;

public class AssetRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, true)]   // ينتهي اليوم — ما زال سارياً
    [InlineData(29, true)]
    [InlineData(30, true)]  // حدّ نافذة التحذير
    [InlineData(31, false)]
    [InlineData(-1, false)] // منتهٍ
    public void Warranty_window_boundaries(int daysFromNow, bool expectedSoon)
    {
        var end = Now.Date.AddDays(daysFromNow);

        Assert.Equal(expectedSoon, AssetRules.IsWarrantyExpiringSoon(end, Now));
    }

    [Fact]
    public void Warranty_end_date_itself_is_still_active()
    {
        var end = Now.Date.AddHours(6); // انتهى اليوم صباحاً — لكنه لا يزال داخل يوم الانتهاء

        Assert.True(AssetRules.IsWarrantyActive(end, Now));
        Assert.False(AssetRules.IsWarrantyExpired(end, Now));
    }

    [Fact]
    public void Expired_and_expiring_soon_are_mutually_exclusive()
    {
        foreach (var offset in new[] { -400, -1, 0, 5, 30, 31, 900 })
        {
            var end = Now.Date.AddDays(offset);

            // «انتهى» و«شارف الانتهاء» لا يجتمعان أبداً — كان الاختلاط هو
            // أصل الخطأ في نسخة التقارير القديمة.
            Assert.False(AssetRules.IsWarrantyExpired(end, Now)
                         && AssetRules.IsWarrantyExpiringWithin(end, Now,
                             AssetRules.WarrantyWarningDays));
        }
    }

    [Fact]
    public void Missing_warranty_date_is_never_expiring_nor_expired()
    {
        Assert.False(AssetRules.IsWarrantyActive(null, Now));
        Assert.False(AssetRules.IsWarrantyExpired(null, Now));
        Assert.False(AssetRules.IsWarrantyExpiringSoon((DateTime?)null, Now));
        Assert.Null(AssetRules.WarrantyDaysRemaining(null, Now));
    }

    [Fact]
    public void Days_remaining_counts_calendar_days()
    {
        Assert.Equal(7, AssetRules.WarrantyDaysRemaining(Now.Date.AddDays(7), Now));
        Assert.Equal(-3, AssetRules.WarrantyDaysRemaining(Now.Date.AddDays(-3), Now));
    }

    [Theory]
    [InlineData(AssetStatus.Disposed, true)]
    [InlineData(AssetStatus.Lost, true)]
    [InlineData(AssetStatus.Active, false)]
    [InlineData(AssetStatus.InStore, false)]
    [InlineData(AssetStatus.UnderMaintenance, false)]
    [InlineData(AssetStatus.Damaged, false)]
    public void Out_of_service_membership(AssetStatus status, bool expected)
        => Assert.Equal(expected, AssetRules.IsOutOfService(status));

    /// <summary>
    /// صيغة استعلام EF يجب أن تُجيب بنفس إجابة الدالة العادية — وإلا انفصلت
    /// «قائمة مُفلترة في قاعدة البيانات» عن «فحص على كائن محمّل» بصمت.
    /// </summary>
    [Fact]
    public void InService_expression_agrees_with_scalar_rule()
    {
        var probe = AssetRules.InService().Compile();

        foreach (var status in Enum.GetValues<AssetStatus>())
        {
            var asset = new Asset { Status = status };
            Assert.Equal(!AssetRules.IsOutOfService(status), probe(asset));
        }
    }

    [Fact]
    public void Portfolio_expression_agrees_with_scalar_rule()
    {
        var probe = AssetRules.InPortfolioValue().Compile();

        foreach (var status in Enum.GetValues<AssetStatus>())
        {
            var asset = new Asset { Status = status };
            Assert.Equal(AssetRules.CountsInPortfolioValue(status), probe(asset));
        }
    }

    [Fact]
    public void Warranty_expression_matches_scalar_over_a_range_of_dates()
    {
        var assets = Enumerable.Range(-35, 75)
            .Select(d => new Asset
            {
                Status = AssetStatus.Active,
                WarrantyEndDate = Now.Date.AddDays(d)
            })
            .ToList();

        var matching = assets.AsQueryable()
            .Count(AssetRules.WarrantyExpiringSoonWithin(Now, AssetRules.WarrantyWarningDays));

        var scalar = assets.Count(a => AssetRules.IsWarrantyExpiringSoon(a.WarrantyEndDate, Now));

        Assert.Equal(scalar, matching);
        Assert.True(matching > 0, "لا بد أن تُنتج النافذة نتائج في هذه العيّنة");
    }

    [Theory]
    [InlineData(null, null, 5)]
    [InlineData(3, 7, 3)]        // عمر الأصل يسبق عمر التصنيف
    [InlineData(null, 7, 7)]     // بلا عمر الأصل → عمر التصنيف
    [InlineData(0, 7, 5)]        // عمر صفر قيمة غير صالحة → الافتراضي النظامي
    [InlineData(-2, null, 5)]
    public void Useful_life_resolution(int? assetLife, int? categoryLife, int expected)
        => Assert.Equal(expected, AssetRules.ResolveUsefulLifeYears(assetLife, categoryLife));

    [Fact]
    public void Age_in_months_is_clamped_at_zero_for_future_dates()
    {
        Assert.Equal(0, AssetRules.AgeInMonths(null, Now));
        Assert.Equal(0, AssetRules.AgeInMonths(Now.Date.AddDays(30), Now));
        Assert.Equal(2, AssetRules.AgeInMonths(Now.Date.AddMonths(-2), Now));
    }

    [Fact]
    public void Default_useful_life_is_used_by_the_resolver_only()
        => Assert.Equal(5, AssetRules.DefaultUsefulLifeYears);
}
