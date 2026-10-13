using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AssetTracking.Tests;

/// <summary>
/// قياس أي تنفيذ فعلي يُحمَّل عند التشغيل، لا ما تقوله شجرة التبعيات وحدها. ثلاث
/// نقاط من ماسح التحذيرات: حزمتا System.Net.Http وSystem.Text.RegularExpressions
/// المحذّر بهما في مشروع الاختبار، والمكتبة الأصلية SQLite خلف محذّر
/// SQLitePCLRaw.lib.e_sqlite3.
///
/// لماذا القياس لا الاكتفاء بالتعليل: الفرق بين «تحذير في التبعيات» و«كود يعمل في
/// العملية» هو وحده ما يجعل أولوية التوصية صحيحة أو مبالغة.
/// </summary>
public class InboxImplementationTests
{
    private static void Report(Type t)
    {
        var a = t.Assembly;
        Console.WriteLine($"{t.FullName} <- {a.GetName().Name} v{a.GetName().Version} @ {a.Location}");
    }

    [Fact]
    public void Regex_and_HttpClient_come_from_the_shared_framework_not_the_flagged_packages()
    {
        Report(typeof(Regex));
        Report(typeof(HttpClient));

        // الإطار المشترك في .NET 8 = الإصدار 8.x ويُحمَّل من مجلد Microsoft.NETCore.App.
        // الحزمة المحذّرة (4.3.0) تحمل إصداراً 4.x وتُحمَّل من nuget/packages.
        Assert.Equal(8, typeof(Regex).Assembly.GetName().Version!.Major);
        Assert.Contains("Microsoft.NETCore.App", typeof(Regex).Assembly.Location);

        Assert.Equal(8, typeof(HttpClient).Assembly.GetName().Version!.Major);
        Assert.Contains("Microsoft.NETCore.App", typeof(HttpClient).Assembly.Location);
    }

    [Fact]
    public void The_flagged_package_versions_are_not_present_in_the_output_folder()
    {
        // لو كانت الحزمتان تُنسَخ إلى مخرج الاختبار لكان هناك احتمال أن تُحمَّل إحداهما
        // بدلاً من الإطار. الفحص على مجلد الإخراج نفسه لا على افتراض البناء.
        var dir = Path.GetDirectoryName(typeof(InboxImplementationTests).Assembly.Location)!;
        Assert.False(File.Exists(Path.Combine(dir, "System.Text.RegularExpressions.dll")));
        Assert.False(File.Exists(Path.Combine(dir, "System.Net.Http.dll")));
    }

    [Fact]
    public void Measured_sqlite_native_version_behind_the_development_provider()
    {
        // المحذّر GHSA-2m69-gcr7-jv3q هو في SQLite نفسه قبل 3.50.2، لا في الغلافManaged.
        // القياس من الاتصال الفعلي الذي تستخدمه قاعدة تطوير SQLite، لا من رقم الحزمة.
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "select sqlite_version()";
        var v = Convert.ToString(cmd.ExecuteScalar());
        Console.WriteLine($"sqlite_version() = {v}");

        Assert.NotNull(v);
    }
}
