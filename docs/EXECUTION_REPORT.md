# تقرير تنفيذ B1 / B2 — أمان العزل أولًا ثم سلامة المعاملات (ATS)

> كل رقم هنا نتيجة أمر نُفِّذ فعليًا على هذا الجهاز في هذه الجلسة، والأمر مكتوب بجانب
> نتيجته. ما لم يُنفَّذ مثبَّت بالسبب لا بالمجاملة.

---

## 1. ملخص تنفيذي

- **B2 (الأولوية الأولى) مُغلقة على مستوى الكود والاختبار:** حارس ملكية واحد
  (`TenantGuard.OwnedByCompanyAsync` / `UserInCompanyAsync`) يُطبَّق على كل مسار كتابة
  يقبل معرّف كيان تابع لشركة: الأصول (قسم/موقع/مورّد/تصنيف)، المستخدم (قسم)، التصنيف (أب)،
  الجرد (موقع + مسؤول + موقع فعلي عند المسح)، الصيانة الدورية (فني)، التذكرة (خط إنتاج).
  الفشل موحَّد ولا يسرّب الوجود، ولا كتابة جزئية عند الرفض.
- **B1 (الأولوية الثانية) مُغلقة على مستوى الكود والاختبار:** 30 موضع «حفظ الكيان ثم حفظ
  التدقيق» لُفّ بـ`DbTransaction.RunAsync` (المجموع الآن 42 موضعًا)، وكاتب التدقيق الموازي
  في `AdminController` حُذف وأُدْمج في `IAuditService` عبر `LogTextAsync` بنفس شكل السطر
  القديم (نص خام لا JSON) كي لا تنكسر صفحة سجل التدقيق.
- **الرجوع مُثبت فعليًا لا بالـmocks:** بقياس «انزع التعهّد» — تعديل واحد يجعل
  `RunAsync` ينفّذ العمل بلا معاملة — أعطت الحزمة **`Failed: 15`**، وبعد الإرجاع
  **`Passed: 114 / Failed: 0`**. أي أن اختبارات الرجوع تعتمد على التعهّد نفسه.
- **لم تُنفَّذ أي migration، ولا تغيير إعدادات نشر/إنتاج، ولا `git push`** (التأكيد في §12).
- بقيت ثلاث حقائق غير مُغلقة عمدًا: ذرّية `CreateUser`/`EditUser` الكاملة (قرار شكل أخطاء)،
  تزييف `X-Forwarded-For` (قرار نشر، والقياس الحي مثبت في §3/§10)، وترقيات التبعيات
  (ممنوعة صراحة؛ التقييم الفردي في §7).

---

## 2. التغييرات الفعلية

### B2 — حارس العزل

| الملف | التغيير |
|---|---|
| `Infrastructure/Data/TenantGuard.cs` (جديد) | `OwnedByCompanyAsync<T>(id, companyId)` و`UserInCompanyAsync(userId, companyId)`: تحقق صريح من الملكية، يعمل أيضًا لمدير النظام حيث الفلتر العام مُطفأ، ويرفض المعرّف غير الموجود والمحذوف ناعماً |
| `Controllers/AssetsController.cs` | `ValidateFormAsync` تمرّر كل رابط (قسم/موقع/مورّد/تصنيف) على الحارس — كان التحقق للتصنيف فقط؛ و`Edit` يفحص الموقع الجديد بنفس الحارس |
| `Controllers/AdminController.cs` | `CreateUser`/`EditUser`: القسم يُقاس على شركة المستخدم **بعد الحفظ**؛ `CreateCategory`: الأب من نفس الشركة |
| `Controllers/InventoryAuditsController.cs` | `Create`: الموقع والمسؤول؛ `ScanItem`: الموقع الفعلي |
| `Controllers/MaintenanceSchedulesController.cs` | `Create`: الفني الافتراضي من شركة الأصل |
| `Controllers/TicketsController.cs` | `Create`: خط الإنتاج من شركة الأصل |

### B1 — المعاملات وكاتب التدقيق

| الملف | التغيير |
|---|---|
| `Controllers/AssetsController.cs` | `Create` (إضافة + حفظ + تدقيق)، `Edit` (حفظ + لوغ النقل + تدقيق؛ فحص التزامن بقي خارج التعهّد عمدًا ليُرجع النموذج بلا استثناء)، `Dispose` |
| `Controllers/AdminController.cs` | 13 موضع مراجع (شركة/قسم/موقع/تصنيف/مورّد/SLA إنشاء+تعديل + `ToggleRef`) و4 مواضع هوية (ذيل `CreateUser` الدورى، `ToggleUser`، `UnlockUser`، ذيل `ResetPassword`) |
| `Controllers/InventoryAuditsController.cs` | `Create`، `Edit` (بقية الأفعال كانت ملفوفة سابقًا) |
| `Controllers/MaintenanceSchedulesController.cs` | `Create`، `Edit`، `Toggle` |
| `Controllers/TicketsController.cs` | `Assign`، `ChangeStatus`، `Resolve`، `Close`، `Reopen` — والإشعارات بقيت **بعد** الالتزام عمدًا |
| `Application/Interfaces/IServices.cs` | `IAuditService.LogTextAsync(...)` — نفس السطر وقيوده بقيم نصية جاهزة |
| `Infrastructure/Services/CoreServices.cs` | `AuditService` أُعيد إلى نقطة كتابة واحدة `WriteAsync(AuditLog, ct)`؛ الحفظ الداخلي هو ما يُنضم السطر لتعهّد الطلب الجاري |
| `Controllers/AdminController.cs` (حذف) | كاتب التدقيق المحلي الذي كان يبني `AuditLog` ويحفظه استُبدل بتفويض صريح لـ`LogTextAsync` — لم يبقَ كاتبان |

القاعدة المطبَّقة: **حفظ واحد = ذرّي بطبيعته في EF Core، فلا معاملة صريحة؛ المعاملة لما
≥ حفظين.** لهذا تُركت مواضع بحفظ واحد كما هي بعد مراجعتها واحدًا واحدًا (مثل إضافة تعليق
تذكرة، `AddPart` داخليًا ملفوف لأنه حفظان).

---

## 3. نتائج عزل الشركات — مقيسة

**الحاجز يُحدث فرقًا (قياس بإيقافه في نقطة واحدة ثم إرجاعه):**

| الأمر | النتيجة |
|---|---|
| `dotnet test … --filter "FullyQualifiedName~TenantIsolationTests\|FullyQualifiedName~CrossTenantWriteTests"` مع الحارس | `Failed: 0, Passed: 25` |
| نفس الأمر مع تعطيل `TenantGuard` (حقن قياس مؤقت، حُذف بعد القياس) | `Failed: 16` |

**التغطية (25 اختبارًا على مسار الكتابة الحقيقي في الـController، لا على دالة مساعدة):**

- رفض بلا أي كتابة: `Create_rejects_department_of_another_company_and_writes_nothing`،
  `Create_rejects_location/vendor/category_of_another_company`،
  `Create_rejects_a_link_that_does_not_exist_at_all`، `Create_rejects_a_soft_deleted_link`،
  `Edit_rejects_foreign_location_and_leaves_the_stored_link_untouched`،
  `CreateUser_rejects_department_of_another_company`،
  `EditUser_rejects_foreign_department_and_keeps_the_stored_one`،
  `CreateCategory_rejects_parent_from_another_company`،
  `AuditCreate_rejects_location_of_another_company_and_writes_nothing`،
  `AuditCreate_rejects_responsible_user_of_another_company`،
  `ScanItem_rejects_foreign_actual_location_and_leaves_the_item_pending`،
  `ScheduleCreate_rejects_technician_of_another_company`،
  `TicketCreate_rejects_production_line_of_another_company`.
- ضوابط موجبة بنفس المسارات (كي لا ينجح الرفض بالتخريب):
  `Create_accepts_the_same_company_links_and_persists_them`،
  `CreateUser_accepts_a_department_from_his_own_company`،
  `CreateCategory_accepts_a_parent_from_the_same_company`،
  `AuditCreate_accepts_same_company_location_and_responsible`،
  `ScanItem_accepts_a_location_from_the_audit_company`،
  `ScheduleCreate_accepts_a_technician_from_the_asset_company`.
- سلوك موثّق لا صدفة: `Edit_accepts_a_legacy_link_to_a_deleted_department_when_it_is_unchanged`
  (التعديل لا يُغلق على قسم حُذف ناعماً إن لم يتغيّر)،
  و`Admin_create_validates_links_against_the_company_it_chose` (مدير النظام يُقاس على
  الشركة التي اختارها لأن الفلتر العام لا يعمل عليه).
- قراءة غير مسرّبة للوجود: `Details_of_another_company_asset_is_refused_like_a_missing_one`،
  `ByTag_of_another_company_asset_is_refused_like_a_missing_one`.

**إثبات حيّ على سيرفر حقيقي (Development/SQLite على `127.0.0.1:5299`، ثم أُوقف):**

| الطلب (بكوكي موظف شركة النيل `nile.emp1@ats.eg`) | النتيجة الحرفية |
|---|---|
| `GET /Assets/Details/53` (أصل يتبع شركة الدلتا) | `302 → /` (تحويل بلا بيانات، لا 403) |
| `GET /Assets/Qr/53` | `302 → /` |
| `GET /Assets` | `200` ← راجع R9 في §10 |
| `GET /Assets/Edit/1` | `302 → /Account/AccessDenied?ReturnUrl=%2FAssets%2FEdit%2F1` |
| `GET /Reports`، `/Warehouses`، `/Admin/Users`، `/InventoryAudits` | `302 → /Account/AccessDenied` لكلٍّ |
| `GET /Custody/MyCustody`، `/Tickets`، `/Notifications` | `200` (مصمَّم كذلك) |
| `GET /Assets` بلا دخول | `302 → /Account/Login?ReturnUrl=%2FAssets` |
| `GET /a/AST-2026-00905` (وسم شركة أخرى) | `302 → /Assets/ByTag/…` ثم تحويل إلى الرئيسية — لا تسريب |

الطلبات كلها قراءات (ومحاولة دخول واحدة بكلمة سر بذرة تجريبية معروفة)؛ لم تُكتب صفوف في
قاعدة التطوير من هذا القياس، والسيرفر أُوقف بعد انتهائه.

---

## 4. نتائج الرجوع — مقيسة على SQLite حقيقية

- **قبل اللّف** (اختبارات الأصول على الكود القديم): `Failed: 3, Passed: 4` في
  `SaveAuditTransactionTests`.
- **قياس «انزع التعهّد»:** `RunAsync` عُدّل مؤقتًا ليمرّر `await work(); return;` بلا
  `BeginTransactionAsync`:

| الأمر | النتيجة |
|---|---|
| `dotnet test …` مع القياس | `Failed: 15, Passed: 99, Total: 114` |
| `dotnet test …` بعد إرجاع الملف من نسخة الاحتياط | `Failed: 0, Passed: 114, Total: 114` |
| `grep -n "PROBE\|قياس" DbTransaction.cs` | لا شيء — التلويث أُزيل والملف مطابق لما قبل القياس |

الساقطة هي 13 اختبار رجوع + 2 من `DbTransactionTests`؛ لا اختبار رجوع ينجح «لأن الـmock
رمى فقط».

**الحالات الخمس المطلوبة:**

| الحالة | الاختبار | النتيجة |
|---|---|---|
| أ. نجاح كامل: الكيان وسطر التدقيق معًا | `Create_persists_the_asset_and_its_audit_row`، `ScheduleCreate_saves_the_schedule_and_its_audit_together`، `ChangeStatus_persists_ticket_history_and_audit_together`، `ToggleUser_saves_the_flag_and_its_audit_together` | نجحت — وتُثبت بسطر تدقيق حقيقي من `AuditService` لا من `FakeAuditService` |
| ب. فشل التدقيق ⇒ الكيان غير محفوظ | `Create_rolls_the_asset_back_when_the_audit_write_fails`، `Edit_rolls_back_both_the_update_and_the_location_log_when_the_audit_fails`، `Dispose_rolls_the_status_change_back_when_the_audit_fails`، و`ScheduleCreate/AuditCreate/AuditEdit/DepartmentCreate/ToggleRef/ChangeStatus/ToggleUser/UnlockUser/ResetPassword/CreateUser_rolls_back…` | نجحت، وكلها تثبت أولًا أن محاولة التدقيق وقعت فعلًا (`audit.Actions`) حتى لا ينجح الاختبار بخروج مبكر |
| ج. فشل حفظ الكيان ⇒ لا سطر تدقيق مضلّل | `Create_writes_no_audit_row_when_the_entity_save_fails` (وسم مكرر يصطدم بالفهرس الفريد) | `Assets=1, AuditLogs=0` |
| د. استثناء في منتصف التدفق ⇒ رجوع كامل | `Edit_rolls_back_both_the_update_and_the_location_log_when_the_audit_fails` (التعديل + لوغ النقل + التدقيق رجعت كلها) | نجح |
| هـ. معاملة محيطة/متداخلة | `AuditService_writes_inside_an_ambient_transaction_and_rolls_back_with_it` + 4 اختبارات `DbTransactionTests` | نجحت |

**حدود موثَّقة لا تُخفى:**

- `CreateUser`: الحساب يُلتزم في `_users.CreateAsync` **قبل** التعهّد؛ اللّف يغطي منح الدور
  وسطر التدقيق فقط. الاختبار `CreateUser_rolls_back_the_role_and_audit_when_the_audit_fails`
  يقول ذلك صراحة (`UserRoles=0`, `AuditLogs=0`، والمستخدم موجود).
- `ResetPassword`: كلمة المرور الجديدة تُلتزم قبل التعهّد؛ الاختبار يثبت رجوع
  `MustChangePassword` وسطر التدقيق **وينص** على أن `PasswordHash` تغيّر فعلًا
  (`Assert.NotEqual(originalHash, stored.PasswordHash)`).
- `EditUser` غير ملفوف بالكامل: مسارات فشله (`UpdateAsync`/`ResetPasswordAsync`) ترجع
  `View("UserForm")` في منتصف التدفق، ولّفه يغيّر شكل الأخطاء الظاهر للمستخدم ← قرار §11.
- لا قراءة على SQL Server/Postgres (لا خادم محلي) — §8.

---

## 5. مسارات «حفظ + تدقيق»: المغطى والمتبقي

عدّ مواضع `DbTransaction.RunAsync` بعد التوحيد
(`grep -r "DbTransaction.RunAsync" --include="*.cs" src`):

| الملف | المواضع |
|---|---|
| `AdminController.cs` | 17 |
| `InventoryAuditsController.cs` | 8 |
| `TicketsController.cs` | 7 |
| `MaintenanceSchedulesController.cs` | 4 |
| `AssetsController.cs` | 3 |
| `CustodyService.cs` | 3 |
| **المجموع** | **42** (كان 12 قبل B1، فالزيادة 30 موضعًا) |

وأُعيد البحث بعد التوحيد عن زوج «حفظ ثم تدقيق» خارج تعهّد بمراجعة كل
`SaveChangesAsync`/`LogAsync` في.controllers العشرة: لم يبقَ إلا `AdminController.EditUser`
(مذكور §4). أما `NotificationsController` وإضافة تعليق التذكرة فلا يكتبان سطر تدقيق أصلًا،
فلا شيء ليُذرَّ فيهما.

**كاتب التدقيق:** نقطة بناء سطر `AuditLog` واحدة الآن (`AuditService.WriteAsync`).
إثبات عدم الانحدار الشكلي: `Admin_audit_row_keeps_the_legacy_raw_text_shape` يقارن سطرًا
حقيقيًا من المسار الموحّد — `NewValues` نص خام («قسم مقبول» لا JSON)، و`EntityName` و
`EntityId` و`UserId` و`UserName` و`CompanyId` و`IpAddress = "127.0.0.1"` مطابقة. السبب
البصري موثّق: `Views/Admin/AuditLog.cshtml:82` (المساعد `Pretty`) يمرر ما لا يبدأ بـ`{` كما
هو، فالتحويل إلى JSON كان سيُظهر نصًا مختلفًا على الصفحة.

---

## 6. نتائج `dotnet test` والبناء

| الأمر (حرفيًا) | النتيجة الحرفية |
|---|---|
| `dotnet test tests/AssetTracking.Tests/AssetTracking.Tests.csproj --filter "FullyQualifiedName~SaveAuditRollbackTests"` | `Failed: 0, Passed: 14` |
| `… --filter "FullyQualifiedName~TenantIsolationTests"` | `Passed: 12` |
| `… --filter "FullyQualifiedName~CrossTenantWriteTests"` | `Passed: 13` |
| `… --filter "FullyQualifiedName~SaveAuditTransactionTests"` | `Passed: 7` |
| `… --filter "FullyQualifiedName~DbTransactionTests"` | `Passed: 4` |
| `dotnet test tests/AssetTracking.Tests/AssetTracking.Tests.csproj` (الحزمة كاملة بعد إرجاع القياس) | **`Failed: 0, Passed: 114, Skipped: 0, Total: 114`** |
| `dotnet build -c Release` | `Build succeeded. 2 Warning(s), 0 Error(s)` — والتحذيران `NU1902` لـMailKit (نفس المستند في مشروعين) |

تقدّم الحزمة خلال المرحلة: 93 ← 100 ← 106 ← **114** (كل زيادة من اختبارات أُضيفت وفُعّلت،
لا من إعادة تسمية). تحذير `NU1902` لم يُسكِته أحد: مذكور كما هو في البناء وفي §7.

---

## 7. تقييم التبعيات — بلا أي ترقية أو إزالة أو استبدال

الأمر: `dotnet list AssetTracking.sln package --vulnerable --include-transitive`.

| الحزمة | الإصدار | مباشرة/انتقالية | من يجلبها | الخطورة | المستند |
|---|---|---|---|---|---|
| `MailKit` | 4.13.0 | **مباشرة** (`Infrastructure.csproj`) | — | Moderate (6.5) | GHSA-9j88-vvj5-vhgr |
| `MimeKit` | 4.13.0 | انتقالية | `MailKit` | Moderate (6.9) | GHSA-g7hc-96xr-gvvx |
| `Newtonsoft.Json` | 11.0.1 | انتقالية | `Hangfire.Core 1.8.14` (عبر `Hangfire.MemoryStorage`/`NetCore`/`SqlServer`) | High (7.5) | GHSA-5crp-9r3c-p9vr |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.6 | انتقالية | `SQLitePCLRaw.bundle_e_sqlite3` ← `Microsoft.EntityFrameworkCore.Sqlite 8.0.11` | High (7.2) | GHSA-2m69-gcr7-jv3q |
| `System.Net.Http` / `System.Text.RegularExpressions` | 4.3.0 | انتقالية | `NETStandard.Library` داخل **مشروع الاختبارات فقط** (xUnit/vstest) | High | GHSA-7jgj-8wvc-jh57 / GHSA-cmhx-cq75-c4mj |

**التعرّض الحقيقي (لا تصنيف المستند فقط):**

- **MailKit — حقن استجابة STARTTLS ⇒ تخفيض SASL:** المستهلك الوحيد `SmtpEmailSender.cs`
  (يُظهره `grep MailKit`) في مسار إرسال البريد. يتوقف على وسيط شبكة على مسار SMTP وعلى
  خادم يقدّم SASL عبر STARTTLS؛ الإعدادات تأتي من `SystemSettings` و`Smtp.Host` فارغ في
  البذرة. لم أشغّل بريدًا فعليًا في هذا القياس (ليس من متطلبات المرحلة).
- **MimeKit — حقن أوامر SMTP عبر `\r\n` في_local-part_ العنوان:** يتطلب عنوانًا غير موثوق
  يمر إلى `MailboxAddress`. عناوين هنا من إعدادات المدير ومن بريد المستخدم المحفوظ عبر
  `CreateUser` (فريد نصّه تحت تحكم المستخدم) — فالمسار موجود نظريًا.
- **Newtonsoft.Json — عمق JSON ⇒ OOM/DoS:** يصل عبر `Hangfire.Core` لتسلسل حمولات الوظائف،
  والحمولات تُولَّد من كودنا (`MaintenanceJobs`) لا من إدخال مستخدم، ولوحة `/jobs` محمية
  بـ`HangfireAdminFilter` (وقستها محليًا: `GET /jobs` → `404` لأن مفتاح `ScheduledJobs` مطفأ).
  تخزين Hangfire على التطوير `MemoryStorage` (`Program.cs:120-128`) فلا JSON قرصي محليًا.
  التعرّض محدود، لكن 11.0.1 قديم جدًا والترقيع في 13.0.1.
- **SQLitePCLRaw/libsqlite3 — فساد ذاكرة عند أقنعة استعلام فيها تجميع أكثر من الأعمدة:**
  المسار المتأثر SQLite فقط، وSQLite مُفعّل في **Development** حصرًا (`Program.cs:30-37`)؛
  النشر على Render postgres (`Program.cs:38-45` + `render.yaml`). **لا إصدار مُرقَّع منشور
  للمستند حتى 2.1.11** — فالخيار الفعلي: إبقاء SQLite محليًا فقط (الحالة الحالية) أو ترقية
  الحزمة عند صدور بنية جديدة.
- **حزما مشروع الاختبارات لا تُنشوران** مع التطبيق، لكنهما تُظهران `NU1903` في `dotnet list`
  في كل مشاريع الحل، وهذا ما يجعل عبارة «البناء نظيف» ناقصة (موثّق سابقًا في PLAN 0.3).

**الإصلاحات المتاحة ومخاطرها (لم يُنفَّذ أي منها):**

| الحزمة | الإصلاح | مخاطرة التوافق |
|---|---|---|
| MailKit/MimeKit | ترقية `MailKit` إلى ≥ 4.16.0 في `Infrastructure.csproj` | منخفضة–متوسطة: `SmtpClient` ثابت، لكن يلزم تجربة إرسال فعلي (SMTP/TLS) |
| Newtonsoft.Json | انتقالية: تقييد مباشر ≥ 13.0.1 في `Web`، أو ترقية `Hangfire.*` | متوسطة: Hangfire يلمس التخزين و`CompatibilityLevel` (`Program.cs:123`) وجدولة الوظائف |
| SQLitePCLRaw | لا إصدار مُرقَّع؛ أو ترقية `Microsoft.EntityFrameworkCore.Sqlite` | متوسطة–عالية على التطوير (سلوك `MAX`/decimal المقاس يعتمد على مزود SQLite) |
| حزما الاختبارات | لا إصلاح مباشر (من `xunit`/`vstest` عبر `NETStandard.Library`) | يمكن تجاهلهما لعدم النشر، أو تقييد مباشر |

توصية غير منفذة: تفعيل كشف فقط (`NuGetAuditMode=all`) بدل الترقية — قرار بناء يبقى لك.

---

## 8. ما لم يُختبر ولماذا

- **SQL Server وPostgres:** كل اختبارات الرجوع على SQLite في الذاكرة. لا خادم محلي للإثنين،
  فسلوك الرجوع على مزود الإنتاج **لم يُختبر**. لا أدّعي أنه «مضمون على Postgres»: اللّف
  يستخدم واجهة EF التجريدية نفسها (`BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`)
  والبرهنة محلية.
- **CI على GitHub:** `.github/workflows/ci.yml` موجود ولم يُشغَّل إطلاقًا (لا دفعة ⇒ لا CI).
  نجاح الحزمة هنا ≠ نجاح هناك.
- **رشّ دخول بحسابات مختلفة:** قست الحد (15/5 دقائق) بطلبات على `/Account/Login`؛ لم تُختبر
  حيًّا فعالية قفل Identity لكل حساب (5 محاولات/15 دقيقة).
- **`GenerateNow`:** مفتاح `PreventiveMaintenance` مطفأ محليًا فلم يُجرَّب حيًا.
- **إطفاء مفاتيح `FeatureGate` حيًا:** لم يُجرَّب في هذه المرحلة.
- **`MustChangePassword` لجلسة قائمة:** لم يُختبر (المتبقي من PLAN 3.1).
- **سباق `SequenceService` بين عمليتين:** سباق العملية الواحدة مُختبر؛ العابر للعمليات يعتمد
  على الفهرس الفريد ويُنتج 500 لا تكرار كود — لم يُختبر بنسختين.
- **`EditUser` الذرّي الكامل وذرّية `CreateAsync`:** لا اختبار لهما لأنهما غير منفذين (قرار
  §11)، لا لأنهما نجحا.
- **الواجهة (ترجمة/وضع داكن/موبايل):** لا ملف عرض عُدّل في هذا العمل، ولا تحقق حي في
  المتصفح يدّعيه التقرير.

---

## 9. الملفات المتغيّرة في هذه المرحلة وسبب كل واحد

| الملف | السبب |
|---|---|
| `src/AssetTracking.Infrastructure/Data/TenantGuard.cs` | جديد — نقطة تحقق الملكية الواحدة لكل مسارات الكتابة |
| `src/AssetTracking.Web/Controllers/AssetsController.cs` | حارس العزل في الإنشاء والتعديل + لفّ الحفظ+التدقيق في الأفعال الثلاثة |
| `src/AssetTracking.Web/Controllers/AdminController.cs` | حقن `IAuditService`، حذف كاتب التدقيق الموازي، لفّ 17 موضعًا، حارس القسم/أب التصنيف |
| `src/AssetTracking.Web/Controllers/InventoryAuditsController.cs` | حارس الموقع/المسؤول/الموقع الفعلي + لفّ `Create`/`Edit` |
| `src/AssetTracking.Web/Controllers/MaintenanceSchedulesController.cs` | حارس الفني + لفّ `Create`/`Edit`/`Toggle` |
| `src/AssetTracking.Web/Controllers/TicketsController.cs` | حارس خط الإنتاج + لفّ 5 أفعال إسناد/حالة |
| `src/AssetTracking.Application/Interfaces/IServices.cs` | `LogTextAsync` في العقد (بديل كاتب Admin الخاص) |
| `src/AssetTracking.Infrastructure/Services/CoreServices.cs` | `AuditService` بنقطة كتابة واحدة |
| `tests/AssetTracking.Tests/WebTestKit.cs` | شركة/قسم/موقع ثانٍ للضوابط الموجبة + `InitializeAsync` قابل للتوريث |
| `tests/AssetTracking.Tests/TenantIsolationTests.cs` | توسيع + تنفيذ `LogTextAsync` في `FakeAuditService` |
| `tests/AssetTracking.Tests/CrossTenantWriteTests.cs` | 13 اختبار عزل لبق المسارات (مستخدم/تصنيف/جرد/جدولة/تذكرة) |
| `tests/AssetTracking.Tests/SaveAuditTransactionTests.cs` | جديد — 7 اختبارات الأصول A–E على SQLite بصفقات |
| `tests/AssetTracking.Tests/SaveAuditRollbackTests.cs` | جديد — 14 اختبار رجوع لبق الوحدات + إثبات شكل سطر Admin |
| `docs/architecture/PLAN.md`، `docs/EXECUTION_REPORT.md` | حالة البنود بالقياس لا بالادعاء |

**لم يُغيَّر:** أي ملف عرض، أي `appsettings*`، أي `.csproj`، أي migration.

---

## 10. المخاطر المتبقية (مرتبة)

| # | الخطر | الدليل/الحالة |
|---|---|---|
| R3 | **تزييف `X-Forwarded-For` يلغي تقييد الدخول لكل IP** | `Program.cs:184-191` (`KnownProxies`/`KnownNetworks` فارغان = ثقة بكل الممرّرات) + `:157` (التقسيم على `RemoteIpAddress`). **مقيس حيًا:** 20 محاولة `POST /Account/Login` برأس XFF مختلف لكل محاولة → `400` عشرين مرة ولا `429` أبدًا؛ وبلا XFF من نفس العنوان → `400×15` ثم **`429` من المحاولة 16**. الأثر نفسه يمتد لعمود `IpAddress` في سجل التدقيق و`LastLoginIp` (نفس الآلية؛ لم تُقَس حيًا) |
| R9 | `/Assets` للقراءة بلا سياسة دور | `AssetsController.cs:22` يرث `[Authorize]` فقط. **مقيس حيًا:** موظف (`nile.emp1`) → `GET /Assets` = `200` رغم أن القائمة تخفي الرابط (`!isEmp`)؛ إخفاء الرابط ليس تحقق صلاحيات (وهو ما نصّت عليه القاعدة). قرار §11 |
| R1b | ذرّية كاملة غير محققة في مساري Identity | `CreateUser` (الحساب يُلتزم قبل التعهّد)، `ResetPassword` (الهاش قبل التعهّد)، `EditUser` (غير ملفوف). موثّق في §4 |
| R5 | 4 تحذيرات تبعيات مخفية + 2 في مشروع الاختبارات | §7؛ لا ترقية قبل موافقة |
| R6 | لوحات تجلب عمودَي قيمة لكل الصفوف | لم يمسسه هذا العمل (B3 مؤجل) |
| R12 | CI لم يُشغَّل على GitHub | لا دفعة |
| R13 | `?page=` خارج المدى بلا حصر | مؤجل كقرار سلوك |
| — | DataProtection keys (PLAN 3.6) | يحتاج migration — محجوب بالموافقة |
| — | `MustChangePassword` لجلسة قائمة | متبقٍ من PLAN 3.1 |

---

## 11. قرارات تحتاج موافقتك

1. **R3 (تزييف XFF):** ضبط `KnownProxies`/`KnownNetworks` على شبكات الوكيل الفعلية
   (تحتاج معلومة topology Render)، أو تجريد/استبدال الرأس عند الحافة، أو تقسيم المحدد على
   `HttpContext.TraceIdentifier`/اسم المستخدم بدل IP. أيّ منها يغيّر سلوك النشر.
2. **R9 (`/Assets` للموظف):** سياسة خادم صريحة (منع/تضييق على العهدة الشخصية) أم توثيق أن
   قراءة سجل الشركة حق لكل موظف؟ اليوم السلوك الثاني والكود لا يمنعه.
3. **ذرّية `CreateUser`/`EditUser` الكاملة:** تتطلب إرجاع النموذج بدل الالتزام الجزئي، أي
   إعادة تشكيل لمسار أخطاء `UserManager` — تعديل سلوكي لا ميكانيكي.
4. **ترقيات التبعيات (§7):** MailKit ≥ 4.16.0، وتقييد Newtonsoft ≥ 13.0.1 أو ترقية Hangfire —
   كلها ممنوعة في هذه المرحلة بانتظار موافقة صريحة لكل حزمة.
5. **`NuGetAuditMode=all`:** كشف-only، لكنه تعديل csproj.
6. **DataProtection keys (3.6):** migration على ثلاث حزم ترحيل منشورة.
7. **دفعة تجريبية لـGitHub CI:** لا دفع بلا إذن صريح.

---

## 12. التأكيد الصريح والتسليم

- **لم تُنفَّذ أي Database Migration** (ولا إنشاء/تعديل/حذف ملف ترحيل، ولا
  `database update`)؛ العمل كله كود واختبارات.
- **لم تُنفَّذ أي تغييرات نشر أو إعدادات إنتاج**: `render.yaml` و`appsettings*` لم تُمَسّ.
- **لم يحدث `git push`** ولا PR ولا إطلاق/نشر؛ ولا commit في هذه المرحلة (كل التغييرات في
  شجرة العمل بانتظار أمرك).
- لم تُحدَّث أو تُزل أو تُستبدل أي حزمة NuGet.
- لم يُعتبر نجاح `dotnet build` دليلًا على صحة الأمان أو المعاملات: الأدلة في §3 و§4
  اختبارات وسيرفر حي، والفرق بين «شُغِّل ونجح» و«شُغِّل وفشل» و«لم يُشغَّل» مكتوب صراحة
  في §6 و§8.

**الخطوات التالية بالترتيب:** قرار R3 (رؤوس الوكيل + تقسيم حدّ الدخول) ← قرار R9 (سياسة
`/Assets` للقراءة) ← B5 (ترقيات التبعيات حزمة حزمة مع اختبار إرسال بريد فعلي) ← B3 (تجميعات
التقارير/الرئيسية/المستودعات وتفرّع المزود) ← ذرّية `EditUser`/`CreateUser` الكاملة (قرار §11
رقم 3) ← دفعة CI بعد الموافقة على الدفع.

---
---

# تقرير تنفيذ B4 / B5 — سياسة قراءة الأصول، ذرّية الهوية، مراجعة الوكيل، تقييم التبعيات
**التاريخ: 2026-10-12**

> مفردات الحالة المستخدمة أدناه: **تم التنفيذ والتحقق** · **تم التنفيذ واختباره محليًا فقط** ·
> **تمت المراجعة دون تعديل** · **يحتاج معلومات أو موافقة** · **لم يُختبر**.
>
> هذا التقرير لا يعتبر §1–§12 السابقة دليلًا على نجاح هذه المرحلة: كل عدد مذكور هنا من
> تشغيل هذه الجولة على شجرة العمل الحالية. أقسام §1–§12 أعلاه تبقى سجلًّا تاريخيًا لما جرى
> في B1/B2، وما نُقض منها في هذه الجولة مُبيَّن في موضعه صراحةً.

## A. Proxy / R3 — مراجعة بلا أي تعديل لسلوك الإنتاج

### A.1 الإعداد الحالي (كود مقروء، لم يُمَسّ)

| الموضع | ما يفعله |
|---|---|
| `Program.cs:184-191` | `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = XForwardedFor \| XForwardedProto, KnownNetworks = { }, KnownProxies = { } })` — أول وسيط في المسار بعد `builder.Build()` |
| `Program.cs:156-163` | سياسة `login`: نافذة ثابتة **15 / 5 دقائق**، `partitionKey = http.Connection.RemoteIpAddress?.ToString() ?? "unknown"` |
| `Program.cs:219-222` | الترتيب: `UseRouting` ← `UseRateLimiter` ← `UseAuthentication` ← `UseAuthorization`؛ المحدّد يُقيَّم من بيانات الوصف (`[EnableRateLimiting("login")]` على `AccountController.cs:37`) داخل `UseRateLimiter` قبل model binding و antiforgery |
| `CurrentUser.cs:36` | `IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString()` |
| `CoreServices.cs:189,205` | `AuditLog.IpAddress = _me.IpAddress` — أي أن سجل التدقيق يقرأ الخاصية نفسها |
| `AccountController.cs:68` | `user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString()` |
| `Program.cs:75-89` | كوكي المصادقة: `SecurePolicy = Always` خارج Development، فلا صحة الكوكي لا تعتمد على كشف البروتوكول |

### A.2 تصحيح جوهري: التعليق في الكود لا يطابق ما يفعله الكود

كان §10 (R3) وPLAN 3.2/3.3/B.6 يقولان إن القائمتين فارغتان ⇒ «نثق بكل الممرّرات» ⇒
`RemoteIpAddress` تحت تحكم العميل في كل بيئة. **هذا القول غير صحيح كما هو**، والسبب اثنان:

1. **الحقلان لا يُمسّان أصلًا.** `KnownProxies` و`KnownNetworks` خاصيتان للقراءة فقط
   (`IList<T>` بمُجلب فقط) في `ForwardedHeadersOptions`، فكتابة `KnownNetworks = { }` في مُهيِّئ
   الكائن هي **مُهيِّئ مجموعة يضيف صفرًا من العناصر** — لا عملية مسح. الافتراضيات تبقى كما هي
   (من مصدر الإطار `dotnet/aspnetcore` عند الوسم `v8.0.11`، المسار
   `src/Middleware/HttpOverrides/src/ForwardedHeadersOptions.cs:81,86`):
   `KnownProxies = [IPv6Loopback]` و`KnownNetworks = [127.0.0.0/8]`.
2. **«فارغتان = ثقة مطلقة» قاعدة موجودة فعلًا** لكنها لا تنطبق هنا (`ForwardedHeadersMiddleware.cs:224`):
   `var checkKnownIps = KnownNetworks.Count > 0 || KnownProxies.Count > 0;` والسطر `:234` يُسقِط
   الترويسات فقط عندما `checkKnownIps && !CheckKnownAddress(remoteIp)`. أي: المسح الصريح
   (`Clear()`) هو ما يجعل كل ممرّر موثوقًا، وليس كتابته `{ }`.

**النتيجة الفعلية للإعداد الحالي:** الترويسات تُقبَل منpeer هو loopback فقط، وتُرَدّ من أي peer آخر.

### A.3 القياس الحيّ هذه الجولة (سيرفر تطوير على `0.0.0.0:5099`، لم يُغيَّر فيه شيء)

20 محاولة `POST /Account/Login` (متن_body غير صالح ⇒ `400`)، والمطلوب عدّ `429`:

| الحالة | الـpeer | ترويسة `X-Forwarded-For` | النتيجة المقيسة | ما تُثبته |
|---|---|---|---|---|
| **B** | `192.168.1.50` (غير loopback) | مختلفة لكل محاولة (`203.0.113.1..20`) | `400×15` ثم **`429` من المحاولة 16** | الترويسة **رُفضت**: التقسيم جرى على العنوان الحقيقي |
| **A** | `127.0.0.1` (loopback) | مختلفة لكل محاولة (`198.51.100.1..20`) | **`400×20` ولا `429` أبدًا** | الترويسة **قُبلت**: كل محاولة حصلت على قسيمة خاصة بها = تجاوز الحدّ |
| **A2** (ضابط على نفس المسار) | `127.0.0.1` | **ثابتة** (`203.0.113.99`) | `400×15` ثم **`429` من 16** | المفتاح المستخدَم هو قيمة الترويسة نفسها والعدّاد حقيقي، والحدّ هو 15 فعلًا |

**قراءة الأثر بدقة (بلا مبالغة وبلا تهوين):**
- **على التطوير (والأي طلب يصل عبر loopback):** `RemoteIpAddress` — ومنها قسيمة حدّ الدخول،
  و`AuditLog.IpAddress`، و`LastLoginIp` — **تحت تحكم المُرسِل**. التزوير لا يحتاج إلا إرسال
  ترويسة، والحدّ يصبح بلا enforceability. هذا مقيس (A وA2).
- **على النشر بخلفية TLS عند الحافة:** السلوك يعتمد على عنوان الـpeer المباشر داخل الحاوية،
  وهو ما لا أملك دليلًا محليًا عليه. إن كان غير loopback (وهو الغالب في خدمات حاويات Render)
  فالترويسات **تُرفض**: لا تزوير ممكن، لكن التقسيم يجري على عنوان الوكيل المشترك ⇒ محموعة
  المستخدمين خلف نفس العنوان تتقاسم 15/5 دقائق (اختناق متبادل)، وسجلات الـIP تدلّ على الوكيل
  لا على العميل، و`Request.Scheme` يبقى `http` داخل الحاوية. لا `UseHttpsRedirection` (مُتَعمَّد،
  PLAN 3.2) و`SecurePolicy=Always`، فلم تُرصد كسور دخول بسبب ذلك في المراجعة.
- **العبارة السابقة في هذا الملف («الثقة بكل الممرّرات… قابلة للتجاوز في الإنتاج») تُصحَّح**:
  القابلية للتجاوز مقيسة على loopback، وليست مُثبتة على مسار الإنتاج.

### A.4 تصميم الثقة المقترح (اقتراح — لم يُنفَّذ ولا يُنفَّذ بلا موافقة)

1. **الخيار المفضّل: قائمة ضيقة بدل «ثق بالكل».** `KnownProxies`/`KnownNetworks` تُملأ من
   شبكة الـpeer المباشر فقط (sidecar محلي ⇒ `127.0.0.0/8` و`::1` يبقى كافيًا؛ وكيل خارجي ⇒
   CIDR المستضيف). يبقى `ForwardLimit = 1` (افتراضي الإطار) فلا سلسلة طويلة تُقبل.
2. **إن تعذّر معرفة العنوان:** مسح القائمتين صراحةً (`Clear()`) مع **إسقاط/استبدال** الترويسة
   عند الحافة، أو تقسيم المحدّد على مفتاح لا يتحكم به المهاجم — لكن **ليس** اسم المستخدم
   (يمنح المهاجم قسيمة لكل حساب ويكشف الوجود بالتوقيت) ولا `TraceIdentifier` (فريد لكل طلب ⇒
   يلغي الحدّ كليًا). هذا بند يحتاج قرارًا لا تنفيذًا تلقائيًا.
3. **سجلّ التدقيق:** إن أُثبتت سلسلة الوكيل، فالأصحّ أن تُقرأ `X-Forwarded-For` من خاصية
   معتمدة لا من `RemoteIpAddress` المغطّاة، مع توثيق أي القيمتين في العمود.

### A.5 المعلومات المطلوبة عن طوبولوجيا الاستضافة (بدونها لا يُختار خيار)

- ما العنوان الذي يظهر كـ`Connection.RemoteIpAddress` داخل حاوية Render عند طلب حقيقي؟
  (يتطلب تسجيلًا مؤقتًا لذلك في السجل — لم أفعله لأنه تغيير سلوك تشغيلي.)
- هل تُنهي Render TLS عند الحافة وترسل `X-Forwarded-Proto: https`؟ وهل يوجد sidecar/nginx محلي؟
- هل `ASPNETCORE_FORWARDEDHEADERS_ENABLED` مضبوط؟ (مراجعة `render.yaml`: **غير مضبوط**؛
  ولا يوجد `FORWARDED_HEADERS_*` فيه.)
- هل هناك حدّ معدل على مستوى الحافة يجعل التقسيم الداخلي أقل أهمية؟

### A.6 ما نُفِّذ وما لم يُنفَّذ من الاختبارات هنا

- **نُفِّذ:** القياس الحيّ الثلاثي أعلاه على سيرفر تطوير حقيقي (الأوامر: `dotnet run --urls
  http://0.0.0.0:5099` + 3×20 `curl POST`)، ومراجعة مصدر الإطار عند `v8.0.11`.
- **لم يُنفَّذ:** اختبار وحدة محلي على `ForwardedHeadersMiddleware` نفسه. السبب موثّق:
  النوع في `Microsoft.AspNetCore.HttpOverrides.dll` وهو من الإطار المشترك فقط (لا حزمة NuGet)،
  ومشروع الاختبارات لا يُسند `Microsoft.AspNetCore.App`؛ إضافته `FrameworkReference` هي تعديل
  `.csproj` وهو محظور في هذه المرحلة. **الحالة: يحتاج موافقة** (تعديل csproj لمشروع الاختبار).
  القياس الحيّ أعلاه يغطي نفس السلوك من طرف الخادم، لكن بشكل أضعف ديمومة من اختبار وحدة.
- **لم يُختبر:** أي شيء على Render فعليًا أو على SQL Server/Postgres (لا خادم، ولا دفعة).
- **تأكيد: لم تُغيَّر أي إعدادات إنتاج** — `Program.cs` في نطاق الوكيل/المحدّد لم يُمَسّ،
  و`render.yaml` و`appsettings*.json` قُرئتا فقط ولم تُكتَبا.

**حالة البند: تمت المراجعة دون تعديل + يحتاج معلومات أو موافقة** (مع تصحيح ادعاء سابق موثّق).

## B. Asset Authorization / R9 — سياسة قراءة الأصول

### B.1 السياسة المطبَّقة (كما قُرِّرت: قراءة أصول شركة الموظف، لا عهدته الشخصية)

| الطبقة | القاعدة | الموضع |
|---|---|---|
| الفلتر العام | كيانات `ICompanyOwned` (ومنها `Asset`) تُفلتر بـ`IsAdmin || CompanyId == CurrentCompanyId` مع `!IsDeleted` | `AppDbContext.OnModelCreating` |
| المستخدمون | **لا فلتر شركة عليهم** (إرث `IdentityUser` بلا `ICompanyOwned`) ⇒ قاعدة صريحة واحدة `ReadableBy(me)` تُطبَّق على كل قراءة أسماء المستخدمين | `TenantGuard.cs` (جديد هذه المرحلة) |
| الرفض | `NotFoundOrForbidden()` ⇒ `404`/توجيه بلا تسريب وجود السجل (`BaseController.cs:22-28`) | بلا تغيير |
| الكتابة | `[Authorize(Policy = Policies.ManagerOrAdmin)]` على `Create`/`Edit`/`Dispose`/`Labels` | بلا تغيير |

**لم يُتخذ أي قرار جديد في شكل الاستجابة:** `Details`/`ByTag`/`Qr` لأصل شركة أخرى تُرجع نفس
ما كانت ترجعه (`302 → /` للـHTML)، لأن القياس أظهر أن السياسة المطلوبة محقَّقة أصلًا بالشكل
الحالي. التغيير الوحيد في السلوك هو **ما تُظهره الصفحات المسموح بها**: أسماء الحائزين من خارج
شركة الطالب صارت تُحجب.

### B.2 الأفعال المفحوصة والمعدَّلة

| المسار | الحالة | التفصيل |
|---|---|---|
| `Assets/Index` (بحث/فلاتر/ترقيم/عدّاد) | **عُدِّل** | خريطة أسماء الحائزين صارت `.ReadableBy(Me)` (`AssetsController.cs:~127`)؛ `TotalCount` يُحسب من نفس الاستعلام المحجوب، فلو لم يُحجب لعدّ أصول شركة أخرى (الضابط أدناه) |
| `Assets/Details` | **عُدِّل** | استخراج اسم الحائز عبر `.ReadableBy(Me)` (`:~251`) |
| `Assets/ByTag/{tag}` | **مُراجَع** | يعيد التوجيه لـ`Details` — يرث حماية الفلتر؛ اختبارات مستقلة أُضيفت له |
| `Assets/Qr/{id}` | **مُراجَع** | يُرجع PNG؛ الفلتر العام يرفض المعرّف الغريب قبل توليده |
| `ResolveCustodyNamesAsync` (تاريخ العهدة) | **عُدِّل** | `.ReadableBy(Me)` (`:~322`) — أسماء `AssignedBy`/`Previous` من `CustodyLog` |
| روابط الأقسام/المواقع/الموردين/التصنيفات | **مُراجَعة** | كلها `ICompanyOwned` ⇒ الفلتر العام يكفي؛ لم تُضف حراسة زائدة (لا «تجريد بلا داعٍ») |
| القائمة الجانبية `/Assets` | **مُراجَعة + تعليق موثّق** | الإخفاء للموظف قرار واجهة فقط؛ التنفيذ على الخادم (`_SideNav.cshtml:23`) |

### B.3 الاختبارات (13 اختبارًا في `AssetReadPolicyTests.cs` على أفعال كونترولرات حقيقية)

تغطية الأدوار × الشركات: موظف (شركة ألف) · مدير شركة ألف · مدير النظام؛ ومسارات: قائمة، بحث
بنص، فلاتر حالة، `Details` لنفس الشركة وللشركة الأخرى، `ByTag` لنفس/للأخرى، `Qr` لنفس/للأخرى،
اسم الحائز في التفاصيل وفي سجل الحركة، ظهور أسماء الأقسام/المواقع، وعدّاد `TotalCount`.
ضابطان مهمّان ضد الاختبارات الفارغة:
- **عدّاد بلا تسريب:** 3 أصول `InStore` في القاعدة (منها أصل شركة باء مزروع بـ`IgnoreQueryFilters`)
  ⇒ `TotalCount = 2` للموظف. لو حُجب العرض فقط وبقي العدّ لكان الفارق 3.
- **إثبات أن `ReadableBy` حاملة:** تعطيلها في نقطة واحدة ⇒ `Failed: 3, Passed: 124` من 127،
  والإرجاع ⇒ `Passed: 127` (الملف أُعيد حرفيًا: `RESTORED-IDENTICAL`).
- **حاجز وصفي:** اختبار انعكاس يثبت أن أفعال القراءة بلا سياسة دور (وراثة `[Authorize]` من
  `BaseController`) وأن أفعال الكتابة تحت `ManagerOrAdmin` — بلا ربط القائمة بالسياسة يدويًا.

**البرهنة الحيّة (سيرفر تطوير، كوكي حقيقية لكل دور):** موظف ألف على `/Assets` = `200` وتظهر
وسوم شركته فقط (صفر تقاطع مع وسوم/معرّفات شركة باء)؛ `/Assets/Details/<غريب>` و
`/Assets/ByTag/<غريب>` و`/Assets/Qr/<غريب>` = `302 → /` بلا جسم يحتوي أي سطر من الأصل؛
`/Assets/Edit/<غريب>` = AccessDenied؛ مجهول الهوية = صفحة الدخول.

**حالة البند: تم التنفيذ والتحقق (اختباريًا على SQLite + حيًا على HTTP محليًا)** — لم يُجرَّب
على SQL Server/Postgres.

### B.4 ما بقي مكتشفًا ولم يُنفَّذ (قرار دفعة لاحقة)

قاعدة «المستخدمون بلا فلتر شركة» مطبَّقة الآن في `AssetsController` (3 مواضع) و
`AdminController.ScopedUsers()` (كانت نسخة مكررة يدوية — وُحِّدت). **المسح أحصى 26 موضع قراءة
`Users` بلا حصر خارج مسار الأصول:** `Custody` 5، `Audits` 4، `Schedules` 4، `Reports` 6،
`Tickets` 7. لم أغيِّرها في هذه الدفعة التزامًا بـ«دفعة واحدة لكل عملية» وبأن بعضها يعرض
أسماء في صفحات إدارة يحكمها `ManagerOrAdmin` أصلًا؛ لكن نفس التسريب نظريًا متاح لموظف يبلغ
مسار تذاكر/عهود. **الحالة: يحتاج قرار دفعة** (مغطى بالسياسة نفسها فور تمريره على `ReadableBy`).

## C. Identity Transactions / R1b — ثلاث دفعات متتالية

### C.1 حدود المعاملة المقاسة (هي الأساس الذي بُني عليه القرار)

- `UserManager`/`UserStore` في هذا المشروع يعملان فوق **نفس `AppDbContext` المنتمي للطلب**
  (تسجيل `AddIdentityCore` + `AddEntityFrameworkStores<AppDbContext>`)، فأي `SaveChanges` من
  Identity داخل تعهّد مفتوح **يُلتزم معه لا قبله**. هذا نُقض في التقرير السابق (§4: «`CreateAsync`
  يلتزم قبل التعهّد») وأُصلح بإثبات معكوس: نقل الكتابة إلى داخل التعهّد جعل اختبار الرجوع
  يفشل **قبل** الإصلاح (`Expected: 0 Actual: 1`) وينجح **بعده**.
- **فشل التحقق في Identity نتيجة لا استثناء** (`PasswordTooShort`، `DuplicateEmail`) ⇒ لا يرجع
  استثناء بل `IdentityResult.Failed` قبل أي كتابة للمخزن. اللف باستثناء صناعي كان يحوّل رفض
  مدخلات صحيح إلى صفحة خطأ ويحرم المستخدم من النموذج ببياناته — لذلك أُضيفت قدرة جديدة:
  **`DbTransaction.RunIfAsync(db, Func<Task<bool>>)`**: `false` ⇒ تراجع وتُرجع `false` بلا
  استثناء؛ استثناء ⇒ تراجع ثم يُعاد الرمي كما في `RunAsync`؛ `true` ⇒ التزام. (اختبار الطعم
  بإلغاء شرط التعهّد: `Failed: 6, Passed: 130` من 136، كلُّها في المسارات الثلاثة.)
- **`AddToRoleAsync` لدور غير موجود يرمي** `InvalidOperationException: Role X does not exist.`
  من `UserStore` (مقيس من الاستثناء نفسه) ⇒ لا `IdentityResult` يُفحص؛ الاستثناء يخرج بعد
  التراجع، فلا يبقى حساب بلا دور. لم تُضف قاعدة «الدور موجود» على `ModelState` لأن ذلك يغيّر
  شكل استجابة موجود (500 → نموذج بأخطاء) وهو خارج «إزالة الحالة الجزئية»؛ مُسجَّل كانحراف
  موثّق وقرار متاح.

### C.2 حالة كل مسار

| المسار | الكتابة داخل التعهّد | ما يثبت الرجوع | حالة |
|---|---|---|---|
| `CreateUser` | `CreateAsync` + `AddToRoleAsync` + سطر التدقيق | 4 اختبارات: نجاح (حساب+دور+سطر تدقيق حقيقي) · فشل التدقيق (0 حساب، 0 دور، 0 سطر) · دور غير موجود يرمي (0 حساب، 0 دور) · كلمة سر مرفوضة (نموذج + رسالة Identity + 0 كتابة + البيانات المدخلة محفوظة) | **تم التنفيذ والتحقق محليًا** |
| `ResetPassword` | `ResetPasswordAsync` + راية `MustChangePassword` + التدقيق | 3 اختبارات: نجاح (الـhash تغيّر + الراية + سطر تدقيق) · فشل التدقيق يرجع **الـhash نفسه** (`Assert.Equal(originalHash, …)`) · سياسة مرفوضة (رسالة Identity في `ToastError` + لا تغيير في القاعدة) | **تم التنفيذ والتحقق محليًا** |
| `EditUser` | `UpdateAsync` + حذف/إضافة الدور + تغيير كلمة المرور الاختياري + التدقيق | 4 اختبارات: نجاح · فشل التدقيق (الاسم والدور يرجعان) · منح دور غير موجود (حذف الدور السابق **يتراجع** — لم يعد يُترك بلا دور) · كلمة سر مرفوضة (ملف/دور/hash/تدقيق كلها ترجع، والنموذج برسائل Identity) | **تم التنفيذ والتحقق محليًا** |

### C.3 أثر التغيير على أخطاء التحقق وسلوك الواجهة

- عقود محفوظة كما كانت: `View("UserForm", vm)` عند الفشل، `IdentityResult.Errors` تمرّ عبر
  `Translate(IdentityError)` نفسه، `FillUserFormAsync` يعيد القوائم، البيانات المدخلة ترجع في
  `vm`، والمسار الناجح `RedirectToAction(nameof(Users))` + نفس نصوص الـToast.
- اختبار `EditUser_rejects_foreign_department_and_keeps_the_stored_one` (فصل B2) استمر ناجحًا
  بلا تعديل — أي أن حراسة العزل السابقة غير متأثرة.
- لا نجاح يُسجَّل كاذبًا: سطر التدقيق داخل نفس التعهّد، فلا «أُنشئ/عُدِّل» في السجل عن كتابة
  رُجِعَت.

### C.4 ما زال غير ذرّي (بدون تلميع)

1. **`AccountController.ChangePassword` (`:139` ثم `:147`)**: تغيير كلمة المرور ثم تصفير راية
   `MustChangePassword` writes منفصلتان بلا تعهّد — نفس صنف العيب، خارج نطاق «CreateUser ثم
   ResetPassword ثم EditUser» في هذه المرحلة. **الحالة: يحتاج قرار دفعة.**
2. **`AccountController.cs:69`** (`LastLoginIp` عند الدخول): كتابة واحدة ⇒ ذرّية بطبيعتها وفق
   القاعدة الموثقة في `ARCHITECTURE.md`، ولم تُلَفّ عمدًا.
3. **كيان مُتتبَّع بعد الرجوع:** بعد `RollbackAsync` تبقى قيم الكيان فيChange Tracker لو
  Context الطلب. تحققت أن لا `SaveChangesAsync` آخر في نفس الطلب قد يُعيد كتابة المرجوع:
   كل المواضع الـ61 الـ`SaveChangesAsync` في الحل داخل أفعال/خدمات صريحة لا وسيط نهاية-طلب،
   ومسارات الفشل هنا تنتهي بـ`View`/`Redirect` بعد `FillUserFormAsync` (قراءات فقط).
   **لم يُختبر** بسيناريو يطلب حفظًا بعد الرجوع — مُسجَّل كحدّ لا كأمان مثبت.
4. الذرّية مثبتة على **SQLite في الذاكرة**. سلوك SqlServer/Npgsql يستخدم نفس واجهة EF
   (`BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`) لكن **لم يُختبر عليهما** ولا على CI.

## D. Dependency Audit / R5 — تقييم محدَّث بلا أي ترقية

### D.1 الفحص (أُعيد تشغيله هذه الجولة)

`dotnet list AssetTracking.sln package --vulnerable --include-transitive` — المصدر الوحيد
المُعلَن في المخرجات: `https://api.nuget.org/v3/index.json`

| الحزمة | المثبّت | مباشرة/انتقالية | من يجلبها (بـ`dotnet nuget why`) | المستند (مُحقَّق من `api.github.com/advisories/<id>`) | الخطورة/CVSS | المُرقَّع | يُنشَر مع التطبيق؟ |
|---|---|---|---|---|---|---|---|
| `MailKit` | 4.13.0 | **مباشرة** (`Infrastructure.csproj:21`) | — | GHSA-9j88-vvj5-vhgr · CVE-2026-41319 · منشور 2026-04-18 | Moderate · 6.5 | `< 4.16.0` ⇒ 4.16.0 (الأحدث 4.18.1) | نعم |
| `MimeKit` | 4.13.0 | انتقالية | `MailKit 4.13.0` | GHSA-g7hc-96xr-gvvx · CVE-2026-30227 · 2026-03-05 | Moderate (بدون CVSS في المستند) | `<= 4.15.0` ⇒ 4.15.1 | نعم |
| `Newtonsoft.Json` | 11.0.1 | انتقالية | `Hangfire.AspNetCore/MemoryStorage/SqlServer 1.8.14` → `Hangfire.Core 1.8.14` (nuspec يطلب `Newtonsoft.Json >= 11.0.1`) | GHSA-5crp-9r3c-p9vr · CVE-2024-21907 | High · 7.5 | `< 13.0.1` ⇒ 13.0.1 (الأحدث المستقرة 13.0.4) | نعم |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.6 | انتقالية | `Microsoft.EntityFrameworkCore.Sqlite 8.0.11` → `SQLitePCLRaw.bundle_e_sqlite3 2.1.6` | GHSA-2m69-gcr7-jv3q · CVE-2025-6965 (SQLite **قبل 3.50.2**: تعداد تجميعات أكثر من الأعمدة ⇒ فساد ذاكرة) | High · **9.8** | المستند يقول `<= 2.1.11` ولا يذكر `first_patched`؛ NuGet فيه 2.1.13 وسلسلة 3.x | نعم (الغلاف)، والمكتبة الأصلية تُستخدم فقط عند مزود SQLite |
| `System.Net.Http` | 4.3.0 | انتقالية | `xunit 2.5.3` → `xunit.assert/core` → `NETStandard.Library 1.6.1` | GHSA-7jgj-8wvc-jh57 · CVE-2018-8292 | High · 7.5 | `< 4.3.4` ⇒ 4.3.4 | **لا** (مشروع الاختبارات فقط) |
| `System.Text.RegularExpressions` | 4.3.0 | انتقالية | نفس السلسلة عبر `NETStandard.Library` (`System.Xml.ReaderWriter`/`XDocument`) | GHSA-cmhx-cq75-c4mj · CVE-2019-0820 (ReDoS) | High · 7.5 | `>=4.3.0,<4.3.1` ⇒ 4.3.1 | **لا** |

### D.2 التعرّض المقاس (حقائق مفصولة عن الاستنتاج)

- **MailKit/MimeKit:** المستهلك الوحيد في الحل `Infrastructure/Email/SmtpEmailSender.cs`
  (`new SmtpClient()` عند `:50`، `SendAsync` عند `:57`). **حقيقة مقاسة بالبحث:** `IEmailSender`
  مُسجَّل (`Program.cs:108`) ومُعرَّف (`IServices.cs:37`) ومُنفَّذ، ولا **مستهلك** واحد له في
  المشروع كله (بحث عن `EmailSender`/`IEmailSender` لا يُرجع غير تلك الثلاثة). ⇒ API المُنذَر
  لا يُستدعى من أي طلب أو مهمة اليوم. **استنتاج مُعلَن:** ثغرة STARTTLS/SASL تتطلب وسيطًا على
  مسار SMTP وخادمًا يقدّم SASL بعد `STARTTLS`؛ لن تحدث من مدخل مستخدم. تبقى حزمة منشورة
  يُنشِّطها أول استدعاء مستقبلي.
- **Newtonsoft.Json:** يُستخدم داخليًا من Hangfire لتسلسل حمولات الوظائف. **حقيقة:** لا
  `FromSqlRaw`/`ExecuteSqlRaw` في الحل (بحث صفر النتائج) ⇒ لا تمرير JSON من المستخدم للمخزن؛
  الحولات يولّدها كودنا (`MaintenanceJobs`)؛ لوحة `/jobs` غير مرسومة لأن `ScheduledJobs=false`
  (`appsettings.json:7`، `Program.cs:230`)؛ تخزين Hangfire على SQLite/Postgres هو
  `MemoryStorage` (`Program.cs:120-128` ⇒ `SqlServer` فقط). **استنتاج مُعلَن:** البلوغ يتطلب
  كتابة في مخزن Hangfire (أو مصدرًا غير موثوق للحمول عند تفعيل SQL storage)؛ التقييم: تعرض
  محدود/غير مباشر، لا «غير قابلة للاستغلال».
- **SQLitePCLRaw:** **مقيس محليًا** (اختبار `Measured_sqlite_native_version…`):
  `select sqlite_version()` ⇒ **3.41.2**، أي أقل من 3.50.2 الذي يطلبه المستند ⇒ الكود الأصلي
  المعني موجود فعلًا في العملية حين يعمل المزود SQLite. **حقيقة القيد:** SQLite يُختار فقط عندما
  `DatabaseProvider=Sqlite` (`Program.cs:30-37`)، و`render.yaml` يضبط `Postgres` و`Production`
  ⇒ المسار محلي/تطويري. **حقيقة ثانية:** لا SQL خام في الحل، فبناء استعلام يستغل تعداد
  التجميعات لا يكون من إدخال مستخدم؛ الاستغلال يفترض ملف قاعدة/استعلام هجومي ⇒ تعرض محدود.
- **حزمتا مشروع الاختبار:** **مقيس** (اختبار `Regex_and_HttpClient_come_from_the_shared_framework…`):
  `typeof(Regex).Assembly` = `System.Text.RegularExpressions v8.0.0.0` من
  `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.31\…`، ومثله `HttpClient`، ولا يوجد
  `System.Text.RegularExpressions.dll`/`System.Net.Http.dll` في مجلد المخرج ⇒ الكود المُنفَّذ هو
  تطبيق إطار العمل لا الكود المُنذَر من 2018/2019. **استنتاج مُعلَن:** التحذير صحيح على مستوى
  الشجرة وغير صحيح على مستوى التنفيذ؛ يبقى تلويث `dotnet list`/`NU1903` وإيجابيته الكاذبة في
  تقارير الآلة.

### D.3 جدول التوصيات مرتَّبًا بالأولوية (لا شيء نُفِّذ)

**أولًا — تستحق إصلاحًا عاجلًا:** لا بند. لا توجد حزمة مُنذَرة تُستهلك من مسار يصل إليه مستخدم
بهذه الشجرة اليوم (الأسباب مقاسة في D.2). هذا حكم على الحالة الراهنة، ليس ضمانة دائمة.

**ثانيًا — تعرض محدود أو غير مباشر (ترقية مجدولة بموافقة، حزمة حزمة):**
| # | الحزمة | التوصية | مخاطرة توافق | اختبار مطلوب قبل القبول |
|---|---|---|---|---|
| 1 | `MailKit` 4.13.0 ⇒ **≥ 4.16.0** | ترقية الحزمة المباشرة الوحيدة؛ تجلب `MimeKit ≥ 4.15.1` تلقائيًا فتحصل بندَي Moderate معًا | متوسطة: `SmtpClient` ثابت API، لكن 4.14+ تغيّر سلوك بعض معالجات TLS/المصادقة | إرسال فعلي على SMTP مع STARTTLS (لم يُجرَّ أي إرسال في هذه المرحلة) |
| 2 | `Newtonsoft.Json` ⇒ **تقييد مباشر ≥ 13.0.1** في `Web` بدل ترقية Hangfire | يرفع CVE-2024-21907 بلا لمس طبقة الجدولة | متوسطة-low: Hangfire يقرأ/يكتب حمولاته بـ Newtonsoft؛ 13.x متوافق خلفيًّا، مع ملاحظة `DefaultSettings/Serialization` في `Program.cs:123` | إنشاء/تنفيذ مهمة Hangfire محلية والتحقق من تفكيك الحمولة قبل/بعد |
| 3 | `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 (CVSS 9.8 نظري) | **لا ترقية الآن**: المستند لا يذكر إصدارًا مُرقَّعًا؛ الخياران: إبقاء SQLite تطويرًا فقط (الحال الآن)، أو ترقية `Microsoft.EntityFrameworkCore.Sqlite` عند صدور bundle يحمل SQLite ≥ 3.50.2 | عالية نسبيا على التطوير: سلوك decimal/`MAX` و`DateTime` يعتمد على بناءة SQLite | مقارنة توليد `AssetTag`/تقارير الإهلاك قبل/بعد على SQLite + Postgres |

**ثالثًا — أدوات الاختبار فقط:** `System.Net.Http` و`System.Text.RegularExpressions` 4.3.0 من
`xunit 2.5.3 → NETStandard.Library 1.6.1`. التوصية: ترقية `xunit` إلى 2.9.x (تسقط سلسلة
`NETStandard.Library`) بدل تقييد الحزمتين مباشرة؛ أو قبولهما مع التوثيق لأنهما لا تُنشَران ولا
تُحمَّلان (مثبت في D.2). **لا ترقية وقعت.**

**رابعًا — يحتاج معلومات إضافية:** (1) هل يُفعَّل البريد فعلًا في أي نشر (لا مستهلك
`IEmailSender` — إن كان قرار المنتج عدم إرسال بريد، فحذف `MailKit` هو الإصلاح الجذري، وهذا
**قرار منتج** لا أمني). (2) أي نسخة SQLite تُبنى مع `Microsoft.EntityFrameworkCore.Sqlite`
اللاحقة عند صدور 8.0.12+ (يتحقق من `bundle_e_sqlite3` لا من رقم الحزمة). (3) هل CI على GitHub
يُظهر `NU1903` في السجل (لم يُشغَّل أي مسار GitHub — لا دفعة).

**تأكيد:** لم تُرقَّ أو تُزل أو تُستبدل أي حزمة، ولم تُعدَّل أي `.csproj` أو lock file، ولم
يُغيَّر أي رقم إصدار. التحذيرين في البناء (`NU1902` MailKit) بقيا ظاهرين كما هما — لم يُخمَد
أي تحذير أمني، ونجاح البناء لا يُعدّ دليل خلوّ.

## E. Verification — التشغيل الفعلي هذه الجولة

| الخطوة | الأمر | النتيجة الحرفية |
|---|---|---|
| خط الأساس قبل التعديل | `dotnet test` | `Passed: 114/114` |
| بعد سياسة قراءة الأصول | `dotnet test` | `Passed: 127/127` |
| بعد دفعة `CreateUser` | `dotnet test --filter …CreateUser` | `Failed: 0, Passed: 6` (4 جديدة + 2 سابقة) |
| بعد دفعة `ResetPassword` | `dotnet test --filter …ResetPassword` | `Failed: 0, Passed: 3` |
| بعد دفعة `EditUser` | `dotnet test --filter …EditUser` | `Failed: 0, Passed: 5` |
| **الحزمة الكاملة بعد كل التعديلات** | `dotnet test tests/AssetTracking.Tests/AssetTracking.Tests.csproj` | **`Passed! - Failed: 0, Passed: 139, Skipped: 0, Total: 139`** |
| بناء النشر | `dotnet build AssetTracking.sln -c Release` | **`Build succeeded. 2 Warning(s) 0 Error(s)`** — التحذيران هما `NU1902` على `MailKit 4.13.0` (GHSA-9j88-vvj5-vhgr)، لم يُخمَدا |
| طُعم 1: تعطيل `TenantGuard.ReadableBy` | اختبار مؤقت + إرجاع | `Failed: 3, Passed: 124` ثم `Passed: 127` والإرجاع `RESTORED-IDENTICAL` |
| طُعم 2: إلغاء شرط التعهّد في `RunIfAsync` | اختبار مؤقت + إرجاع | `Failed: 6, Passed: 130, Total: 136` (كلها في Identity) ثم `Passed: 136` |
| **تأكيد أخير بعد تحديث الوثائق** | `dotnet test AssetTracking.sln -v minimal` | **`Passed! - Failed: 0, Passed: 139, Skipped: 0, Total: 139, Duration: 6 s`** (الحالة الحالية للشجرة، لا نسخة سابقة) |
| مسح الحواجز | `grep -c [Fact] ArchitectureGuardTests.cs` | 10 حواجز، كلها في الحزمة الناجحة |
| `.csproj` الممسوحة من الشجرة | `git status --porcelain \| grep csproj` | ملف واحد فقط `Application.csproj` — حزم ميتة حُذفت في دفعة 0.4 السابقة، **ولا تعديل csproj في هذه المرحلة** |

### الملفات المتغيرة في هذه المرحلة (وليس في B1/B2)

| الملف | التغيير |
|---|---|
| `Infrastructure/Data/TenantGuard.cs` | + `ReadableBy(this IQueryable<ApplicationUser>, ICurrentUser)` — قاعدة «المستخدمون بلا فلتر شركة» في موضع واحد |
| `Infrastructure/Data/DbTransaction.cs` | + `RunIfAsync` (تعهد مشروط برفض نتيجة لا استثناء)، مع توثيق حدود الانضمام لمعاملة قائمة |
| `Web/Controllers/AssetsController.cs` | 3 قراءات أسماء مستخدمين صارت `.ReadableBy(Me)` (`Index`، `Details`، `ResolveCustodyNamesAsync`) |
| `Web/Controllers/AdminController.cs` | `ScopedUsers()` وُحِّدت على `ReadableBy`؛ `CreateUser` و`ResetPassword` و`EditUser` نُقلت كتاباتها إلى داخل `RunIfAsync` مع الحفاظ على رسائل Identity وعقد النموذج |
| `Web/Views/Shared/_SideNav.cshtml` | تعليق فقط: الإخفاء قرار واجهة، التنفيذ على الخادم |
| `tests/…/AssetReadPolicyTests.cs` | جديد — 13 اختبار سياسة قراءة |
| `tests/…/SaveAuditRollbackTests.cs` | Identity: 4 `CreateUser` + 3 `ResetPassword` + 4 `EditUser`، وبذرة دور/كلمة مرور (`SeedRoleAsync`, `RolesOfAsync`, `EditVm`) |
| `tests/…/InboxImplementationTests.cs` | جديد — 3 قياسات تنفيذية للتبعيات (إطار مشترك 8.0.0.0، لا DLL في المخرج، `sqlite_version = 3.41.2`) |
| `docs/architecture/PLAN.md`، `docs/EXECUTION_REPORT.md` | هذا التقرير وتحديث البنود 0.3، 3.2، 3.3، 4.1، B.5، B.6، B.7، +B.8، وقائمة المؤجلات |

### ما لم يُنفَّذ من التحقق ولماذا
- **SQL Server / Postgres:** لا خادم محلي؛ كل الرجوع على SQLite في الذاكرة. **لم يُختبر** على مزود الإنتاج.
- **GitHub CI:** لم تُدفَع أي نسخة ⇒ `ci.yml` لم يُشغَّل على GitHub. **لم يُختبر**.
- **Render:** لم يُمَسّ أي إعداد نشر ولا طُلب نشر. **لم يُختبر**.
- **إرسال بريد فعلي:** لا مستهلك لـ`IEmailSender` في الحل ⇒ لم يُجرَّب.
- **اختبار وحدة على `ForwardedHeadersMiddleware`:** محجوب بتعديل `.csproj` (مفصّل في A.6)؛
  استُبدل بقياس حيّ على السيرفر.

### المخاطر والقرارات المفتوحة
1. قرار R3 (سلسلة ثقة الوكيل + مفتاح تقسيم الحدّ) — يحتاج معلومة طوبولوجيا Render.
2. دفعة `AccountController.ChangePassword` (نفس صنف ذرّية C.4).
3. تمرير `ReadableBy` على 26 قراءة `Users` خارج مسار الأصول (B.4).
4. ترقيات التبعيات حزمة حزمة (D.3) — كل واحدة تحتاج موافقة منفصلة.
5. `NuGetAuditMode=all`/حذف `MailKit`/`DataProtection`/`?page=` clamp/تجميعات B3/N+1 — كلها
   باقية مؤجلة كما هي موثَّقة في PLAN.

## F. حماية إصلاحات B0/B1/B2 — كيف تحققت أنها باقية

لم أراجع التقرير السابق، بل قست الحالة:

| الإصلاح | القيد | مقيس هذه الجولة |
|---|---|---|
| `TenantGuard.OwnedByCompanyAsync`/`UserInCompanyAsync` | لم تُحذف ولم تُخفَّف؛ أُضيفت إليها `ReadableBy` | 10 + 5 مواضع استدعاء (`grep -rn`)، و`CrossTenantWriteTests` (13) و`TenantIsolationTests` نجحت كلها داخل 139/139 |
| توحيد كاتب التدقيق + `LogTextAsync` وشكل السطر النصي | لا تغيير في `CoreServices`/`AuditService` | 4 مواضع `LogTextAsync`؛ اختبار parity سطر الإدارة ما زال ناجحًا |
| `DbTransaction.RunAsync` في مسارات الحفظ+التدقيق | لم يُمسّ بدنه | **40 موضع استدعاء** (كان 42؛ الفارق أن الثالث `CreateUser`/`ResetPassword`/`EditUser` انتقلت من `RunAsync` إلى `RunIfAsync` — نفس التعهّد، وقدرة رفض إضافية)، و3 مواضع `RunIfAsync` كلها في `AdminController` |
| اختبارات العزل والرجوع السابقة | لم تُحذف ولا واحدة | الحزمة 139/139 تشمل كل ما سبق؛ **اختبار واحد استُبدل عمدًا**: كان يوثق أن حساب `CreateUser` يبقى بعد فشل التدقيق، وصار يوثق أنه يتراجع — لأن الحدّ الموثِّق أُغلق بتنفيذ النقل |
| `SequenceService` وحجز السباق | لا تغيير | `SemaphoreSlim _gate` في `:16، :34، :68` و`SequenceServiceTests` (3) ناجحة |
| `Pagination.ClampSize` وحدّ حجم الصفحة | لا تغيير | 9 مواضع + `PaginationTests` ناجحة |
| الحواجز المعمارية الـ10 | لا تغيير | 10 `[Fact]` في `ArchitectureGuardTests.cs` وكلها في التشغيل الناجح |
| إصلاحات B0 للواجهة (القالب، `_PagerModern`، `DisplayHelper`، الحواجز الوصفية) | لا تعديل فيها في هذه المرحلة | `git diff` روجِع: التغييرات محصورة في الملفات التسعة جدول E؛ `Tickets/IndexModern.cshtml` يبقى محذوفًا (5.6) ولا مرجع له (`grep IndexModern` = Assets/InventoryAudits/Reports/Warehouses/Custody فقط) |

**لم أُجبر على أي تعديل اضطراري في B0/B1/B2.** التعديل الوحيد المسمّى «تماسّ» هو ترقية
`AdminController.ScopedUsers()` من نسخة يدوية مكررة إلى `TenantGuard.ReadableBy` — وهي نقلة
لسطر واحد إلى مصدر واحد بسلوك مكافئ مقاسًا (غير مدير بشركة `null` لا يرى صفوفًا في الحالتين).

## التسليم المختصر لهذه المرحلة (خلاصة B4/B5)

- **R9 سياسة قراءة الأصول:** **تم التنفيذ والتحقق** (13 اختبارًا + قياس حي + طعم إثبات الحمل).
- **R1b ذرّية الهوية:** `CreateUser`/`ResetPassword`/`EditUser` **تم التنفيذ واختباره محليًا
  فقط** على SQLite؛ المتبقي (ChangePassword) موثّق بوضعه الصحيح.
- **R3 الوكيل:** **تمت المراجعة دون تعديل**، مع **تصحيح ادعاء سابق** بالقياس الحيّ الثلاثي؛
  القرار **يحتاج معلومات أو موافقة**.
- **R5 التبعيات:** **تمت المراجعة دون تعديل** — فحص حديث، مستندات موثقة من `api.github.com`،
  وأرقام إصدارات موثقة من `api.nuget.org`، وتعرّض مقاس حيث أمكن؛ **يحتاج موافقة** لكل ترقية.
- **لا migrations، لا تغييرات نشر، لا `git push`، لا ترقيات حزم** (تأكيد مفصّل §12 أعلاه
  يسري على هذه المرحلة أيضًا).


---

## G. مرحلة الإضافات (F) — المخازن ومواقع الشركة

مرحلة **إضافة فيتشر** لا إعادة هيكلة: المستخدم قرّر صراحةً «عدينا مرحلة إعادة الهيكله»،
وأعطى صلاحية تغيير قاعدة البيانات «بشرط أن يكون التغيير محسوباً وبلا أخطاء».

### G.1 القرار الذي حدّد شكل كل شيء

الطلب كان يُقرأ طبيعياً ككيان `Warehouse` جديد. لم يُفعل ذلك، لأن الفرق بين «مخزن» و
«موقع شركة» **قاعدة على `Location.Type`** لا هوية منفصلة: جدول واحد + `ParentLocationId`
اختياري = رحيل إضافي بالكامل (عمودان قابلان لـ null)، بلا `_backfill_`، وبلا أي مساس بجدول
`Asset` الذي يرتبط بالموقع أصلاً. لو فُصلت المخازن في جدول لَتماسّ كل مسار نقل للأصول.

### G.2 طبقة البيانات — مقيسة لا مفترضة

`dotnet run` ببيئة Development (SQLite) شغّل `DbSeeder.cs:42 MigrateAsync()`، ثم قياس
مخطط `assettracking_dev.db` مباشرة بـ `pragma_table_info('Locations')` عبر مسبار مؤقت
(`Microsoft.Data.Sqlite`) أُرفق بنتيجة الاختبار ثم حُذف:

```
Notes TEXT notnull=0 || ParentLocationId INTEGER notnull=0
```

كلاهما موجود وقابل لـ null — وهو شرط وجوب لأن `TwoCompanyFixture` يبني مخططه بـ
`EnsureCreatedAsync`؛ عمود إلزامي كان سيكسر كل الاختبارات القديمة. الرحيلتان:
`20261012181232_AddWarehouseHierarchy` (SQLite/SqlServer) و`20261012181259_AddWarehouseHierarchy`
(Postgres). **لم تُطبَّق رحلة Postgres على أي قاعدة منشورة.**

### G.3 كاتب واحد وحواجز واحدة

`ILocationService` هو مدخل الكتابة الوحيد للسطور المشتركة بين شاشة الإدارة القديمة ووحدة
المخازن: حراسة الشركة (`OwnedByCompanyAsync` + `IgnoreQueryFilters` لأن فلتر الشركة يُعطَّل
لمدير النظام)، حراسة نوع الأب (`LocationRules.CanHostWarehouse` — لا مخزن ولا خط إنتاج)،
رفض الربط بالنفس، وربط الأب بالمخازن فقط. الفشل رسالة `ToastError` نصّ الحاجز نفسه، لا
«غير موجود» — كي لا يُنسب الرفض لسبب خاطئ. الحفظ والتدقيق في `DbTransaction.RunAsync` واحدة.

الإرجاع: `CustodyService.ReturnAsync(assetId, toLocationId, …)` — `null` = السلوك القديم
حرفياً، واختيار مخزن ينقل الأصل ويكتب `LocationLog` بالتاريخ والسبب «إرجاع عهدة». الوجهة
تُحارس بنفس حارس الشركة، وأُضيف حارس «السجل الموقوف ليس وجهة صالحة» في الخدمة نفسها
(الحارس على حدّ الكتابة لا في القائمة فقط، لأن النموذج يُرسل مباشرة).

### G.4 الواجهة — ما أُصلح وما أُضيف

- **العطل المُبلَّغ عنه:** زر «إضافة مخزن» كان يذهب إلى نموذج الموقع. الآن
  `/Warehouses/Create` (و`?type=1` لموقع شركة) — مقاساً حياً من الصفحة نفسها.
- وحدتان مستقلتان بنفس روح القالب الحديث: `/Warehouses` (المخازن بكودها ووصفها وموقعها
  وعدد أصولها)، `/Warehouses/Sites` (كل موقع ومخازنه داخله)، `/Warehouses/Create|Edit|Details`.
- كرتان في لوحة الإعدادات بنفس شكل كروت الشركات والمستخدمين: «المخازن ١» و«مواقع الشركة ٣»
  بعدّاديهما، يفتحان الصفحتين.
- نموذج الأصل وقائمة إرجاع العهدة يصنّفان الوجهات في `<optgroup>`: «المخازن» ثم «مواقع الشركة».

### G.5 الترجمة (محرك AR→EN) — ما لُوحظ ثم أُصلح

| الملاحظة الحيّة | Cause | الإصلاح (مصدر واحد) |
|---|---|---|
| عناوين مجموعات `<select>` بقيت عربية | `TreeWalker` يرفض `OPTION` ولا يمسّ خاصية `label` | فرع `optgroup[label]` في `translateNodeTree` بـ `translateExact` — أسماء البيانات لا تُفصل |
| `Cairo الجديدة` داخل خلية مدينة | بيانات قاعدة تمرّ من مسح `ar2en` الجزئي | `data-notranslate` على الخلية — نفس نمط بقية خلايا البيانات |
| `Return عهدة` في آخر النقلات | مفتاح قديم `'إرجاع': 'Return'` التقط جزءاً من قيمة مخزَّنة | السبب صار عقدة نصّ مستقلة + مفتاح كامل `'إرجاع عهدة': 'Custody Return'` |
| عمود `Type` يظهر `Category` | `'النوع': 'Category'` من كتلة تفاصيل الأصل | تصحيح المفتاح نفسه إلى `'Type'` — 9 ملفات تحمل كلمة «النوع» (أكثرها رأس عمود أو تسمية حقل) |
| زر «Add Location» على صفحة المواقع | `'إضافة موقع'` يخدم نموذج المواقع القديم أيضاً | النص في صفحتي الوحدات صار «إضافة موقع شركة» (مفتاح موجود) بدل مفتاح جديد مائل |
| «اسم الموقع مطلوب» على نموذج مخزن | رسالة نموذج مشتركة صيغت للموقع | صارت «الاسم مطلوب» + `'Name Is Required'` — تناسب الحقلين معاً |

مفاتيح الوحدة أُضيفت تحت لافتتين (`وحدة المخازن ومواقع الشركة`، `إرجاع العهدة إلى مخزن`)،
ورُفضت مفاتي قصيرة عامة (`'وصف'`، `'إنشاء'` مجرّدة) لأن مسح الأجزاء كان يفسد «وصف أعمال
الصيانة» و«إنشاء مستخدم» في صفحات أخرى — ومقياس عدم leftovers: ناسح نصّي على الصفحات الست
الجديدة + صفحة الإرجاع أعاد **صفر** سطر `NEED`.

### G.6 الاختبارات والحواجز (قيس لا وصف)

- `WarehouseFeatureTests.cs` — **22 اختباراً** على أفعال الكونترولر والخدمة الحقيقيتين
  (SQLite + نفس `LocationService`/`CustodyService`): الإنشاء بالاسم والكود والوصف والأب،
  كل رسالة رفض بعينها، عدم لمس شركة أخرى، رجوع الوصف عند فشل التدقيق، الإرجاع لمخزن مختار
  (النقلة المؤرخة) وبقاء السلوك القديم بلا اختيار، رفض المخزن الموقوف وغيابه من القائمة،
  عدّادات اللوحة، وتفرّع `SubWarehouses` الذي يُثبت أن الإسقاط المتداخل **يُترجَم إلى SQL**.
- `ArchitectureGuardTests.Warehouse_predicate_lives_only_in_location_rules` — يمنع كتابة
  `Type == LocationType.Warehouse` خارج `LocationRules` (جدول واحد؛ شرط على سطحين = قائمتان مختلفتان).
- الحزمة كاملة: `Passed! - Failed: 0, Passed: 162, Skipped: 0, Total: 162`
  (`dotnet test AssetTracking.sln`)، و`node --check wwwroot/js/theme.js` سليم.

### G.7 التحقق الحيّ (سيرفر Development على `127.0.0.1:5180`)

| المسار | الملاحظة المقاسة |
|---|---|
| `/Warehouses/Create` → حفظ | «تمت إضافة «مخزن قطع الغيار»» + تحويل إلى `/Warehouses` لا إلى نموذج الموقع |
| `/Warehouses` | سطر new: `WH-SPARES-01` · «يقع داخل: فرع مدينة نصر» · 0 أصول؛ عدّاد «إجمالي الأصول بالمخازن 4» صار 5 بعد الإرجاع |
| `/Warehouses/Sites` | «فرع مدينة نصر» يعرض «مخزن قطع الغيار» داخله؛ «مخازن مرتبطة بموقع 1» و«مخازن بلا موقع 1» |
| `/Custody/Return?assetId=3` | `<optgroup>` «المخازن» (2) و«مواقع الشركة» (3)؛ تنفيذ الإرجاع إلى 18 → الأصل صار «في المخزن» موقعه مخزن قطع الغيار، والنقلة «موقع مشروع التجمع الخامس → مخزن قطع الغيار 2026-10-12 - 09:08 م — إرجاع عهدة» |
| `/Admin/AuditLog` | سطر `CreateLocation` (#18) وسطر `CustodyReturn` بأسماء حقول عربية بعد إضافة مفتاحَي الإجراء وحقول النقل |
| حفظ برابط غير صالح (مخزن كأب) | رسالة الحاجز ظهرت في التوست **ولم يتغيّر المخزّن** — ثم أُوقف السجل التجريبي فاختفى من قائمة الإرجاع |
| الوضع الإنجليزي | الصفحات الأربع + صفحة الإرجاع: الكروم إنجليزي بالكامل، وأسماء البيانات (المخازن/الأصول/المدن) تبقى عربية بلا فصل |
| عرض 894px | `scrollWidth - clientWidth = 0` على كل الصفحات، والشبكات `grid-cols-*` + `md:grid-cols-[…]` حاضرة |

### G.8 ما لم يُنفَّذ أو لم يُقَس (بلا تلميع)

- **قياس أقل من 768px لم يُنجَز:** `window.resizeTo` في جلسة browser-use غيّر `outerWidth`
  فقط وبقي `innerWidth = 894`، فالتحقق من تخطيط الموبايل الهيكلي (كلاسات + انعدام overflow)
  لا البصري. لا ادعاء برؤية الشاشة الصغيرة.
- **صفحة الأصل لا تزال تعرض المواقع الموقوفة في قائمة الموقع** (سلوك سابق للوحدة، مصدره
  `ReferencePickers.LocationsAsync`)؛ لم تُصفَّ لأن نموذج تعديل الأصل يفقد قيمته الحالية لو
  أُخفيت وجهته القديمة. المُصلَح هنا هو قائمة **الوجهة** في الإرجاع فقط.
- **باقي في قاعدة التطوير سجلّان من التحقق الحيّ:** «مخزن قطع الغيار» (18) نشط ومختبَر،
  و«مخزن اختبار الحراسة» (19) **موقوف** — لا مسار حذف للمواقع في التطبيق (لا `DeleteLocation`)،
  فالتعطيل هو أقل أثر ممكن. الأصل 3 نُقل إلى 18 عملياً.
- **لا commit ولا `git push`** (لم يُطلب)، ولا تطبيق لرحلة Postgres، ولا ملفات نشر.

---

## H. المرحلة S1 — فصل المخازن عن فروع الشركة فصلاً كاملاً

### H.0 ما الذي تغيّر عن §G ولماذا

نصّ الميزة اللاحق ألغى قرار §G.1 حرفياً: «**لا تنفّذهما ككيان Location عام واحد ولا تدمج
نماذجهما في مسار إضافة موقع مشترك**». القرار السابق (جدول `Locations` واحد + قاعدة على
`Type`) كان هو *السبب الجذري* للعطل المُبلَّغ عنه، لا حلّه: ما دام الكيانان سطرين في جدول
واحد، فأي قائمة مشتقّة بالنوع يمكن أن تُفتح بنوع الآخر. لذلك نُفِّذت المرحلة الأولى
(كيانات وجداول ومسارات كتابة وواجهات منفصلة) مع تجميد الجدول القديم بلا حذف، وتُرك إسقاطه
للمرحلة الثانية لأن حذف بنية بيانات لا يُقاس بلا إذن.

**تصحيح لادعاء سابق في هذا التقرير:** §G.4 قال إن الزر صار يذهب إلى `/Warehouses/Create?type=1`
لموقع شركة. هذا المسار المشترك **زال**؛ النموذجان اليوم مساران مختلفان بكتّابين مختلفين.

### H.1 طبقة البيانات — مقيسة من الرحلة نفسها

رحلة واحدة على كل مزود: `20261013090602_SplitPlacesWarehouseSiteLine` (SQLite) و
`20261013091841_SplitPlacesWarehouseSiteLine` (Postgres). ما تفعله `Up()`، بالترتيب:

| الخطوة | ما نُفِّذ | لماذا بهذا الشكل |
|---|---|---|
| جداول | `CompanySites` + `Warehouses` + `ProductionLines` (`CreateTable` ×3) | ثلاثة أسئلة لثلاثة أشياء: «أي فرع هذا؟» / «أين يُحفظ؟» / «على أي خط يُعمل؟» |
| فهارس | `HasIndex(CompanyId, Code).IsUnique()` لكل جدول + فهرس على `CompanySiteId` و`IsDeleted` | الكود فريد **داخل الشركة** لا عالمياً (§6)، ومطابق لحارس `CompanyScopedCodes.IsTakenAsync` |
| مفاتيح | `FK_Warehouses_CompanySites_CompanySiteId` بسلوك **`Restrict`** | حذف فرع به مخزن مرفوض صراحةً؛ لا `Cascade` يمحو مخازن، ولا `SetNull` يُخفي العلاقة. **لاحقاً** شُدِّد هذا المرجع إلى قيد مركّب `(CompanySiteId, CompanyId)` في رحلة ثانية — H.10 |
| نسخ الصفوف | 4 عبارات `INSERT … SELECT` من `Locations` بفلتر `Type` (الموقع: `<>3 AND <>8`، المخزن: `=3`، الخط: `=8`) مع `LegacyLocationId = l."Id"` | نسخة بلا فقد: كل عمود موقع شركة أُخذ **حرفياً** (عنوان/مدينة/محافظة/إحداثيات/شخص اتصال/هاتف)، والعمودان المطويان (`Notes`/`Address`) فُتِقا بقاعدة معروفة لا بتقدير |
| ترميم الرابط | `UPDATE Warehouses SET CompanySiteId = (SELECT … WHERE s.LegacyLocationId = l.ParentLocationId)` | سلّم الأب القديم كان يجيب عن «أي فرع هذا؟» للمخزن؛ أبٌ ليس موقع شركة يترك المخزن **مستقلاً** — لا اختراع علاقة غير موجودة |
| أعمدة المستهلكين | `Assets`, `InventoryAudits`, `InventoryAuditItems` (`Expected*`/`Actual*`), `LocationLogs` (`From*`/`To*`) | كل حامل موضع صار ثلاثة أعمدة مستقلة قابلة لـ null بدل عمود واحد + نوع |

الجدول القديم **باقٍ ولم يُحذف**؛ لم تُمَسّ له صفوف ولا أعمدة، وحواجز الاختبار تمنع كاتباً
جديداً عليه (H.4). **لم تُطبَّق أي رحلة على قاعدة منشورة** — الرحلتان وُلِّدتا محلياً
وطُبِّقت `MigrateAsync` على `assettracking_dev.db` فقط عبر `DbSeeder.cs:42`.

### H.2 ثلاثة كتّاب، ثلاثة أسطح، لا قاسم مشترك مموّه

`Infrastructure/Services/WarehousingServices.cs` يحمل `CompanySiteService` و
`WarehouseService` و`ProductionLineService`. لكلٍّ `CreateAsync`/`UpdateAsync` بنفس عقد
الأمان، وبلا خدمة جامعة:

| الحاجز | نصّه الحرفي | موضعه |
|---|---|---|
| شركة غائبة | `يجب تحديد الشركة` | أول سطر في كل `CreateAsync` |
| كود متكرر داخل الشركة | `كود المخزن «{code}» مستخدم في هذه الشركة` (ومثله للموقع والخط) | `CompanyScopedCodes.IsTakenAsync<TEntity>` قبل أي بناء كيان |
| رابط عبر شركة | `Messages.HiddenRecord` (404 لا 403) | `OwnedByCompanyAsync<CompanySite>(input.CompanySiteId, companyId)` في **كلا** المسارين: الإنشاء والتعديل |
| سجل غير موجود أو عبر شركة | `Messages.HiddenRecord` | قراءة `Warehouses`/`CompanySites` عبر الفلتر العالمي + `IgnoreQueryFilters` حيث يلزم لمدير النظام |
| ذرّية | — | `DbTransaction.RunAsync { SaveChanges; LogTextAsync }` واحدة: فشل التدقيق يُلغي السطر (H.4) |

إخفاء الرسالة عن الربط العابر شركة **عمد**: رفضٌ باسم «غير موجود» لا يفضح وجود سجل لشركة
أخرى، وهو نفس نمط بقية الحواجز في الحل.

### H.3 الأصل والعهدة: التخزين بالمخازن، وفرع الشركة ليس وجهاً للحفظ

- `Asset` صار يحمل `WarehouseId` و`CompanySiteId` و`ProductionLineId` بدل `LocationId`،
  و`PlaceRules.HasOnePlace` تمنع أكثر من موضع واحد في آنٍ واحد (قاعدة واحدة، لا ثلاث
  تحققات مبعثرة).
- `CustodyService.ReturnAsync(int assetId, int? toWarehouseId, …)`: الوجهة **مخزن أو لا شيء**
  (البقاء حيث هو). فرع الشركة لم يعد وجهاً صالحاً للإرجاع — ليس لأنه «ممنوع إدارياً» بل لأن
  إجابته سؤالاً مختلفاً. الحارس على حدّ الكتابة (`OwnedByCompanyAsync<Warehouse>`) لا في
  القائمة فقط، لأن النموذج يُرسل مباشرة.
- التاريخ محفوظ كما هو: النقلة تُقيد في `LocationLogs` بـ `FromWarehouseId`/`ToWarehouseId`
  والسبب، و`PlaceMoves.RecentAsync` و`PlaceNames.LoadAsync` يقرآنها بعد الفصل. لم تُحوَّل
  صفوف سابقة ولا حُذفت.
- «أصل بلا موضع» في عدّاد شاشة المخازن = **لا مخزن ولا فرع ولا خط**
  (`WarehousesController.cs:73-76`)، لا «لا مخزن» وحدها؛ لو كانت «لا مخزن» لُعِدّ كل أصل
  عند فرعٍ تائهاً.

### H.4 الاختبارات — 28 موسومة بالبنود + 3 حواجز جديدة

`tests/AssetTracking.Tests/WarehouseSiteSeparationTests.cs` — **28 اختباراً** على أفعال
الكونترولرات والخدمات وقاعدة SQLite نفسها (لا دوال معزولة): كل بند من §7 موسوم في تعليق
القسم (`§7-1 و §7-6`، `§7-2 و §7-3`، … `§7-11`) فالتقرير لا يحتاج بادّعاء.

| بند §7 (نصّ الميزة) | الاختبار الذي يغلقه |
|---|---|
| 1) موقع شركة بلا أي مخزن، وسجل كامل | `Site_is_created_with_zero_warehouses_and_is_a_complete_record` — يُثبَت السطر كتابةً لا استدعاءً: الكاتب `AuditService` الحقيقي، والمطالب `Action/EntityName/EntityId/NewValues/UserId` على سطر `CreateCompanySite` المخزَّن |
| 2) مخزن بلا موقع شركة | `Warehouse_is_created_with_no_site_and_keeps_its_own_identity` — سطر تدقيق واحد `CreateWarehouse` لا `CreateCompanySite` |
| 3) المخزن يعمل بلا ربط | `Warehouse_with_no_site_still_holds_assets_and_counts_them` |
| 4) رابط بموقع من الشركة نفسها | `Warehouse_links_to_a_site_of_its_own_company` + `Editing_a_warehouse_unlinks_its_site_without_changing_what_the_row_is` |
| 5) رابط عابر شركة مرفوض | `Warehouse_cannot_link_a_site_of_another_company_and_writes_nothing` + `Editing_a_warehouse_to_another_companys_site_is_refused_and_keeps_the_link` (الأثر: **الربط القديم باقٍ**، لا مسح نصف) |
| 6) موقع بلا مخازن يُعرض وتفتح بطاقته | `Site_index_lists_a_site_that_holds_no_warehouse` + `Site_details_open_for_a_site_that_holds_no_warehouse` |
| 7) مخازن كثيرة تحت موقع واحد | `One_site_holds_many_warehouses_and_lists_them_all` |
| 8) «إضافة مخزن» لا تُنشئ موقعاً ولا العكس | `Add_warehouse_saves_a_warehouse_row_and_creates_no_site` + `Add_site_saves_a_site_row_and_creates_no_warehouse` + `Each_unit_has_its_own_form_and_its_own_edit_page` |
| 9) تتبّع الأصول يختار المخازن | `Asset_form_stores_the_asset_in_a_warehouse_and_the_warehouse_answers` + `Asset_at_a_site_appears_on_the_site_screen_not_as_warehouse_stock` (بضابط موجب على عدّاد «بلا موضع»: أصل بلا مخزن وبلا فرع) |
| 10) التسليم/القبول/الإرجاع مع بنية التاريخ | `Assign_accept_and_return_keep_working_and_leave_dated_history` + `Return_page_offers_active_warehouses_and_never_a_site` + `Returning_to_a_company_site_is_refused_and_keeps_the_asset_where_it_was` + `Return_to_another_companys_warehouse_is_refused_and_writes_nothing` |
| 11) لا قراءة ولا كتابة عبر شركة | `Another_company_can_neither_read_nor_write_either_entity` + `Admin_of_one_company_cannot_move_an_asset_to_another_companys_warehouse` |
| §6: لا كيان موضع عام | `Production_line_is_neither_a_site_nor_a_warehouse` + `Unified_location_table_has_no_new_rows_after_both_entities_are_written` + `Code_is_unique_inside_the_company_only` |
| §6: القاعدة نفسها ترفض الربط العابر (لا الكاتب وحده) | `The_database_itself_refuses_a_cross_company_site_link` — كتابة مباشرة على SQLite بلا مرور بالكاتب: رفض الرابط العابر، وقبول رابط الشركة نفسها و`null`، مع ضابط مرآة (شركة ب ← فرع ب) — H.10 |
| 12–13) بقاء كل الاختبارات القديمة ونجاحها جميعاً | **لم يُمثَّل باختبار مسمّى** — قيس بعدّاد الحزمة: 169/169 (H.5)، منها 141 اختباراً سابقاً لم يُحذف ولا واحد |
| ذرّية الحفظ + التدقيق | `Saving_a_new_warehouse_rolls_back_when_the_audit_fails` + `Editing_a_site_rolls_back_when_the_audit_fails` |

حواجز نصّية جديدة في `ArchitectureGuardTests.cs` (من 10 إلى **12**)، كلها تقرأ الشجرة:

| الحاجز | ما يمنع |
|---|---|
| `Place_entity_is_never_chosen_by_a_type_column` | أي `\bLocationType\.` خارج تعريف الجدول المجمَّد — أي قائمة مشتقّة بالنوع تعيد الشكل الذي كان يحوّل «إضافة مخزن» إلى «إضافة موقع» |
| `Retired_unified_location_table_has_no_writer` | `Locations.Add`/`Update`/`Remove` و`new Location {` — العودة إلى الجدول الموحَّد تُكسر في البناء لا في مراجعة بشرية |
| `Standalone_pages_use_the_shared_head` | أي صفحة `Layout = null` بلا `_Head` — ومنشؤه عطل الفاصلة المنقوطة في H.6 |

### H.5 القياس النهائي (أوامر ونتائج حرفية)

| الخطوة | الأمر | النتيجة الحرفية |
|---|---|---|
| حزمة الاختبار كاملة | `dotnet test tests/AssetTracking.Tests --nologo` | **`Passed! - Failed: 0, Passed: 169, Skipped: 0, Total: 169, Duration: 10 s`** |
| حزمة القبول وحدها | `dotnet test … --filter ~WarehouseSiteSeparationTests` | `Passed! - Failed: 0, Passed: 28, Total: 28` |
| الحل بالوضع المُصدَر | `dotnet build AssetTracking.sln -c Release` | **`Build succeeded. 2 Warning(s) 0 Error(s)`** — التحذيران `NU1902` على `MailKit 4.13.0`، لم يُخمَدا ولا رُقِّيا |
| محرك الترجمة | `node --check wwwroot/js/theme.js` | سليم |
| تغطية القاموس على 7 ملفات رؤية للوحدتين | ناسح يمثّل المحرك + قاموس `ar2en` | **978 مفتاح، 4 بقايا** (كلها سابقة للوحدة — H.7) |

### H.6 عطِلان لم يكشفهما لا بناءٌ ولا اختبار — وُجدا حياً وأُصلحا في الجذر

| الملاحظة الحيّة | السبب الجذري | الإصلاح (مصدر واحد، لا 13 تعديلاً) |
|---|---|---|
| فاصلة منقوطة تظهر كنصّ فوق كل صفحة مستقلة | `@await Html.PartialAsync("_Head");` — الـ `;` خارج كتلة تعليمات الـ Razor يُطبَع كتلة نصّ أولى داخل `<body>` | حُذفت من **13 موضع استدعاء** (`Account/Login`، `Assets/IndexModern`، `CompanySites/{Details,Form,Index}`، `Custody/{IndexModern,MyCustodyModern}`، `Home/AdminDashboard`، `InventoryAudits/IndexModern`، `Reports/IndexModern`، `Warehouses/{Details,Form,IndexModern}`) + حاجز `Standalone_pages_use_the_shared_head` يمنع رأساً ثامناً. القياس بعد البناء: عقد النصّ الأولى في `body` = `[]` |
| نموذج تعديل الأصل يمسح المخزن/القسم/المورّد المحفوظ عند الحفظ (مدير الشركة، وليس مدير النظام في الإنشاء) | `filterByCompany()` كان يعمّم «اختر الشركة أولاً» على كل قائمة تحمل `data-company`، بينما قائمة الشركات لا تُرسم إلا لـ `IsAdmin && IsNew` (`Views/Assets/Form.cshtml:53`) ⇒ `companySel === null` ⇒ `hide` يصحّ على كل الخيارات ⇒ تعطيل + تفريغ المحفوظ. مقاس حياً: `option[value="5"] {hidden:true, disabled:true}` مع أن بطاقة المخزن تثبت `WarehouseId=5` في القاعدة | `if (!companySel) return;` — القوائم مصلوحة على شركة المستخدم من الخدمة نفسها، فأي حجب هنا بلا معنى. بعد الإصلاح: `WarehouseId="5" «مخزن قطع الغيار»`، `VendorId=1`، والتصنيف `1/27` كلها معاد تعبئتها مع 5 خيارات مفعّلة |

### H.7 الواجهة والترجمة — ما قُطع وما بقي

- الوحدتان منفصلتان بالكامل: `/Warehouses/{Index,Create,Edit,Details}` و
  `/CompanySites/{Index,Create,Edit,Details}`، ونموذجان لا يقاسمان حقلاً مضللاً، و`Save` لكلٍّ
  كاتبه. رسائل النموذج باسم الكيان: `اسم المخزن مطلوب` و`اسم الموقع مطلوب`.
- ثلاثة كروت في `Views/Admin/Index.cshtml` لا كرت واحد: «مواقع وفروع الشركة» و«المخازن» و
  «خطوط الإنتاج»، وأزرارها مقاسة حياً بالإنجليزية: `View Company Sites -> /CompanySites`،
  `Add Company Site -> /CompanySites/Create`، `View Warehouses -> /Warehouses`،
  `Add Warehouse -> /Warehouses/Create`.
- **تصحيح مصطلح:** الزرّان كانا «عرض المواقع / إضافة موقع» ⇒ `View Locations / Add Location`،
  أي أن كلمة الكيان المرفوض (Location) كانت تعود في واجهة الوحدات. صارا «عرض مواقع الشركة /
  إضافة موقع شركة» + مفتاح `'عرض مواقع الشركة': 'View Company Sites'`. هذا إصلاح في مصدر النص
  العربي لا في القاموس وحده، وإلا بقيت العربية مائلة.
- مفاتيح الوحدة: كتلة موسومة `/* وحدتا المخازن ومواقع الشركة */` رفعت القاموس من **901 مفتاحاً**
  (القياس قبل هذه المرحلة) إلى **978** — أي **+77 مفتاحاً** — تحمل فصل المصطلح حرفياً: `المخزن هو «أين يُحفظ الأصل؟» — يعمل وحده…`،
  `— بلا فرع: مخزن مستقل —`، `بلا مخازن — وضع طبيعي`،
  `«أين يُحفظ الأصل؟» — مخزن فقط، والفرع ليس مخزنًا.`. ومفاتيح صفة المنشأة التي أعادت قياساً
  حياً: `مبنى / موقع مشروع` و`شقة / سكن` كانت تخرج `Building / موقع مشروع` لأن المسح الجزئي
  يلتقط جزءاً من التسمية المزدوجة — صار لكلٍّ مفتاح كامل.
- **القياس لا الانطباع:** ناسح يمثّل المحرك حرفياً (مطابقة تامة على النصّ المقصوص، ثم مسح
  أطول مفتاح بحدود كلمات، واستثناء `SHORT_EXACT`، وتفرّعات الشرطة `— …`) على ملفات الوحدتين
  السبعة أعاد **4 بقايا فقط**، كلها سابقة للوحدة وفي نصّ سجل التدقيق وSLA:
  «والقيمة قبل وبعد التغيير، مع عنوان الـ IP ووقت التنفيذ.»، «سياسات مستوى الخدمة (»،
  «سيتم توليد وسم الأصل تلقائياً بالصيغة»، «يجب تحديد الشركة لهذا الدور». سبب البقاء أنها
  مقاطع مبعثرة على أسطر متعددة فالمطابقة التامة لا تراها؛ تُرِكت لأن معالجتها تعديل في صفحات
  خارج نطاق الميزة.

### H.8 التحقق الحيّ (سيرفر Development على `localhost:5180`)

القياس من DOM ومن الهندسة المحسوبة، لا من لقطات (اللقطات محجوبة في هذه الجلسة
بـ`NATIVE_BROWSER_VIEWPORT_UNAVAILABLE`، والعرض مثبّت على حجم الموبايل).

| المسار / الوضع | الملاحظة المقاسة |
|---|---|
| `/Warehouses/Create` (عربي) | حفظ «مخزن التحقق الحي» أنشأ **مخزناً فقط**؛ ظهر في `/Warehouses` بـ «— مستقل —»، ولم يظهر في `/CompanySites` |
| `/CompanySites/Create` (عربي) | حفظ «فرع التحقق الحي» أنشأ **موقعاً فقط**؛ بطاقته «بلا مخازن — وضع طبيعي» ولم يظهر في قائمة المخازن |
| `/Assets/3` (عربي) | بعد نقل الأصل إلى المخزن الجديد: بطاقته «الموضع مخزن التحقق الحي»، وهو مدرج في `/Warehouses/Details/7` |
| `/Warehouses` (إنجليزي) | مسحه أعاد **صفر** نصّ واجهة عربي داخل الصفحة؛ الباقي إشعارات قاعدة البيانات واسم مستخدم (بيانات) |
| `/Warehouses/Create` (إنجليزي) | `Warehouse Name *`، `Warehouse Code`، `Company Branch (optional)`، `— No branch: standalone warehouse —`، زرّ `Create Warehouse`؛ الوصف والعنوان الاختياريان لا يقطعان الإنشاء |
| `/CompanySites` و`/CompanySites/Create` (إنجليزي) | `Company Sites & Branches`، و`Facility Kind` بقيمه الستّ كما خرجت من الـ DOM: `Office`، `Factory`، `Building / Project Site`، `Apartment / Housing`، `Branch`، `Other`، وبلا أي نصّ عربي متبقٍ داخل `main` |
| `/Warehouses/Details/5` (إنجليزي) | `Warehouse — Code WH-SPARES-01 — Belongs To A Branch …` — الفرع مذكور كمرجع اختياري، لا كهوية للمخزن |
| `/CompanySites/Details/1` (إنجليزي) | `Office — Code HQ-CAI` مع أقسام «Warehouses Of This Branch» و«Assets At The Branch Itself» — الموقع بلا مخازن يظل بطاقة كاملة |
| `/Assets/Edit/3` (إنجليزي) | ثلاث قوائم مستقلة `Warehouse` / `Company Site` / `Production Line`، والمحفوظ معاد تعبئته (`7`، قسم `3`، مورّد `1`) — قياس إعادة اختبار الإصلاح في H.6 |
| `/Admin` (إنجليزي) | الكروت الثلاثة بمصطلح الكيان، وبلا كلمة Location في أي زرّ |
| `/Admin` (عربي، الافتراضي) | رؤوس الكروت: «مواقع وفروع الشركة»، «المخازن»، «خطوط الإنتاج»؛ `dir="rtl"`؛ لا عقد نصّ زائدة في `<body>` |

### H.9 ما لم يُنفَّذ أو لم يُقَس (بلا تلميع)

- **§6 على مستوى القاعدة أُغلق** (كان الثغرة المعلنة في هذه الفقرة). التفصيل والقياس في **H.10**.
  الباقي المؤجَّل حقاً: **المرحلة 2 (إسقاط `Locations`)** — الكيان والجدول والأعمدة القديمة باقية،
  مجمّدة بحاجز «لا كاتب». الحذف بنيوي ويحتاج إذناً.
- **لا مسار حذف للكيانين:** الإيقاف (`IsActive`) هو الأداته المتاحة، كما كان للمواقع؛ لم
  تُخترع حافة حذف لم تُطلب.
- **لم يُختبر على مزود الإنتاج:** لا Postgres محلياً ولا SQL Server؛ كل الاختبارات على SQLite
  في الذاكرة عبر `TwoCompanyFixture` + `EnsureCreatedAsync`. رحلة Postgres وُلِّدت ولم تُطبَّق
  على أي قاعدة منشورة.
- **عرض أقل من 768px بصرياً لم يُنجَز** (نفس حدّ §G.7): تخطيط الموبايل مُثبَت بالكلاسات وبانعدام
  `scrollWidth - clientWidth` عند 894px، لا برؤية الشاشة الصغيرة.
- **بقايا حيّة في قاعدة بيانات التطوير** من التحقق: «مخزن التحقق الحي» و«فرع التحقق الحي»
  نشطان، والأصل `AST-2026-00003` صار موضعه «مخزن التحقق الحي»، و«مخزن قطع الغيار» و«مخزن اختبار
  الحراسة» (موقوف) من جولات سابقة. **لم يُمحَ شيء** لأن المحو أثر غير قابل للتراجع في بيانات
  يراها المستخدم.
- **لم يُختبر الإرجاع حيّاً حتى النهاية:** `/Custody/Return?assetId=…` يحوّل إلى بطاقة الأصل إن
  لم تكن هناك عهدة نشطة — سلوك قائم سليم. قياس «المخازن فقط» اعتمد على الاختبارات الثلاثة في
  H.4 (بما فيها رفض فرع شركة ورفض مخزن عابر) لا على نقرة حيّة. إسناد عهدة أُعدّ في الـ DOM ثم
  **لم يُرسَل** عمداً، لإبقاء قاعدة التطوير بلا عهدة معلّقة من صنعي.
- **لا `git commit` ولا `git push`** (لم يُطلب)، **ولا تغيير إعداد نشر**، **ولا ترقية حزم**،
  **ولا تشغيل CI على GitHub**.

### H.10 إغلاق §6 داخل القاعدة — الرحلة وقياسها على نسخة حقيقية

**ما أُضيف في النموذج** (`WarehousingConfig.cs`):
`CompanySiteConfig` فهرس فريد على `(Id, CompanyId)`؛ و`WarehouseConfig` وصلته صارت:

```csharp
.HasOne(x => x.CompanySite).WithMany(s => s.Warehouses)
    .HasForeignKey(x => new { x.CompanySiteId, x.CompanyId })
    .HasPrincipalKey(p => new { p.Id, p.CompanyId })
    .OnDelete(DeleteBehavior.Restrict);
```

**الرحلتان المولَّدتان:** SQLite `20261013111742_WarehouseSiteCompositeOwnership`
(`AssetTracking.Infrastructure`)، وPostgres `20261013111753_WarehouseSiteCompositeOwnership`
(`AssetTracking.Migrations.Postgres`). لم تُطبَّق أيٌّ منهما على قاعدة منشورة.

**SQL SQLite كما أخرجه `dotnet ef migrations script`:** إعادة بناء الجدولين عبر `ef_temp_*`
مع `INSERT … SELECT` (نقل كل الأعمدة نصاً)، و`AK_CompanySites_Id_CompanyId UNIQUE
("Id","CompanyId")`، و`IX_CompanySites_Id_CompanyId` الفريد، و
`FK_Warehouses_CompanySites_CompanySiteId_CompanyId FOREIGN KEY ("CompanySiteId","CompanyId")
REFERENCES "CompanySites" ("Id","CompanyId") ON DELETE RESTRICT`.

| ما قيس | الطريقة | النتيجة الحرفية |
|---|---|---|
| الاختبار الجديد يقيس القيد المركّب فعلاً | حذف المركّب مؤقتاً إلى `.HasForeignKey(x => x.CompanySiteId)` ثم `--filter` | **`Failed! - Failed: 1, Passed: 0`** — فالخُضوع السابق ليس عرضياً |
| الإدانة بنوع الانتهاك لا بأي استثناء | مطالبة `msg.Contains("FOREIGN KEY")` بدل `ThrowsAsync<DbUpdateException>` فقط | النسخة الأولى كانت تمرّ لسبب خطأ؛ شُدِّدت المطالبة فأصبحت الإدانة بالانتهاك نفسه |
| من يفرض المفاتيح الأجنبية في الاختبار | `Foreign Keys=False` على نفس الاتصال، ثم بحذف المفتاح | مع `False`: **`Failed: 1`**؛ بحذف المفتاح: **`Passed`** ← `Microsoft.Data.Sqlite` يفرضها **افتراضياً** (عكس تعليقِي السابق). صُحِّح التعليق للمقيس وثُبِّتت `Foreign Keys=True` صراحة |
| الرحلة على **نسخة** من قاعدة التطوير | `cp assettracking_dev.db → fk_copy.db` ثم `dotnet ef database update …` | `Done.` والأعداد مطابقة قبل/بعد: Warehouses 7 · CompanySites 11 · Assets 54 · LocationLogs 2 · InventoryAudits 6 · InventoryAuditItems 28 · Companies 4 |
| سلامة النسخة بعد الرحلة | `PRAGMA foreign_key_check` / `integrity_check` | `[]` و `ok`، و`__EFMigrationsHistory` يضم `…111742_WarehouseSiteCompositeOwnership` |
| القاعدة ترفض بنفسها (خارج التطبيق) | كتابة مباشرة على النسخة مع `PRAGMA foreign_keys=ON` | **رفض** بـ `FOREIGN KEY constraint failed`: إدراج فرع شركة ٣ تحت مخزن شركة ١، ومرجع معلّق (`999`)، و**تحديث** رابط سليم إلى فرع شركة أخرى. **قبول**: فرع نفس الشركة، ومخزن `null` (المستقل صالح بقاعدة MATCH SIMPLE) |
| هل الفهرس الفريد ترف؟ | حذف `IX_CompanySites_Id_CompanyId`، توليد الرحلة، تطبيقها على نسخة | **فشلت الرحلة**: `SQLite Error 1: 'foreign key mismatch - "ef_temp_Warehouses" referencing "CompanySites"'` بينما **169 اختباراً باقون خُضراً** (مسار `EnsureCreated` لا يرى الترتيب). أُعيد الفهرس وسببُه موثّق في `WarehousingConfig.cs` |
| قاعدة التطوير الحقيقية | نسخة احتياط `assettracking_dev.pre_fk_backup.db` ثم إقلاع التطبيق (`DbSeeder.cs:42`) | التاريخ يضم الرحلة، والـ DDL فيه FK المركّب، `foreign_key_check []`، `integrity_check ok`، وفرق المحتوى مقابل نسخة ما قبل الرحلة: **صف واحد فقط** (`Warehouses.Id=7`: `UpdatedAt`/`UpdatedBy` من تحريك حيّ أُعيد إلى حالته الأصلية) |
| حيّاً — تحريف النموذج | مدير شركة ١ حقن `<option value="6">` (فرع شركة ٣) في `/Warehouses/Create` وأرسل | `POST /Warehouses/Save` أعاد النموذج بتوست **`السجل غير موجود أو لا تملك صلاحية الوصول إليه`** و**صفر صفوف جديدة** (الإجمالي 7 كما كان) |
| حيّاً — المسار السليم | `/Warehouses/Edit/7` ← ربط بفرع ٣ (نفس الشركة) ← حفظ، ثم ← «— بلا فرع —» ← حفظ | الاتجاهان حُفظا؛ الصف رجع إلى `null`؛ لا صفوف جديدة |
| صفحات الوحدتين والأصل | `fetch` من داخل الصفحة | `/Warehouses` · `/Warehouses/Create` · `/Warehouses/Details/5` · `/CompanySites` · `/CompanySites/Create` · `/Admin` · `/` = **200** لكلٍّ؛ `/Assets/Edit/1` يعرض ثلاث قوائم مستقلة `WarehouseId` / `CompanySiteId` / `ProductionLineId` وبلا أي حقل `LocationId` |

**ما لم يُقس في هذه الجولة:** لا لقطة شاشة — السطح البصري كان مخفياً
(`NATIVE_BROWSER_VIEWPORT_UNAVAILABLE`) فالتحقق هيكلي (DOM + قاعدة) لا بصري؛ ورحلة Postgres
وُلِّدت ولم تُطبَّق (لا Postgres محلياً ولا قاعدة منشورة يُسمَح بلمسها)؛ و`/Assets/Form?id=1`
أعاد 404 لأن المسار الصحيح `/Assets/Edit/{id}` — عنوان خاطئ مني لا عطل في التطبيق.
