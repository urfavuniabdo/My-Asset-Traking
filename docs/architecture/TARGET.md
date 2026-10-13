# المعمارية المستهدفة وخطة الترحيل — نظام تتبع الأصول (ATS)

> المرجع: نتائج التدقيق في `AUDIT.md`. هذا الملف يصف **الشكل المطلوب** و**قواعد الاعتماديات**
> و**كيف ننتقل** دون إعادة كتابة. فلسفة النقل: أصغر تغيير متماسك يزيل مصدر الإزدواج، لا تغيير
> شكلي يغيّر أسماء.

---

## 1. القرار المعماري

**Monolith طبقاتي مُوحَّد القالب (Layered Modular Monolith) — وهو شكل المشروع الحالي نفسه.**

لا ننتقل إلى Clean Architecture ولا نضيف Repository ولا CQRS ولا ميكروسيرفيس، للأسباب التالية:

| المعيار | الواقع المقاس | النتيجة |
|---|---|---|
| حجم الكود | 27.8 ألف سطر C# + 11.9 ألف سطر Razor | monolith واحد كافٍ |
| عدد الوحدات | 12 controller / 9 كيانات مستأجرة / 8 خدمات | لا مبرر لتقسيم نشر |
| فريق الصيانة | مطوّر واحد ثم مجموعة صغيرة | الطبقات الزائدة ستبطئهم |
| نموذج النشر | Docker + Render instance واحدة + IIS داخلي | لا حاجة لحدود مستقلة بالنشر |
| inversion الحقيقية | `Application/Interfaces/IServices.cs` (9 عقود) تُستعمل في كل Controllers والـ Hub | **التبعية معكوسة فعلياً بالفعل** — المتبقي هو توحيد القواعد لا إضافة طبقات |

الطبقات الأربعة باقية كما هي، مع **تقنين** ما يُسمح لكل طبقة به (القسم 3).

---

## 2. مسؤوليات كل طبقة في الشكل المستهدف

```
AssetTracking.Domain
  Entities/      ← الكيانات + سلوكها الداخلي القليل (نمط RecalculateDowntime الموجود)
  Common/Constants.cs   ← الثوابت الرقمية الوحيدة للقواعد (نافذة الضمان، حدود القيم…)
  Rules/         ← ✅ منفّذ: قواعد عمل نقية بلا EF ولا HTTP (`AssetRules`, `DepreciationCalculator`)

AssetTracking.Application
  Interfaces/    ← عقود الخدمات + ICurrentUser          (يبقى كما هو)
  Common/Result.cs      ← تبقى `Result<T>` للكتابة؛ أُزيل `PagedResult<T>`
                              و`NotFoundOrForbiddenException` (صفر مستهلك — محقق بالبحث)

AssetTracking.Infrastructure
  Data/          ← AppDbContext + الفلاتر العامة + Configurations + DbSeeder
  Services/      ← تنفيذ عقود Application (تبقى تصلح قاعدة البيانات مباشرة)
  Jobs/ Email/ Identity/  ← كما هي

AssetTracking.Web
  Controllers/   ← orchestration فقط: فحص صلاحية، استدعاء خدمة/قاعدة، بناء ViewModel، عرض
  ViewModels/    ← حقول العرض + إسقاط من LINQ (لا حساب قواعد)
  Helpers/       ← DisplayHelper (عرض) + StatusPresentation (جديد: حالة/لون/أيقونة)
  Views/         ← قالب واحد حديث + القالب القديم، بلا نسخ يدوي للرأس
```

---

## 3. قواعد الاعتماديات (قابلة للفحص آلياً)

هذه القواعد هي الـ guardrails المطلوبة في Phase 8. العمود الأخير يوضّح حالتها **الفعلية** في
`tests/AssetTracking.Tests/ArchitectureGuardTests.cs` (فحص نص المصدر على `src/**` باستثناء
`obj/bin/Migrations`) — لا نكتب «منفَّذ» قبل أن يُكسر البناء فعلاً بانتهاك تجريبي:

| # | القاعدة | السبب من التدقيق | الحالة |
|---|---|---|---|
| R1 | `Domain` لا يشير إلى أي مشروع آخر، ولا إلى `Microsoft.EntityFrameworkCore` | يبقّي القواعد قابلة للاختبار بلا قاعدة بيانات | ✅ `Domain.csproj` بلا أي مرجع |
| R2 | `Application` لا يستخدم `DbContext` ولا `IActionResult` ولا `HttpContext` | كشف أن EF معلن في `Application.csproj` بلا استخدام | ✅ أُزيلت `Microsoft.EntityFrameworkCore` و`FluentValidation` و`Logging.Abstractions` (صفر استخدام محقق) |
| R3 | أي كيان `ICompanyOwned` لا يُقرأ في Web خارج استعلام واحد يبدأ من `_db.<Set>` المفلتر عاماً — ويُمنع `IgnoreCompanyFilter = true` في طبقة Web | الحواجز الحالية صحيحة (`AppDbContext.cs:66-109`)؛ القاعدة تمنع إضافتها في controller | ⏳ لم يُكتب كحاجز آلي بعد |
| R4 | لا `IQueryable` ولا `AppDbContext` في نماذج العرض | محقق اليوم (صفر حالة) — نثبّته | ✅ `ViewModels_do_not_touch_the_database` |
| R5 | كل flow كتابة متعددة الخطوات (أكثر من SaveChanges أو SaveChanges + إشعار/تدقيق) يعيش في `Infrastructure.Services` ومعامل صريح | `CustodyService.cs:71→73→78` وغياب المعاملات الكامل | ✅ `DbTransaction.RunAsync` + 4 اختبارات SQLite-في-الذاكرة؛ التغطية الفعلية موثّقة في `PLAN.md` §4.1 (الاستثناء الوحيد `GenerateNow` — وحدتها مُطفأة فلم أمكن تشغيلها حياً) |
| R6 | أي `Controller` عام يجب أن يحمل `[FeatureGate]` على مستوى الفئة إذا كان له رابط في `_SideNav` أو مفتاح في `FeatureFlags` | `AUDIT.md §2.4` — ثلاثة وحدات بلا gate | 🟡 اكتمل لكل من له مفتاح (`InventoryAudits`, `Reports` — `PLAN.md` §5.8)؛ المتبقي وحدات بلا رابط في القائمة — مؤجَّلة كقرار منتج لا كنقص كود |
| R7 | كل رقم في قاعدة عمل (أيام الضمان، عتبات SLA، حدود الإهلاك) يأتي من `Domain.Common.Constants`/`AssetRules` أو من `SlaPolicy` في القاعدة — لا رقم حرفي في controller أو view | `AUDIT.md` P1-2/P1-3/P1-4 | ✅ حاجزان: `Warranty_window_numbers…` + `Sla_numbers…` |
| R8 | القالب الحديث لا يُنشئ رأس صفحة يدوياً: أي `.cshtml` فيه `Layout = null` يجب أن يستدعي `_Head.cshtml`، وسكربت ضبط اللغة قبل أول رسم له مصدر واحد (`_ThemeInit.cshtml`) لا يُعاد كتابته في رأس أو شريط | 10/10/9/8 نسخ متطابقة + 3 نسخ من سكربت الثيم | ✅ `Standalone_pages_use_the_shared_head` + `Language_init_script_is_not_inlined_again` (المرحلة 5.1) |
| R9 | ترقيم الصفحات من مصدر واحد: `_Pager.cshtml` لقالب Bootstrap و`_PagerModern.cshtml` للصفحات الحديثة، ورياضية المدى عبر `Web.Helpers.Pagination` (بما فيها حدّ حجم الصفحة `ClampSize`) | 4 صفحات حديثة تعيد اختراعه + 7 نسخ من `Math.Ceiling` بثلاثة أشكال + `MaxPageSize` لا يستدعيه أحد | ✅ `Modern_pages_do_not_hand_roll_the_pager` + `Paging_math_lives_only_in_the_pagination_helper` (المرحلة 5.2/5.9) + `Pagination.ClampSize` في 8 نماذج قوائم (`PLAN.md` §5.11) — انحراف مُبيَّن في `PLAN.md`: `_Pager` غير قابل للاعتماد في الصفحات الحديثة لأن علاماته تُنمَّق من Bootstrap فقط |
| R10 | لا `.cshtml` بلا مرجع من controller (صفحات ميتة) | `Tickets/IndexModern.cshtml` 333 سطراً | 🟡 المنتهِك الوحيد حُذف (`PLAN.md` §5.6) والمسح اليدوي نظيف؛ لم يتحول لحاجز آلي (مسح نصي له يعطي إيجابيات كاذبة) |

**إثبات أن الحواجز لا تُمرّر:** في الدفعة الأولى أُضيف ملف مصدر مؤقت بأربعة انتهاكات متعمّدة
فأصبح `Failed: 4, Passed: 36` برسالة تُظهر `الملف:السطر`، ثم حُذف فعاد `Passed: 40`.
وفي جولة المراجعة المستقلة رُفع الإثبات إلى **انتهاكات دلالية** — صيغ مختلفة عن النص القديم
(اسم متغيّر بدل `WarrantyEndDate`، قسمة بلا أقواس، شرط بترتيب معكوس، `is not (… or …)`،
`font-semibold` مع ترتيب classes مختلف، اقتباس مزدوج لـ`ats_lang`) بثلاثة ملفات طُعم
(`ProbeGuard.cs` + `ViewModels/ProbeGuardViewModel.cs` + `_ProbeGuard.cshtml`): النتيجة
**`Failed: 9, Passed: 1`** من 10 (الاستثناء هو اختبار التعليقات نفسه)، وحذفها أعاد **68/68**.
القياس كشف أيضاً أن التوسيع الأول كان يُعمي حاجزين (تعليق `@* … *@` من سطر واحد إبقى الحالة
«داخل تعليق») وأن نمطاً أوسع للترقيم أنتج خمس إيجابيات كاذبة على بطاقات يومية — فالأرقام
الحالية هي بعد تصحيح الاثنين. التجاوزات المتبقية مقيسة وموثّقة سطرًا سطرًا في
`docs/ARCHITECTURE_REFACTORING_REPORT.md` §4 و`PLAN.md` §7.1؛ ودليل إضافي لرياضية الترقيم
أن صيغة الحاجز تطابق كود `HEAD` الذي كان ينتهكها فعلاً (`git show`) — فليس حاجزاً زخرفياً.

---

## 4. المواضع التي فيها أكثر من نسخة: الأصل المعتمد

| القاعدة | النسخة الأصلية المعتمدة | ما يُلغى |
|---|---|---|
| نافذة تحذير الضمان | `Domain.Rules.AssetRules.WarrantyWarningDays` / `WarrantyCriticalDays` + `IsWarrantyExpiringSoon(end, now)` + تعبير EF `WarrantyExpiringSoonWithin(now, days)` | 6 تعبيرات مبعثرة (2 controllers، 1 job، 3 views) — ✅ موحَّد |
| أهلية الأصل للتسليم/الإهلاك | `AssetRules.InService()` (عضوية + تعبير EF) و`IsOutOfService(status)` | 3 عضويات `Status != …` مختلفة — ✅ موحَّد |
| حساب الإهلاك | `Domain.Rules.DepreciationCalculator` (قسط شهري/سنوي، ثابت ومتناقص، أرضية القيمة الاسمية، متراكم، نسبة، اكتمال) | النسخة المستقيمة في `ReportViewModels.cs` مقابل `DepreciationService` — ✅ مصدر واحد يستخدمه الاثنان |
| عمر الأصل الافتراضي | `AssetRules.ResolveUsefulLifeYears(assetLife, categoryLife)` + `DefaultUsefulLifeYears` | `?? 0` في التقرير كان يجعل القسط صفراً بصمت — ✅ corrected |
| قيم SLA الافتراضية | `Constants.SlaDefaults.For(priority)` وتُقرأ `SlaPolicy` من القاعدة إن وُجدت | `CoreServices.cs:31-35` مقابل `DbSeeder.cs:315-318` — ✅ مصدر واحد يغذّي البذرة والخدمة |
| إجمالي محفظة الأصول | `AssetRules.InPortfolioValue()` (يستثني المستهلَك فقط) + `HeldInCustody()` | تعريفان مختلفان للوحة والتقارير — ✅ أرقام اللوحة = أرقام التقرير (تم قياسه) |
| اسم/لون/أيقونة الحالة | `Web.Helpers.StatusPresentation` (تغذّي Views والـ JSON معاً) | 4× `StCls`، 3× `CatIcon`، 8× `roleLabel` — ⏳ المرحلة 6 |
| الترجمة العربية↔الإنجليزية للأسماء | خريطة C# واحدة تُصدَّر JSON عند التشغيل | ازدواج `DisplayHelper.cs:56-327` مع `theme.js` (1130 مفتاح) — ⏳ خارج نطاق الخطة الحالية |
| ترقيم النتائج | `Views/Shared/_Pager.cshtml` | 4 نسخ يدوية في الصفحات الحديثة — ⏳ المرحلة 6 |
| رأس الصفحة + الثيم + tailwind.config | `Views/Shared/_Head.cshtml` | 9 نسخ — ⏳ المرحلة 6 |
| عزل المستأجر | `AppDbContext` الفلاتر العامة + `ScopedUsers()` | بقية الفلاتر اليدوية تكرار دفاعي مقبول، لا تُزال قسراً |

**قاعدة قرار:** التوحيد إجباري فقط عندما تكون النسخ **قد انفصلت فعلاً** (نتائج مختلفة).
التكرار الحميد (scaffolding متشابه في controllers) يُترك — التوحيد القسري له تكلفة بلا عائد.

---

## 5. استراتيجية الوصول للبيانات والعقود

- **تُقبل** DbContext داخل Controllers للقراءة والإسقاط (315 نقطة استدعاء — تغييرها الجماعي
  بلا عائد يُخالف «لا إعادة كتابة بلا داعٍ»).
- **تُلزم** الكتابة المتعددة بالمرور عبر خدمة + معامل واحد: `DbTransaction.RunAsync(_db, async work => …)`
  — يبدأ المعاملة ويُلزمها، وعند الاستثناء يُلغيها ثم يُعيد الرمي، وينضم إلى معاملة قائمة إن وُجدت
  (EF يرفض معاملتين على نفس السياق). **القرار الفعلي عن `AuditService.LogAsync`: أُبقيَ حفظه الداخلي**
  فينضم تلقائياً للمعاملة الجارية، بدل تغيير عقد كل مستدعيه — التفصيل في `PLAN.md` §4.1.
- ترشيح نماذج العرض (`projection`) يبقى LINQ-to-DB بلا طبقة واسطة (31 إسقاطاً يعمل، ولا نسخة ضمنية منه).
- إسقاطات القوائم تُبقي `AsNoTracking()`؛ المسارات التي تضيف فلترة في الذاكرة بعد `ToListAsync`
  تُقيد بـ `Take`/`Skip` في القاعدة (P0-2) لأن النشر السحابي على Postgres لا يعانيه قيد SQLite.

---

## 6. طبقة العرض الموحدة (الجزء الذي يوقف الأخطاء المتكررة)

الهدف ليس «Tailwind بدل Bootstrap»، الهدف أن **القلق الواحد يُصلح مرة واحدة**:

1. `_Head.cshtml`: كل CDNs + `tailwind.config` (نفس الـ9 ألوان) + سكربت منع الوميض + أنماط
   التمرير + سمات `lang`/`dir` من حالة اللغة بدل تثبيتها.
2. `_ModernShell.cshtml` (Layout بديل اختياري): غلاف `<html><body>` + `_TopBar` + `_SideNav`
   + مكان `@RenderBody()`، فتصغر الصفحات الحديثة إلى محتوى فقط.
3. `site.css` tokens: `--surface`/`--surface-2`/`--tint`/`--line`/`--text-1..3`، ثم
   `html[data-theme="dark"]` يعرّفها مرة — فيحلّ محل مطاردة 279 قاعدة و98 لوناً مقوفاً.
4. `wwwroot/js/components.js`: toast، modal، تأكيد الحذف، الترقيم، ومسح QR من مصدر واحد
   (بدل 60 بلوك `<script>` موزع على 19 ملفاً).
5. القائمة الجانبية: مصدر واحد للصلاحية — تُشتق من أسماء السياسات نفسها بدل bools يدوية
   (يصلح P1-10: link يظهر لفني ويصادَر له AccessDenied).

**الصفحة الميتة:** `Tickets/IndexModern.cshtml` — القرار في `PLAN.md` (إما ربطه بـ
`TicketsController.cs:51` أو حذفه)، ولا يُترك كما هو لأنه يمتص الإصلاحات.

---

## 7. استراتيجية الاختبار

| الطبقة | النوع | الأداة | النطاق |
|---|---|---|---|
| Domain.Rules | unit خالص | xUnit | الضمان، الأهلية، الإهلاك، حدود القيم — أرقام معروفة مسبقاً |
| Infrastructure.Services | integration على SQLite في الذاكرة | xUnit + EF InMemory/Sqlite | Custody assign/respond/return: انتقال الحالة، منع المزدوج، rollback عند فشل الإشعار |
| Web | characterization للمخارج | xUnit + Mvc.Testing | كود الاستجابة للمسارات الحساسة: 404 للوحدات المطفأة، 403 للدور الخاطئ |
| الحدود المعمارية |_architecture tests | xUnit + reflection على الأنواع | R1–R10 |
| يدوي | استرجاع المسارات الذهبية | متصفح على `localhost:5080` | دخول/لوحة/قائمة بأموال/عهدة/مصدر Excel |

اختبارات **characterization** تُكتب قبل إعادة الهيكلة في كل flow مستهدف، لتثبيت السلوك الحالي
(البند 8 من بروتوكول العمل: لا إعادة هيكلة بلا شبكة أمان).

---

## 8. الترحيل (Incremental، بلا إيقاف العمل)

كل مرحلة مستقلة وقابلة للنشر وحدها، وبلا تغيير في الـ contract العام:

| المرحلة | المحتوى | خطر الانحدار | طريقة التحقق |
|---|---|---|---|
| 0 | `.gitignore` خاص بـ .NET + مشروع اختبارات فارغ + CI (build/test/format/NuGet audit) | منخفض | بناء CI |
| 1 | حزام الأمان: توحيد قواعد P0 (الإهلاك، الضمان، الأهلية، SLA) + اختبارات لها | متوسط | الاختبارات + مطابقة أرقام اللوحة/التقرير يدوياً |
| 2 | حدود الاستعلام: paging في SQL، وإصلاح إجمالي الفلتر | منخفض | قياس قبل/بعد + مطابقة القيم |
| 3 | أمان النشر: بذرة + فرض تغيير كلمة المرور + HTTPS/كوكيز + Rate limiter + Excel sanitize + esc() | متوسط | اختبار يدوي لكل مسار |
| 4 | المعاملات في تدفقات الكتابة + إزالة SaveChanges المكرر من AuditService | **مرتفع نسبياً** | characterization tests قبل/بعد |
| 5 | DataProtection keys (يحتاج migration) — **مرحلة مؤجلة بانتظار موافقة** | مرتفع (مخطط القاعدة) | نشر تجريبي على Render |
| 6 | القاسم المشترك للواجهة: `_Head` + `_Pager` + `StatusPresentation` + tokens + حذف/ربط الميتة | متوسط (مظهر) | فحص بصري لكل قالب + EN/AR + داكن/فاتح |
| 7 | FeatureGate على الفئات الناقصة + توحيد صلاحيات القائمة | منخفض | فحص مسارات مباشرة لكل دور |
| 8 | حواجز معمارية R1–R10 في CI + دليل `ARCHITECTURE.md` | منخفض | تشغيل الاختبارات |

---

## 9. معايير القبول (تطابق المطلوب في Phase 5)

1. ملكية واضحة: كل قاعدة عمل لها ملف واحد يُقرأ منه القرار.
2. حدود مفهومة: R1–R10 مُنفّذة كاختبارات لا كتعليمات.
3. اقتران أقل: القالب الحديث لا ينسخ رأساً، والقائمة لا تعرف الأدوار يدوياً.
4. إعادة استخدام متوقعة: `_Pager`/`_Head`/`StatusPresentation`/`components.js`.
5. مصدر وحيد للقواعد المشتركة: جدول §4.
6. منطق قابل للاختبار: `Domain.Rules` بلا EF ولا HTTP.
7. وصول بيانات متسق: قراءة من الفلاتر العامة، وكتابة بخدمة + transaction.
8. نقاط توسع: وحدات جديدة = سياسة + gate + `_Head`، بلا نسخ صفحة.
9. أخطاء وتحقق متناسقان: `Result<T>` لكل كتابة + toast موحد.
10. مسار ترحيل: المراحل 0–8 أعلاه، كل واحدة مستقلة النشر.

**ما لا نفعله:** لا إعادة تسمية جماعية، لا تقسيم `DisplayHelper`، لا طبقة Repository،
لا مهاجرة لـ Blazor/React، لا إطار ميكروسيرفيس — كلها تغيير بلا عائد يمس «احتفظ بالوظائف».

**ولا نضيف `UseHttpsRedirection`:** النشر على Render يُنهي TLS ويرسل `X-Forwarded-Proto`،
والبرنامج Already يفكّ تلك الترويسات (`ForwardedHeadersOptions` في `Program.cs`) ويضبط
`Cookie.SecurePolicy = Always` خارج Development. إضافة إعادة التوجيه داخل الحاوية تعرض
حلقة إعادة توجيه عند فحص الصحة الذي يأتي بـ HTTP داخلي، بلا مكسب أمني فعلي — المكسب
الأمني موجود أصلاً في الترويسات + الكوكيز الآمنة.
