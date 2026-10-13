using AssetTracking.Domain.Common;
using AssetTracking.Web.Helpers;
using AssetTracking.Web.ViewModels;

namespace AssetTracking.Tests;

/// <summary>
/// اختبارات مساعد الترقيم الموحّد — يحمي السلوك الذي كان موزّعاً على سبع نماذج عرض
/// بثلاثة أشكال، ومنها حالة PageSize = 0 التي كانت تعطي قسمة على صفر في بعضها.
/// </summary>
public class PaginationTests
{
    [Theory]
    [InlineData(0, 20, 1)]     // لا سجلات: صفحة واحدة لا صفر — الصفر يكسر حلقات الترقيم
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(40, 20, 2)]
    [InlineData(41, 20, 3)]
    [InlineData(5, 0, 1)]      // حجم صفحة صفير: لا قسمة على صفر
    [InlineData(5, -1, 1)]
    [InlineData(-3, 20, 1)]    // عدّاد سالبي (لا يجب أن يحدث) لا ينجب صفحات سالبة
    public void TotalPages_is_never_below_one_and_rounds_up(int totalCount, int pageSize, int expected)
        => Assert.Equal(expected, Pagination.TotalPages(totalCount, pageSize));

    [Theory]
    [InlineData(0, 20)]          // صفير من المستدعي: الافتراضي لا جلب بلا حد
    [InlineData(-7, 20)]
    [InlineData(20, 20)]         // القيم النظامية تمر كما هي
    [InlineData(40, 40)]
    [InlineData(200, 200)]       // الحد نفسه مسموح
    [InlineData(201, 200)]
    [InlineData(100_000, 200)]   // ?PageSize=100000 لا يحوّل الفهرس إلى جلب جدول كامل
    public void ClampSize_enforces_the_declared_page_size_bounds(int requested, int expected)
        => Assert.Equal(expected, Pagination.ClampSize(requested));

    [Fact]
    public void List_view_models_clamp_the_page_size_they_bind()
    {
        // حجم الصفحة يجيء من نص الاستعلام، فأيّ نموذج قائمة يقبل 100000 كان يجلب الجدول كاملاً
        Assert.Equal(AppConstants.MaxPageSize, new AssetIndexViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new CustodyIndexViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new TicketIndexViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new ScheduleIndexViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new AuditIndexViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new AuditLogViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new UserListViewModel { PageSize = 100_000 }.PageSize);
        Assert.Equal(AppConstants.MaxPageSize, new NotificationListViewModel { PageSize = 100_000 }.PageSize);

        Assert.Equal(AppConstants.DefaultPageSize, new AssetIndexViewModel { PageSize = 0 }.PageSize);
        Assert.Equal(40, new AuditLogViewModel { PageSize = 40 }.PageSize);
    }
}
