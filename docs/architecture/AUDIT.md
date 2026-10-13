# تقرير التدقيق المعماري والهندسي — نظام تتبع الأصول (ATS)

> تاريخ التدقيق: 2026-10-08 · النسخة المرجعية: `4a3a066` على `main`
> المنهجية: قراءة كل ملفات المصدر (60 ملف C# خارج `obj/bin`، 60 ملف `.cshtml`،
> 5 ملفات JS/CSS أساسية، 5 ملفات `.csproj`، `Program.cs`، `render.yaml`، `Dockerfile`)،
> ثم تحقق عددي بـ grep لكل رقم ورد في هذا التقرير.
> كل ملاحظة أدلةها `ملف:سطر` مقروء من المصدر الفعلي، لا مستنتج.
>
> **التقرير لحظة زمنية**: حالة كل ملاحظة بعد الإصلاح تُتتبَّع بأرقامها نفسها في `PLAN.md`،
> والقواعد الجارية اليوم في `ARCHITECTURE.md`. لم يُعدَّل هنا إلا ما ثبت أنه **ادعاء خاطئ**
> (P1-7 — راجع §5).

---

## 1. ملخص تنفيذي

المشروع **ليس فوضى معمارية**. فيه طبقات حقيقية (Domain / Application / Infrastructure / Web)،
وفيه عزل multi-tenant على مستوى EF بتطبيق مركزي واحد، وفيه نمط توحيد القوائم
(`_SideNav` / `_TopBar` / `_Pager`) يعمل في 8 صفحات. Problems الحقيقية ليست "غياب العمارة"،
بل **ثلاث فئات محددة**:

| الفئة | الأعراض | التكلفة |
|---|---|---|
| **أ. قواعد عمل مكررة بنسخ غير متطابقة** | نفس القاعدة (نافذة الضمان، الإهلاك، الأهلية للتسليم) مكتوبة 4–6 مرات بصيغ مختلفة، والنتيجة أرقام متضاربة بين اللوحة والتقارير | أعلى خطر حقيقي — المستخدم يرى تناقض بلا خطأ ظاهر |
| **ب. قاسم مشترك للواجهة غير مُستخرج** | 9 صفحات `Layout = null` تنسخ رأس الصفحة و`tailwind.config` وسكربت الثيم وأنماط التمرير يدوياً (10/10/9/8 نسخة متطابقة by hash)، و4 صفحات إعادة كتابة الترقيم رغم وجود `_Pager.cshtml`، وصفحة `Tickets/IndexModern.cshtml` (333 سطر) **ميتة لا يستدعيها أي controller** | هذا هو السبب الجذري لدورات «صلحت صفحة وعادت المشكلة في الثانية» |
| **ج. فجور الإنتاج (production readiness)** | بذرة كلمة مرور ثابتة في الإنتاج + عدم فرض تغييرها، keys التشفير مؤقتة (تسجيل خروج جماعي مع كل إعادة تشغيل على Render)، لا `UseHttpsRedirection` ولا `Cookie.SecurePolicy`، 44 نقطة `SaveChangesAsync` بلا أي transaction في الحل كله، **صفر اختبارات وصفر CI**، كود وتبعيات ميتة في `Application` | مخاطر أمان وتوافر ملموسة على النشر الحالي |

القرار المعماري: **لا Clean Architecture، لا Repositories، لا AutoMapper، لا ميكروسيرفيس.**
الشكل الحالي (monolith بطبقات + DbContext داخل Controllers) مناسب لحجم 27.8 ألف سطر C# و12 Controller.
المطلوب: إصلاح المواضع التي **انفصلت فيها النسخة عن الأصل**، وإخراج القاسم المشترك للواجهة، وسدّ
فجور النشر — ثم فرض ذلك آلياً (حواجز، انظر `PLAN.md`).

---

## 2. الخريطة المعمارية الحالية

### 2.1 رسم الاعتماديات (مقروء من كل `.csproj`)

```
AssetTracking.Domain            ← لا اعتماديات
AssetTracking.Application       → Domain        + EFCore, Logging.Abstractions, FluentValidation
AssetTracking.Infrastructure    → Application   + EFCore/Identity/SqlServer/Sqlite/Npgsql, Hangfire, MailKit, QRCoder, ClosedXML, Serilog
AssetTracking.Migrations.Postgres → Infrastructure + Npgsql
AssetTracking.Web               → Infrastructure, Migrations.Postgres   (لا ref مباشر على Application أو Domain)
```

Web يصل إلى `Application` بالوراثةTRANSITIVE ويستخدم `ICurrentUser` و`Result<T>` من هناك،
لكنه أيضاً يستدعي `AssetTracking.Infrastructure.Data.AppDbContext` مباشرة في 10 من 12 controller.

### 2.2 تدفق الطلب (النمط السائد فعلياً)

```
HTTP → Program.cs pipeline (ForwardedHeaders → Static → Routing → AuthN → AuthZ)
     → Controller : BaseController  [Authorize(Policy=…)] (+ [FeatureGate] أحياناً)
         ├─ Me/IsAdmin/IsManager  ← ICurrentUser ← Claims ← AppClaimsPrincipalFactory
         ├─ _db.<DbSet> …  315 نقطة استدعاء على 24 DbSet  (AppDbContext مُحقن مباشرة)
         │     └─ الفلتر العام في AppDbContext يضيف عزل الشركة + !IsDeleted تلقائياً
         ├─ projection → ViewModel row (تخطيط من طرف الخادم، بلا entities خام للعرض)
         ├─ _notify / _audit / _seq (خدمات Application.Interfaces ← Infrastructure.Services)
         └─ SaveChangesAsync (ضمن المعاملة الضمنية) → View / RedirectToAction + TempData toast
     → SignalR (NotificationHub) للإشعارات اللحظية · Hangfire للمهام الدائرية (مشروط)
```

### 2.3 وحدتان للعرض تعملان في وقت واحد (مهم جداً لفهم الأخطاء المتكررة)

| | القالب الحديث (`Layout = null` + Tailwind CDN) | القالب القديم (`_Layout.cshtml` + Bootstrap RTL) |
|---|---|---|
| عدد الصفحات | 9 | 43 |
| Tailwind preflight | **مُفعَّل** | **مُعطَّل** (`_Layout.cshtml:45` `corePlugins:{preflight:false}` — الملف الوحيد الذي يضبطه) |
| الترجمة للإنجليزية | runtime عبر `theme.js` (1130 مفتاح) | نفس المحرك |
| الوضع الداكن | 279 قاعدة `html[data-theme="dark"]` في `site.css` تعالج أسماء classes، و**0 استخدام** لتبديل `dark:` الأصلي في أي view | كما يسار |

الاختيار بينهما **في وقت التشغيل حسب الدور**: `AssetsController.cs:171` و`WarehousesController.cs:75`
يُرجعان الحديث دائماً، لكن `CustodyController.cs:122`، `InventoryAuditsController.cs:93`،
`ReportsController.cs:119` تعمل `if (Me.IsAdmin) return View("IndexModern", vm)` — أي أن **المدير
يرى صفحة والفني/مدير الشركة يرى صفحة أخرى مختلفة بالكامل لنفس البيانات**.

### 2.4 حدود الوحدة (Feature Flags)

المحرك موجود ومصمم صح (`FeatureGateAttribute.cs` يحجب المسار فعلياً ويعيد 404 لـ AJAX)،
لكنه مُركَّب على 4 مواضع فقط: `MaintenanceSchedulesController.cs:21`، `TicketsController.cs:23`،
`ReportsController.cs:239`، `ReportsController.cs:506`. **لا gate على** `InventoryAuditsController`
(651 سطر) ولا `WarehousesController` ولا `ReportsController.Index` — فإطفاء المفتاح يخفي الرابط
من القائمة ويترك `/InventoryAudits` و`/Warehouses` و`/Reports` تعمل بالكامل.

---

## 3. ما هو سليم ولا يُمسّ ( ممنوع إعادة كتابته )

مدقق شخصياً، وهذه نقاط قوة حقيقية يجب الحفاظ عليها:

1. **عزل الشركات مركزي**: `AppDbContext.cs:66-109` يطبّق فلتر عام على كل كيان `ICompanyOwned`
   (18 كيان) + حذف ناعم، و`SaveChangesAsync` override في `:120-160` يملأ Created/Updated/Deleted
   تلقائياً. النمط صحيح ولا يحتاج Repository layer.
2. **الحماية من CSRF سليمة**: كل `[HttpPost]` يحمل `[ValidateAntiForgeryToken]`
   (تطابق عددي 100%: Admin 18/18، Tickets 9/9، InventoryAudits 8/8، Assets 3/3، Custody 3/3،
   Notifications 2/2، Maintenance 4/4، Account 3/3)، وForms البسيطة `<form method="post">`
   تحقنها FormTagHelper تلقائياً (`_ViewImports.cshtml` يسجّل الـ TagHelpers).
3. **لا SQL injection**: صفر `FromSql`/`ExecuteSqlRaw`/سلسلة SQL يدوية، وصفر `Process.Start`.
4. **async نظيف**: صفر `.Result` / `.Wait()` / `GetAwaiter().GetResult()` / `async void` في مسار الطلب.
5. **لا entities خام للعرض**: صفر `return View(entity)`؛ 31 projection داخل LINQ + 10 `new XxxViewModel`
   — هذا نمط صحيح وليس تكرراً يستحق التوحيد.
6. **التعامل مع الـ uploads آمن**: صفر `IFormFile`؛ ملفات QR بأسماء مولّدة من الخادم (`AST-…`).
7. **Identity مشدود**: قفل 5 محاولات/15 دقيقة، سياسة كلمة مرور كاملة (`Program.cs:56-66`)،
   ولوحة Hangfire للمدير فقط (`Program.cs:290-297`).
8. **`DisplayHelper`**: رغم أنه 355 سطر و4 مسؤوليات، هو أكثر أصول المشروع اتساقاً (210 استخدام في
   Views و30 في Controllers). تقسيمه الآن = تكلفة بلا عائد.

---

## 4. النتائج حسب الخطورة

### P0 — صحة بيانات وتوافر الخدمة

**P0-1 · قاعدة الإهلاك محسوبة بطريقتين مختلفتين، والتقرير يعرض رقماً لا يُنتجه الدفتر**
- المحرك الحقيقي: `Infrastructure/Services/CoreServices.cs:80-85` — شهري، قسط ثابت أو متناقص
  حسب `a.Method`، مع أرضية القيمة التخريدية `if (opening - monthly < salvage) monthly = opening - salvage`.
- نسخة مستقلة في `Web/ViewModels/ReportViewModels.cs:201-203`:
  `Math.Round((PurchaseValue - SalvageValue) / UsefulLifeYears, 2)` — **سنوي، القسط الثابت فقط،
  يتجاهل `Method`، وبلا أرضية تخريدية**، بينما `:213` يحسب `IsFullyDepreciated`.
- النتيجة: تقرير الإهلاك قد يعرض قسطاً سنوياً على أصل بلغ قيمته التخريدية، ورقم لا يطابق جدول
  `DepreciationEntry`. حالياً محجوب بـ `Financials: false` (`appsettings.json`) — يجب الإصلاح
  **قبل** تشغيل هذا المفتاح.

**P0-2 · قائمة الأصول تقرأ جدول الأصول كاملاً في كل طلب (خطر OOM على Render free = 512MB)**
- `AssetsController.cs:73-118`: عند `sort=value|value_desc` يتم تخطي `Skip/Take` في SQL
  (`:75-88` شرطية) ثم `ToListAsync()` لكل الصف المفلتر (`:110`) وترتيب وترقيم في الذاكرة (`:112-118`).
- `AssetsController.cs:146-151`: إجماليات القيمة تُحسب بقراءة **كل أصول الشركة** (عمودان) في كل طلب.
- العذر الموثق في التعليق (`:71-72`، `:145`) هو قيد SQLite على `decimal` — لكن النشر السحابي على
  **Postgres** (`render.yaml` → `DatabaseProvider=Postgres`)، والقيد لا يوجد هناك.

**P0-3 · إجماليات الفلتر تحسب شيئاً آخر غير ما تعلنه**
- `AssetsController.cs:146-149`: الاستعلام يبدأ من `_db.Assets` مباشرة بلا إعادة تطبيق أي من
  فلاتر `q / categoryId / locationId / departmentId / status` المطبقة على `query` في `:53-67`،
  بينما التعليق يقول «إجماليات الفلتر الحالي». البطاقة أعلى الجدول تعرض إجمالي المخزون كله
  بغض النظر عن الفلتر المطبق.

**P0-4 · كلمة مرور بذرة ثابتة في الإنتاج، وعلامة «يجب تغيير كلمة المرور» لا يفرضها أي كود**
- `DbSeeder.cs:26` `private const string DefaultPassword = "Admin@123";` — يُنشئ `admin@ats.eg`
  و21 حساباً بهذه الكلمة؛ `DbSeeder.cs:36` يجعل الوضع التجريبي **هو الافتراضي عند غياب المفتاح**
  (`!bool.TryParse(...) || v`)؛ `Program.cs:197-204` يستدعي `SeedAsync` بلا أي شرط بيئة، و`DbSeeder.cs:40`
  ينفّذ `db.Database.MigrateAsync()` عند الإقلاع، والحماية الوحيدة `DbSeeder.cs:43` (`Companies.Any`)
  تمنع إعادة البذر لا البذر الأول في الإنتاج.
- `ApplicationUser.MustChangePassword` (`Identity/ApplicationUser.cs:32`) **يُقرأ فقط**
  (`AccountController.cs:131` يضبطه `false`، `AdminController.cs:122` يعرضه) — لا يوجد أي middleware
  أو فلتر يجبر المستخدم على تغييرها. أي حساب بذرة في Render يبقى على كلمة معروفة وموثقة في README.

### P1 — أمان، اتساق القواعد، وموثوقية النشر

**P1-1 · لا معاملات في الحل كله مع تدفقات كتابة متعددة**
- صفر `BeginTransaction` / `CreateExecutionStrategy` (تحقق grep على كامل `src`).
- 44 نقطة `SaveChangesAsync` في 6 controllers + 9 في الخدمات. مثال: `CustodyService.AssignAsync`
  يسجّل (`:71`) ثم يرسل إشعاراً (`:73`) ثم سجل تدقيق `_audit.LogAsync` (`:78`) الذي يعمل
  `SaveChangesAsync` خاصاً به (`CoreServices.cs:205`) — 3 التزامات لكل طلب؛ فشل الأخيرة يترك
  عهدة معلّقة بلا أثر تدقيقي. نفس النمط في `RespondAsync:113→117→125` و`ReturnAsync:162→164→169`.

**P1-2 · نافذة «الضمان يقترب من الانتهاء» مكتوبة 6 مرات بدلالات مختلفة**
`AssetsController.cs:168-169` · `HomeController.cs:185-186` · `BackgroundJobs.cs:163` (عتبتان 30/7 يوم)
· `Views/Assets/Index.cshtml:155-156` · `Views/Custody/MyCustody.cshtml:123` ·
`Views/Reports/Assets.cshtml:223` — وهذه الأخيرة تعني **منتهي** لا **شارف الانتهاء**.

**P1-3 · أهلية الأصل للتسليم بثلاث عضويات مختلفة**
`AssetsController.cs:147` و`AdminController.cs:1243`: `Status != Disposed` فقط؛
`CustodyController.cs:243`: `!= Disposed && != Lost`؛ `CoreServices.cs:62`: `is Disposed or Lost`.
الحارس الخادمي في `CustodyService.cs:40` صحيح، لكن قائمة الاختيار تعرض أصولاً مرفوضة.

**P1-4 · إعدادات SLA الافتراضية لها مصدران**
`CoreServices.cs:31-35` (Critical 1/4، High 4/24، Medium 8/72، افتراضي 24/168) مقابل سياسات البذرة
`DbSeeder.cs:315-318` — أي تعديل في أحدهما لا ينعكس على الآخر.

**P1-5 · مفاتيح DataProtection مؤقتة → تسجيل خروج جماعي مع كل إعادة تشغيل**
- لا `PersistKeysTo*` في أي مكان (grep). على Render القرص لحظي، فكل cold start/redeploy يولّد
  مفاتيح جديدة → كل كوكيز `ats_auth` القديمة تُرفض. مع الخطة المجانية (نوم بعد 15 دقيقة) يعني هذا
  أن المستخدم **يفقد جلسته عدة مرات يومياً**. الإصلاح يمس مخطط القاعدة → يحتاج موافقة (انظر PLAN).

**P1-6 · لا إعادة توجيه HTTPS ولا صرامة كوكيز مع الثقة المطلقة في رؤوس الوكيل**
- `Program.cs:156-157` `KnownNetworks = { }, KnownProxies = { }` (موثق أنه مقصود لأجل Render) —
  أي عميل يستطيع انتحال `X-Forwarded-Proto`، و`ConfigureApplicationCookie` في `Program.cs:73-83`
  لا يضبط `SecurePolicy` (افتراضياً `SameAsRequest`)، ولا يوجد `UseHttpsRedirection` في الملف كله
  (`:165` يضبط `UseHsts` فقط). النتيجة المحتملة: كوكيز مصادقة تُرسل فوق HTTP، و`LastLoginIp`
  قابل للانتحال (`AccountController.cs:66`).

**P1-7 · ~~حقن معادلات/صيغ في تصدير Excel~~ — مسحوبة (ادعاء خاطئ)**
- كان الملاحظة: `Helpers/ExcelExporter.cs:108` `case string s: cell.Value = s;` بلا sanitize،
  فافتُرض أن نصاً يبدأ بـ `=` أو `+` أو `-` أو `@` يُكتب كصيغة.
- **الدحض بالقياس**: مشروع فحص مؤقت بنفس حزمة المشروع (`ClosedXML 0.104.2`) مرّر نفس مسارات الاستغلال
  المفترضة عبر نفس السطر بالضبط (`cell.Value = "…"`)، ثم أعاد قراءة الملف وفكّ XML الورقة:
  ```
  RA1 DataType=Text FormulaEmpty=True Raw==1+1
  RB1 DataType=Text FormulaEmpty=True Raw=+SUM(A1:A2)
  RC1 DataType=Text FormulaEmpty=True Raw=-2+3
  RD1 DataType=Text FormulaEmpty=True Raw=@SUM(1+1)
  RE1 DataType=Text FormulaEmpty=True Raw==cmd|' /C calc'!A0
  hasFormulaElement=False  hasFormulaClose=False
  ```
  ClosedXML لا يفسّر السلسلة كصيغة إطلاقاً عند إسنادها لـ `Value`؛ لا عنصر `<f>` في الملف الناتج،
  فالخليّة تبقى نصاً في Excel. **لا يُضاف sanitize ولا بادئة `'`** — كان سيكون كوداً بلا غاية
  يُفسد القيم المعروضة.
- المتبقي الحقيقي في نفس الملف: لا شيء على حقن الصيغ. مخزن التصدير الوحيد الذي يستحق الحذر هو
  أي تصدير CSV مستقبلي (لا يوجد اليوم)، لأن هناك Excel نفسه يفسّر الحقول.

**P1-8 · تسريب XSS مقوَّد في عارض الإشعارات**
- `wwwroot/js/notifications.js:36-39` دالة `esc()` تعتمد `textContent/innerHTML` فتهرّب `< > &`
  **لكنها لا تُهرّب علامات الاقتباس**، ونتيجتها تُدرج داخل سمات: `:44 href="' + esc(href)` و`:46 esc(n.icon)`
  و`:45 data-id="' + n.id` (بلا أي هرّب)، مع `listEl.innerHTML` في `:62` و`insertAdjacentHTML` في `:128`.
  استغلالها مشروط بوصول نص تحرّره المدخلات إلى `url/icon/message`.

**P1-9 · الصفحة الميتة التي تلتقط الإصلاحات**
- `Views/Tickets/IndexModern.cshtml` (333 سطر) غير مُستدعى من أي controller: البحث عن
  `IndexModern` في الكود يعطي 5 مواضع فقط — `AssetsController.cs:171`، `CustodyController.cs:122`،
  `InventoryAuditsController.cs:93`، `ReportsController.cs:119`، `WarehousesController.cs:75`؛
  و`TicketsController.cs:51` يعمل `return View(vm)` → `Tickets/Index.cshtml` القديم.
  أي «إصلاح شامل» طُبّق على الصفحات الحديثة وصل لهذه الصفحة بلا أن يراه أحد.

**P1-10 · تضارب صلاحيات بين القائمة والـ policy**
- `_SideNav.cshtml:16` تعرض `/Warehouses` بشرط `!isEmp` — أي تظهر **للفني**، بينما
  `WarehousesController.cs:18` يشترط `Policies.ManagerOrAdmin` = (Admin, CompanyManager) فقط
  (`Program.cs:89`) → الفني يضغط فيظهر له AccessDenied.
- الوحدات الثلاث `InventoryAudits` و`MaintenanceSchedules` و`Profile` **لا وجود لها في القائمة**
  (`_SideNav.cshtml:12-20` فيه 7 عناصر فقط) رغم أنها `TechnicianOrAbove` وتعمل بالكامل.

**P1-11 · لا اختبارات ولا CI ولا حواجز**
- صفر مشروع اختبارات (`find *Test*` بلا نتيجة)، صفر `.github/workflows` عدا keep-alive،
  و`Dockerfile` يعمل `restore` ثم `publish` بلا `build` صارم أو فحص. أي إصلاح معماري اليوم
  بلا شبكة أمان.

**P1-12 · `.gitignore` سليم — لكن هناك كود ميت وتبعيات معلنة بلا استخدام**
- صحّحت افتراضاً خاطئاً أثناء التدقيق: `.gitignore` يحوي قسم `.NET` كاملاً (`[Bb]in/` في السطر 36،
  `[Oo]bj/`، `*.db` في 46، `wwwroot/uploads/*`، `appsettings.Production.json`)، و`git check-ignore`
  يؤكد أن `assettracking_dev.db` و`bin/` محجوبان فعلاً. لا مشكلة هنا.
- المتبقي الحقيقي: `Application/Common/Result.cs:25` (`PagedResult<T>`) و`:39`
  (`NotFoundOrForbiddenException`) **بلا أي مستهلك** (مطابقة وحيدة في تعريفهما نفسه)، ونص استثناء
  الأخير مكتوب يدوياً مرة أخرى في `BaseController.cs:26,28`؛ `FluentValidation` معلنة في
  `Application.csproj:12` بلا أي `AbstractValidator` في الحل، و`Microsoft.EntityFrameworkCore`
  معلنة في نفس المشروع (`:11`) بينما كود `Application` لا يستخدم EF إطلاقاً.

### P2 — قابلية صيانة

**P2-1 · رأس الصفحة المنسوخ**: بتجزئة `md5sum` للمناطق المتشابهة، 10 نسخ متطابقة لسكربت منع وميض
الثيم (`Assets/IndexModern.cshtml:98` وأخواتها)، 10 لنسخ Tailwind/FontAwesome/خط Cairo،
9 لكتلة `tailwind.config` (العلامة التجارية + القاهرة) بـ hash واحد `53d81cfb`، و8 لكتلة `<style>`
خاصة بـ `::-webkit-scrollbar`. إصلاح لون أو إضافة token = 9–10 تعديلات.

**P2-2 · الترقيم يعاد اختراعه**: `_Pager.cshtml` موجود ومستعمل في 8 صفحات (`Assets/Index:182`،
`Tickets/Index:277`، `Admin/Users:273`، `Admin/AuditLog:281`، `Custody/Index:150`،
`InventoryAudits/Index:178`، `MaintenanceSchedules/Index:245`، `Notifications/Index:109`)
لكن 4 صفحات حديثة تكتب ترقيمها يدوياً (`Assets/IndexModern.cshtml:278-291`،
`Custody/IndexModern.cshtml:294-307`، `InventoryAudits/IndexModern.cshtml:323-342`،
`Tickets/IndexModern.cshtml:311-324`).

**P2-3 · مساعدات العرض منسوخة**: `StCls` (لون الحالة) في 4 ملفات، `CatIcon` في 3،
وتعبير `roleLabel` الثلاثي في 8 ملفات رغم أن `_TopBar.cshtml:6` يرسمه أساساً.

**P2-4 · الترجمة بمصدرين**: `DisplayHelper.cs:56-327` يملك الأسماء العربية لـ14 enum في C#،
و`theme.js:15-1258` يملك **1130** زوج عربي→إنجليزي يتضمن نفس المفردات (`'مفتوحة'→'Open'`،
`'في المخزن'→'In Storage'`). إضافة عضو enum تستدعي تعديل مكانين. أيضاً جدول `I18N`
(`theme.js:1264`، 47 مفتاح) **يتداخل** مع `ar2en` (نص واحد بمسارين: keyed pass في `:1541-1569`
وامر substitution في `:1375+`)، و55 view من 60 تعتمد كلياً على المحرك الزمني.

**P2-5 · الوضع الداكن الترقيعي**: 279 قاعدة `html[data-theme="dark"]` في `site.css` تعالج
أسماء utility (`.bg-white` في `:1056,1132` و`.text-slate-800` في `:1069,1170,1317`) بينما
**98 حرفاً hexadecimalاً مقصوفاً** (`bg-[#f1f5fa]`×22، `bg-[#e7f0fe]`×18، `bg-[#e9f1fc]`×10)
و**106 استخدام `bg-white`**، و**صفر** استخدام لـ`dark:` في كل الـ views — أي أن التغطية
تعتمد على مطابقة النصوص لا على tokens.

**P2-6 · `HomeController.Index` = 225 سطراً**: دور واحد يجمع بناء 40+ عدّاد
(`_db.…CountAsync`) وتوزيع حسب الدور وبطاقات الحالة؛ نقطة تركز الخطر الأولى في المشروع.

**P2-7 · متفرقات**: `AssetsController.cs:400` و`BaseController.cs:15` خدمة-locator بدل حقن؛
`SequenceService.cs:41-58` يمسح كل أكواد السنة في الذاكرة لكل توليد مع `SemaphoreSlim`
محلي-لل-عملية (`:16`) فلا يحمي من التكرار عند أكثر من نسخة؛ `Result.cs` فيه `PagedResult<T>`
(`:25`) و`NotFoundOrForbiddenException` (`:39`) بلا أي مستهلك (ونص استثنائه مكتوب يدوياً مرتين
في `BaseController.cs:26,28`)؛ حزمة `FluentValidation` معلنة في `Application.csproj` بلا أي
`AbstractValidator` في الحل، و`Microsoft.EntityFrameworkCore` معلنة هناك بلا استخدام.

**P2-8 · لا تقييد معدل على الدخول**: لا `AddRateLimiter` في `Program.cs`؛ القفل لكل حساب
فيصلح رشّ كلمة مرور على قائمة بريدية صالحة دون أن يُفعَّل القفل.

### P3 — تحسينات صغيرة

`Program.cs:78-79` كوكيز 7 أيام منزلقة (طويل)، `/health` مجهول الهوية (`:180`) يكشف حالة القاعدة،
`_SideNav.cshtml:25` فيه `aria-label="القائمة الرئيسية"` غير مترجم، زران أيقونيان بلا `aria-label`
في `Custody/IndexModern.cshtml:279,284` (`title` وحده لا يكفي)، و`dir="rtl"` مثبّت يدوياً في 10 ملفات
مع أسهم `fa-chevron-right` باتجاه واحد.

`/Assets?page=99` (قياس حيّ: `http=200`، لا استثناء، لكن النتيجة قائمة فارغة وكبسولة «السابق» وحدها):
`page` لا يُحصر بآخر صفحة فعلية في مؤشرات الأصول/العهدة/الجرد — `Math.Max`/`Math.Min` النافذة يجعلها
مقلوبة (`from > to`) فيصمت الحلقة. الحصر سلوك جديد للمستخدم (إعادة توجيه أو عرض آخر صفحة)، فلم يُنفَّذ
بدون قرار — بند 5.10 في الخطة.

تحذير تبعيات معلَن في كل بناء: `NU1902 MailKit 4.13.0 (moderate, GHSA-9j88-vvj5-vhgr)` من
`AssetTracking.Infrastructure.csproj`. الترقية تغيير تبعية (خطر سلوك في بروتوكولات البريد) لا إصلاح
آمن بحت، لذلك يُعرض ولا يُنفَّذ تلقائياً؛ الحواجز الحالية لا تمنع ظهوره لأنه تحذير لا خطأ.

---

## 5. ادعاءات فحصتها ورفضتها (حتى لا تُبنى قرارات على خطأ)

| الادعاء المرفوض | كيف دحضه |
|---|---|
| «لا توجد حماية CSRF على 85 نموذج POST» | عدّاد فعلي: كل `[HttpPost]` يقابله `[ValidateAntiForgeryToken]` بنفس العدد في كل controller (انظر §3 بند 2)، والوسوم `<form method="post">` تحقن التوكن تلقائياً عبر FormTagHelper المسجّل في `_ViewImports.cshtml` |
| «تعديل/حذف المستخدمين عبر الشركات (BLOA) في AdminController» | موجود لكن مُطبَّق: `AdminController.cs:1205-1213` دالة `ScopedUsers()` (موثّق كـ«حاجز أمني إلزامي») تُستعمل في كل نقاط الكتابة على `Users` (`:212,235,316,340,355`)؛ المتبقي الحقيقي صغير: فحص البريد المكرر في `:246` يستعلام `_db.Users` بلا scope → يكشف وجود بريد في شركة أخرى |
| «إسناد العهدة لموظف من شركة أخرى ممكن» | محجوب: `CustodyService.cs:48` يقارن `target.CompanyId != asset.CompanyId` (الفجوة الوحيدة: مستخدم بـ `CompanyId == null` يمر بقصد/إهمال) |
| «مزوّد Postgres غير مسجّل رغم أن `render.yaml` يطلبه» | غير صحيح: `Program.cs:36-43` يسجل `UseNpgsql` مع `NormalizePostgresUrl` (`:268`) وتبديل سلوك الطوابع الزمنية |
| «لوحة Hangfire بلا تحقق صلاحية» | `Program.cs:186-189` يمرر `HangfireAdminFilter` و`:290-297` تشترط Admin؛ والمفتاح `ScheduledJobs` مطفأ أصلاً في `appsettings.json` |
| «`render.yaml` غير موجود» | موجود في جذر المستودع ويعرّف خدمة Docker مجانية + قاعدة Postgres |
| «118 نقطة `_db.` في الـ controllers» | العدد الفعلي **315** على 24 `DbSet` — المشكلة أكبر من التقدير لا أصغر |
| «`ExcelExporter` يسمح بحقن صيغ في Excel» (كان P1-7) | دحض عددي بـ ClosedXML 0.104.2: `cell.Value = "=1+1"` → `DataType=Text`، `FormulaA1` فارغ، ولا عنصر `<f>` في XML الورقة؛ النص يبقى نصاً عند الفتح. التفاصيل في §P1-7. |

**مرتان صُغت فيهما ملاحظة ثم دُحِضت بقياس قبل الإصلاح**: «.gitignore معطوب» (راجع §P1-12)
وهذه. القاعدة المستخلصة ومطبقة في `PLAN.md`: لا يُكتب إصلاح قبل دليل تشغيلي، ولا يُعلَّم شيء «منفَّذ»
قبل أن يُنفَّذ فعلاً.

---

## 6. المخاطر التي لم يتسنى التحقق منها

- لم أُنِفّذ أي exploit حي: تقييمات XSS (§P1-8) ثابتة من الكود قراءةً، لكن مصدر قيم
  `n.url`/`n.icon` في الإشعارات لم يُتتبَّع لكل مسارات الإنشاء، ولم يُجرَّب حقن فعلي في المتصفح.
  (كنت أضيفت هنا «CSV injection (§P1-7)» — سحبناها بالقياس، انظر §5.)
- لا توجد بيانات حجم إنتاجي فعلي؛ تقدير خطر الذاكرة في §P0-2 مبني على حد Render المجاني
  (512MB) وعلى النمو السلوكي للاستعلام (O(كل الأصول))، لا على قياس.
- تعارض سلوك الفلتر العام عند `_currentUser == null` أو غير المصادَق (`AppDbContext.cs:114`)
  مع نقاط `[AllowAnonymous]` خارج صفحات الدخول — راجعت قائمة السمات ولم أجد مسار كتابة مجهولاً،
  لكنه يعتمد على بقاء هذه القائمة كما هي.

**التالي:** المعمارية المستهدفة في `TARGET.md`، وخطة الإصلاح المرتبة بحالة التنفيذ في `PLAN.md`.
