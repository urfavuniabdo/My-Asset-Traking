# دليل المعمارية — اقرأه قبل أن تكتب سطراً

> مختصر وقابل للتنفيذ. القرارات الكاملة وأدلتها في `TARGET.md`، والنتائج في `AUDIT.md`،
> والحالة الجارية في `PLAN.md`. كل قاعدة هنا لها حاجز آلي أو مسح يدوي مُوثَّق.

التشغيل: `dotnet build AssetTracking/AssetTracking.sln` ثم
`dotnet test AssetTracking/tests/AssetTracking.Tests/AssetTracking.Tests.csproj`.
الاختبارات تغطي قواعد العمل + الحواجز المعمارية + سلوك التعهّدات (SQLite في الذاكرة).

---

## 1. الطبقات وما يُسمح لكل طبقة

| الطبقة | مسموح | ممنوع |
|---|---|---|
| `Domain` | كيانات + `Rules/` نقية + `Common/Constants.cs` | أي مرجع لمشروع آخر، أي `EntityFrameworkCore` |
| `Application` | عقود الخدمات (`Interfaces/IServices.cs`) + `Result<T>` | `DbContext`، `IActionResult`، `HttpContext`، أي حزمة زائدة |
| `Infrastructure` | تنفيذ العقود، `AppDbContext`، الوظائف الخلفية، البذرة | منطق عرض/HTTP |
| `Web` | Controllers تنظيمية + ViewModels إسقاط + عروض | قواعد عمل مكتوبة داخلها، استعلامات داخل ViewModels |

**عزل المستأجر والحذف الناعم** يعملان من الفلاتر العامة في `AppDbContext` — لا تكتب
فلتر شركة يدوي في controller، ولا تستخدم `IgnoreCompanyFilter` خارج الوظائف الخلفية والبذرة.

---

## 2. أين تعيش قاعدة العمل

`AssetTracking.Domain/Rules/` هي المصدر الوحيد:

- `AssetRules` — الضمان (`WarrantyWarningDays=30` / `WarrantyCriticalDays=7`)،
  خارج الخدمة `IsOutOfService`/`InService()`، قيمة المحفظة `InPortfolioValue()`،
  المحوز في العهدة `HeldInCustody()`، العمر `ResolveUsefulLifeYears`، `AgeInMonths`.
- `DepreciationCalculator` — القسط الشهري/السنوي (ثابت ومتناقص) + أرضية القيمة الاسمية
  + المتراكم والنسبة والاكتمال. **نفس الدالة** تستخدمها خدمة الإهلاك وتقرير الإهلاك.
- `Constants.SlaDefaults.For(priority)` — القيم الاحتياطية للـ SLA، وتُستخدم في البذرة
  وفي `SlaService` معاً.
- `Common.Messages` — نصوص الرسائل التي تقولها طبقتان معاً (مثل `HiddenRecord` التي يردّها
  `BaseController.NotFoundOrForbidden()` و`CustodyService`). نص يُكتب في مكانين = نص يتغيّر
  في واحد منهما بصمت.

### قاعدة الكتابة المزدوجة (.scalar ↔ تعبير EF)
كل قاعدة يحتاجها الكود والاستعلام معاً تحتاج نسختين: دالة عادية (scalar) للتقييم في C#،
ونسخة `Expression<Func<Asset,bool>>` لينفّذها SQL:

```csharp
public static bool IsOutOfService(AssetStatus status) => …;      // للأكواد والـ Views
public static Expression<Func<Asset, bool>> InService() => …;    // للاستعلامات
```

الزوجان يجب أن يتفقا — وهناك اختبار لكل زوج يقارن العدّ في الذاكرة بعدّ EF.
عند إضافة قاعدة: أضف الشكلين معاً + اختبار المطابقة في `AssetRulesTests`.

---

## 3. الوصول للبيانات والكتابة

- **قراءة:** استعلام واحد يبدأ من `_db.<Set>` مع إسقاط مباشر إلى Row/ViewModel
  (`AsNoTracking()` للقوائم). لا `IQueryable` ولا `AppDbContext` داخل `.cshtml`.
- **ذرّية:** كل `SaveChangesAsync` وحده تعهّد كامل في EF — لا تحتاج معاملة صريحة لكتابة مفردة.
- **تدفّق متعدد الحفظ** (كيان + سجل حالة + تدقيق، أو بنود + عدادات):
  `await DbTransaction.RunAsync(_db, async () => { … })` — يفتح معاملة ويضمّها عند الفشل،
  وينضم للمعاملة المفتوحة إن وُجدت بدل أن يرمي استثناء.
- **الإشعارات بعد الالتزام:** `_notify` تدفع عبر SignalR فوراً؛ إذا نُادت داخل التعهّد
  قد يوقظ الجرس بيانات لم تُلتزم. اجعلها بعد `RunAsync`.
- **سجل التدقيق:** `AuditService.LogAsync` يحفظ السجل بنفسه. داخل `DbTransaction` ينضم
  تلقائياً للتعهّد فلا يفصل عن بقية الكتابة.

### الترقيم والإجماليات
`AssetsController.ApplyIndexFilters(...)` تبني الفلتر مرة واحدة، ويُشتق منها العدد
والصفحة والإجماليات — فالفلتر الذي يراه المستخدم هو نفسه ما يجمعه المبلغ.
فرز «القيمة» يجلب `(Id, PurchaseValue)` فقط لترتيب الصفحة في الذاكرة، ثم يُجلب صف
العرض بت `Contains` (لا تحميل جدول كامل لترتيب صفحة).

---

## 4. الواجهة: قلق واحد = موضع إصلاح واحد

| الاحتياج | المكوّن المشترك |
|---|---|
| رأس صفحة حديث (خط/ثيم/مانع وميض/`tailwind.config`) | `Views/Shared/_Head.cshtml` — لكل صفحة `Layout = null` |
| تطبيق الثيم/اللغة قبل أول رسم | `Views/Shared/_ThemeInit.cshtml` (رأس القالبين يستدعيه؛ النسخة التي كانت في `_TopBar` زائدة وحُذفت) |
| قائمة جانبية + شريط علوي | `Views/Shared/_SideNav.cshtml` + `_TopBar.cshtml` (القديم والحديث كلاهما يستدعيهما) |
| ترقيم — قالب Bootstrap | `Views/Shared/_Pager.cshtml` |
| ترقيم — القشرة الحديثة (Tailwind) | `Views/Shared/_PagerModern.cshtml` + `PagerModernModel` (الرابط تُمرَّر كدالة لأن كل صفحة تملك فلاترها) |
| حساب عدد الصفحات | `Web.Helpers.Pagination.TotalPages(totalCount, pageSize)` — لا `(int)Math.Ceiling` يدوية في نماذج العرض |
| حدود حجم الصفحة | `Web.Helpers.Pagination.ClampSize(requested)` — خاصية `PageSize` في كل نموذج قائمة تمرّ به: صفر/سالب → `DefaultPageSize`، وأكبر من `MaxPageSize` (200) → 200. بدونه كان `?PageSize=100000` يجلب الجدول كاملًا |
| صلاحيات الروابط | `NavPolicyFilter` يقيّم السياسات المسجّلة ويضعها في `ViewData["PolicyAccess"]` |
| ألوان/حالة/أيقونة/دور | `DisplayHelper` (`Name(«أي enum»)`, `BadgeClass`, `RoleLabel`, `CategoryIcon`, `DateDash`/`TimeDash`/`DateTimeDash`) |
| خيارات فلترة من enum | `Enum.GetValues<T>()` + `DisplayHelper.Name` — لا قوائم `(رقم, "نص")` مكتوبة يدوياً في قالب |
| نص تقوله طبقتان | `Domain/Common/Messages` (مثل `HiddenRecord`) |
| لغة/ثيم | `wwwroot/js/theme.js` — مفتاح عربي ↔ إنجليزي |

**لا تكتب قائمة أدوار يدوياً في قالب:** `IsInRole(Admin) || IsInRole(CompanyManager)`
انفصلت عن السياسة الحقيقية وجعلت رابط «المخزون» يظهر للفنّي ثم يُرفض له.
استخدم `allowed(Policies.ManagerOrAdmin)`.

**ترجمة أي نص جديد:** أضف المفتاح في كتلة النصوص داخل `theme.js` بنفس الصياغة العربية
حرفياً (المحرك يطابق المفتاح الكامل أولاً ثم يطابق داخل النص).

**الوحدات القابلة للإخفاء:** أي `AppFeature` له رابط في القائمة يجب أن يكون على وحدة الـ
Controller حاجز `[FeatureGate(AppFeature.X)]` على مستوى الفئة — إخفاء الرابط وحده لا يمنع
كتابة العنوان.

---

## 5. الحواجز الآلية

`tests/AssetTracking.Tests/ArchitectureGuardTests.cs` يمسح `src/**` (باستثناء
`obj/bin/Migrations`) بعد تجريد الأسطر من التعليقات (`//`، و`/* */`، و`@* *@`، و`<!-- -->` —
آلة حالة في `CodeLines`/`IndexOfCommentStart` فلا يُحسب تعليق توثيقي كثبوت) ويكسر البناء عند:

1. رقم نافذة ضمان مكتوب يدوياً على أي اسم فيه `warranty` (`.AddDays(<number>)`).
2. قسمة الفرق بين `Purchase…` و`Salvage…` خارج `DepreciationCalculator` — بلا اشتراط أقواس أو ترتيب.
3. إعادة كتابة «خارج الخدمة» بأي ترتيب للشروط أو بصيغة `is not (Disposed or Lost)`.
4. `AppDbContext` داخل مجلد `ViewModels/`.
5. رقم SLA حرفي في `ResolutionHours` أو `ResponseHours` ببذرة أو خدمة أو وحدة ويب.
6. ترقيم صفحات مكتوب باليد في صفحة حديثة — مصدره الوحيد `Views/Shared/_PagerModern.cshtml`. (حدّ معلَن: التمييز عن بطاقة Tailwind بنفس المقاسات يكون بوجود رابط صفحة في نفس السطر، فنسخة ترقيم لا تبني رابطاً في السطر نفسه لا تُكشف — الحواجز نصية سطرية لا شجرية.)
7. صفحة `Layout = null` لا تستدعي `_Head` — تُقبل الصيغتان `PartialAsync("_Head")` و`<partial name="_Head" />`.
8. `(int)Math.Ceiling(… / …)` خارج `Pagination.cs` — كانت سبع نسخ بثلاثة أشكال.
9. قراءة `ats_lang` من `localStorage` في غير `_ThemeInit.cshtml`، بأي أسلوب اقتباس أو نيل بالفهرس.

حدود معلَنة تشترك فيها الحواجز: الأنماط **سطرية**، فصيغة مقسومة على سطرين لا تُرى، وحيلة السقف
`(total + size - 1) / size` تمرّ. القياس الفعلي لكل حاجز — ما يُصاب وما يعبر — في
`docs/ARCHITECTURE_REFACTORING_REPORT.md` §4.

رسالة الفشل تُظهر `الملف:السطر  النص`. لإضافة حاجز: انتهِكْه عمداً في ملف مصدر مؤقت
وتأكد أنه يفشل قبل أن تعتبره ناجحاً — الحواجز أُثبت كسرها بانتهاك تجريبي.

CI (`.github/workflows/ci.yml`): restore → build (Release) → test على كل push/PR لـ main.

---

## 6. ممنوعات مختصرة

- قاعدة عمل في controller أو view بدل `Domain/Rules`.
- `IgnoreCompanyFilter = true` في طبقة Web.
- تحميل جدول ثم ترتيب/جمع في الذاكرة بلا `Skip/Take` مسبق.
- نسخة جديدة من نص موجود: أضف المفتاح لـ `theme.js` ولا تكرر القالب.
- `git commit` لملفات تحمل أسراراً (`appsettings.Production.json`، `.env`، مفاتيح SSH).
