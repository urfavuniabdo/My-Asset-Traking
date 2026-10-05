# نظام إدارة وتتبع الأصول والدعم الفني
### Asset Tracking & Technical Support System (ATS)

نظام متكامل لإدارة الأصول الثابتة، العهد الشخصية، الصيانة الوقائية، تذاكر الدعم الفني،
الجرد الدوري والتقارير — مبني بـ **ASP.NET Core 8** بواجهة عربية كاملة (RTL) للبيئة المصرية.

---

## نظرة عامة

| البند | التفاصيل |
|---|---|
| **الاسم** | نظام إدارة وتتبع الأصول والدعم الفني (ATS) |
| **الهدف** | تتبع دورة حياة الأصل من الشراء حتى التخريد، مع إدارة العهد والصيانة والدعم الفني |
| **الإطار** | ASP.NET Core 8.0 LTS — MVC + Razor Views |
| **بيئة الإنتاج المستهدفة** | IIS 10 داخلي (On-Premises) + SQL Server 2022 |
| **بيئة التطوير/التجربة** | SQLite (تبديل تلقائي عبر مفتاح `DatabaseProvider`) |
| **الواجهة** | عربية بالكامل، اتجاه RTL، خط Cairo، عملة `ج.م` |

---

## المعمارية

معمارية طبقية نظيفة (Clean Layered Architecture) — 4 مشروعات:

```
AssetTracking.Domain          ← الكيانات، الـ Enums، الثوابت (لا تبعيات)
      ↑
AssetTracking.Application     ← الواجهات (Interfaces) و Result<T>
      ↑
AssetTracking.Infrastructure  ← EF Core, Identity, الخدمات, Migrations
      ↑
AssetTracking.Web             ← Controllers, Razor Views, ViewModels
```

### المكدس التقني

| الطبقة | التقنية |
|---|---|
| ORM | EF Core 8.0.11 + Migrations |
| المصادقة | ASP.NET Core Identity 8 (PBKDF2) |
| عزل الشركات | EF Core **Global Query Filters** على مستوى قاعدة البيانات |
| المهام الخلفية | Hangfire 1.8.14 (لوحة `/jobs` — للمدير فقط) |
| الإشعارات الفورية | SignalR 8 — `NotificationHub` على `/hubs/notifications` |
| باركود / QR | QRCoder 1.6.0 |
| البريد | MailKit 4.13.0 |
| تصدير Excel | ClosedXML 0.104.2 (عربي RTL) |
| السجلات | Serilog.AspNetCore 8.0.3 |
| التحقق | FluentValidation 11.10.0 |
| الواجهة (CDN) | Bootstrap 5.3.3 RTL, Bootstrap Icons, Cairo, Chart.js 4.4.4, html5-qrcode |

---

## الأمن والصلاحيات

### الأدوار الأربعة

| الدور | الوصف |
|---|---|
| `Admin` | مدير النظام — وصول لكل الشركات، وهو الوحيد الذي يدير الشركات |
| `CompanyManager` | مدير شركة — وصول كامل داخل شركته فقط |
| `Technician` | فني دعم — التذاكر، الصيانة، الجرد |
| `Employee` | موظف — عهدي الشخصية وفتح التذاكر |

### السياسات (Policies)
`AdminOnly` · `ManagerOrAdmin` · `TechnicianOrAbove` · `AuthenticatedUser`

### الحواجز الأمنية المُطبَّقة والمُختبَرة

- **عزل الشركات على مستوى قاعدة البيانات** — فلتر عام (Global Query Filter) على كل كيان
  يحقّق `ICompanyOwned`، فلا يمكن لمدير شركة رؤية بيانات شركة أخرى مهما كان الاستعلام.
- **عزل يدوي للمستخدمين** — `ApplicationUser` لا يحقّق `ICompanyOwned`، لذا يُفلتر
  يدوياً عبر `AdminController.ScopedUsers()`.
- **مقاومة IDOR** — أي محاولة وصول لسجل خارج نطاق الشركة تُرجع صياغة
  "غير موجود" (`NotFoundOrForbidden()`) ولا تُرجع 403 مطلقاً، حتى لا يُستنتج وجود السجل.
- **التزامن المتفائل (Optimistic Concurrency)** — `RowVersion` على `Asset` و `MaintenanceTicket`.
- **قفل الحساب** — 5 محاولات فاشلة ⇒ قفل 15 دقيقة.
- **منع رفع الصلاحيات** — مدير الشركة لا يستطيع إنشاء/ترقية مستخدم لدور `Admin`.
- **منع تعطيل الحساب الشخصي**.
- **سجل تدقيق غير قابل للتعديل أو الحذف** — يُكتب من داخل النظام فقط.
- **حماية CSRF** — `__RequestVerificationToken` على كل عمليات POST.

---

## خريطة المسارات (Route Inventory)

### المصادقة والحساب — `AccountController`
| المسار | الطريقة | الصلاحية |
|---|---|---|
| `/Account/Login` | GET/POST | عام |
| `/Account/Logout` | POST | مُصادَق |
| `/Account/Profile` | GET/POST | مُصادَق |
| `/Account/ChangePassword` | GET/POST | مُصادَق |
| `/Account/AccessDenied` | GET | عام |

### لوحة المعلومات — `HomeController`
| المسار | الوصف |
|---|---|
| `/` | لوحة معلومات تتغيّر حسب الدور (KPIs + رسوم Chart.js) |

### الأصول — `AssetsController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Assets` | قائمة الأصول (بحث/فلترة/ترقيم) | مُصادَق |
| `/Assets/Details/{id}` | تفاصيل الأصل + السجل الزمني | مُصادَق |
| `/Assets/Create` · `/Assets/Edit/{id}` | إضافة/تعديل أصل | مدير أو أعلى |
| `/Assets/Scan` | مسح باركود/QR بالكاميرا | مُصادَق |
| `/Assets/Qr/{id}` | توليد صورة QR للأصل | مُصادَق |
| `/Assets/Transfer` · `/Assets/Dispose` | نقل / تخريد | مدير أو أعلى |

### العهد — `CustodyController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Custody/MyCustody` | عهدي الشخصية + قبول/رفض | مُصادَق |
| `/Custody` | إدارة العهد (تسليم/استرجاع) | فني أو أعلى |
| `/Custody/Assign` · `/Custody/Return` | تسليم / استرجاع عهدة | فني أو أعلى |

### التذاكر — `TicketsController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Tickets` | قائمة التذاكر + KPIs + فلاتر SLA | مُصادَق |
| `/Tickets/Details/{id}` | تفاصيل + المحادثة + المرفقات | مُصادَق |
| `/Tickets/Create` | فتح تذكرة جديدة | مُصادَق |
| `/Tickets/Assign` | تسليم لفني | فني أو أعلى |
| `/Tickets/ChangeStatus` · `/Tickets/Resolve` · `/Tickets/Close` | تغيير الحالة / حل / إغلاق | فني أو أعلى |

### الصيانة الوقائية — `MaintenanceSchedulesController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/MaintenanceSchedules` | خطط الصيانة الدورية | فني أو أعلى |
| `/MaintenanceSchedules/Create` · `/Edit/{id}` | إضافة/تعديل خطة | مدير أو أعلى |
| `/MaintenanceSchedules/ToggleSchedule` | تفعيل/تعطيل خطة | مدير أو أعلى |
| `/MaintenanceSchedules/GenerateTicket` | توليد تذكرة صيانة من الخطة | فني أو أعلى |

### الجرد — `InventoryAuditsController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/InventoryAudits` | جلسات الجرد | فني أو أعلى |
| `/InventoryAudits/Create` | جرد جديد | مدير أو أعلى |
| `/InventoryAudits/Details/{id}` | تنفيذ الجرد + المسح | فني أو أعلى |
| `/InventoryAudits/StartAudit` · `/Scan` · `/CompleteAudit` · `/CancelAudit` | دورة حياة الجرد | فني أو أعلى |

### التقارير — `ReportsController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Reports` | مركز التقارير | مدير أو أعلى |
| `/Reports/Assets` | تقرير الأصول (+ تصدير Excel) | مدير أو أعلى |
| `/Reports/Tickets` | تقرير التذاكر وأداء SLA | مدير أو أعلى |
| `/Reports/Custody` | تقرير العهد | مدير أو أعلى |
| `/Reports/Depreciation` | تقرير الإهلاك | مدير أو أعلى |
| `/Reports/Export{Assets\|Tickets\|Custody\|Depreciation}` | تصدير XLSX عربي RTL | مدير أو أعلى |

### الإدارة — `AdminController`
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Admin` | لوحة الإدارة (9 مؤشرات + 8 أقسام) | مدير أو أعلى |
| `/Admin/Users` | المستخدمون (فلترة: `q`, `role`, `companyId`, `active`, `page`) | مدير أو أعلى |
| `/Admin/CreateUser` · `/Admin/EditUser?id=` | إضافة/تعديل مستخدم | مدير أو أعلى |
| `/Admin/ToggleUser` · `/UnlockUser` · `/ResetPassword` | تعطيل / فتح قفل / إعادة كلمة مرور | مدير أو أعلى |
| `/Admin/Companies` | الشركات | **مدير النظام فقط** |
| `/Admin/CreateCompany` · `/Admin/EditCompany/{id}` | إضافة/تعديل شركة | **مدير النظام فقط** |
| `/Admin/Departments` + `Create/Edit` | الإدارات والأقسام | مدير أو أعلى |
| `/Admin/Locations` + `Create/Edit` | المواقع | مدير أو أعلى |
| `/Admin/Categories` + `Create/Edit` | تصنيفات الأصول + إعدادات الإهلاك | مدير أو أعلى |
| `/Admin/Vendors` + `Create/Edit` | المورّدون | مدير أو أعلى |
| `/Admin/Sla` + `Create/Edit` | سياسات مستوى الخدمة | مدير أو أعلى |
| `/Admin/ToggleRef` | تفعيل/تعطيل أي سجل مرجعي | مدير أو أعلى |
| `/Admin/AuditLog` | سجل التدقيق (فلترة: `q`, `act`, `entityName`, `from`, `to`, `page`) | مدير أو أعلى |

> **ملاحظة مهمة:** معامل فلترة نوع العملية في سجل التدقيق اسمه **`act`** وليس `action`،
> لأن `action` يتعارض مع قيمة المسار (Route Value) في ASP.NET Core فيُربَط تلقائياً
> بقيمة `"AuditLog"` ويُفرغ كل النتائج.

### الإشعارات والمهام
| المسار | الوصف | الصلاحية |
|---|---|---|
| `/Notifications` | مركز الإشعارات | مُصادَق |
| `/hubs/notifications` | SignalR Hub (مجموعات `user:{userId}`) | مُصادَق |
| `/jobs` | لوحة Hangfire | **مدير النظام فقط** |

---

## نموذج البيانات

### الكيانات الرئيسية

| الكيان | الوصف |
|---|---|
| `Company` | الشركة — جذر عزل البيانات |
| `Department` | الإدارة/القسم (تابع لشركة، له مدير ومركز تكلفة) |
| `Location` | الموقع (مكتب/مصنع/مخزن/مبنى/شقة/فرع/أخرى) |
| `Category` | تصنيف الأصل — شجري + إعدادات إهلاك افتراضية |
| `Vendor` | المورّد (مع تقييم من 1 إلى 5) |
| `Asset` | الأصل — الكيان المحوري (`RowVersion` للتزامن) |
| `CustodyLog` | سجل العهد (تسليم/استرجاع/قبول/رفض) |
| `MaintenanceTicket` | تذكرة الدعم الفني (`RowVersion`) |
| `MaintenanceSchedule` | خطة الصيانة الوقائية الدورية |
| `SlaPolicy` | سياسة مستوى الخدمة (استجابة/حل/تنبيه بالساعات) |
| `InventoryAudit` + `InventoryAuditItem` | الجرد الدوري وبنوده |
| `AuditLog` | سجل التدقيق — غير قابل للتعديل |
| `Notification` | الإشعارات |
| `ApplicationUser` / `ApplicationRole` | Identity (المستخدم **لا** يحقّق `ICompanyOwned`) |

### تسلسل الأكواد
```
الأصول   : AST-YYYY-NNNNN     مثال: AST-2026-00042
التذاكر  : TKT-YYYY-NNNNN     مثال: TKT-2026-00021
الجرد    : AUD-YYYY-NNNNN     مثال: AUD-2026-00006
```

### قيم الـ Enums — **تبدأ من 1 وليس من 0**
```csharp
DepreciationMethod { StraightLine = 1, DecliningBalance = 2 }
LocationType       { Office=1, Factory=2, Warehouse=3, Building=4, Apartment=5, Branch=6, Other=7 }
AuditStatus        { Draft=1, InProgress=2, Completed=3, Cancelled=4 }
```
> إرسال القيمة `0` لأي منها يُنتج خطأ تحقّق: `The value '0' is invalid.`

### حساب الإهلاك
```
القسط السنوي (القسط الثابت) = (قيمة الشراء − القيمة التخريدية) ÷ العمر الإنتاجي
```

---

## دليل الاستخدام

### بيانات الدخول التجريبية
كلمة المرور لجميع الحسابات: **`Admin@123`**

| البريد | الدور | الشركة |
|---|---|---|
| `admin@ats.eg` | مدير النظام | كل الشركات (`CompanyId = NULL`) |
| `nile.manager@ats.eg` | مدير شركة | شركة النيل للمقاولات العمومية |
| `nile.tech1@ats.eg` / `nile.tech2@ats.eg` | فني دعم | شركة النيل |
| `nile.emp1@ats.eg` … `nile.emp3@ats.eg` | موظف | شركة النيل |
| `delta.*@ats.eg` | — | مجموعة الدلتا للصناعات الغذائية |
| `alex.*@ats.eg` | — | شركة الإسكندرية للنقل واللوجستيات |
| `said.*@ats.eg` | — | شركة الصعيد للتنمية الزراعية |

> **ملاحظة:** حساب `admin@ats.eg` ليس مرتبطاً بشركة، لذا يجب اختيار الشركة يدوياً
> في نماذج الإضافة.

### سيناريوهات مقترحة للتجربة

1. **موظف** — سجّل الدخول بـ `nile.emp1@ats.eg` ← `عهدي` لعرض العهد وقبولها،
   ثم `التذاكر` ← `تذكرة جديدة` لفتح تذكرة دعم.
2. **فني** — `nile.tech1@ats.eg` ← `التذاكر` لتسلّم تذكرة وتغيير حالتها وحلها،
   ثم `الجرد` لتنفيذ جلسة جرد بالمسح.
3. **مدير شركة** — `nile.manager@ats.eg` ← `الأصول` لإضافة أصل، `التقارير` لتصدير
   Excel، `الإدارة` لإدارة البيانات المرجعية. **لن يرى بيانات أي شركة أخرى.**
4. **مدير النظام** — `admin@ats.eg` ← `الإدارة` ← `الشركات` (متاحة له وحده)،
   و`سجل التدقيق` لمراجعة كل العمليات الحساسة عبر كل الشركات.

---

## التشغيل في بيئة الشركة (IIS + SQL Server)

### المتطلبات
- Windows Server + IIS 10 + **ASP.NET Core 8 Hosting Bundle**
- SQL Server 2022 (أو 2019)
- .NET 8 SDK (للبناء فقط)

### الخطوات

```powershell
# 1) ضبط الاتصال في appsettings.Production.json
#    "DatabaseProvider": "SqlServer"
#    "ConnectionStrings:DefaultConnection": "Server=...;Database=AssetTracking;..."

# 2) تطبيق الترحيلات (Migrations)
dotnet ef database update --project src\AssetTracking.Infrastructure ^
                          --startup-project src\AssetTracking.Web

# 3) النشر
dotnet publish src\AssetTracking.Web -c Release -o C:\inetpub\AssetTracking

# 4) في IIS: أنشئ Application Pool بـ "No Managed Code"
#    ووجّه الموقع إلى C:\inetpub\AssetTracking
```

### مفاتيح الإعداد المهمة
| المفتاح | القيم |
|---|---|
| `DatabaseProvider` | `SqlServer` \| `Sqlite` |
| `EnableBackgroundJobs` | `true` \| `false` (يشغّل/يوقف Hangfire) |
| `Smtp:*` | إعدادات البريد (MailKit) |

---

## حالة التطوير

### ✅ مُنجَز ومُختبَر
- [x] المصادقة، الأدوار الأربعة، السياسات، قفل الحساب، تغيير كلمة المرور
- [x] عزل الشركات على مستوى قاعدة البيانات (Global Query Filters) + عزل يدوي للمستخدمين
- [x] لوحة معلومات ديناميكية حسب الدور مع رسوم Chart.js
- [x] **الأصول** — CRUD، نقل، تخريد، QR، مسح بالكاميرا، سجل زمني، تزامن متفائل
- [x] **العهد** — تسليم، استرجاع، قبول/رفض من الموظف، سجل كامل
- [x] **التذاكر** — دورة حياة كاملة، تسليم لفني، محادثة، مؤشرات SLA
- [x] **الصيانة الوقائية** — خطط دورية، تفعيل/تعطيل، توليد تذاكر
- [x] **الجرد** — جلسات جرد بالمسح، مطابقة، تقرير الفوارق
- [x] **التقارير** — 4 تقارير + تصدير XLSX عربي RTL
- [x] **الإدارة** — المستخدمون، الشركات، الإدارات، المواقع، التصنيفات، المورّدون، SLA
- [x] **سجل التدقيق** — مُعرَّب بالكامل مع عرض الفروقات (قبل ← بعد)
- [x] الإشعارات الفورية (SignalR) + مركز الإشعارات
- [x] المهام المجدولة (Hangfire)
- [x] تعريب كامل 100% + RTL + عملة `ج.م`
- [x] بيانات تجريبية غنية: 4 شركات، 21 مستخدماً، 24 تصنيفاً، 14 موقعاً، 16 سياسة SLA

### 🔜 مقترحات للتطوير القادم
- [ ] رفع المرفقات إلى تخزين خارجي (حالياً في قاعدة البيانات)
- [ ] استيراد الأصول من ملف Excel (Bulk Import)
- [ ] تصدير التقارير بصيغة PDF
- [ ] تطبيق موبايل للمسح الميداني (PWA)
- [ ] تكامل مع Active Directory / LDAP للمصادقة الموحّدة
- [ ] لوحة تحليلات متقدمة (تكلفة الملكية الإجمالية TCO)
- [ ] إشعارات البريد التلقائية عند تجاوز SLA
- [ ] اختبارات وحدة وتكامل آلية (المجلد `tests/` جاهز)

---

## المستودع

- **GitHub**: https://github.com/Amr-Walid/assets_tracking
- **الفرع**: `main`

---

*آخر تحديث: 2026-08-20*
