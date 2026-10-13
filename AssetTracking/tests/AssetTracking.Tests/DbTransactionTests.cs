using AssetTracking.Domain.Entities;
using AssetTracking.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Tests;

/// <summary>
/// اختبارات التعهّد الواحد — شبكة الأمان لإصلاح P1 الذي وحّد الكتابات المتعددة
/// (عهدة/تذكرة/جرد/صيانة دورية) في commit واحد بدل حفظ مستقلّ لكل جدول.
/// SQLite في الذاكرة على اتصال مفتوح واحد: نفس مسار الكتابة الحقيقي بلا قاعدة دائمة.
/// </summary>
public class DbTransactionTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public DbTransactionTests()
    {
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .RelationalEventId.AmbientTransactionWarning))
            .Options;
    }

    private AppDbContext NewDb()
    {
        var db = new AppDbContext(_options);
        // بلا مستخدم حقيقي هنا — الفلتر العام كان سيُخفي كل شيء
        db.IgnoreCompanyFilter = true;
        return db;
    }

    private static Company NewCompany(string code) => new()
    {
        NameAr = $"شركة {code}",
        NameEn = code,
        Code = code,
        City = "القاهرة"
    };

    public async Task InitializeAsync()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Commit_persists_every_saved_write()
    {
        await using var db = NewDb();

        await DbTransaction.RunAsync(db, async () =>
        {
            db.Companies.Add(NewCompany("TX1"));
            await db.SaveChangesAsync();

            db.Companies.Add(NewCompany("TX2"));
            await db.SaveChangesAsync();
        });

        await using var check = NewDb();
        Assert.Equal(2, await check.Companies.CountAsync());
    }

    [Fact]
    public async Task Failure_after_the_first_save_rolls_the_whole_flow_back()
    {
        // هذا شكل التدفقات المُصلَّحة حرفياً: كيان ثم سجل حالة في حفظين
        await using var db = NewDb();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbTransaction.RunAsync(db, async () =>
            {
                db.Companies.Add(NewCompany("TX3"));
                await db.SaveChangesAsync();

                db.Companies.Add(NewCompany("TX4"));
                await db.SaveChangesAsync();

                throw new InvalidOperationException("فشل بعد الالتزام الأول");
            }));

        await using var check = NewDb();
        Assert.Equal(0, await check.Companies.CountAsync());
    }

    [Fact]
    public async Task Writes_before_a_failed_transaction_do_not_disappear()
    {
        await using var db = NewDb();

        db.Companies.Add(NewCompany("BEFORE"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbTransaction.RunAsync(db, async () =>
            {
                db.Companies.Add(NewCompany("INSIDE"));
                await db.SaveChangesAsync();
                throw new InvalidOperationException("فشل داخل التعهّد");
            }));

        await using var check = NewDb();
        Assert.Equal(1, await check.Companies.CountAsync());
        Assert.Equal("BEFORE", (await check.Companies.SingleAsync()).Code);
    }

    [Fact]
    public async Task Ambient_transaction_is_joined_instead_of_started_twice()
    {
        // EF يرفض معاملة ثانية على نفس السياق — الانضمام هو السلوك المطلوب
        await using var db = NewDb();
        await using var tx = await db.Database.BeginTransactionAsync();

        await DbTransaction.RunAsync(db, async () =>
        {
            db.Companies.Add(NewCompany("TX5"));
            await db.SaveChangesAsync();
        });

        await tx.CommitAsync();

        await using var check = NewDb();
        Assert.Equal(1, await check.Companies.CountAsync(c => c.Code == "TX5"));
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
