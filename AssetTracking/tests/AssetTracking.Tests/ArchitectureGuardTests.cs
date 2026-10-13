using System.Text.RegularExpressions;

namespace AssetTracking.Tests;

/// <summary>
/// حواجز معمارية آلية — كل فحص يمنع تكرار خطأ وُجد فعلاً في هذا المستودع
/// (قواعد مكتوبة بأكثر من مكان، ونسخ من صيغة الإهلاك داخل طبقة العرض).
/// تكسر البناء عند ظهور الانتهاك، لا عند اختباره يدوياً.
/// </summary>
public class ArchitectureGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AssetTracking.sln")))
            dir = dir.Parent;

        return dir?.FullName
               ?? throw new InvalidOperationException("لم يُعثر على AssetTracking.sln أعلى مجلد الاختبارات.");
    }

    private static IEnumerable<string> Sources(params string[] projectNames)
    {
        foreach (var project in projectNames)
        {
            var root = Path.Combine(RepoRoot, "src", project);
            if (!Directory.Exists(root)) continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (ext is not (".cs" or ".cshtml")) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains("Migrations")) continue;

                yield return file;
            }
        }
    }

    private static IEnumerable<(string File, int Line, string Text)> Lines(
        IEnumerable<string> files, string pattern)
    {
        var rx = new Regex(pattern, RegexOptions.Compiled);
        foreach (var file in files)
        {
            foreach (var (number, text) in CodeLines(file))
            {
                if (rx.IsMatch(text)) yield return (file, number, text.Trim());
            }
        }
    }

    /// <summary>
    /// سطور الكود فقط، مع أرقامها الحقيقية. التعليقات الكتلية وRazor وHTML تُستخرج من السطر
    /// ويُفحص ما عداها، فإعادة كتابة صيغة قديمة داخل تعليق توثيقي ليست انتهاكاً ولا تكسر البناء.
    /// السطر كله داخل تعليق يُتخطى، وتُكرَّم أرقام الأسطر حتى بعد الحذف.
    /// </summary>
    private static IEnumerable<(int Number, string Text)> CodeLines(string file)
    {
        string? closer = null;
        var number = 0;
        foreach (var raw in File.ReadLines(file))
        {
            number++;
            var line = raw;

            if (closer != null)
            {
                var end = line.IndexOf(closer, StringComparison.Ordinal);
                if (end < 0) continue;
                line = line[(end + closer.Length)..];
                closer = null;
            }

            while (true)
            {
                var cut = IndexOfCommentStart(line, out var openLength, out var pendingCloser);
                if (cut < 0) break;

                if (pendingCloser == null)
                {
                    line = line[..cut];
                    break;
                }

                var closedAt = line.IndexOf(pendingCloser, cut + openLength, StringComparison.Ordinal);
                if (closedAt < 0)
                {
                    line = line[..cut];
                    closer = pendingCloser;
                    break;
                }

                line = string.Concat(line[..cut], " ", line[(closedAt + pendingCloser.Length)..]);
            }

            yield return (number, line);
        }
    }

    private static readonly (string Open, string Close)[] BlockOpeners =
    [
        ("/*", "*/"), ("@*", "*@"), ("<!--", "-->")
    ];

    /// <summary>أول بداية تعليق في السطر: // إلى آخره (بلا إغلاق) أو فتح كتلي بمغلقه. -1 لا شيء.</summary>
    private static int IndexOfCommentStart(string line, out int openLength, out string? pendingCloser)
    {
        openLength = 0;
        pendingCloser = null;
        var best = -1;

        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] != '/' || line[i + 1] != '/') continue;
            if (i > 0 && line[i - 1] == ':') continue;   // https:// ليست بداية تعليق
            best = i;
            openLength = 2;
            break;
        }

        foreach (var (open, close) in BlockOpeners)
        {
            var at = line.IndexOf(open, StringComparison.Ordinal);
            if (at < 0 || (best >= 0 && at >= best)) continue;
            best = at;
            openLength = open.Length;
            pendingCloser = close;
        }

        return best;
    }

    private static void AssertClean(IEnumerable<(string File, int Line, string Text)> hits, string why)
    {
        var list = hits.ToList();
        Assert.True(list.Count == 0,
            $"{why}\n" + string.Join("\n", list.Select(h =>
                $"  {Path.GetRelativePath(RepoRoot, h.File)}:{h.Line}  {h.Text}")));
    }

    private static readonly string[] WebAndInfra =
        { "AssetTracking.Web", "AssetTracking.Infrastructure", "AssetTracking.Application" };

    [Fact]
    public void Warranty_window_numbers_are_not_written_by_hand_outside_rules()
        => AssertClean(
            Lines(Sources(WebAndInfra), @"(?=[^\n]*[Ww]arranty)(?=[^\n]*\.AddDays\(\s*[-+]?\s*\d)"),
            "نافذة الضمان يجب أن تأخذ عدد الأيام من AssetRules.WarrantyWarningDays / WarrantyCriticalDays.");

    [Fact]
    public void Depreciation_formula_lives_only_in_domain_rules()
        => AssertClean(
            Lines(Sources(WebAndInfra), @"(?=[^\n]*[Pp]urchase)(?=[^\n]*[Ss]alvage)(?=[^\n]*/\s*[\w(])"),
            "حساب القسط مكرَّر خارج DepreciationCalculator — نفس الصيغة بانفصالها هي خطأ P0-1.");

    [Fact]
    public void Out_of_service_predicate_is_not_rewritten_outside_rules()
        => AssertClean(
            Lines(Sources(WebAndInfra), @"(?=[^\n]*Status)(?=[^\n]*!=)(?=[^\n]*Disposed)(?=[^\n]*Lost)")
                .Concat(Lines(Sources(WebAndInfra), @"(?=[^\n]*is not)(?=[^\n]*Disposed)(?=[^\n]*Lost)")),
            "عضوية «خارج الخدمة» يجب أن تكون AssetRules.IsOutOfService / InService().");

    [Fact]
    public void ViewModels_do_not_touch_the_database()
        => AssertClean(
            Lines(Sources("AssetTracking.Web"), @"AppDbContext")
                .Where(h => h.File.Contains($"{Path.DirectorySeparatorChar}ViewModels{Path.DirectorySeparatorChar}")),
            "نماذج العرض تُمرَّر لها البيانات جاهزة — لا استعلامات داخلها.");

    [Fact]
    public void Sla_numbers_are_not_hardcoded_in_seeders_or_services()
        => AssertClean(
            Lines(Sources(WebAndInfra), @"(Resolution|Response)Hours\s*=\s*\d"),
            "قيم SLA تُقرأ من SlaDefaults.For(priority) وإلا انفصلت البذرة عن السلوك الاحتياطي.");

    [Fact]
    public void Modern_pages_do_not_hand_roll_the_pager()
        => AssertClean(
            Lines(Sources("AssetTracking.Web"), @"(?=[^\n]*(PageUrl\(|asp-route-page|[?&]page=))(?=[^\n]*rounded-xl)(?=[^\n]*justify-center)")
                .Where(h => !h.File.EndsWith("_PagerModern.cshtml")),
            "ترقيم صفحات القشرة الحديثة له مصدر واحد هو Views/Shared/_PagerModern.cshtml — ثلاث نسخ يدوية كانت موجودة فعلاً. حدّ الحاجز النصي: التمييز بين خلية ترقيم وبطاقة بنفس مقاسات Tailwind يكون بوجود رابط صفحة في نفس السطر، فنسخة بلا رابط صفحة لا تُكشف.");

    [Fact]
    public void Paging_math_lives_only_in_the_pagination_helper()
        => AssertClean(
            Lines(Sources("AssetTracking.Web"), @"Math\.Ceiling\([^)]*/")
                .Where(h => !h.File.EndsWith("Pagination.cs")),
            "عدد الصفحات يُحسب عبر Web.Helpers.Pagination.TotalPages — كان مكتوباً سبع مرات بثلاثة أشكال مختلفة.");

    [Fact]
    public void Language_init_script_is_not_inlined_again()
        => AssertClean(
            Lines(Sources("AssetTracking.Web"), @"(?=[^\n]*ats_lang)(?=[^\n]*localStorage)")
                .Where(h => !h.File.EndsWith("_ThemeInit.cshtml")),
            "سكربت ضبط اللغة قبل أول رسم له مصدر واحد هو Views/Shared/_ThemeInit.cshtml — كان ثلاث نسخ، إحداها في _TopBar زائدة عن رأسي القالبين.");

    [Fact]
    public void Comments_do_not_count_as_violations_and_line_numbers_stay_true()
    {
        // إعادة كتابة صيغة قديمة داخل تعليق توثيقي ليست انتهاكاً — كانت تكسر البناء
        var file = Path.Combine(Path.GetTempPath(), $"guard-comments-{Guid.NewGuid():N}.cs");
        File.WriteAllText(file, """
            // صيغة الإهلاك القديمة: (PurchaseValue - SalvageValue) / Years
            /* تعليق كتلي
               يبدأ هنا ويستمر
               (PurchaseValue - SalvageValue) / Years */

            @* تعليق Razor
               (PurchaseValue - SalvageValue) / Years *@

            <!-- تعليق HTML
                 (PurchaseValue - SalvageValue) / Years -->

            var legacy = (PurchaseValue - SalvageValue) / Years;
            """);

        try
        {
            var lines = CodeLines(file).ToList();
            var mentions = lines.Where(l => l.Text.Contains("SalvageValue")).ToList();

            Assert.Single(mentions);
            Assert.Equal(12, mentions[0].Number);
            Assert.Single(lines.Where(l => l.Text.Trim().Length > 0));   // لم ينجُ من الملف غير سطر الكود الحقيقي
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// «مخزن» و«خط إنتاج» لم يعودا قيمةً في عمود نوع: الفصل عن فروع الشركة هو فصل
    /// جداول لا شرط حالة. أي قراءة لـ LocationType خارج تعريف الكيان المجمَّد تعني
    /// قائمة تُشتق بالنوع — وهو بالضبط الشكل الذي كان يحوّل «إضافة مخزن» إلى «إضافة موقع».
    /// </summary>
    [Fact]
    public void Place_entity_is_never_chosen_by_a_type_column()
        => AssertClean(
            Lines(Sources("AssetTracking.Web", "AssetTracking.Infrastructure",
                    "AssetTracking.Application", "AssetTracking.Domain"),
                    @"\bLocationType\s*\.")
                .Where(h => !h.File.EndsWith("Organization.cs") && !h.File.EndsWith("Enums.cs")),
            "التمييز بين المخزن وموقع الشركة وخط الإنتاج بالكيان لا بالنوع — LocationType باقٍ فقط لتعريف جدول Locations المجمَّد.");

    /// <summary>
    /// جدول المواقع الموحَّد مجمَّد: لا كاتب جديدًا عليه. الغرض أن العودة إلى
    /// «Locations بعمود نوع» تُكسر في البناء لا في مراجعة بشرية، فالكيان ما زال في
    /// النموذج حتى تُسقطه المرحلة الثانية.
    /// </summary>
    [Fact]
    public void Retired_unified_location_table_has_no_writer()
        => AssertClean(
            Lines(Sources("AssetTracking.Web", "AssetTracking.Infrastructure", "AssetTracking.Application"),
                @"\b(Locations\.(Add|AddRange|Update|Remove|RemoveRange)|new Location\s*[\{(])"),
            "المسارات تكتب في Warehouses وCompanySites وProductionLines — الكتابة في Locations تعيد الكيان الموحَّد الذي رُفض بنصِّ الميزة.");

    [Fact]
    public void Standalone_pages_use_the_shared_head()
    {
        // أي صفحة Layout = null بدون _Head تعني رأساً ثامناً يُنسى عند أول تعديل للخط أو الثيم.
        var files = Sources("AssetTracking.Web")
            .Where(f => f.EndsWith(".cshtml") && !f.EndsWith("_Head.cshtml"))
            .ToList();

        var offenders = new List<(string File, int Line, string Text)>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("Layout = null")) continue;
            if (text.Contains("PartialAsync(\"_Head\"") || text.Contains("name=\"_Head\"")) continue;
            var line = File.ReadLines(file).Select((l, i) => (l, i))
                .First(x => x.l.Contains("Layout = null"));
            offenders.Add((file, line.i + 1, "Layout = null بلا _Head.cshtml (لا PartialAsync ولا <partial name=\"_Head\" />)"));
        }

        Assert.True(offenders.Count == 0,
            "الصفحات المستقلة يجب أن تستورد رأس الصفحة الموحّد _Head.cshtml.\n"
            + string.Join("\n", offenders.Select(h =>
                $"  {Path.GetRelativePath(RepoRoot, h.File)}:{h.Line}  {h.Text}")));
    }
}
