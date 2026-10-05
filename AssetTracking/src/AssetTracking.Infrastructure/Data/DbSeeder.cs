using AssetTracking.Domain.Common;
using AssetTracking.Domain.Entities;
using AssetTracking.Domain.Enums;
using AssetTracking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssetTracking.Infrastructure.Data;

/// <summary>
/// تهيئة قاعدة البيانات ببيانات تجريبية مصرية 🇪🇬
/// (٤ شركات، ٢١ مستخدم، ٤٨ أصلاً، ٢٠ تذكرة، قيود إهلاك ...)
/// تعمل مرة واحدة فقط — تتحقق من وجود البيانات أولاً.
/// </summary>
public static class DbSeeder
{
    private const string DefaultPassword = "Admin@123";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        db.IgnoreCompanyFilter = true;

        await db.Database.MigrateAsync();
        await SeedRolesAsync(roles);

        if (await db.Companies.AnyAsync())
        {
            log.LogInformation("قاعدة البيانات مهيّأة بالفعل — تم تخطي البيانات التجريبية.");
            return;
        }

        log.LogInformation("جارٍ تهيئة البيانات التجريبية المصرية…");

        var companies = await SeedCompaniesAsync(db);
        var depts = await SeedDepartmentsAsync(db, companies);
        var locs = await SeedLocationsAsync(db, companies);
        var vendors = await SeedVendorsAsync(db, companies);
        var cats = await SeedCategoriesAsync(db, companies);
        await SeedSlaPoliciesAsync(db, companies);
        var appUsers = await SeedUsersAsync(users, companies, depts);
        var assets = await SeedAssetsAsync(db, companies, cats, depts, locs, vendors, appUsers);
        await SeedCustodyAsync(db, assets, appUsers);
        await SeedTicketsAsync(db, assets, appUsers);
        await SeedSchedulesAsync(db, assets, appUsers);
        await SeedDepreciationAsync(db, assets);
        await SeedAuditsAsync(db, assets, locs, appUsers);
        await SeedSettingsAsync(db);

        log.LogInformation("✅ تمت تهيئة البيانات التجريبية بنجاح.");
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roles)
    {
        foreach (var r in Roles.All)
        {
            if (await roles.RoleExistsAsync(r)) continue;
            await roles.CreateAsync(new ApplicationRole(r)
            {
                NameAr = Roles.ArabicName(r),
                Description = $"دور {Roles.ArabicName(r)}"
            });
        }
    }

    private static async Task<List<Company>> SeedCompaniesAsync(AppDbContext db)
    {
        var list = new List<Company>
        {
            new() { NameAr = "شركة النيل للمقاولات العمومية", NameEn = "Nile General Contracting", Code = "NILE",
                    City = "القاهرة", Address = "٢٧ شارع رمسيس، وسط البلد، القاهرة", Phone = "0223951200",
                    Email = "info@nile-contracting.eg", TaxNumber = "٢٠٠-٤٥٦-٧٨٩", CommercialRegister = "١٢٣٤٥" },
            new() { NameAr = "مجموعة الدلتا للصناعات الغذائية", NameEn = "Delta Food Industries", Code = "DELTA",
                    City = "المنصورة", Address = "المنطقة الصناعية، طريق جمصة، المنصورة", Phone = "0502301455",
                    Email = "contact@delta-foods.eg", TaxNumber = "٢٠١-٣٣٣-١٢٢", CommercialRegister = "٢٢٩٩١" },
            new() { NameAr = "شركة الإسكندرية للنقل واللوجستيات", NameEn = "Alexandria Transport & Logistics", Code = "ALEX",
                    City = "الإسكندرية", Address = "طريق الكورنيش، سيدي جابر، الإسكندرية", Phone = "0334876500",
                    Email = "ops@alex-logistics.eg", TaxNumber = "٢٠٢-٧٧٧-٩٩٠", CommercialRegister = "٣٣٤٥٦" },
            new() { NameAr = "شركة الصعيد للتنمية الزراعية", NameEn = "Upper Egypt Agri Development", Code = "SAID",
                    City = "أسيوط", Address = "طريق أسيوط الغربي، أسيوط", Phone = "0882312700",
                    Email = "info@saeed-agri.eg", TaxNumber = "٢٠٣-١٢١-٤٤٥", CommercialRegister = "٤٤١٢٣" }
        };

        db.Companies.AddRange(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task<List<Department>> SeedDepartmentsAsync(AppDbContext db, List<Company> c)
    {
        var list = new List<Department>
        {
            new() { CompanyId = c[0].Id, NameAr = "الإدارة الهندسية", Code = "ENG", CostCenter = "CC-101" },
            new() { CompanyId = c[0].Id, NameAr = "تقنية المعلومات", Code = "IT", CostCenter = "CC-102" },
            new() { CompanyId = c[0].Id, NameAr = "الشؤون المالية", Code = "FIN", CostCenter = "CC-103" },
            new() { CompanyId = c[0].Id, NameAr = "الموارد البشرية", Code = "HR", CostCenter = "CC-104" },
            new() { CompanyId = c[1].Id, NameAr = "الإنتاج", Code = "PROD", CostCenter = "CC-201" },
            new() { CompanyId = c[1].Id, NameAr = "مراقبة الجودة", Code = "QC", CostCenter = "CC-202" },
            new() { CompanyId = c[1].Id, NameAr = "الصيانة", Code = "MNT", CostCenter = "CC-203" },
            new() { CompanyId = c[1].Id, NameAr = "تقنية المعلومات", Code = "IT", CostCenter = "CC-204" },
            new() { CompanyId = c[2].Id, NameAr = "أسطول النقل", Code = "FLEET", CostCenter = "CC-301" },
            new() { CompanyId = c[2].Id, NameAr = "المستودعات", Code = "WH", CostCenter = "CC-302" },
            new() { CompanyId = c[2].Id, NameAr = "خدمة العملاء", Code = "CS", CostCenter = "CC-303" },
            new() { CompanyId = c[3].Id, NameAr = "العمليات الزراعية", Code = "AGRI", CostCenter = "CC-401" },
            new() { CompanyId = c[3].Id, NameAr = "الإدارة العامة", Code = "ADM", CostCenter = "CC-402" }
        };

        db.Departments.AddRange(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task<List<Location>> SeedLocationsAsync(AppDbContext db, List<Company> c)
    {
        var list = new List<Location>
        {
            new() { CompanyId = c[0].Id, NameAr = "المقر الرئيسي — وسط البلد", Code = "HQ-CAI", Type = LocationType.Office,
                    City = "القاهرة", Governorate = "القاهرة", Latitude = 30.0561m, Longitude = 31.2394m, ContactPerson = "أ. هشام عبد العال", ContactPhone = "01012345678" },
            new() { CompanyId = c[0].Id, NameAr = "مخزن العبور", Code = "WH-OBR", Type = LocationType.Warehouse,
                    City = "العبور", Governorate = "القليوبية", Latitude = 30.2050m, Longitude = 31.4600m, ContactPerson = "م. سامي جاد", ContactPhone = "01123456789" },
            new() { CompanyId = c[0].Id, NameAr = "موقع مشروع التجمع الخامس", Code = "SITE-TG5", Type = LocationType.Building,
                    City = "القاهرة الجديدة", Governorate = "القاهرة", Latitude = 30.0080m, Longitude = 31.4300m, ContactPerson = "م. وليد فؤاد", ContactPhone = "01234567890" },
            new() { CompanyId = c[0].Id, NameAr = "فرع مدينة نصر", Code = "BR-NSR", Type = LocationType.Branch,
                    City = "القاهرة", Governorate = "القاهرة", Latitude = 30.0596m, Longitude = 31.3400m },
            new() { CompanyId = c[1].Id, NameAr = "مصنع المنصورة الرئيسي", Code = "FAC-MNS", Type = LocationType.Factory,
                    City = "المنصورة", Governorate = "الدقهلية", Latitude = 31.0409m, Longitude = 31.3785m, ContactPerson = "م. طارق الشاذلي", ContactPhone = "01098765432" },
            new() { CompanyId = c[1].Id, NameAr = "مخزن المنتج النهائي", Code = "WH-FIN", Type = LocationType.Warehouse,
                    City = "المنصورة", Governorate = "الدقهلية", Latitude = 31.0450m, Longitude = 31.3800m },
            new() { CompanyId = c[1].Id, NameAr = "مكتب الإدارة — المنصورة", Code = "OFF-MNS", Type = LocationType.Office,
                    City = "المنصورة", Governorate = "الدقهلية", Latitude = 31.0380m, Longitude = 31.3810m },
            new() { CompanyId = c[2].Id, NameAr = "مركز اللوجستيات — سيدي جابر", Code = "LOG-ALX", Type = LocationType.Warehouse,
                    City = "الإسكندرية", Governorate = "الإسكندرية", Latitude = 31.2156m, Longitude = 29.9553m, ContactPerson = "ك. عمرو بدير", ContactPhone = "01555667788" },
            new() { CompanyId = c[2].Id, NameAr = "جراج الأسطول — العامرية", Code = "GAR-AMR", Type = LocationType.Other,
                    City = "الإسكندرية", Governorate = "الإسكندرية", Latitude = 31.0900m, Longitude = 29.8000m },
            new() { CompanyId = c[2].Id, NameAr = "مكتب ميناء الإسكندرية", Code = "OFF-PRT", Type = LocationType.Office,
                    City = "الإسكندرية", Governorate = "الإسكندرية", Latitude = 31.1900m, Longitude = 29.8700m },
            new() { CompanyId = c[2].Id, NameAr = "سكن العاملين — سموحة", Code = "APT-SMH", Type = LocationType.Apartment,
                    City = "الإسكندرية", Governorate = "الإسكندرية", Latitude = 31.2100m, Longitude = 29.9400m },
            new() { CompanyId = c[3].Id, NameAr = "مزرعة أسيوط الغربية", Code = "FRM-AST", Type = LocationType.Other,
                    City = "أسيوط", Governorate = "أسيوط", Latitude = 27.1800m, Longitude = 31.1600m, ContactPerson = "أ. جمال سيد", ContactPhone = "01066554433" },
            new() { CompanyId = c[3].Id, NameAr = "المقر الإداري — أسيوط", Code = "HQ-AST", Type = LocationType.Office,
                    City = "أسيوط", Governorate = "أسيوط", Latitude = 27.1830m, Longitude = 31.1830m },
            new() { CompanyId = c[3].Id, NameAr = "مخزن المستلزمات الزراعية", Code = "WH-AGR", Type = LocationType.Warehouse,
                    City = "أسيوط", Governorate = "أسيوط", Latitude = 27.1750m, Longitude = 31.1700m }
        };

        db.Locations.AddRange(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task<List<Vendor>> SeedVendorsAsync(AppDbContext db, List<Company> c)
    {
        var list = new List<Vendor>
        {
            new() { CompanyId = c[0].Id, NameAr = "المهندس للتوريدات التقنية", Code = "V-ENG", ContactPerson = "م. أحمد سليم",
                    Phone = "01001234567", Email = "sales@mohandes-tech.eg", Rating = 4 },
            new() { CompanyId = c[0].Id, NameAr = "الشرق للأثاث المكتبي", Code = "V-SHRQ", ContactPerson = "أ. منى رأفت",
                    Phone = "01112345678", Email = "info@sharq-furniture.eg", Rating = 3 },
            new() { CompanyId = c[1].Id, NameAr = "دلتا للمعدات الصناعية", Code = "V-DLT", ContactPerson = "م. كريم فتحي",
                    Phone = "01223456789", Email = "sales@delta-equip.eg", Rating = 5 },
            new() { CompanyId = c[1].Id, NameAr = "النصر لقطع الغيار", Code = "V-NSR", ContactPerson = "أ. سعيد حلمي",
                    Phone = "01034567890", Email = "parts@nasr-spare.eg", Rating = 4 },
            new() { CompanyId = c[2].Id, NameAr = "البحر المتوسط للشاحنات", Code = "V-MED", ContactPerson = "م. رامي عزت",
                    Phone = "01145678901", Email = "trucks@med-motors.eg", Rating = 4 },
            new() { CompanyId = c[2].Id, NameAr = "الإسكندرية لأنظمة التتبع", Code = "V-ALXT", ContactPerson = "م. دينا سمير",
                    Phone = "01256789012", Email = "gps@alex-track.eg", Rating = 5 },
            new() { CompanyId = c[3].Id, NameAr = "الصعيد للآلات الزراعية", Code = "V-SAG", ContactPerson = "أ. محمود ربيع",
                    Phone = "01067890123", Email = "sales@saeed-machines.eg", Rating = 3 },
            new() { CompanyId = c[3].Id, NameAr = "وادي النيل للري الحديث", Code = "V-WNL", ContactPerson = "م. هالة نبيل",
                    Phone = "01178901234", Email = "irrigation@wadinile.eg", Rating = 4 }
        };

        db.Vendors.AddRange(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task<List<Category>> SeedCategoriesAsync(AppDbContext db, List<Company> comps)
    {
        var list = new List<Category>();

        foreach (var c in comps)
        {
            list.AddRange(new[]
            {
                new Category { CompanyId = c.Id, NameAr = "أجهزة حاسب ولاب توب", Code = "IT-PC", UsefulLifeYears = 4, SalvageRate = 0.10m, Icon = "bi-laptop" },
                new Category { CompanyId = c.Id, NameAr = "طابعات وماسحات", Code = "IT-PRN", UsefulLifeYears = 5, SalvageRate = 0.08m, Icon = "bi-printer" },
                new Category { CompanyId = c.Id, NameAr = "أجهزة شبكات", Code = "IT-NET", UsefulLifeYears = 6, SalvageRate = 0.05m, Icon = "bi-router" },
                new Category { CompanyId = c.Id, NameAr = "أثاث مكتبي", Code = "FRN", UsefulLifeYears = 10, SalvageRate = 0.15m, Icon = "bi-lamp" },
                new Category { CompanyId = c.Id, NameAr = "مركبات ومعدات نقل", Code = "VEH", UsefulLifeYears = 8, SalvageRate = 0.20m, Icon = "bi-truck" },
                new Category { CompanyId = c.Id, NameAr = "معدات وآلات", Code = "MCH", UsefulLifeYears = 12, SalvageRate = 0.10m, Icon = "bi-gear-wide-connected" }
            });
        }

        db.Categories.AddRange(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task SeedSlaPoliciesAsync(AppDbContext db, List<Company> comps)
    {
        var list = new List<SlaPolicy>();

        foreach (var c in comps)
        {
            list.AddRange(new[]
            {
                new SlaPolicy { CompanyId = c.Id, Priority = TicketPriority.Critical, NameAr = "حرجة", ResponseHours = 1, ResolutionHours = 4, EscalationWarningHours = 1 },
                new SlaPolicy { CompanyId = c.Id, Priority = TicketPriority.High, NameAr = "عالية", ResponseHours = 4, ResolutionHours = 24, EscalationWarningHours = 2 },
                new SlaPolicy { CompanyId = c.Id, Priority = TicketPriority.Medium, NameAr = "متوسطة", ResponseHours = 8, ResolutionHours = 72, EscalationWarningHours = 4 },
                new SlaPolicy { CompanyId = c.Id, Priority = TicketPriority.Low, NameAr = "منخفضة", ResponseHours = 24, ResolutionHours = 168, EscalationWarningHours = 8 }
            });
        }

        db.SlaPolicies.AddRange(list);
        await db.SaveChangesAsync();
    }

    private static async Task<List<ApplicationUser>> SeedUsersAsync(
        UserManager<ApplicationUser> um, List<Company> c, List<Department> d)
    {
        var defs = new (string email, string name, string role, int? companyIdx, int? deptIdx, string job)[]
        {
            ("admin@ats.eg", "أحمد محمود عبد الرحمن", Roles.Admin, null, null, "مدير النظام"),
            ("nile.manager@ats.eg", "هشام عبد العال", Roles.CompanyManager, 0, 0, "مدير عام"),
            ("nile.tech1@ats.eg", "مصطفى كامل", Roles.Technician, 0, 1, "فني دعم فني أول"),
            ("nile.tech2@ats.eg", "إسلام عبد الحميد", Roles.Technician, 0, 1, "فني صيانة"),
            ("nile.emp1@ats.eg", "رانيا الطوخي", Roles.Employee, 0, 0, "مهندسة مدنية"),
            ("nile.emp2@ats.eg", "كريم الشناوي", Roles.Employee, 0, 2, "محاسب"),
            ("nile.emp3@ats.eg", "نورهان فتحي", Roles.Employee, 0, 3, "أخصائية موارد بشرية"),
            ("delta.manager@ats.eg", "طارق الشاذلي", Roles.CompanyManager, 1, 4, "مدير المصنع"),
            ("delta.tech1@ats.eg", "عماد الدين حسن", Roles.Technician, 1, 6, "فني صيانة ميكانيكية"),
            ("delta.tech2@ats.eg", "شريف مرسي", Roles.Technician, 1, 7, "فني تقنية معلومات"),
            ("delta.emp1@ats.eg", "منال السيد", Roles.Employee, 1, 5, "مسؤولة جودة"),
            ("delta.emp2@ats.eg", "أيمن رزق", Roles.Employee, 1, 4, "مشرف إنتاج"),
            ("delta.emp3@ats.eg", "سلمى عادل", Roles.Employee, 1, 7, "محللة بيانات"),
            ("alex.manager@ats.eg", "عمرو بدير", Roles.CompanyManager, 2, 8, "مدير العمليات"),
            ("alex.tech1@ats.eg", "محمد الجندي", Roles.Technician, 2, 8, "فني أسطول"),
            ("alex.emp1@ats.eg", "هبة زكي", Roles.Employee, 2, 10, "أخصائية خدمة عملاء"),
            ("alex.emp2@ats.eg", "خالد عبد اللطيف", Roles.Employee, 2, 9, "أمين مخزن"),
            ("said.manager@ats.eg", "جمال سيد أحمد", Roles.CompanyManager, 3, 12, "مدير الفرع"),
            ("said.tech1@ats.eg", "أشرف قناوي", Roles.Technician, 3, 11, "فني معدات زراعية"),
            ("said.emp1@ats.eg", "ولاء ممدوح", Roles.Employee, 3, 12, "إدارية"),
            ("said.emp2@ats.eg", "سيد رمضان", Roles.Employee, 3, 11, "مشرف زراعي")
        };

        var created = new List<ApplicationUser>();
        var n = 1;
        _userRoles.Clear();

        foreach (var (email, name, role, ci, di, job) in defs)
        {
            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                JobTitle = job,
                EmployeeNumber = $"EMP-{n:D4}",
                CompanyId = ci.HasValue ? c[ci.Value].Id : null,
                DepartmentId = di.HasValue ? d[di.Value].Id : null,
                IsActive = true,
                PhoneNumber = $"010{Random.Shared.Next(10000000, 99999999)}"
            };

            var res = await um.CreateAsync(u, DefaultPassword);
            if (res.Succeeded)
            {
                await um.AddToRoleAsync(u, role);
                created.Add(u);
                _userRoles[u.Id] = role;
            }
            n++;
        }

        return created;
    }

    // خريطة (معرّف المستخدم ← الدور) تُستخدم داخل المُهيّئ فقط، حتى نُسند
    // التذاكر والعهد لأصحاب الأدوار الصحيحة دون استعلام Identity في كل دورة.
    private static readonly Dictionary<string, string> _userRoles = new();

    private static List<ApplicationUser> InRole(List<ApplicationUser> users, string role, int? companyId = null)
        => users.Where(u => _userRoles.TryGetValue(u.Id, out var r) && r == role
                            && (companyId == null || u.CompanyId == companyId))
                .ToList();

    private static async Task<List<Asset>> SeedAssetsAsync(AppDbContext db, List<Company> comps,
        List<Category> cats, List<Department> depts, List<Location> locs,
        List<Vendor> vendors, List<ApplicationUser> users)
    {
        // (اسم، ماركة، موديل، فئة (0..5)، قيمة الشراء)
        var templates = new (string name, string brand, string model, int catOffset, decimal price)[]
        {
            ("لاب توب ديل لاتيتيود", "Dell", "Latitude 5540", 0, 42000m),
            ("لاب توب لينوفو ثينك باد", "Lenovo", "ThinkPad E14", 0, 38500m),
            ("حاسب مكتبي إتش بي", "HP", "ProDesk 400 G9", 0, 27000m),
            ("شاشة سامسونج ٢٧ بوصة", "Samsung", "S27A600", 0, 9800m),
            ("طابعة ليزر إتش بي", "HP", "LaserJet Pro M428", 1, 15500m),
            ("ماسح ضوئي إبسون", "Epson", "DS-770 II", 1, 22000m),
            ("طابعة باركود زبرا", "Zebra", "ZT411", 1, 34000m),
            ("سويتش سيسكو ٢٤ منفذ", "Cisco", "Catalyst 2960X", 2, 56000m),
            ("راوتر ميكروتيك", "MikroTik", "CCR2004", 2, 31000m),
            ("نقطة وصول يوبيكيتي", "Ubiquiti", "UniFi U6 Pro", 2, 7400m),
            ("مكتب إداري خشبي", "الشرق", "EXEC-180", 3, 12500m),
            ("كرسي مكتبي طبي", "الشرق", "ERGO-PRO", 3, 6800m),
            ("خزانة ملفات معدنية", "الشرق", "FC-4D", 3, 5200m),
            ("سيارة تويوتا هايلوكس", "Toyota", "Hilux 2023", 4, 1450000m),
            ("شاحنة إيسوزو", "Isuzu", "NPR 75", 4, 1980000m),
            ("مقطورة نقل بضائع", "البحر المتوسط", "TR-40T", 4, 890000m),
            ("رافعة شوكية تويوتا", "Toyota", "8FGU25", 5, 720000m),
            ("مولد كهربائي كاتربيلر", "Caterpillar", "DE220 GC", 5, 1250000m),
            ("مضخة مياه صناعية", "Grundfos", "NB 80-250", 5, 168000m),
            ("ضاغط هواء أطلس", "Atlas Copco", "GA 30", 5, 445000m),
            ("جهاز تكييف مركزي", "Carrier", "38QUS048", 5, 98000m),
            ("ماكينة تعبئة أوتوماتيك", "دلتا", "FILL-2000", 5, 2350000m)
        };

        var assets = new List<Asset>();
        var rnd = new Random(20260820);
        var year = DateTime.UtcNow.Year;
        var seq = 1;

        for (var ci = 0; ci < comps.Count; ci++)
        {
            var comp = comps[ci];
            var compCats = cats.Where(x => x.CompanyId == comp.Id).ToList();
            var compDepts = depts.Where(x => x.CompanyId == comp.Id).ToList();
            var compLocs = locs.Where(x => x.CompanyId == comp.Id).ToList();
            var compVendors = vendors.Where(x => x.CompanyId == comp.Id).ToList();

            // ١٢ أصلاً للشركة الأولى، ثم ١٢/١٢/١٢
            var count = ci == 0 ? 15 : ci == 1 ? 13 : ci == 2 ? 11 : 9;

            for (var i = 0; i < count; i++)
            {
                var t = templates[(ci * 7 + i) % templates.Length];
                var cat = compCats[t.catOffset];
                var purchaseDate = new DateTime(year - rnd.Next(1, 5), rnd.Next(1, 13), rnd.Next(1, 28));
                var salvage = Math.Round(t.price * (cat.SalvageRate ?? 0.1m), 2);

                var a = new Asset
                {
                    CompanyId = comp.Id,
                    AssetTag = $"{AppConstants.AssetTagPrefix}-{year}-{seq:D5}",
                    NameAr = t.name,
                    Brand = t.brand,
                    Model = t.model,
                    SerialNumber = $"SN{rnd.Next(100000, 999999)}{(char)('A' + rnd.Next(0, 26))}",
                    CategoryId = cat.Id,
                    DepartmentId = compDepts.Count > 0 ? compDepts[i % compDepts.Count].Id : null,
                    LocationId = compLocs.Count > 0 ? compLocs[i % compLocs.Count].Id : null,
                    VendorId = compVendors.Count > 0 ? compVendors[i % compVendors.Count].Id : null,
                    PurchaseDate = purchaseDate,
                    PurchaseValue = t.price,
                    SalvageValue = salvage,
                    BookValue = t.price,
                    UsefulLifeYears = cat.UsefulLifeYears,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    InvoiceNumber = $"INV-{year - 1}-{rnd.Next(1000, 9999)}",
                    WarrantyStartDate = purchaseDate,
                    WarrantyEndDate = purchaseDate.AddYears(rnd.Next(1, 4)),
                    WarrantyProvider = compVendors.Count > 0 ? compVendors[i % compVendors.Count].NameAr : null,
                    Status = AssetStatus.Active,
                    Specifications = $"موديل {t.model} — حالة جيدة"
                };

                assets.Add(a);
                seq++;
            }
        }

        // توزيع الحالات لتغطية كل قيم الـEnum
        assets[3].Status = AssetStatus.InStore;
        assets[3].Notes = "مخزون احتياطي — جاهز للتسليم كعهدة";
        assets[17].Status = AssetStatus.InStore;
        assets[17].Notes = "مخزون احتياطي — جاهز للتسليم كعهدة";
        assets[30].Status = AssetStatus.InStore;
        assets[41].Status = AssetStatus.InStore;

        assets[6].Status = AssetStatus.UnderMaintenance;
        assets[22].Status = AssetStatus.UnderMaintenance;

        assets[11].Status = AssetStatus.Damaged;
        assets[11].Notes = "تلف في لوحة التحكم — بانتظار قرار الاستبعاد";
        assets[35].Status = AssetStatus.Damaged;

        assets[13].Status = AssetStatus.Disposed;
        assets[13].DisposalDate = DateTime.UtcNow.AddMonths(-2);
        assets[13].DisposalReason = "انتهاء العمر الافتراضي — تم البيع كخردة";

        assets[27].Status = AssetStatus.Lost;
        assets[27].Notes = "مفقود بعد جرد ٢٠٢٥ — تم عمل محضر";

        db.Assets.AddRange(assets);
        await db.SaveChangesAsync();
        return assets;
    }

    private static async Task SeedCustodyAsync(AppDbContext db, List<Asset> assets, List<ApplicationUser> users)
    {
        var logs = new List<CustodyLog>();
        var rnd = new Random(7);

        // العهد تُسلَّم للموظفين والفنيين (لا للمديرين ولا لمدير النظام)
        var employees = users.Where(u => u.CompanyId != null
                                         && _userRoles.TryGetValue(u.Id, out var r)
                                         && (r == Roles.Employee || r == Roles.Technician))
                             .ToList();

        foreach (var comp in assets.GroupBy(a => a.CompanyId))
        {
            var compEmps = employees.Where(u => u.CompanyId == comp.Key).ToList();
            if (compEmps.Count == 0) continue;

            var eligible = comp.Where(a => a.Status == AssetStatus.Active).Take(5).ToList();

            for (var i = 0; i < eligible.Count; i++)
            {
                var a = eligible[i];
                var emp = compEmps[i % compEmps.Count];

                // ٣ مقبولة (عهدة فعلية) + ١ معلّقة + ١ مرفوضة
                var status = i < 3 ? CustodyStatus.Accepted
                           : i == 3 ? CustodyStatus.Pending
                           : CustodyStatus.Rejected;

                var when = DateTime.UtcNow.AddDays(-rnd.Next(10, 200));

                logs.Add(new CustodyLog
                {
                    CompanyId = a.CompanyId,
                    AssetId = a.Id,
                    Action = CustodyAction.Assign,
                    Status = status,
                    NewUserId = emp.Id,
                    AssignedByUserId = (InRole(users, Roles.CompanyManager, comp.Key).FirstOrDefault()
                                        ?? users.First(u => u.CompanyId == null)).Id,
                    ActionDate = when,
                    RespondedAt = status == CustodyStatus.Pending ? null : when.AddHours(rnd.Next(1, 48)),
                    Reason = "تسليم عهدة للاستخدام الوظيفي",
                    ResponseNote = status == CustodyStatus.Rejected ? "الجهاز به عيب ظاهر عند الاستلام" : null
                });

                if (status == CustodyStatus.Accepted)
                {
                    a.CurrentCustodyUserId = emp.Id;
                    a.CustodySince = when.AddHours(2);
                }
            }
        }

        db.CustodyLogs.AddRange(logs);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTicketsAsync(AppDbContext db, List<Asset> assets, List<ApplicationUser> users)
    {
        var titles = new[]
        {
            ("الجهاز لا يعمل نهائياً", "عند الضغط على زر التشغيل لا يستجيب الجهاز إطلاقاً.", TicketPriority.Critical),
            ("بطء شديد في الأداء", "الجهاز يستغرق وقتاً طويلاً في فتح البرامج.", TicketPriority.Medium),
            ("صوت غير طبيعي من المحرك", "يوجد صوت طقطقة عند التشغيل يستدعي الفحص.", TicketPriority.High),
            ("تسريب زيت", "لوحظ تسريب زيت أسفل الوحدة.", TicketPriority.High),
            ("الطابعة لا تسحب الورق", "الورق يتوقف في المدخل ويحدث انحشار متكرر.", TicketPriority.Medium),
            ("انقطاع متكرر في الشبكة", "الاتصال ينقطع كل عدة دقائق.", TicketPriority.High),
            ("شاشة بها خطوط رأسية", "ظهور خطوط ملوّنة تعيق العمل.", TicketPriority.Low),
            ("طلب صيانة دورية", "حسب الجدول الوقائي المتفق عليه.", TicketPriority.Low),
            ("ارتفاع حرارة غير معتاد", "الوحدة تسخن بشكل زائد بعد ساعة من التشغيل.", TicketPriority.Critical),
            ("عجلة أمامية تحتاج استبدال", "تآكل واضح في الإطار الأمامي الأيمن.", TicketPriority.Medium)
        };

        var statuses = new[]
        {
            TicketStatus.Open, TicketStatus.Open,
            TicketStatus.Assigned, TicketStatus.Assigned,
            TicketStatus.InProgress, TicketStatus.InProgress, TicketStatus.InProgress, TicketStatus.InProgress,
            TicketStatus.WaitingParts, TicketStatus.WaitingParts,
            TicketStatus.Resolved, TicketStatus.Resolved, TicketStatus.Resolved,
            TicketStatus.Closed, TicketStatus.Closed, TicketStatus.Closed,
            TicketStatus.Closed, TicketStatus.Closed, TicketStatus.Closed,
            TicketStatus.Cancelled
        };

        var rnd = new Random(99);
        var year = DateTime.UtcNow.Year;
        var eligible = assets.Where(a => a.Status != AssetStatus.Disposed).ToList();

        var tickets = new List<MaintenanceTicket>();

        for (var i = 0; i < statuses.Length; i++)
        {
            var asset = eligible[(i * 3) % eligible.Count];
            var (title, desc, prio) = titles[i % titles.Length];

            // الفنيون فقط هم من تُسند إليهم التذاكر، والمُبلِّغون موظفون —
            // هكذا تبدو البيانات التجريبية منطقية وتظهر لوحة الفني ممتلئة.
            var techs = InRole(users, Roles.Technician, asset.CompanyId);
            if (techs.Count == 0) techs = InRole(users, Roles.Technician);

            var requesters = InRole(users, Roles.Employee, asset.CompanyId);
            if (requesters.Count == 0) requesters = InRole(users, Roles.Employee);

            var tech = techs.Count > 0 ? techs[i % techs.Count] : users[0];
            var requester = requesters.Count > 0 ? requesters[i % requesters.Count] : users[0];

            var reported = DateTime.UtcNow.AddDays(-rnd.Next(1, 90)).AddHours(-rnd.Next(0, 20));
            var st = statuses[i];

            var resolutionHours = prio switch
            {
                TicketPriority.Critical => 4,
                TicketPriority.High => 24,
                TicketPriority.Medium => 72,
                _ => 168
            };

            var t = new MaintenanceTicket
            {
                CompanyId = asset.CompanyId,
                TicketNumber = $"{AppConstants.TicketNumberPrefix}-{year}-{(i + 1):D5}",
                AssetId = asset.Id,
                Title = title,
                Description = desc,
                Type = i % 7 == 0 ? TicketType.Preventive : TicketType.Corrective,
                Priority = prio,
                Status = st,
                RequestedByUserId = requester.Id,
                AssignedTechnicianId = st == TicketStatus.Open ? null : tech.Id,
                ReportedAt = reported,
                ResponseDueAt = reported.AddHours(prio == TicketPriority.Critical ? 1 : 4),
                ResolutionDueAt = reported.AddHours(resolutionHours)
            };

            if (st != TicketStatus.Open)
                t.FirstRespondedAt = reported.AddHours(rnd.Next(1, 6));

            if (st is TicketStatus.Resolved or TicketStatus.Closed)
            {
                t.ResolvedAt = reported.AddHours(rnd.Next(5, 100));
                t.Resolution = "تم الإصلاح واستبدال الأجزاء التالفة واختبار التشغيل بنجاح.";
                t.RootCause = "تآكل طبيعي نتيجة الاستخدام المستمر.";
                t.LaborCost = rnd.Next(300, 3000);
                t.PartsCost = rnd.Next(500, 12000);
                t.TotalCost = t.LaborCost + t.PartsCost;
                t.SatisfactionRating = rnd.Next(3, 6);
            }

            if (st == TicketStatus.Closed)
            {
                t.ClosedAt = t.ResolvedAt!.Value.AddHours(rnd.Next(1, 48));
                t.ClosedByUserId = tech.Id;
            }

            // ٥ تذاكر متجاوزة SLA
            if (i is 4 or 5 or 8 or 9 or 2 && st != TicketStatus.Closed)
            {
                t.ResolutionDueAt = DateTime.UtcNow.AddDays(-rnd.Next(1, 10));
                t.IsSlaBreached = true;
                t.SlaBreachedAt = t.ResolutionDueAt;
                t.IsEscalated = true;
            }

            tickets.Add(t);
        }

        db.MaintenanceTickets.AddRange(tickets);
        await db.SaveChangesAsync();

        // تعليقات + سجلات + قطع غيار
        var comments = new List<TicketComment>();
        var tlogs = new List<TicketLog>();
        var parts = new List<TicketPart>();

        var bodies = new[]
        {
            "تم استلام التذكرة وجارٍ الفحص المبدئي.",
            "تحتاج قطعة غيار غير متوفرة حالياً — تم طلبها من المورّد.",
            "شكراً على السرعة في الاستجابة.",
            "تم الفحص وتحديد سبب العطل.",
            "برجاء إبلاغي عند الانتهاء لأتمكن من استخدام الجهاز."
        };

        var partNames = new[]
        {
            ("مروحة تبريد", 450m), ("لوحة تحكم", 3200m), ("فلتر زيت", 280m),
            ("سير نقل حركة", 950m), ("بطارية", 1800m), ("كابل شبكة CAT6", 120m),
            ("أسطوانة طابعة", 2400m)
        };

        var ti = 0;
        foreach (var t in tickets)
        {
            var cCount = ti % 3 + 1;
            for (var k = 0; k < cCount; k++)
            {
                comments.Add(new TicketComment
                {
                    CompanyId = t.CompanyId,
                    TicketId = t.Id,
                    AuthorUserId = t.AssignedTechnicianId ?? t.RequestedByUserId,
                    Body = bodies[(ti + k) % bodies.Length],
                    IsInternal = k == 1
                });
            }

            tlogs.Add(new TicketLog
            {
                CompanyId = t.CompanyId, TicketId = t.Id, Action = "Created",
                ToValue = "Open", ByUserId = t.RequestedByUserId, OccurredAt = t.ReportedAt
            });

            if (t.AssignedTechnicianId != null)
            {
                tlogs.Add(new TicketLog
                {
                    CompanyId = t.CompanyId, TicketId = t.Id, Action = "Assigned",
                    FromValue = "Open", ToValue = "Assigned",
                    ByUserId = t.RequestedByUserId, OccurredAt = t.ReportedAt.AddHours(1)
                });
            }

            if (t.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                tlogs.Add(new TicketLog
                {
                    CompanyId = t.CompanyId, TicketId = t.Id, Action = "Resolved",
                    ToValue = "Resolved", ByUserId = t.AssignedTechnicianId, OccurredAt = t.ResolvedAt!.Value
                });

                var (pn, pp) = partNames[ti % partNames.Length];
                var qty = (ti % 2) + 1;
                parts.Add(new TicketPart
                {
                    CompanyId = t.CompanyId, TicketId = t.Id,
                    PartName = pn, PartNumber = $"P-{1000 + ti}",
                    Quantity = qty, UnitPrice = pp, TotalPrice = pp * qty
                });
            }

            ti++;
        }

        db.TicketComments.AddRange(comments);
        db.TicketLogs.AddRange(tlogs);
        db.TicketParts.AddRange(parts);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSchedulesAsync(AppDbContext db, List<Asset> assets, List<ApplicationUser> users)
    {
        var list = new List<MaintenanceSchedule>();
        var rnd = new Random(31);

        var machines = assets
            .Where(a => a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Lost)
            .Take(14).ToList();

        foreach (var a in machines)
        {
            // الصيانة الوقائية مسؤولية فني من نفس الشركة
            var tech = InRole(users, Roles.Technician, a.CompanyId).FirstOrDefault()
                       ?? InRole(users, Roles.Technician).FirstOrDefault();
            var start = DateTime.UtcNow.AddMonths(-rnd.Next(2, 12)).Date;
            var freq = (ScheduleFrequency)rnd.Next(2, 7);

            list.Add(new MaintenanceSchedule
            {
                CompanyId = a.CompanyId,
                AssetId = a.Id,
                Title = $"صيانة دورية — {a.NameAr}",
                Description = "فحص شامل وتنظيف وتزييت واختبار تشغيل.",
                Checklist = "١) فحص بصري\n٢) تنظيف\n٣) تزييت\n٤) اختبار تشغيل\n٥) توثيق النتائج",
                Frequency = freq,
                Status = ScheduleStatus.Active,
                StartDate = start,
                NextDueDate = DateTime.UtcNow.AddDays(rnd.Next(-5, 40)).Date,
                LeadTimeDays = 3,
                DefaultTechnicianId = tech?.Id,
                DefaultPriority = TicketPriority.Medium,
                EstimatedCost = rnd.Next(500, 8000)
            });
        }

        db.MaintenanceSchedules.AddRange(list);
        await db.SaveChangesAsync();
    }

    private static async Task SeedDepreciationAsync(AppDbContext db, List<Asset> assets)
    {
        var entries = new List<DepreciationEntry>();

        // ٤ أشهر إهلاك رجعي لكل أصل مملوك
        var eligible = assets.Where(a =>
            a.Status != AssetStatus.Disposed &&
            a.Status != AssetStatus.Lost &&
            a.PurchaseDate != null &&
            a.PurchaseValue > 0).ToList();

        foreach (var a in eligible)
        {
            var purchase = a.PurchaseValue!.Value;
            var salvage = a.SalvageValue ?? 0m;
            var life = a.UsefulLifeYears ?? 5;
            var monthly = Math.Round((purchase - salvage) / (life * 12m), 2);

            var book = purchase;

            for (var back = 4; back >= 1; back--)
            {
                var when = DateTime.UtcNow.AddMonths(-back);
                if (book <= salvage) break;

                var amount = monthly;
                if (book - amount < salvage) amount = book - salvage;
                if (amount <= 0) break;

                var closing = Math.Round(book - amount, 2);

                entries.Add(new DepreciationEntry
                {
                    CompanyId = a.CompanyId,
                    AssetId = a.Id,
                    Year = when.Year,
                    Month = when.Month,
                    OpeningValue = book,
                    DepreciationAmount = amount,
                    ClosingValue = closing,
                    Method = DepreciationMethod.StraightLine,
                    CalculatedAt = new DateTime(when.Year, when.Month, 1)
                });

                book = closing;
            }

            a.BookValue = book;
        }

        db.DepreciationEntries.AddRange(entries);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAuditsAsync(AppDbContext db, List<Asset> assets,
        List<Location> locs, List<ApplicationUser> users)
    {
        var audits = new List<InventoryAudit>();
        var year = DateTime.UtcNow.Year;
        var rnd = new Random(55);
        var n = 1;

        foreach (var g in assets.GroupBy(a => a.CompanyId).Take(4))
        {
            var loc = locs.FirstOrDefault(l => l.CompanyId == g.Key);
            // الجرد يقوده مدير الشركة
            var resp = InRole(users, Roles.CompanyManager, g.Key).FirstOrDefault()
                       ?? users.FirstOrDefault(u => u.CompanyId == g.Key);

            var status = n switch
            {
                1 => AuditStatus.Completed,
                2 => AuditStatus.InProgress,
                3 => AuditStatus.Draft,
                _ => AuditStatus.Completed
            };

            var a = new InventoryAudit
            {
                CompanyId = g.Key,
                Code = $"{AppConstants.AuditCodePrefix}-{year}-{n:D5}",
                Title = $"جرد {(status == AuditStatus.Completed ? "سنوي" : "دوري")} — {loc?.NameAr ?? "عام"}",
                Description = "جرد فعلي بمسح أكواد QR ومطابقة المواقع.",
                LocationId = loc?.Id,
                Status = status,
                ScheduledDate = DateTime.UtcNow.AddDays(-rnd.Next(5, 60)).Date,
                ResponsibleUserId = resp?.Id
            };

            if (status != AuditStatus.Draft) a.StartedAt = a.ScheduledDate.AddHours(9);
            if (status == AuditStatus.Completed) a.CompletedAt = a.ScheduledDate.AddHours(16);

            audits.Add(a);
            n++;
        }

        // جرد خامس ملغي
        audits.Add(new InventoryAudit
        {
            CompanyId = assets[0].CompanyId,
            Code = $"{AppConstants.AuditCodePrefix}-{year}-{n:D5}",
            Title = "جرد استثنائي — مؤجَّل",
            Status = AuditStatus.Cancelled,
            ScheduledDate = DateTime.UtcNow.AddDays(-90).Date,
            Notes = "تم التأجيل لظروف تشغيلية."
        });

        db.InventoryAudits.AddRange(audits);
        await db.SaveChangesAsync();

        // بنود الجرد
        var items = new List<InventoryAuditItem>();

        foreach (var au in audits.Where(x => x.Status != AuditStatus.Draft && x.Status != AuditStatus.Cancelled))
        {
            var compAssets = assets.Where(a => a.CompanyId == au.CompanyId).Take(8).ToList();
            var k = 0;

            foreach (var a in compAssets)
            {
                var result = au.Status == AuditStatus.Completed
                    ? (k % 7 == 0 ? AuditItemResult.Missing
                       : k % 5 == 0 ? AuditItemResult.Misplaced
                       : AuditItemResult.Found)
                    : (k < 4 ? AuditItemResult.Found : AuditItemResult.Pending);

                items.Add(new InventoryAuditItem
                {
                    CompanyId = au.CompanyId,
                    AuditId = au.Id,
                    AssetId = a.Id,
                    Result = result,
                    ExpectedLocationId = a.LocationId,
                    ActualLocationId = result == AuditItemResult.Misplaced ? null : a.LocationId,
                    ScannedAt = result == AuditItemResult.Pending ? null : au.StartedAt?.AddMinutes(k * 12),
                    ScannedByUserId = au.ResponsibleUserId
                });
                k++;
            }

            au.TotalExpected = compAssets.Count;
            au.TotalScanned = items.Count(x => x.AuditId == au.Id && x.Result != AuditItemResult.Pending);
            au.TotalMissing = items.Count(x => x.AuditId == au.Id && x.Result == AuditItemResult.Missing);
        }

        db.InventoryAuditItems.AddRange(items);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSettingsAsync(AppDbContext db)
    {
        db.SystemSettings.AddRange(
            new SystemSetting { Key = SettingKeys.AppName, Value = "نظام إدارة وتتبع الأصول والدعم الفني", Category = "General", Description = "اسم النظام" },
            new SystemSetting { Key = SettingKeys.Currency, Value = AppConstants.CurrencySymbol, Category = "General", Description = "رمز العملة" },
            new SystemSetting { Key = SettingKeys.AppBaseUrl, Value = "https://localhost:5001", Category = "General", Description = "الرابط الأساسي (يُستخدم في QR)" },
            new SystemSetting { Key = SettingKeys.SmtpHost, Value = "", Category = "Smtp", Description = "خادم البريد" },
            new SystemSetting { Key = SettingKeys.SmtpPort, Value = "587", Category = "Smtp", Description = "منفذ البريد" },
            new SystemSetting { Key = SettingKeys.SmtpUser, Value = "", Category = "Smtp", Description = "مستخدم البريد" },
            new SystemSetting { Key = SettingKeys.SmtpPassword, Value = "", Category = "Smtp", Description = "كلمة مرور البريد", IsSecret = true },
            new SystemSetting { Key = SettingKeys.SmtpEnableSsl, Value = "true", Category = "Smtp", Description = "تشفير SSL/TLS" },
            new SystemSetting { Key = SettingKeys.SmtpFromName, Value = "نظام إدارة الأصول", Category = "Smtp", Description = "اسم المُرسل" },
            new SystemSetting { Key = SettingKeys.NotificationRetentionDays, Value = "90", Category = "General", Description = "مدة الاحتفاظ بالإشعارات المقروءة" }
        );

        await db.SaveChangesAsync();
    }
}
