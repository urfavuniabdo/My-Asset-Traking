# مراجعة مستقلة بعد إعادة الهيكلة — مصفوفة التتبع والمخاطر (دليل التحقق)

**التاريخ:** 2026-10-11 · **الغرض:** إثبات حالة كل بند معماري بالكود الفعلي، لا بسرد ما تم.
**منهجية:** قراءة الكود الحالي + `git status/diff/show` + تشغيل `dotnet test` + **قياسات متعمَّدة**
(ملفات انتهاك مؤقتة، واختبار ترجمة SQL على SQLite، وتجربة معاملة على SqlServer) حُذفت بعد القياس.
**قاعدة الحكم:** كل سطر إما مُبرهَن بتشغيل فعلي، أو مُصحَّح لأنه لم يصمد أمام القياس، أو معلَن كغير قابل للتحقق.

الحالات: ✅ مكتمل ومثبت · 🟡 مكتمل جزئيًا · ⬜ لم يُنفَّذ · ⏸️ مؤجَّل · ❓ غير قابل للتحقق محليًا

---

## 0. ما قسته فعلًا في هذه الجولة (المخرجات الحرفية)

| القياس | الأمر/الطريقة | النتيجة الحرفية |
|---|---|---|
| حالة الاختبارات | `dotnet test` | `Passed! - Failed: 0, Passed: 56, Skipped: 0, Total: 56` (لحظة المراجعة؛ صارت **68/68** بعد دفعة الإصلاح الآمن أدناه) |
| حواجز الواجهة التسعة على انتهاكات حقيقية | 3 ملفات طُعم (`ProbeBad.cs`, `ViewModels/ProbeBadVm.cs`, `_ProbeBad.cshtml`) | `Failed: 9, Passed: 0, Total: 9` — كل حاجز أصاب مخالفته |
| ضيق أنماط الحواجز (عينات «يجب أن تمر») | `ProbeOk.cs` + `_ProbeOk.cshtml` | `Failed: 1, Passed: 8` — الثمانية المرادفة دلاليًا **لم تُكتشف**، والواحد الذي فشل كان **إيجابية كاذبة**. *(قياس ما قبل التوسيع؛ نتيجته بعد التوسيع في §4)* |
| الإيجابيات الكاذبة من التعليقات | تعليق `/* … */` فيه `Math.Ceiling(TotalCount` + تعليق Razor `@* … *@` فيه `getItem('ats_lang')` | `Failed: 2, Passed: 7` — الحاجزان 7 و8 يفشلان على **تعليق توثيقي**، وتخطّي `//` يعمل صح |
| `Sum` على `decimal` في SQLite | اختبار مؤقّت على `AppDbContext` بـSQLite في الذاكرة | `System.NotSupportedException : SQLite cannot apply aggregate operator 'Sum' on expressions of type 'decimal'.` — أي أن تعليق `HomeController.cs:150` **صحيح** |
| المعاملات على SqlServer مع `EnableRetryOnFailure(3)` | اختبار مؤقّت: `db.Database.BeginTransactionAsync()` + `DbTransaction.RunAsync` | كلاهما وصل لفتح الاتصال: `SqlException: A network-related or instance-specific error…` — **لم يظهر** `InvalidOperationException` المتوقَّع نظريًا |
| الفهارس الفريدة | `grep IsUnique src` + `grep IX_Assets_AssetTag` في مهاجرة Postgres | `AssetConfig.cs:53` + `20261006185834_InitialCreate.cs:1077-1080 unique: true` — الفهرس **موجود ومنشور** |
| التبعيات الضعيفة | `dotnet list package --vulnerable --include-transitive` | 4 تحذيرات عالية/متوسطة **مرحلة** لا تظهر في البناء (انظر §3) |
| التدفقات غير المغلّفة | `grep -A2 SaveChangesAsync` ثم مطابقة موضعها بمواضع `DbTransaction.RunAsync` | 31 موضع حفظ+تدقيق مزدوج خارج معاملة (تفصيلها في §2.1) |
| **سباق التوليد — حالة «قبل»** | تعطيل الحجز أحادي الاتجاه في `SequenceService.Reserve` وتشغيل `SequenceServiceTests` | `Failed: 2, Passed: 1` — `Assert.Equal() Failure: Expected: 25 Actual: 1` (25 طلبًا متزامنًا أعطت **كودًا واحدًا**) و«AST-2026-00001 لم يتقدم على AST-2026-00001» |
| **سباق التوليد — حالة «بعد»** | إعادة الإصلاح وتشغيل نفس الاختبارات ثم الحزمة كاملة | `Passed: 3, Failed: 0` ثم `Passed! - Failed: 0, Passed: 68, Total: 68` |
| `PageSize` من نص الاستعلام | `new AssetIndexViewModel { PageSize = 100_000 }.PageSize` ونظائره في 8 نماذج | `200` لكل النماذج (`Pagination.ClampSize`) — كان `MaxPageSize` معرَّفًا ولا يُستدعى من أي موضع |
| الحواجز موسَّعة على طُعوم دلالية | `ProbeGuard.cs` + `_ProbeGuard.cshtml` + `ViewModels/ProbeGuardViewModel.cs` (حُذفت بعد القياس) | `Failed: 9, Passed: 1` من 10 — التسعة أصابت نسخًا **بأسلوب بصري/ترتيبي مختلف** عن النص القديم، والوحيد الذي مرّ هو اختبار التعليقات (وهو المطلوب) |
| تعليق على سطر واحد كان يُعمي الحاجز | نفس الطُعم: `_ProbeGuard.cshtml` يبدأ بـ`@* … *@` مُغلق في السطر نفسه | قبل الإصلاح: `Failed: 5, Passed: 5` (حاجزا الترقيم واللغة **فوّتا** المخالفة لأن بقية الملف حُسب تعليقًا) — بعد إصلاح آلة الحالة: الطُعمان يفشلان كما يجب |

> ملفات الطُعم حُذفت كلها؛ `git status --short | grep -i probe` لا يُظهر شيئًا، وأُعيد `dotnet test` → **68/68**، و`dotnet build -c Release` → `0 Error(s)` مع تحذيرين `NU1902 MailKit`.

---

## 1. مصفوفة التتبع — البنود المعمارية بالحالة الفعلية

| البند | الحالة قبل | التغيير الفعلي (كما هو في الكود الآن) | دليل من الكود | اختبار يثبت | لم يُختبر / التصحيح | حالة |
|---|---|---|---|---|---|---|
| صيغة الإهلاك موحَّدة | محسوبة في 4 مواضع | `Domain/Rules/DepreciationCalculator` مصدر وحيد | `DepreciationCalculator.cs`؛ لا قسمة Purchase/Salvage في Web/Infra | `DepreciationCalculatorTests` + حاجز 2 | — | ✅ |
| نافذة تحذير الضمان | 6 نسخ | `AssetRules.WarrantyWarningDays/CriticalDays` | `Constants.cs`/`AssetRules` | `AssetRulesTests` (حدود 29/30/31) + حاجز 1 | بعد التوسيع: `warrantyStart.AddDays(45)` أُفشل بالقياس (§4) | ✅ |
| «خارج الخدمة» | عضويات مختلفة | `AssetRules.IsOutOfService` / `InService()` | استدعاءات في Controller/Service | اختبار مطابقة scalar↔EF + حاجز 3 | بعد التوسيع: `status != Lost && status != Disposed` المعكوسة أُفشلت بالقياس (§4) | ✅ |
| قيم SLA | مكتوبة مرتين | `SlaDefaults.For(priority)` | `Constants.cs` | مقارنات قيم + حاجز 5 | — | ✅ |
| عزل الطبقات | EF معلن في Application بلا استخدام | `Domain.csproj` بلا أي مرجع؛ `Application.csproj` مرجعه الوحيد Domain | مقرأ في هذه الجولة (`cat` الملفين) | حاجز 4 + بناء | — | ✅ |
| إجماليات «الفلتر الحالي» | لا تطبّق الفلتر | تُحسب من نفس `query` المفلتر | `AssetsController.cs:52` ثم `:146` | قياس حيّ (مقارنة مع/بدون فلتر) | — | ✅ |
| الترتيب المالي بلا جلب كامل | جدول كامل في الذاكرة | مفتاحان فقط للصف المفلتر + صفحة واحدة | `AssetsController.cs:76,80,88` | قياس عدد الأعمدة/الصفوف | بقي `Sum` في SQL — **والقياس أثبت أن SQLite لا يدعمه على decimal**، فالتصحيح يحتاج تفرّع مزوِّد (§3-R6) | 🟡 |
| ترقيم القشرة الحديثة | 3 نسخ يدوية | `_PagerModern.cshtml` + `PagerModernModel` في 3 صفحات مع حفظ الفلاتر | `Assets/IndexModern.cshtml:202`, `Custody:228`, `InventoryAudits:263` | حاجز 6 (+لقطة `/Custody`) | — | ✅ |
| رياضيات عدد الصفحات | 7 نسخ بثلاثة أشكال | `Web/Helpers/Pagination.TotalPages` في 8 خواص | `AssetViewModels.cs:46`، `AdminViewModels.cs:35,459`، `AuditViewModels.cs:19`، `CustodyViewModels.cs:58`، `NotificationViewModels.cs:27`، `ScheduleViewModels.cs:21`، `TicketViewModels.cs:66` | `PaginationTests` (8 حالات) + حاجز 7 | — | ✅ |
| `page` خارج المدى | يُعرض فارغًا | **لم يُمسّ** — حد أدنى فقط | `AssetsController.cs:48`، `TicketsController.cs:56` | — | ⏸️ قرار سلوك (لا يُعدّ «إصلاحًا آمنًا») | ⬜ |
| رأس الصفحة الموحَّد | 8 نسخ | `_Head.cshtml` + `_ThemeInit.cshtml` مصدر واحد | حاجز 9 يمرّ على 8/8 صفحات | `Standalone_pages_use_the_shared_head` | القياس: كان يرفض `<partial name="_Head"/>` — التوسيع قبول الصيغتين، والطُعم أثبت: المخالفة تُكشف والصيغة المشروعة تمرّ (§4) | ✅ |
| صفحة ميتة | `Tickets/IndexModern.cshtml` | محذوفة | `git status` (D) + لا تفرّع في `TicketsController` | — | — | ✅ |
| معاملات التدفقات متعددة الخطوات | صفر معاملات | `DbTransaction.RunAsync` مستخدَمة في **12 موضعًا فقط**: CustodyService (3)، InventoryAuditsController (6)، TicketsController (2)، MaintenanceSchedulesController (1) | `DbTransaction.cs:16-35`؛ مواضع الاستخدام بالأسطر 72/119/171، 422/473/528/577/617/648، 438/857، 473 | `DbTransactionTests` 4 حالات (commit/rollback/كتابة سابقة/انضمام) — مُعاد تشغيلها هنا | **التصحيح الأهم:** 31 موضعًا آخر «حفظ + تدقيق» بلا معاملة (§2.1). ادعاء الخطة السابق كان مبالغًا | 🟡 |
| `AuditService.LogAsync` ينضم للمعاملة | يحفظ مستقلًا | لا تغيير في تعاقده: ما زال `SaveChangesAsync` (`CoreServices.cs:194`) — ينضم فقط **داخل** `RunAsync` | مقروء | لا اختبار انضمام حقيقي داخل `LogAsync` | في التدفقات غير المغلّفة لا انضمام إطلاقًا | 🟡 |
| كاتب تدقيق مكرر في Admin | — | `AdminController.cs:1347-1365` دالة خاصة `LogAsync` تكتب `AuditLogs` وتحفظ ثانية — **نسخة موازية** ل`AuditService` بـ18 استعمالاً | مقيس بالعدّ (18) | لا شيء | لم تُذكر في الخطة أصلًا — §2.2 | ⬜ |
| `SequenceService` | مسح السنة لكل توليد + قفل محلي، **و25 طلبًا متزامنًا يعطي كودًا واحدًا** | حجز أحادي الاتجاه داخل `_gate` (`_reserved` على مستوى العملية): لا يُعاد رقم سبق حجزه ولو لم يُحفظ صاحبه | `SequenceService.cs:19` (الحقل) و`:72-78` (`Reserve`) | `SequenceServiceTests` (3): تزامن 25 طلبًا → 25 كودًا متمايزًا، تتابع بلا حفظ → رقم متزايد، لكل بادئة تسلسلها. **قبل/بعد مقيسان** (§0) | يبقى السباق **عبر عمليتين** على الفهرس الفريد (500)؛ النشر الحالي على Render نسخة واحدة. مسح السنة كاملًا في `MAX` لم يُحوَّل لـSQL لأنه يحتاج تفرّع مزوِّد (§3-R6) | 🟡 |
| حجم الصفحة من نص الاستعلام | `MaxPageSize = 200` معرَّف ولا يستدعيه أي موضع، فـ`?PageSize=100000` كان يجلب الجدول كاملًا | `Pagination.ClampSize` + خاصية `PageSize` تمرّ به في 8 نماذج قوائم | `Pagination.cs:19-23`، `AssetViewModels.cs:44-45`، `AdminViewModels.cs:33-34,458-459`، `AuditViewModels.cs:17-18`، `CustodyViewModels.cs:56-57`، `NotificationViewModels.cs:22-23`، `ScheduleViewModels.cs:19-20`، `TicketViewModels.cs:64-65` | `PaginationTests`: 7 حالات حدّ + اختبار رفض `100000` في الثمانية | حجة `page` الخارجة عن المدى ما زالت غير مصحَّحة (§3-R13) | ✅ |
| سياسات القائمة | أدوار منسوخة يدويًا | `NavPolicyFilter` يقيّم السياسات المسجّلة بعد `next()` | `NavPolicyFilter.cs:34-46` | قياس حيّ لكل دور | الفلتر **للعرض فقط**؛ الفرض الحقيقي من `[Authorize]` | ✅ (بحدّه الموثّق) |
| `FeatureGate` على الفئة | وحدات بلا بوابة | 4 بوابات فئة: `InventoryAuditsController.cs:20`, `MaintenanceSchedulesController.cs:21`, `ReportsController.cs:22`, `TicketsController.cs:23` | مقيس بـgrep | لا اختبار إطفاء حقيقي | ❓ إطفاء المفتاح لم يُجرَّب | ✅/❓ |
| كوكي الجلسة وHTTPS | `SameAsRequest` دائمًا | `SameSite=Lax` + `SecurePolicy=Always` خارج Development | `Program.cs:83-89` | فحص رءوس محلي | `UseHttpsRedirection` مُرحلة بقرار (`TARGET.md` §9) | 🟡 |
| تقييد معدل الدخول | لا شيء | سياسة `login` 15/5دقائق بمفتاح `RemoteIpAddress` | `Program.cs:157-164` + `[EnableRateLimiting]` `AccountController.cs:37` | لم يُختبر برشّ فعلي | ⚠️ مفتاحه قابل للتزييف عبر `X-Forwarded-For` (§3-R3) | 🟡 |
| `MustChangePassword` | لا يفرضه شيء | إجباري عند الدخول (`AccountController.cs:75-79`) | مقيس بمراجعة الكود | لم تُختبر جلسة قائمة | ⬜ اعتراض الجلسة القائمة غير منفَّذ | 🟡 |
| `KnownProxies/KnownNetworks` | ثقة مطلقة | **باقية فارغة** | `Program.cs:184-191` | — | لم تُقيَّم كخطر في التقرير السابق — §3-R3 | ⬜ |
| حواجز معمارية | صفر | 9 حواجز نصية **موسَّعة دلاليًا** + حجب التعليقات (كتلية/Razor/HTML) + اختبار يثبت أن التعليق لا يكسر البناء | `ArchitectureGuardTests.cs:44-120` (`CodeLines` + `IndexOfCommentStart`) والأنماط في `:99-152` | `Comments_do_not_count_as_violations…` + قياس الطُعم `Failed: 9, Passed: 1` (§4) | الحدود المتبقية وثقّها سطرًا سطرًا في §4 — منها أن ترقيمًا يدويًا بلا رابط صفحة لا يُكشف | ✅/🟡 |
| CI | لا شيء | `ci.yml` مكتوب؛ لم يُشغَّل على GitHub | `.github/workflows/ci.yml` | — | ⏸️ يحتاج دفعة/دفعًا للموافقة | ❓ |
| تحذيرات التبعيات | غير معلَنة | البناء يُظهر `NU1902 MailKit` فقط | مقيس: `dotnet build -c Release` → 2 Warning | — | **التصحيح:** المسح المتضمّن يكشف 4 تحذيرات عالية الخطورة مرحلة (§3-R5)؛ عبارة «نظيف عدا MailKit» كانت ناقصة | 🟡 |

---

## 2. الفجوات التي كشفتها المراجعة المستقلة

### 2.1 31 موضع «حفظ الكيان ثم حفظ التدقيق» بلا معاملة
> **مُغلقة في الدفعة B1** — المواضع الـ31 لُفّت بـ`DbTransaction.RunAsync` (المجموع الآن 42
> موضعًا)، والرجوع قيس بخلع التعهّد نفسه: `Failed: 15` ثم `Passed: 114`. التفصيل والحدود
> الموثقة (`EditUser` وذيل `CreateUser`/`ResetPassword`) في `docs/EXECUTION_REPORT.md` §4–§5.
توزيعها المحسوب (كلها `await _db.SaveChangesAsync();` يتبعه `LogAsync` خارج أي `RunAsync`):

| الملف | المواضع | الأفعال |
|---|---|---|
| `AdminController.cs` | 18 | CreateUser/EditUser/ToggleUser/UnlockUser/ResetPassword + CRUD الشركات/Aقسام/المواقع/الموردين/التصنيفات/SLA |
| `TicketsController.cs` | 5 | Assign 521/522 · ChangeStatus 585/586 · Resolve 692/693 · Close 739/740 · Reopen 780/781 |
| `AssetsController.cs` | 3 | Create 464/465 · Edit 572/584 · Dispose 610/611 |
| `MaintenanceSchedulesController.cs` | 3 | Create 283/285 · Edit 351/352 · Toggle 403/404 |
| `InventoryAuditsController.cs` | 2 | Create 280/282 · Edit 345/346 |

النتيجة المترتبة: فشل التدقيق بعد التزام الكيان يترك أثرًا operationاليًا بلا سجل تدقيق (خلاف قاعدة الخطة نفسها «≥ حفظين = معاملة»)، ولا يوجد ما يمنع انحراف الحالة. الإصلاح ميكانيكي بالمُعرِّف الموجود `DbTransaction.RunAsync`، بلا مخطط ولا تغيير سلوك ظاهر.

### 2.2 كاتب تدقيق موازٍ في `AdminController`
> **مُغلقة في الدفعة B1** — الحذف والتوحيد إلى `IAuditService.LogTextAsync`، مع اختبار
> مطابقة شكلي (`Admin_audit_row_keeps_the_legacy_raw_text_shape`) يثبت بقاء النص الخام
> والأعمدة نفسها كي لا تنكسر `Views/Admin/AuditLog.cshtml`.
`AdminController.cs:1347-1365` يعيد بناء ما يفعله `AuditService.LogAsync` (`CoreServices.cs:177-194`) مع اختلاف: هذا يأخذ strings جاهزة وذاك يسرْل JSON، وكلاهما يحفظ مباشرة. هذا تكرار منطق عمل (ممنوع في `TARGET.md` §6) غير مُدرج في الخطة.

### 2.3 حقن مفاتيح أجنبية بين الشركات في نموذج الأصل
> **مُغلقة في الدفعة B2** — حاجز `TenantGuard` صار نقطة تحقق واحدة على كل مسارات الربط
> (أصل/مستخدم/تصنيف أب/جرد موقع+مسؤول+موقع فعلي/جدولة فني/تذكرة خط إنتاج)، مقيس بـ25
> اختبارًا + قياس قلب الحاجز (`Failed: 16` عند تعطيله) + طلبات HTTP حيّة للشركة الأخرى
> ترجع `302 → /` بلا بيانات. التفاصيل: `docs/EXECUTION_REPORT.md` §3 و`docs/architecture/PLAN.md`
> (المرحلة B).
`AssetsController.cs` يسند `DepartmentId`/`LocationId`/`VendorId` من النموذج مباشرة (`:439-441` إنشاء، `:532-534` تعديل) بينما التحقق من ملكية الشركة موجود **للتصنيف فقط** (`okCat` حوالي `:666-671`). نفس النمط: `AdminController.cs:269` (`user.DepartmentId`)، و`InventoryAuditsController.cs:466` (`ActualLocationId`). القبول هنا يعني سلامة بيانات + تسريب اسم موقع شركة أخرى في صفحة التفاصيل (`:236`).

### 2.4 ادعاءات صمدت وأخرى سقطت
- سقط ادعاء «فهرس `AssetTag` الفريد يحتاج migration»: موجود ومنشور على Postgres (§0). **إعادة المحاولة عند التصادم إصلاح آمن بلا migration**، ولا يجوز تأجيله بحجة قرار آخر.
- سقط «بريد المستخدم oracle عابر للشركات»: `Program.cs:68` يضبط `RequireUniqueEmail = true`، فالبريد فريد عالميًا بالتصميم، والتحقق الحالي **ضروري** لا ثغرة؛ لا يُصلَّح بالتقييد على الشركة وإلا انكسر الإنشاء.
- لم يُثبَت ادعاء «`EnableRetryOnFailure` يُبطل كل المعاملات على SqlServer»: القياس وصل لفتح الاتصال دون `InvalidOperationException` (انظر §0)، ويبقى الأمر ❓ غير قابل للحسم بلا خادم فعلي؛ التوصية باللف داخل `CreateExecutionStrategy` تظل نمط EF الموصى به ولا تغيّر سلوك SQLite/Postgres.

---

## 3. سجل المخاطر المرتب (أولًا بأول)

| # | الخطر | الدليل | الأثر | الإصلاح الممكن | يحتاج موافقتك؟ |
|---|---|---|---|---|---|
| R1 | 31 موضع حفظ+تدقيق بلا معاملة + كاتب تدقيق مكرر | §2.1/§2.2 | أثر نصف-مكتوب بلا سجل تدقيق | **تم في B1** (42 موضعًا الآن، وكاتب واحد) | لا — **منفَّذ**؛ الرجوع مقيس بخلع التعهّد (`Failed: 15` → `Passed: 114`) |
| R2 | حقن FK بين الشركات | §2.3 | سلامة بيانات + تسريب عابر للشركات | **تم في B2** (`TenantGuard` على كل مسارات الكتابة) | لا — **منفَّذ**؛ 25 اختبارًا + قياس تعطيل الحاجز + طلبات حيّة |
| R3 | تزييف `X-Forwarded-For` من أي عميل | `Program.cs:184-191` + `:157` + `AccountController.cs:68` + `AdminController.cs:1360` | **مقيس حيًا في B1/B2:** 20 محاولة دخول بلا XFF → `429` من المحاولة 16؛ وبنفس العدد مع `X-Forwarded-For` مختلف لكل محاولة → **صفر `429`**. الأثر يمتد لعمود `IpAddress` في سجل التدقيق (نفس الآلية، غير مقاس حيًا) | ضبط `KnownProxies/KnownNetworks` على شبكات الوكيل، أو تجريد الرأس عند الحافة، أو تقسيم المحدد على اسم المستخدم | **نعم** — قرار نشر (يتوقف على topology Render) |
| R4 | سباق توليد `AssetTag` داخل العملية | `SequenceService.cs:19,72-78` بعد الإصلاح + الفهرس الفريد المنشور | **مقيس قبل:** 25 طلبًا متزامنًا أعطت كودًا واحدًا → `DbUpdateException`/500. **بعد:** 25 كودًا متمايزًا | يبقى سباق **عمليتين** معتمدًا على الفهرس؛ حلقة إعادة محاولة حول `SaveChangesAsync` إن صار النشر متعدد النسخ | لا — تم §5-B0 |
| R5 | 4 تحذيرات تبعية عالية/متوسطة مرحلة لا يراها البناء | `dotnet list package --vulnerable --include-transitive`: `SQLitePCLRaw.lib.e_sqlite3 2.1.6` High (via `Microsoft.EntityFrameworkCore.Sqlite 8.0.11`، **تطوير فقط** — ولا إصدار مُرقَّع منشور حتى 2.1.11)، `Newtonsoft.Json 11.0.1` High (via `Hangfire.Core 1.8.14`)، `MailKit/MimeKit 4.13.0` Moderate (مباشرة/انتقالية) | تقييم أمان ناقص + تحديثات مؤجَّلة بلا سجل | **تم التقييم فقط في B4** (تعرّض كل حزمة من الكود + الإصلاحات المتاحة ومخاطرتها: `docs/EXECUTION_REPORT.md` §7). **إضافة مقيسة:** مشروع الاختبارات يحمل High إضافيين (`System.Net.Http 4.3.0`، `System.Text.RegularExpressions 4.3.0` عبر `NETStandard.Library` من xUnit/vstest) لا يُنشَران مع التطبيق. تمكين `NuGetAuditMode=all` كشف-only | الترقية **نعم**؛ تفعيل الكشف لا |
| R6 | لوحات تجلب عمودَي قيمة لكل الصفوف | `ReportsController.cs:39-46`، `HomeController.cs:154-157`، `WarehousesController.cs:45-48` + حلقات `Where/FirstOrDefault` داخل `Select` (`:63-65`) | نمو بلا سقف على خطة 512MB | `SumAsync` في SQL حيث يدعم المزوِّد (Postgres) مع إبقاء المسار الحالي على SQLite (القياس: SQLite **يرفض** decimal Sum) | **نعم** — تفرّع على المزوِّد قرار تصميمي |
| R7 | تسليم عهدة لموظف بلا شركة | `CustodyService.cs:50` (`target.CompanyId != null && …`) | تجاوز فحص الشركة في حالة نادرة | رفض التسليم عندما تكون شركة الموظف فارغة إلا لمدير نظام | **نعم** — يشدّد قاعدة عمل |
| R8 | تعديل بالحالة عبر GET | `NotificationsController.cs:92-108` `Open(id)` بلا `[ValidateAntiForgeryToken]` | CSRF ضعيف الأثر (تعليم مقروء فقط) + CSRF غير محمي من Prefetch | تحويله POST أو قبوله موثّقًا كسلوك مقصود | لا — إصلاح صغير لو طُلب |
| R9 | `/Assets` بلا سياسة دور | `AssetsController.cs:22` (فقط `[Authorize]` الموروث) | **مقيس حيًا في B1/B2:** بكوكي موظف (`nile.emp1`) — `GET /Assets` = `200`، و`/Assets/Details/{id}` = `200`، بينما `/Assets/Edit/1` و`/Reports` و`/Warehouses` و`/Admin/Users` و`/InventoryAudits` = `302 → AccessDenied`. أي أن الكتابة محكومة على الخادم فعلًا، والقراءة ليست كذلك، وإخفاء الرابط في القائمة ليس تحقق صلاحيات | سياسة دور أو قرار منتج بأن ذلك مقصود (موثّق سابقًا في `PLAN.md` 5.7) | **نعم** — قرار منتج (لم يُشدَّد ولم يُرخَّ في هذا العمل) |
| R10 | قدرات الحواجز أضيق من المسميات | §4 | مخالفات دلالية كانت تعبر، وتعليقات توثيقية كانت تكسر البناء | **تم:** أنماط 1/2/3/5/6/7/8 بـlookahead بلا اعتماد على الترتيب، وحجب `//` و`/* */` و`@* *@` و`<!-- -->` | لا — تحسين أدوات (تم §5-B0) |
| R11 | `MaxPageSize = 200` معرَّف ولا يُستخدم | `Constants.cs:101-102` | `?PageSize=100000` كان يحوّل الفهرس إلى جلب جدول كامل | **تم:** `Pagination.ClampSize` في 8 نماذج قوائم + 8 حالات اختبار | لا — نظافة (تم §5-B0) |
| R12 | CI لم يُشغَّل على GitHub | لا دفعة | لا تأكيد أن الحواجز تُطبَّق خارج الجهاز | دفعة تجريبية بعد الموافقة | **نعم** — دفعة/دفع |
| R13 | حجة `page` الخارجة عن المدى | `AssetsController.cs:48` (حد أدنى فقط) و`TicketsController.cs:56` | `?page=99999` يعرض فهرسًا فارغًا بدل إرجاعه لآخر صفحة حقيقية | حد علوي بجانب الحد الأدنى الموجود | **نعم** — يعدّل استجابة قائمة، وهو قرار سلوك |

---

## 4. قدرات الحواجز التسعة — مقيسة بعد التوسيع

طريقة القياس: ملفات طُعم تنتهك **الفكرة** لا **النص** (ترتيب classes مختلف، أحرف صغيرة، اقتباس مزدوج، ترتيب معكوس للشروط)، تشغيل `dotnet test --filter ArchitectureGuardTests`، ثم حذف الطُعم.
النتيجة الحرفية على طُعم الانتهاك: **`Failed: 9, Passed: 1` من 10** (الوحيد الذي مرّ هو اختبار التعليقات — وهو المطلوب). قبل التوسيع كان نفس الطُعم يعطي `Failed: 5, Passed: 5`.

| # | الحاجز | مخالفة يجب أن تفشل (مقيسة على الطُعم) | عينة صحيحة يجب أن تمرّ (مقيسة) | ما يبقى يفوّته (مقيس أو مستنتج من النمط) |
|---|---|---|---|---|
| 1 | `Warranty_window_numbers_…` | `warrantyStart.AddDays(45)` (اسم متغيّر، لا `WarrantyEndDate`) → فشل ✅ | تمرير العدد من `AssetRules.WarrantyWarningDays` | `end.AddDays(45)` بلا كلمة warranty في السطر؛ لا يمسح `Domain` |
| 2 | `Depreciation_formula_…` | `purchase / 1m - salvage / years` (بلا أقواس، بترتيب معكوس) → فشل ✅ | `DepreciationCalculator.AnnualCharge(...)` | **مقيس:** الصيغة مقسومة على سطرين → تمرّ (الحواجز سطرية) |
| 3 | `Out_of_service_predicate_…` | `s != Lost && s != Disposed` معكوس الترتيب → فشل ✅ · `s is not (Disposed or Lost)` → فشل ✅ (بعد إضافة الشرط الثاني) | `AssetRules.IsOutOfService(s)` | شرط واحد فقط (`s != Disposed`) لا يُكشف — وهو قد يكون صحيحًا في سياقه |
| 4 | `ViewModels_do_not_touch_the_database` | `ViewModels/ProbeGuardViewModel.cs` فيه `AppDbContext` → فشل ✅ | نفس النوع في Controller/Service → يمرّ | نموذج عرض خارج مجلد `ViewModels`؛ `IQueryable` مُمرَّر للـVM |
| 5 | `Sla_numbers_are_not_hardcoded_…` | `ResponseHours = 8, ResolutionHours = 24` → فشل ✅ | `SlaDefaults.For(priority)` | الأرقام داخل استدعاء دالة أو تعبير حسابي |
| 6 | `Modern_pages_do_not_hand_roll_the_pager` | نسختان يدويتان بـ`font-semibold`/`text-[12px]` وبترتيب classes مختلف → فشل ✅ (سطران) | `_PagerModern.cshtml` نفسه (مستثنى) + كل بطاقات `w-9 h-9` و`w-11 h-11` → تمرّ | **مقيس:** ترقيم يدوي بلا رابط صفحة في نفس السطر (JS-قياد) لا يُكشف — وهو الثمن مقابل إزالة خمس إيجابيات كاذبة ظهرت عند تجربة نمط أوسع |
| 7 | `Paging_math_lives_only_in_the_pagination_helper` | `Math.Ceiling(count / (double)size)` بأسماء عامة → فشل ✅ | `(total + size - 1) / size` → **تمرّ** (مقيس: تجاوز معروف) · تعليق `/* … Math.Ceiling(TotalCount …) */` → **يمرّ الآن** بعد كان يكسر البناء | حيلة السقف بلا قسمة صريحة؛ القسمة على سطور |
| 8 | `Language_init_script_is_not_inlined_again` | `getItem("ats_lang")` باقتباس مزدوج → فشل ✅ · `localStorage['ats_lang']` بنيل بالفهرس → فشل ✅ (بعد تحويل النمط إلى `ats_lang` + `localStorage`) | `_ThemeInit.cshtml` (مستثنى) · تعليق Razor `@* … *@` → **يمرّ الآن** | أي قراءة لـ`ats_lang` لا تذكر `localStorage` في السطر نفسه |
| 9 | `Standalone_pages_use_the_shared_head` | صفحة `Layout = null` بلا رأس → فشل ✅ | `<partial name="_Head" />` → **تمرّ الآن** (كانت إيجابية كاذبة، والطُعم يثبت الإغلاق) | صيغة ثالثة لدمج نفس الملف (مكوّن Razor) لا تُعرف |

آلة التعليقات نفسها مغطاة باختبار دائم: `Comments_do_not_count_as_violations_and_line_numbers_stay_true` — ملف مؤقت فيه `//` و`/* */` و`@* *@` و`<!-- -->` وكلها تحمل الصيغة القديمة، فيبقى سطر الكود وحده برقمه الحقيقي (12). القياس كشف أيضًا خللًا في أول صيغة للآلة: تعليق `@* … *@` مُغلق في سطر واحد كان يُبقي الحالة «داخل تعليق» إلى آخر الملف فيعمّي الحاجزين 6 و8 (`Failed: 5, Passed: 5`)؛ الصيغة الحالية تُغلقه في نفس السطر.

الخلاصة: الحواجز الآن تصيب الصيغ المكافئة دلاليًا في 7 من 7 حالات الطُعم، وتبقى لها تجاوزات موثقة أعلاه — أهمها أن كل شيء سطري: ما انقسم على سطرين أو استُخلص في JS لا يُرى. أي توثيق يعني «الحواجز تمنع تكرار الخطأ» يُقرأ بهذه الحدود.

---

## 5. خطة العمل المرتبة بالخطورة — B0 مُنفَّذة، وB1–B5 بانتظار موافقتك

| دفعة | المحتوى | ملفات | نوع الاختبار قبل/بعد | الحالة |
|---|---|---|---|---|
| **B0 (آمنة، بلا موافقة)** | R4 حجز تسلسلي أحادي الاتجاه + R11 تفعيل `MaxPageSize` عبر `Pagination.ClampSize` + R10 توسيع الحواجز وحجب التعليقات وإصلاح الإيجابية الكاذبة في حاجز 9 | `SequenceService.cs`، `Pagination.cs`، 8 نماذج قوائم، `ArchitectureGuardTests.cs` | **قبل/بعد مقيسان:** 25 طلبًا متزامنًا → كود واحد (Failed: 2) ثم 25 كودًا متمايزًا؛ طُعم دلالي → `Failed: 5` ثم `Failed: 9, Passed: 1`؛ الحزمة `dotnet test` → **68/68** و`build -c Release` → 0 أخطاء | ✅ **منفَّذة في هذه الجولة** |
| **B1** | R1: لف 31 موضع حفظ+تدقيق بـ`RunAsync` وإسناد Admin لـ`AuditService` بدل كاتبه المكرر | `AdminController` (18)، `TicketsController` (5)، `AssetsController` (3)، `MaintenanceSchedulesController` (3)، `InventoryAuditsController` (2)، `CoreServices.cs` | اختبارات تكامل SQLite لكل controller: فشل التدقيق بعد حفظ الكيان → رجوع الكل؛ `dotnet test` كامل + قياس حيّ عبر HTTP على مسار واحد لكل مجموعة | ⏸️ **بموافقتك** — تعديل بنيوي واسع |
| **B2** | R2: تعميم فحص ملكية الشركة على `DepartmentId`/`LocationId`/`VendorId` في الإنشاء والتعديل + نفس النمط في `AdminController.cs:269` و`InventoryAuditsController.cs:466` | `AssetsController.cs`، `AdminController.cs`، `InventoryAuditsController.cs` | اختبار وحدة رافض لمعرّف من شركة أخرى + اختبار قبول لمعرّف صحيح + رسالة الخطأ بالعربية كما في `okCat` | ⏸️ **بموافقتك** — يرفض مدخلات كان يقبلها |
| **B3** | R6: تجميعات SQL حيث يدعمها المزوِّد (Postgres) مع الحفاظ على مسار SQLite المقيس؛ واستبدال الحلقات التربيعية في `WarehousesController` بـ`GroupBy` | `ReportsController.cs`، `HomeController.cs`، `WarehousesController.cs` | اختبار ترجمة على SQLite/Postgres + قياس عدد الصفوف الجالبة قبل/بعد | ⏸️ **بموافقتك** — تفرّع على المزوِّد قرار تصميمي |
| **B4 (نشر/إعداد)** | R3 قائمة وكلاء معروفة، R9 سياسة `/Assets`، R7 حالة الموظف بلا شركة، R13 حدّ علوي لحجة `page` | `Program.cs`، `AssetsController.cs`، `CustodyService.cs` | قياس حيّ برأس `X-Forwarded-For` مزوّر (محلي) + فحص قائمة لكل دور | ⏸️ **بموافقتك** — قرارات نشر/سلوك |
| **B5 (تبعيات)** | R5 بعد تقييم كل تحذير على حدة: `MailKit`/`MimeKit`، `Newtonsoft.Json` (via Hangfire)، `SQLitePCLRaw` (dev-only) | `.csproj`/`Directory.Packages.props` | بناء Release + اختبارات + تشغيل إرسال بريد فعلي | ⏸️ **بموافقتك** — تحديث تبعية مؤثرة |
| **بانتظار قرار آخر** | DataProtection keys (migration)، اعتراض جلسة `MustChangePassword` القائمة، CI على GitHub (دفعة)، توحيد فاصل التاريخ، Tailwind وقت البناء، صفحة تذاكر حديثة، 5.4/5.5 | — | — | ⏸️ موافقة (migration/دفعة/سلوك) |

---

## 6. كيف تتحقق بيدك (قياسات هذه الجولة قابلة للتكرار)

```bash
cd AssetTracking
dotnet test                                                  # 68/68 (56 قبل دفعة B0)
dotnet build -c Release                                      # 2 Warning (NU1902) فقط — لأن فحص البناء مباشر بطبيعته
dotnet list package --vulnerable --include-transitive         # يكشف ما لا يراه البناء (R5)

# لإثبات قدرة الحواجز على الانتهاكات «الدلالية» لا النصية: طُعم واحد يشيل التسعة
#   src/AssetTracking.Web/ProbeGuard.cs + ViewModels/ProbeGuardViewModel.cs + Views/Shared/_ProbeGuard.cshtml
#   dotnet test --filter FullyQualifiedName~ArchitectureGuardTests   # Failed: 9, Passed: 1
#   (الوحيد الذي يمر هو Comments_do_not_count_as_violations — لأنه يختبر أن التعليق لا يُفشّل البناء)
# لإثبات التجاوزات المتبقية: ProbeEscapes.cs + Views/Shared/_ProbeEscape.cshtml
#   قبل توسيع 3 و8 → Failed: 0 (أربع صيغ مكافئة تعبر) · بعد التوسيع → Failed: 2،
#   ويتبقى تعبر فعلاً: الصيغة المقسومة على سطرين، وحيلة السقف (total + size - 1) / size
# ثم احذف كل الطُعوم: dotnet test → 68/68

# لإثبات أن الإصلاح التسلسلي ضروري فعلاً (حالة «قبل»):
#   اجعل SequenceService.Reserve يرجع candidate مباشرةً بلا حجز
#   dotnet test --filter SequenceServiceTests        # Failed: 2 — 25 طلبًا متزامنًا أعطت كودًا واحدًا
#   ثم أعِد الإصلاح                                 # Passed: 3

# لإثبات أن SQLite لا يدعم SUM على decimal (سبب تأجيل B3 جزئيًا):
#   اختبار مؤقت بـ UseSqlite + db.Assets.Sum(a => a.PurchaseValue!.Value)
#   => NotSupportedException: SQLite cannot apply aggregate operator 'Sum' … 'decimal'
```

**حدود هذه المراجعة (لا تُقرأ كنجاح):** لا SQL Server محليًا، ولا قاعدة Postgres حقيقية، ولا متصفح بعرض < 768px، ولا تشغيل CI على GitHub، ولا رشّ حقيقي على تقييد الدخول. إصلاحات B0 مثبّتة باختبارات وحدة وقياس محلي فقط — لم تُجرَّب حيًا في المتصفح على مسارات القوائم. ما لم يُقَس هنا مكتوب ❓/⬜ في الجدول أعلاه، لا «تم».
