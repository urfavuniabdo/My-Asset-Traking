using AssetTracking.Infrastructure.Data;
using AssetTracking.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Tests;

/// <summary>
/// اختبارات التوليد المتزامن للأكواد التسلسلية. قراءة MAX وحدها لا تكفي: طلبان
/// متزامنان يقرآن نفس الرقم قبل التزام أيهما، فيسقط الثاني على الفهرس الفريد
/// ويظهر للمستخدم كخطأ 500. SQLite في الذاكرة على اتصال مفتوح واحد.
/// </summary>
public class SequenceServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public SequenceServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(_options);
        db.IgnoreCompanyFilter = true;
        return db;
    }

    public async Task InitializeAsync()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _connection.Dispose();

    private async Task<string> NextTagAsync()
    {
        await using var db = NewDb();
        return await new SequenceService(db).NextAssetTagAsync(companyId: 1);
    }

    [Fact]
    public async Task Concurrent_requests_never_receive_the_same_tag()
    {
        var year = DateTime.UtcNow.Year;

        var tags = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => NextTagAsync()));

        Assert.Equal(25, tags.Distinct().Count());
        Assert.All(tags, tag => Assert.Matches($@"^AST-{year}-\d{{5}}$", tag));
    }

    [Fact]
    public async Task Consecutive_generations_increase_even_before_the_first_is_saved()
    {
        var first = await NextTagAsync();
        var second = await NextTagAsync();

        var number = (string tag) => int.Parse(tag.Split('-')[2]);
        Assert.True(number(second) > number(first), $"«{second}» لم يتقدم على «{first}» مع أن أياً منهما لم يُحفظ.");
    }

    [Fact]
    public async Task Every_prefix_keeps_its_own_sequence()
    {
        await using var db = NewDb();
        var seq = new SequenceService(db);

        var tag = await seq.NextAssetTagAsync(1);
        var ticket = await seq.NextTicketNumberAsync(1);
        var audit = await seq.NextAuditCodeAsync(1);

        Assert.StartsWith("AST-", tag);
        Assert.StartsWith("TKT-", ticket);
        Assert.StartsWith("AUD-", audit);
    }
}
