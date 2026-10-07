/* ═══════════════════════════════════════════════════════════════
   نظام إدارة الأصول — تبديل الثيم (داكن/فاتح) واللغة (عربي/إنجليزي)
   يُخزَّن الإعداد في localStorage ويُطبَّق فور تحميل الصفحة
   ترجمة شاملة وتلقائية لكافة النصوص الثابتة في كافة الصفحات
   تحديث ديناميكي فوري بدون وميض وبدون الحاجة لإعادة تحميل الصفحة
   ═══════════════════════════════════════════════════════════════ */

(function () {
    'use strict';

    /* ─────────────────────────── قاموس الترجمات ──────────────────────────── */
    var T = {
        // قاموس التحويل من العربية إلى الإنجليزية
        ar2en: {
            // ── الشريط العلوي وعناصر التنقل
            'نظام إدارة الأصول': 'Asset Management System',
            'ابحث بكود الأصل أو الاسم…': 'Search by asset code or name…',
            'الإشعارات': 'Notifications',
            'تحديد الكل كمقروء': 'Mark all as read',
            'عرض كل الإشعارات': 'View all notifications',
            'جارٍ التحميل…': 'Loading…',
            'الملف الشخصي': 'My Profile',
            'تغيير كلمة المرور': 'Change Password',
            'تسجيل الخروج': 'Sign Out',
            'مسح QR': 'Scan QR',

            // ── أدوار المستخدمين
            'مدير النظام': 'System Admin',
            'مدير شركة': 'Company Manager',
            'فني دعم': 'Support Tech',
            'موظف': 'Employee',

            // ── التنقل الجانبي
            'الرئيسية': 'Home',
            'الأصول': 'Assets',
            'المخازن': 'Warehouses',
            'العهد': 'Custody',
            'الدعم الفني': 'Support',
            'التقارير': 'Reports',
            'الإعدادات': 'Settings',
            'الجرد': 'Inventory',
            'المخزون': 'Warehouse',
            'الجدولة': 'Scheduling',
            'الإشعارات': 'Notifications',

            // ── لوحة المعلومات
            'لوحة المعلومات': 'Dashboard',
            'نحو إدارة أذكى للأصول': 'Smarter Asset Management',
            'لا مهام عالقة اليوم — يوم مثير!': 'No pending tasks today — Great job!',
            'لا مهام قادمة — كل شيء تحت السيطرة': 'No upcoming tasks — Everything under control',
            'إجراءات سريعة': 'Quick Actions',
            'إصدار عهدة': 'Issue Custody',
            'إضافة أصل جديد': 'Add New Asset',
            'طلب دعم فني': 'Request Support',
            'استعلام عن أصل': 'Scan / Lookup Asset',
            'آخر الأصول المضافة': 'Recently Added Assets',
            'عرض الكل': 'View All',
            'المهام والمواعيد القادمة': 'Upcoming Tasks & Due Dates',
            'معلومات سريعة': 'Quick Info',
            'أصول تحتاج صيانة': 'Assets Needing Maintenance',
            'أصول خارج الخدمة': 'Out of Service Assets',
            'عمليات منتهية': 'Completed Operations',
            'أحدث طلبات الدعم الفني': 'Recent Support Tickets',
            'لا توجد أصول بعد': 'No assets yet',
            'لا توجد تذاكر بعد': 'No tickets yet',
            'لا توجد مواعيد قادمة': 'No upcoming dates',

            // ── صفحة الأصول
            'الأصول': 'Assets',
            'إدارة ومتابعة جميع أصول الشركة': 'Manage and track all company assets',
            'أصل جديد': 'New Asset',
            'طباعة ملصقات': 'Print Labels',
            'إجمالي الأصول': 'Total Assets',
            'الأصول النشطة': 'Active Assets',
            'قيد الاستخدام': 'In Use',
            'جميع الفئات': 'All Categories',
            'مُفلتَر': 'Filtered',
            'مُفلتَرة': 'Filtered',
            'إلغاء الفلاتر': 'Clear Filters',
            'إجمالي الشراء': 'Total Purchase',
            'الدفترية': 'Book Value',
            'لا توجد أصول مطابقة للفلاتر المختارة.': 'No assets match the selected filters.',
            'الوسم': 'Tag',
            'الأصل': 'Asset',
            'التصنيف': 'Category',
            'الموقع': 'Location',
            'الحالة': 'Status',
            'العهدة': 'Custody',
            'قيمة الشراء': 'Purchase Value',
            'الضمان': 'Warranty',
            'تذاكر مفتوحة': 'Open Tickets',
            'التفاصيل': 'Details',

            // ── فلاتر الأصول
            'بحث': 'Search',
            'الوسم، الاسم، الرقم التسلسلي، الماركة…': 'Tag, name, serial, brand…',
            'الكل': 'All',
            'تطبيق': 'Apply',
            'إعادة تعيين': 'Reset',

            // ── حالات الأصول
            'نشط': 'Active',
            'في المخزن': 'In Storage',
            'تحت الصيانة': 'Under Maintenance',
            'تالف': 'Damaged',
            'مستبعد': 'Disposed',
            'مفقود': 'Lost',
            'قيد الصيانة': 'Under Maintenance',

            // ── صفحة الأصول — إحصائيات
            'أصول تحتاج متابعة': 'Assets Needing Follow-up',
            'ضمان ينتهي قريبًا': 'Warranty Expiring Soon',
            'ضمانات تنتهي قريباً': 'Warranties Expiring Soon',

            // ── تفاصيل الأصل
            'بيانات الأصل': 'Asset Details',
            'البيانات الأساسية': 'Basic Information',
            'اسم الأصل': 'Asset Name',
            'النوع': 'Category',
            'الماركة': 'Brand',
            'الموديل': 'Model',
            'الرقم التسلسلي': 'Serial Number',
            'تاريخ الشراء': 'Purchase Date',
            'تاريخ الإضافة': 'Date Added',
            'سعر الشراء': 'Purchase Price',
            'تاريخ انتهاء الضمان': 'Warranty Expiry Date',
            'الإدارة': 'Department',
            'الوصف': 'Description',
            'ملاحظات': 'Notes',
            'تاريخ الضمان': 'Warranty Date',
            'الإجراءات': 'Actions',
            'تعديل': 'Edit',
            'حذف': 'Delete',
            'حذف الأصل': 'Delete Asset',
            'هل أنت متأكد؟': 'Are you sure?',
            'سجل العمليات': 'Operation Log',
            'عهد الأصل': 'Asset Custody',
            'تسليم عهدة': 'Assign Custody',
            'استرجاع العهدة': 'Return Custody',
            'رُدَّ': 'Responded',
            'المخزن': 'Warehouse',
            'رقم الأصل': 'Asset No.',
            'كود الأصل': 'Asset Code',
            'تصدير': 'Export',

            // ── صفحة إنشاء/تعديل الأصل
            'إضافة أصل': 'Add Asset',
            'إنشاء أصل جديد': 'Create New Asset',
            'حفظ': 'Save',
            'إلغاء': 'Cancel',
            'حفظ التعديلات': 'Save Changes',
            'تعديل الأصل': 'Edit Asset',

            // ── العهد
            'إدارة العهد': 'Custody Management',
            'عهد الموظفين': 'Employee Custodies',
            'متابعة وتسليم واسترجاع عهد الأصول بين الموظفين': 'Track, assign and return asset custody between employees',
            'تسليم عهدة جديدة': 'Assign New Custody',
            'تصدير التقرير': 'Export Report',
            'إجمالي حركات العهد': 'Total Custody Movements',
            'بانتظار موافقة الموظف': 'Awaiting Employee Approval',
            'مقبولة': 'Accepted',
            'مرفوضة': 'Rejected',
            'تم الإرجاع': 'Returned',
            'بانتظار الموافقة': 'Pending Approval',
            'الحركة': 'Movement',
            'نوع الحركة': 'Movement Type',
            'من': 'From',
            'إلى': 'To',
            'سلّمها': 'Assigned By',
            'التاريخ': 'Date',
            'ملاحظة': 'Note',
            'رقم الحركة': 'Movement No.',
            'تسليم': 'Assign',
            'نقل': 'Transfer',
            'استرجاع': 'Return',
            'لا توجد حركات عهدة مطابقة.': 'No custody movements found.',
            'من تاريخ': 'From Date',
            'إلى تاريخ': 'To Date',

            // ── الحالات في العهد
            'عهدي': 'My Custody',
            'الأصول في عهدتي': 'Assets in My Custody',
            'أصل مسجّل باسمك': 'Asset registered in your name',
            'عهد بانتظار موافقتك': 'Custodies Pending Your Approval',
            'تحتاج قبول أو رفض': 'Requires acceptance or rejection',

            // ── التذاكر
            'تذاكر الصيانة': 'Maintenance Tickets',
            'تذكرة جديدة': 'New Ticket',
            'تذكرة': 'Ticket',
            'تذاكر': 'Tickets',
            'مفتوحة (جديدة)': 'Open (New)',
            'قيد المعالجة': 'In Progress',
            'خارقة لـ SLA': 'SLA Breached',
            'بدون فني': 'Unassigned',
            'ماكينات متوقفة الآن': 'Machines Currently Down',
            'إجمالي زمن التوقف': 'Total Downtime',
            'الكل': 'All',
            'غير المغلقة': 'Open',
            'المُكلَّف بها': 'Assigned to Me',
            'طلباتي': 'My Requests',
            'متأخرة': 'Breached',
            'متوقفة الآن': 'Down Now',
            'رقم التذكرة': 'Ticket No.',
            'الموضوع': 'Subject',
            'خط الإنتاج': 'Production Line',
            'فئة العطل': 'Fault Category',
            'الأولوية': 'Priority',
            'زمن التوقف': 'Downtime',
            'الفني': 'Technician',
            'المُبلِّغ': 'Reporter',
            'موعد الحل': 'Due Date',
            'لا توجد تذاكر مطابقة.': 'No tickets found.',
            'فتح تذكرة جديدة': 'Open New Ticket',
            'غير مُكلَّفة': 'Unassigned',
            'متأخرة': 'Overdue',
            'باقي': 'Remaining',
            'ساعة': 'hours',
            'دقيقة': 'min',

            // ── أولويات التذاكر
            'منخفضة': 'Low',
            'متوسطة': 'Medium',
            'عالية': 'High',
            'حرجة': 'Critical',

            // ── حالات التذاكر
            'مفتوحة': 'Open',
            'مُكلَّف بها': 'Assigned',
            'جاري العمل': 'In Progress',
            'بانتظار قطع غيار': 'Waiting for Parts',
            'تم الحل': 'Resolved',
            'مغلقة': 'Closed',
            'ملغاة': 'Cancelled',
            'قيد التنفيذ': 'In Progress',
            'منجز': 'Resolved',
            'مجدول': 'Scheduled',

            // ── فئات الأعطال
            'ميكانيكي / تآكل واهتراء': 'Mechanical / Wear & Tear',
            'كهربائي / تأسيسات': 'Electrical / Wiring',
            'حساسات / معايرة بصرية': 'Sensors / Visual Calibration',
            'برمجيات / اتصال MES': 'Software / MES Connection',
            'هوائي / هيدروليكي': 'Pneumatic / Hydraulic',
            'ضبط ومعايرة': 'Tuning & Calibration',
            'مستهلكات': 'Consumables',
            'أخرى': 'Other',

            // ── المخازن
            'إدارة المخازن': 'Warehouse Management',
            'مخازن الشركة والأصول المتواجدة داخل كل مخزن': 'Company warehouses and assets inside each warehouse',
            'إضافة مخزن جديد': 'Add New Warehouse',
            'إجمالي المخازن': 'Total Warehouses',
            'إجمالي الأصول بالمخازن': 'Total Assets in Warehouses',
            'أصول تحت الصيانة': 'Assets Under Maintenance',
            'أصول بدون مخزن': 'Assets Without Warehouse',
            'المخزن': 'Warehouse',
            'الكود': 'Code',
            'الأصول داخله': 'Assets Inside',
            'القيمة الإجمالية': 'Total Value',
            'لا توجد مخازن بعد — أضف أول مخزن من الزر الأعلى': 'No warehouses yet — add the first warehouse using the button above',
            'إضافة مخزن': 'Add Warehouse',
            'عرض كل الأصول': 'View All Assets',
            'جرد المخازن': 'Inventory Audit',
            'معلومات المخازن': 'Warehouse Information',
            'الأصول موزعة كالتالي:': 'Assets are distributed as follows:',
            'إجمالي الأصول في المخزن': 'Total Assets in Warehouse',

            // ── نوع الموقع
            'مكتب': 'Office',
            'مصنع': 'Factory',
            'مخزن': 'Warehouse',
            'مبنى': 'Building',
            'سكن': 'Residential',
            'فرع': 'Branch',

            // ── التقارير
            'التقارير': 'Reports',
            'تقارير تحليلية جاهزة للتصدير إلى Excel': 'Analytical reports ready for Excel export',
            'كل الشركات': 'All Companies',
            'إحصائيات عامة': 'General Statistics',
            'مؤشرات الأداء': 'Performance Indicators',
            'التوزيعات والتحليلات': 'Distributions & Analysis',
            'عدد الأصول': 'Asset Count',
            'غير المستبعدة': 'Excluding Disposed',
            'إجمالي قيمة الشراء': 'Total Purchase Value',
            'القيمة الدفترية الحالية': 'Current Book Value',
            'القيمة الدفترية:': 'Book Value:',
            'مجمّع الإهلاك': 'Accumulated Depreciation',
            'فرق الشراء عن الدفتري': 'Purchase vs Book Value Difference',
            'تذاكر الصيانة': 'Maintenance Tickets',
            'مفتوحة الآن:': 'Open Now:',
            'تكاليف الصيانة': 'Maintenance Costs',
            'أجور + قطع غيار': 'Labor + Spare Parts',
            'أصول بعهدة موظفين': 'Assets with Employees',
            'عهد سارية': 'Active Custodies',
            'خلال 60 يوماً': 'Within 60 days',
            'عمليات جرد مكتملة': 'Completed Inventory Audits',
            'أرشيف الجرد': 'Audit Archive',
            'التقارير المتاحة': 'Available Reports',
            'تقرير الأصول': 'Assets Report',
            'تقرير التذاكر': 'Tickets Report',
            'تقرير العهد': 'Custody Report',
            'تقرير الإهلاك': 'Depreciation Report',
            'عرض': 'View',
            'تصدير Excel': 'Export Excel',
            'جرد كامل للأصول مع التصنيف والموقع والإدارة والحالة': 'Full inventory of assets with category, location, department and status',
            'وحائز العهدة. فلاتر: الحالة، التصنيف، الموقع، فترة الشراء.': 'and custody holder. Filters: status, category, location, purchase period.',
            'أداء الدعم الفني: نسبة الالتزام بـ SLA، متوسط زمن الحل، التكاليف،': 'Support performance: SLA compliance rate, avg resolution time, costs,',
            'وتوزيع التذاكر على الفنيين. فلاتر: الحالة، النوع، الفني، الفترة.': 'and ticket distribution by technician. Filters: status, type, technician, period.',
            'سجل حركات تسليم واستلام وإرجاع العهد، بالإضافة إلى ملخّص': 'Record of custody assignment, transfer and return movements, plus summary',
            'العهد الحالية لكل موظف وقيمتها. فلاتر: الموظف، الحالة، الفترة.': 'of current custodies per employee with values. Filters: employee, status, period.',
            'القيمة الدفترية ومجمّع الإهلاك والقسط السنوي لكل أصل،': 'Book value, accumulated depreciation and annual installment per asset,',
            'مع نسبة الإهلاك وطريقة الحساب. فلتر: التصنيف.': 'with depreciation rate and calculation method. Filter: category.',
            'معلومات سريعة — حالة الأصول': 'Quick Info — Asset Status',
            'التذاكر حسب الأولوية': 'Tickets by Priority',
            'لا توجد تذاكر مفتوحة': 'No open tickets',
            'Assets by Category': 'Assets by Category',
            'كل تقرير يمكن تصديره إلى ملف Excel بصيغة': 'Each report can be exported to an Excel file in',
            'مع صفحة مُهيّأة من اليمين إلى اليسار وصفّ ملخّص في أعلى الملف. الفلاتر المُطبّقة على الشاشة': 'format with RTL-configured sheet and summary row at the top. Filters applied on screen',
            'تُطبَّق أيضاً على الملف المُصدَّر.': 'are also applied to the exported file.',

            // ── إحصائيات الداشبورد
            'طلبات الدعم الفني': 'Support Tickets',
            'في انتظار المعالجة': 'Pending Processing',
            'الأصول المستحقة للصيانة': 'Assets Due for Maintenance',
            'خلال 7 أيام قادمة': 'Within Next 7 Days',
            'خلال ٧ أيام': 'Within 7 days',
            'خلال ٣٠ يوماً': 'Within 30 days',
            'مؤشر أداء الدعم': 'Support KPI',
            'الأصول حسب الحالة': 'Assets by Status',
            'الأصول حسب التصنيف': 'Assets by Category',
            'التذاكر النشطة حسب الأولوية': 'Active Tickets by Priority',
            'تذاكري حسب الأولوية': 'My Tickets by Priority',
            'قيمة الشراء الإجمالية': 'Total Purchase Value',
            'تذاكر مفتوحة': 'Open Tickets',
            'تذاكر حُلّت هذا الشهر': 'Tickets Resolved This Month',

            // ── الإدارة والإعدادات
            'الإدارة والإعدادات': 'Administration & Settings',
            'إدارة المستخدمين والبيانات المرجعية للنظام': 'Manage users and system reference data',
            'مدير النظام — كل الشركات': 'System Admin — All Companies',
            'مستخدم جديد': 'New User',
            'المستخدمون': 'Users',
            'حسابات مُفعَّلة': 'Active Accounts',
            'الشركات': 'Companies',
            'الإدارات': 'Departments',
            'المواقع': 'Locations',
            'التصنيفات': 'Categories',
            'المورّدون': 'Vendors',
            'سياسات SLA': 'SLA Policies',
            'سجل التدقيق': 'Audit Log',
            'المستخدمون والصلاحيات': 'Users & Permissions',
            'إضافة وتعديل حسابات المستخدمين، تحديد الأدوار (مدير نظام / مدير شركة / فني دعم / موظف)،': 'Add and edit user accounts, define roles (System Admin / Company Manager / Support Tech / Employee),',
            'تفعيل أو تعطيل الحسابات، فتح الحسابات المقفلة وإعادة تعيين كلمات المرور.': 'activate or deactivate accounts, unlock locked accounts and reset passwords.',
            'بيانات الشركات المشتركة في النظام: الاسم، الكود، الرقم الضريبي، السجل التجاري': 'Data of companies in the system: name, code, tax number, commercial register',
            'وبيانات التواصل. كل شركة معزولة تماماً عن غيرها على مستوى قاعدة البيانات.': 'and contact info. Each company is fully isolated at the database level.',
            'الهيكل التنظيمي للشركة: الإدارات، مراكز التكلفة ومدير كل إدارة.': 'Company organizational structure: departments, cost centers, and department manager.',
            'تُستخدم في توزيع الأصول وتقارير التكلفة.': 'Used in asset distribution and cost reports.',
            'مواقع تواجد الأصول: مقرات، فروع، مخازن، مراكز بيانات ومواقع العملاء —': 'Asset locations: headquarters, branches, warehouses, data centers and client sites —',
            'مع العنوان والمحافظة ومسؤول الموقع.': 'with address, governorate and site manager.',
            'شجرة تصنيف الأصول مع العمر الإنتاجي الافتراضي، نسبة القيمة التخريدية': 'Asset classification tree with default useful life, salvage value percentage',
            'وطريقة الإهلاك — وهي الأساس الذي تُحسب عليه قيم الإهلاك تلقائياً.': 'and depreciation method — the basis for automatic depreciation calculation.',
            'بيانات المورّدين وشركات الصيانة: جهة الاتصال، الهاتف، الرقم الضريبي': 'Vendor and maintenance company data: contact, phone, tax number',
            'وتقييم المورّد من 1 إلى 5.': 'and vendor rating from 1 to 5.',
            'تحديد مدة الاستجابة ومدة الحل المستهدفة لكل أولوية تذكرة، وساعات التنبيه': 'Set response time and target resolution time per ticket priority, and alert hours',
            'قبل الاستحقاق. تُحسب عليها نسبة الالتزام في تقرير التذاكر.': 'before deadline. Used to calculate SLA compliance in the tickets report.',
            'سجل غير قابل للتعديل لكل العمليات الحساسة: من نفَّذ العملية، على أي كيان،': 'Immutable log of all sensitive operations: who performed the operation, on which entity,',
            'والقيمة قبل وبعد التغيير، مع عنوان الـ IP ووقت التنفيذ.': 'and value before and after the change, with IP address and execution time.',
            'ملاحظة أمنية:': 'Security Note:',
            'كل البيانات المرجعية معزولة على مستوى الشركة تلقائياً بواسطة فلاتر قاعدة البيانات.': 'All reference data is automatically isolated per company by database filters.',
            'مدير الشركة لا يرى ولا يعدّل بيانات شركة أخرى، ولا يستطيع منح صلاحية «مدير نظام» لأي مستخدم.': 'Company manager cannot view or edit another company\'s data, nor grant "System Admin" to any user.',
            'عرض المستخدمين': 'View Users',
            'إضافة مستخدم': 'Add User',
            'عرض الشركات': 'View Companies',
            'إضافة شركة': 'Add Company',
            'عرض الإدارات': 'View Departments',
            'إضافة إدارة': 'Add Department',
            'عرض المواقع': 'View Locations',
            'إضافة موقع': 'Add Location',
            'عرض التصنيفات': 'View Categories',
            'إضافة تصنيف': 'Add Category',
            'عرض المورّدين': 'View Vendors',
            'إضافة مورّد': 'Add Vendor',
            'عرض السياسات': 'View Policies',
            'إضافة سياسة': 'Add Policy',
            'استعراض السجل': 'Browse Log',
            'الإدارات والأقسام': 'Departments & Sections',
            'تصنيفات الأصول': 'Asset Categories',
            'سياسات مستوى الخدمة (SLA)': 'Service Level Policies (SLA)',

            // ── صفحة الجرد
            'الجرد الدوري': 'Periodic Inventory Audit',
            'جلسات الجرد والفوارق': 'Audit sessions & variances',
            'إنشاء جرد جديد': 'Create New Audit',
            'جلسة جرد جديدة': 'New Audit Session',
            'جارية': 'In Progress',
            'مكتملة': 'Completed',
            'الفروق': 'Variances',
            'بدأ بواسطة': 'Started By',
            'المدة': 'Duration',
            'لا توجد جلسات جرد.': 'No audit sessions found.',

            // ── المستخدمون
            'الاسم الكامل': 'Full Name',
            'البريد الإلكتروني': 'Email Address',
            'الرقم الوظيفي': 'Employee ID',
            'المسمى الوظيفي': 'Job Title',
            'رقم الهاتف': 'Phone Number',
            'الدور في النظام': 'System Role',
            'آخر تسجيل دخول': 'Last Login',
            'نشط': 'Active',
            'غير نشط': 'Inactive',
            'مقفل': 'Locked',
            'فتح الحساب': 'Unlock Account',
            'إعادة تعيين كلمة المرور': 'Reset Password',
            'تفعيل': 'Activate',
            'تعطيل': 'Deactivate',
            'بيانات المستخدم': 'User Details',
            'بياناتك المسجّلة في النظام': 'Your profile details in system',

            // ── تسجيل الدخول
            'تسجيل الدخول': 'Sign In',
            'أدخل بياناتك للمتابعة': 'Enter your credentials to continue',
            'كلمة المرور': 'Password',
            'تذكّرني على هذا الجهاز': 'Remember me on this device',
            'دخول': 'Sign In',
            'نظام إدارة وتتبع الأصول والدعم الفني': 'Asset Tracking & IT Support System',
            'تتبع الأصول لحظياً': 'Real-time Asset Tracking',
            'إدارة العقود والموردين': 'Contract & Vendor Management',
            'نظام تذاكر الصيانة': 'Maintenance Ticket System',
            'تنبيهات ومواعيد دورية': 'Periodic Alerts & Audits',
            'تقارير وإحصائيات متقدمة': 'Advanced Reports & Analytics',
            'سجل تدقيق كامل للعمليات': 'Full Audit Logs',

            // ── الترقيم
            'السابق': 'Previous',
            'التالي': 'Next',
            'الصفحة': 'Page',
            'من': 'of',
            'نتيجة': 'results',
            'عرض': 'Show',

            // ── النماذج العامة
            'الاسم': 'Name',
            'الكود': 'Code',
            'الرمز': 'Code',
            'الوصف': 'Description',
            'الحالة': 'Status',
            'إضافة': 'Add',
            'حفظ': 'Save',
            'إلغاء': 'Cancel',
            'تعديل': 'Edit',
            'حذف': 'Delete',
            'تأكيد': 'Confirm',
            'إغلاق': 'Close',
            'طباعة': 'Print',
            'تفاصيل': 'Details',
            'بحث': 'Search',
            'تصفية': 'Filter',
            'تطبيق': 'Apply',
            'مسح QR': 'Scan QR',

            // ── رسائل عامة
            'تم الحفظ بنجاح': 'Saved successfully',
            'تم الحذف بنجاح': 'Deleted successfully',
            'حدث خطأ': 'An error occurred',
            'يرجى المحاولة مرة أخرى': 'Please try again',
            'لا توجد بيانات': 'No data available',
            'جارٍ التحميل': 'Loading',

            // ── إشعارات
            'إشعار جديد': 'New Notification',
            'لا توجد إشعارات': 'No notifications',
            'إشعارات غير مقروءة': 'Unread notifications',
            'الإشعار': 'Notification',
            'كل الإشعارات مقروءة': 'All notifications are read',
            'غير المقروء فقط': 'Unread only',
            'لا توجد إشعارات غير مقروءة': 'No unread notifications',
            'لديك': 'You have',
            'إشعاراً غير مقروء': 'unread notifications',

            // ── الملف الشخصي
            'الملف الشخصي': 'Profile',
            'تغيير كلمة المرور': 'Change Password',
            'كلمة المرور الحالية': 'Current Password',
            'كلمة المرور الجديدة': 'New Password',
            'تأكيد كلمة المرور الجديدة': 'Confirm New Password',

            // ── الجدولة والصيانة
            'جدول الصيانة': 'Maintenance Schedule',
            'صيانة وقائية مستحقة': 'Preventive Maintenance Due',
            'التالي': 'Next',
            'موعد الصيانة القادمة': 'Next Maintenance Date',
            'دوري': 'Periodic',
            'يومي': 'Daily',
            'أسبوعي': 'Weekly',
            'شهري': 'Monthly',
            'سنوي': 'Yearly',

            // ── المعلومات المالية
            'ج.م': 'EGP',
            'القيمة': 'Value',
            'نسبة الإهلاك': 'Depreciation Rate',
            'طريقة الإهلاك': 'Depreciation Method',
            'القسط السنوي': 'Annual Installment',
            'القيمة التخريدية': 'Salvage Value',
            'العمر الإنتاجي': 'Useful Life',
            'سنوات': 'years',

            // ── الأزرار العامة
            'تصدير إلى Excel': 'Export to Excel',
            'طباعة الملصقات': 'Print Labels',
            'مسح الفلاتر': 'Clear Filters',

            // ── إرشادات وتعليمات
            'مسح أصل بالوسم': 'Scan Asset Tag',
            'فتح بطاقة الأصل مباشرة': 'Open asset card directly',
            'المهام والمواعيد القادمة': 'Upcoming Tasks & Due Dates',

            // ── بيانات مكملة
            'رقم التذكرة': 'Ticket No',
            'العنوان': 'Title',
            'تاريخ الإنشاء': 'Created Date',
            'تاريخ الإضافة': 'Date Added',
            'اسم الأصل': 'Asset Name',
            'إجمالي تذاكري': 'Total My Tickets',
            'كل ما قدّمته من طلبات': 'All submitted requests',
            'تذاكر قيد المعالجة': 'Tickets In Progress',
            'لم تُغلق بعد': 'Not closed yet',
            'تذاكري (الإجمالي)': 'My Tickets (Total)',
            'جاري العمل عليها': 'Currently in progress',
            'بانتظار البدء': 'Waiting to start',
            'تجاوزت SLA': 'Breached SLA',
            'تحتاج تدخل عاجل': 'Urgent action required',
            'تم حلها هذا الشهر': 'Resolved this month',
            'إنجازك الشهري': 'Your monthly performance',

            // ── أيام الأسبوع
            'الأحد': 'Sunday',
            'الإثنين': 'Monday',
            'الثلاثاء': 'Tuesday',
            'الأربعاء': 'Wednesday',
            'الخميس': 'Thursday',
            'الجمعة': 'Friday',
            'السبت': 'Saturday',

            // ── شهور السنة
            'يناير': 'January',
            'فبراير': 'February',
            'مارس': 'March',
            'أبريل': 'April',
            'مايو': 'May',
            'يونيو': 'June',
            'يوليو': 'July',
            'أغسطس': 'August',
            'سبتمبر': 'September',
            'أكتوبر': 'October',
            'نوفمبر': 'November',
            'ديسمبر': 'December',

            // ── متنوع
            'نعم': 'Yes',
            'لا': 'No',
            'موافق': 'OK',
            'رفض': 'Reject',
            'قبول': 'Accept',
            'إرسال': 'Send',
            'تحميل': 'Download',
            'رفع': 'Upload',
            'اختر': 'Select',
            'اختر ملف': 'Choose File',
            'شركتك': 'Your Company',

            // ── نصوص الجسم في الصفحات الحديثة والنماذج
            'ابحث عن أصل أو موقع أو رقم...': 'Search for an asset, location or number...',
            'ابحث باسم الأصل أو كوده...': 'Search by asset name or code...',
            'التصنيف: الكل': 'Category: All',
            'الموقع: الكل': 'Location: All',
            'الحالة: الكل': 'Status: All',
            'لا توجد أصول مطابقة': 'No matching assets',
            'كود:': 'Code:',
            'تصنيف:': 'Category:',
            'موقع:': 'Location:',
            'ضمان:': 'Warranty:',
            'رمز QR': 'QR Code',
            'الاسم': 'Name',
            'الحركة:': 'Movement:',
            'من:': 'From:',
            'إلى:': 'To:',
            'التاريخ:': 'Date:',
            'نواقص:': 'Short:',
            'الدفترية:': 'Book:',
            'عرض التفاصيل': 'View Details',
            'موافقة': 'Approve',
            'رفض': 'Reject',
            'عرض': 'View',
            'تصدير': 'Export',
            'إضافة مخزن': 'Add Warehouse',
            'عرض كل الأصول': 'View All Assets',
            'جرد المخازن': 'Warehouse Audit',
            'الأصول موزعة كالتالي:': 'Assets are distributed as follows:',
            'لا توجد مخازن بعد — أضف أول مخزن من الزر الأعلى': 'No warehouses yet — add the first warehouse from the button above',
            'مسؤول': 'Responsible',
            'التاريخ المخطط': 'Scheduled Date',
            'ابحث بكود الجرد أو العنوان أو الموقع...': 'Search by audit code, title or location...',
            'ابحث باسم الأصل، الكود، أو الموظف...': 'Search by asset name, code or employee...',
            'نوع الحركة: الكل': 'Movement Type: All',
            'الحالة: الكل': 'Status: All',
            'من تاريخ': 'From Date',
            'إلى تاريخ': 'To Date',
            'إعادة تعيين': 'Reset',
            'تصدير التقرير': 'Export Report',
            'تسليم عهدة جديدة': 'Assign New Custody',
            'إدارة العهد': 'Custody Management',
            'عهدي': 'My Custody',
            'الأصول في عهدتي': 'Assets in My Custody',
            'متابعة وتسليم واسترجاع عهد الأصول بين الموظفين': 'Track, assign and return asset custody between employees',
            'إجمالي حركات العهد': 'Total Custody Movements',
            'مقبولة': 'Accepted',
            'مرفوضة': 'Rejected',
            'تم الإرجاع': 'Returned',
            'بانتظار الموافقة': 'Pending Approval',
            'لا توجد حركات عهد مطابقة': 'No custody movements found',
            'مسودات': 'Drafts',
            'جاري التنفيذ': 'In Progress',
            'نواقص مراجعة': 'Shortages to review',
            'جلسات جرد تحتاج مراجعة': 'Audit sessions needing review',
            'لا نواقص معلّقة': 'No pending shortages',
            'كل الجلسات المكتملة نظيفة — أحسنت': 'All completed sessions are clean — well done',
            'كود الجرد': 'Audit Code',
            'عنوان الجرد': 'Audit Title',
            'المسؤول': 'Responsible',
            'التاريخ المخطط': 'Scheduled Date',
            'التقدم': 'Progress',
            'النواقص': 'Shortages',
            'لا توجد جلسات جرد مطابقة': 'No matching audit sessions',
            'مسؤول:': 'Owner:',
            'التاريخ:': 'Date:',
            'نواقص:': 'Short:',
            'الدفترية:': 'Book:',
            'ضمانات تنتهي قريبًا': 'Warranties expiring soon',
            'حل متكامل لإدارة أصولك، تتبعها، وتقديم الدعم الفني بكفاءة ووضوح في مكان واحد': 'An integrated solution to manage, track and support your assets — clearly, in one place',

            // ── نصوص إضافية للصفحات الحديثة
            'إدارة ومتابعة جميع أصول الشركة': 'Manage and track all company assets',
            'أصل جديد': 'New Asset',
            'طباعة ملصقات': 'Print Labels',
            'ابحث باسم الأصل أو كوده...': 'Search by asset name or code...',
            'التصنيف: الكل': 'Category: All',
            'الموقع: الكل': 'Location: All',
            'الحالة: الكل': 'Status: All',
            'إجمالي الأصول': 'Total Assets',
            'أصول تحتاج متابعة': 'Assets Needing Follow-up',
            'ضمان ينتهي قريبًا': 'Warranty Expiring Soon',
            'الاسم': 'Name',
            'كود الأصل': 'Asset Code',
            'التصنيف': 'Category',
            'الموقع': 'Location',
            'الحالة': 'Status',
            'تاريخ الضمان': 'Warranty Date',
            'الإجراءات': 'Actions',
            'نشط': 'Active',
            'في المخزن': 'In Storage',
            'قيد الصيانة': 'Under Maintenance',
            'تالف': 'Damaged',
            'مستبعد': 'Disposed',
            'مفقود': 'Lost',
            'الجرد الدوري للأصول': 'Periodic Asset Audit',
            'متابعة عمليات الجرد الدورية للأصول واكتشاف الفروقات إن وجدت': 'Track periodic asset audits and detect variances if any',
            'إدارة العهد': 'Custody Management',
            'متابعة وتسليم واسترجاع عهد الأصول بين الموظفين': 'Track, assign and return asset custody between employees',
            'إجمالي حركات العهد': 'Total Custody Movements',
            'مقبولة': 'Accepted',
            'مرفوضة': 'Rejected',
            'تم الإرجاع': 'Returned',
            'بانتظار الموافقة': 'Pending Approval',
            'نوع الحركة': 'Movement Type',
            'من': 'From',
            'إلى': 'To',
            'تاريخ الطلب': 'Request Date',
            'الحالة': 'Status',
            'ملاحظات': 'Notes',
            'عرض التفاصيل': 'View Details',
            'تسليم': 'Assign',
            'نقل': 'Transfer',
            'استرجاع': 'Return',
            'المخزن': 'Warehouse',
            'رقم الحركة': 'Movement No.',
            'الأصل': 'Asset',
            'ابحث باسم الأصل، الكود، أو الموظف...': 'Search by asset name, code or employee...',
            'نوع الحركة: الكل': 'Movement Type: All',
            'من تاريخ': 'From Date',
            'إلى تاريخ': 'To Date',
            'لا توجد حركات عهد مطابقة': 'No custody movements found',
            'حسابات تجريبية (كلمة المرور: Admin@123)': 'Demo accounts (password: Admin@123)',
            'فتح القائمة': 'Open menu',
            'القائمة الرئيسية': 'Main menu',
            'القائمة': 'Menu',
            'المواصفات الفنية': 'Technical Specs',
            'البيانات المالية والإهلاك': 'Financials & Depreciation',
            'بيانات الشراء': 'Purchase Details',
            'سيتم توليد وسم الأصل تلقائياً بالصيغة AST-YYYY-NNNNN.': 'The asset tag will be generated automatically as AST-YYYY-NNNNN.',
            'لا يمكن تعديله.': 'cannot be edited.',
            'يجب أن يكون فريداً على مستوى الشركة.': 'Must be unique within the company.',
            '— اختر —': '— Select —',
            '— بدون —': '— None —',
            '— اختر الشركة —': '— Select company —',
            '— اختر التصنيف الرئيسي أولاً —': '— Select the main category first —',
            'إضافة أصل جديد': 'Add New Asset',
            'تعديل بيانات الأصل': 'Edit Asset Details',
            'الاسم بالإنجليزية': 'English Name',
            'التصنيف الرئيسي': 'Main Category',
            'التصنيف الفرعي': 'Subcategory',
            'اختر التصنيف الرئيسي أولاً لتظهر تصنيفاته الفرعية.': 'Select the main category first to show its subcategories.',
            'اختر الشركة أولاً، فالتصنيفات تختلف من شركة لأخرى.': 'Select the company first — categories differ per company.',
            'لو لم يكن للتصنيف الرئيسي فروع، سيُستخدم هو نفسه.': 'If the main category has no children, it will be used as-is.',
            'صيانة إصلاحية': 'Corrective',
            'صيانة وقائية': 'Preventive',
            'تركيب': 'Installation',
            'فحص': 'Inspection',
            'المهندس مباشرة': 'Engineer directly',
            'فني تحت الإشراف': 'Supervised technician',
            'فني مستقل': 'Technician alone',
            'جهة خارجية': 'External vendor',
            'قبل الإصلاح': 'Before repair',
            'بعد الإصلاح': 'After repair',
            'القسط الثابت': 'Straight line',
            'القسط المتناقص': 'Declining balance',
            'مسودة': 'Draft',
            'مكتمل': 'Completed',
            'ملغي': 'Cancelled',
            'لم يُجرد': 'Not counted',
            'موجود': 'Found',
            'بمكان مختلف': 'Misplaced',
            'مُفعَّل': 'Active',
            'موقوف': 'Paused',
            'منتهي': 'Ended',
            'ربع سنوي': 'Quarterly',
            'نصف سنوي': 'Semi-annual',
            'إرجاع': 'Return',
            'شقة': 'Apartment',
            'خط إنتاج': 'Production line',
            'الآن': 'Just now',
            'تعذّر تحميل الإشعارات': 'Failed to load notifications',
            'أصول:': 'Assets:',
            'قيمة:': 'Value:',
            'كود:': 'Code:',
            'الجرد الدوري للأصول': 'Periodic Asset Audit',
            'متابعة عمليات الجرد الدورية للأصول واكتشاف الفروقات إن وجدت': 'Track periodic asset audits and detect variances if any',
            'مسودات': 'Drafts',
            'جاري التنفيذ': 'In Progress',
            'نواقص مراجعة': 'Shortages to review',
            'جلسات جرد تحتاج مراجعة': 'Audit sessions needing review',
            'لا نواقص معلّقة': 'No pending shortages',
            'كل الجلسات المكتملة نظيفة — أحسنت': 'All completed sessions are clean — well done',
            'كود الجرد': 'Audit Code',
            'عنوان الجرد': 'Audit Title',
            'المسؤول': 'Responsible',
            'التاريخ المخطط': 'Scheduled Date',
            'التقدم': 'Progress',
            'النواقص': 'Shortages',
            'لا توجد جلسات جرد مطابقة': 'No matching audit sessions',
            'مسؤول:': 'Owner:',
            'التاريخ:': 'Date:',
            'نواقص:': 'Short:',
            'الدفترية:': 'Book:',
            'ضمانات تنتهي قريبًا': 'Warranties expiring soon',
            'حل متكامل لإدارة أصولك، تتبعها، وتقديم الدعم الفني بكفاءة ووضوح في مكان واحد': 'An integrated solution to manage, track and support your assets — clearly, in one place',

            // ── نصوص إضافية للصفحات الحديثة
            'إدارة ومتابعة جميع أصول الشركة': 'Manage and track all company assets',
            'أصل جديد': 'New Asset',
            'طباعة ملصقات': 'Print Labels',
            'ابحث باسم الأصل أو كوده...': 'Search by asset name or code...',
            'التصنيف: الكل': 'Category: All',
            'الموقع: الكل': 'Location: All',
            'الحالة: الكل': 'Status: All',
            'إجمالي الأصول': 'Total Assets',
            'أصول تحتاج متابعة': 'Assets Needing Follow-up',
            'ضمان ينتهي قريبًا': 'Warranty Expiring Soon',
            'الاسم': 'Name',
            'كود الأصل': 'Asset Code',
            'التصنيف': 'Category',
            'الموقع': 'Location',
            'الحالة': 'Status',
            'تاريخ الضمان': 'Warranty Date',
            'الإجراءات': 'Actions',
            'نشط': 'Active',
            'في المخزن': 'In Storage',
            'قيد الصيانة': 'Under Maintenance',
            'تالف': 'Damaged',
            'مستبعد': 'Disposed',
            'مفقود': 'Lost',
            'الجرد الدوري للأصول': 'Periodic Asset Audit',
            'متابعة عمليات الجرد الدورية للأصول واكتشاف الفروقات إن وجدت': 'Track periodic asset audits and detect variances if any',
            'إدارة العهد': 'Custody Management',
            'متابعة وتسليم واسترجاع عهد الأصول بين الموظفين': 'Track, assign and return asset custody between employees',
            'إجمالي حركات العهد': 'Total Custody Movements',
            'مقبولة': 'Accepted',
            'مرفوضة': 'Rejected',
            'تم الإرجاع': 'Returned',
            'بانتظار الموافقة': 'Pending Approval',
            'نوع الحركة': 'Movement Type',
            'من': 'From',
            'إلى': 'To',
            'تاريخ الطلب': 'Request Date',
            'الحالة': 'Status',
            'ملاحظات': 'Notes',
            'عرض التفاصيل': 'View Details',
            'تسليم': 'Assign',
            'نقل': 'Transfer',
            'استرجاع': 'Return',
            'المخزن': 'Warehouse',
            'رقم الحركة': 'Movement No.',
            'الأصل': 'Asset',
            'ابحث باسم الأصل، الكود، أو الموظف...': 'Search by asset name, code or employee...',
            'نوع الحركة: الكل': 'Movement Type: All',
            'من تاريخ': 'From Date',
            'إلى تاريخ': 'To Date',
            'لا توجد حركات عهد مطابقة': 'No custody movements found',
            'حسابات تجريبية (كلمة المرور: Admin@123)': 'Demo accounts (password: Admin@123)',
            'فتح القائمة': 'Open menu',
            'القائمة الرئيسية': 'Main menu',
            'القائمة': 'Menu',
            'المواصفات الفنية': 'Technical Specs',
            'البيانات المالية والإهلاك': 'Financials & Depreciation',
            'بيانات الشراء': 'Purchase Details',
            'سيتم توليد وسم الأصل تلقائياً بالصيغة AST-YYYY-NNNNN.': 'The asset tag will be generated automatically as AST-YYYY-NNNNN.',
            'لا يمكن تعديله.': 'cannot be edited.',
            'يجب أن يكون فريداً على مستوى الشركة.': 'Must be unique within the company.',
            '— اختر —': '— Select —',
            '— بدون —': '— None —',
            '— اختر الشركة —': '— Select company —',
            '— اختر التصنيف الرئيسي أولاً —': '— Select the main category first —',
            'إضافة أصل جديد': 'Add New Asset',
            'تعديل بيانات الأصل': 'Edit Asset Details',
            'الاسم بالإنجليزية': 'English Name',
            'التصنيف الرئيسي': 'Main Category',
            'التصنيف الفرعي': 'Subcategory',
            'اختر التصنيف الرئيسي أولاً لتظهر تصنيفاته الفرعية.': 'Select the main category first to show its subcategories.',
            'اختر الشركة أولاً، فالتصنيفات تختلف من شركة لأخرى.': 'Select the company first — categories differ per company.',
            'لو لم يكن للتصنيف الرئيسي فروع، سيُستخدم هو نفسه.': 'If the main category has no children, it will be used as-is.',
            'صيانة إصلاحية': 'Corrective',
            'صيانة وقائية': 'Preventive',
            'تركيب': 'Installation',
            'فحص': 'Inspection',
            'المهندس مباشرة': 'Engineer directly',
            'فني تحت الإشراف': 'Supervised technician',
            'فني مستقل': 'Technician alone',
            'جهة خارجية': 'External vendor',
            'قبل الإصلاح': 'Before repair',
            'بعد الإصلاح': 'After repair',
            'القسط الثابت': 'Straight line',
            'القسط المتناقص': 'Declining balance',
            'مسودة': 'Draft',
            'مكتمل': 'Completed',
            'ملغي': 'Cancelled',
            'لم يُجرد': 'Not counted',
            'موجود': 'Found',
            'بمكان مختلف': 'Misplaced',
            'مُفعَّل': 'Active',
            'موقوف': 'Paused',
            'منتهي': 'Ended',
            'ربع سنوي': 'Quarterly',
            'نصف سنوي': 'Semi-annual',
            'إرجاع': 'Return',
            'شقة': 'Apartment',
            'خط إنتاج': 'Production line',
            'الآن': 'Just now',
            'تعذّر تحميل الإشعارات': 'Failed to load notifications',
            'أصول:': 'Assets:',
            'قيمة:': 'Value:',
            'كود:': 'Code:',
            'إضافة مخزن': 'Add Warehouse',
            'عرض كل الأصول': 'View All Assets',
            'جرد المخازن': 'Warehouse Audit',
            'الأصول موزعة كالتالي:': 'Assets are distributed as follows:',
            'لا توجد مخازن بعد — أضف أول مخزن من الزر الأعلى': 'No warehouses yet — add the first one from the button above',
            'مسؤول': 'Responsible',
            'التاريخ المخطط': 'Scheduled Date',
            'ابحث بكود الجرد أو العنوان أو الموقع...': 'Search by audit code, title or location...',
            'ابحث باسم الأصل، الكود، أو الموظف...': 'Search by asset name, code or employee...',
            'اشرح الخطوات التي نفّذتها لإصلاح المشكلة…': 'Describe the steps you took to fix the issue…',
            'مثال: تلف في مزوّد الطاقة نتيجة تقلّب التيار': 'e.g. Power supply damage due to voltage fluctuation',
            'مثال: لاب توب ديل لاتيتيود': 'e.g. Dell Latitude laptop',
            'مثال: الجهاز لا يعمل بعد انقطاع الكهرباء': 'e.g. Device does not work after a power outage',
            'اشرح الأعراض، متى بدأت، وما جرّبته…': 'Describe the symptoms, when they started, and what you tried…',
            'مثال: مروحة تبريد': 'e.g. Cooling fan',
            'اكتب تحديثاً أو استفساراً…': 'Write an update or a question…',
            'ملاحظة (اختياري)': 'Note (optional)',
            'رقم التذكرة، الموضوع، الأصل…': 'Ticket no., subject, asset…',
            'سبب الاستبعاد (اختياري)': 'Disposal reason (optional)',
            'مثال: أجهزة حاسب ولاب توب': 'e.g. Computers and laptops',
            'مثال: صيانة دورية لمكيّف الدور الثاني': 'e.g. Periodic AC maintenance — 2nd floor',
            'وصف أعمال الصيانة المطلوبة…': 'Describe the required maintenance work…',
            'عنوان الجدول، الوصف، الأصل…': 'Schedule title, description, asset…',
            'كود الجرد أو العنوان…': 'Audit code or title…',
            'وسم أو اسم الأصل…': 'Tag or asset name…',
            'ملاحظات الإغلاق (اختياري)': 'Closing notes (optional)',
            'سبب الإلغاء (اختياري)': 'Cancellation reason (optional)',
            'مثال: جرد ربع سنوي — مخزن القاهرة': 'e.g. Quarterly audit — Cairo warehouse',
            'نطاق الجرد والأصول المشمولة…': 'Audit scope and included assets…',
            'مثال: نهاية خدمة الموظف': 'e.g. End of employee service',
            'مثال: الجهاز سليم، خدوش بسيطة على الغلاف الخارجي': 'e.g. Device is fine, minor scratches on the case',
            'مثال: الجهاز سليم واستلمت الشاحن والحافظة': 'e.g. Device is fine; charger and case received',
            'وسم الأصل، اسمه، سبب التسليم…': 'Asset tag, name, assignment reason…',
            'مثال: تسليم لابتوب لمهام العمل الميداني': 'e.g. Laptop issued for field work',
            'الاسم أو الكود…': 'Name or code…',
            'الاسم، البريد، الرقم الوظيفي…': 'Name, email, employee ID…',
            '8 أحرف على الأقل': 'At least 8 characters',
            'مثال: أحمد محمود عبد الله': 'e.g. Ahmed Mahmoud Abdullah',
            'مثال: مهندس شبكات': 'e.g. Network engineer',
            'مثال: سياسة الأولوية العالية': 'e.g. High-priority policy',
            'مثال: المقر الرئيسي': 'e.g. Headquarters',
            'القاهرة': 'Cairo',
            'المستخدم، الكيان، المعرّف…': 'User, entity, identifier…',
            'مثال: شركة النيل للتكنولوجيا': 'e.g. Nile Technology Company',
            'مثال: الإدارة الهندسية': 'e.g. Engineering department',
            'مثال: شركة الأهرام لتكنولوجيا المعلومات': 'e.g. Ahram IT Company',

            // ── صفحة الجرد — الفهرس (نصوص كانت مبتترجمش)
            'عملية جرد جديدة': 'New Audit Session',
            'مراجعة النواقص': 'Review Shortages',
            'تحديث النتائج': 'Update Results',

            // ── نموذج إنشاء/تعديل عملية الجرد
            'جديد': 'New',
            'إنشاء عملية جرد': 'Create Audit Session',
            'إنشاء عملية الجرد': 'Create Audit Session',
            'تعديل عملية الجرد': 'Edit Audit Session',
            'بيانات عملية الجرد': 'Audit Session Data',
            'خطوات الجرد': 'Audit Steps',
            '— كل المواقع —': '— All Locations —',
            '— بدون تحديد —': '— Unassigned —',
            'أصول داخل النطاق حالياً': 'Assets in scope right now',
            'يُحسَب العدد النهائي عند بدء الجرد': 'The final count is computed when the audit starts',
            'حدّد نطاق الجرد ثم ابدأه من صفحة التفاصيل — سيقوم النظام بتعبئة بنود الجرد من الأصول تلقائياً.': 'Define the audit scope, then start it from the details page — the system auto-fills the audit items from assets.',
            'حسابك مدير عام غير مرتبط بشركة واحدة — حدّد الشركة التي يُسجَّل الجرد عليها.': 'Your account is a global admin not tied to one company — choose the company this audit is recorded under.',
            'تحديد الموقع يقصر بنود الجرد على أصول ذلك الموقع فقط.': 'Picking a location limits the audit items to that location\'s assets only.',
            'أنشئ عملية الجرد وتبقى مسودة.': 'Create the audit session; it stays as a draft.',
            'اضغط «بدء الجرد» لتُعبَّأ البنود من الأصول داخل النطاق.': 'Press "Start Audit" to fill the items from in-scope assets.',
            'امسح وسم كل أصل أو سجّل نتيجته يدوياً.': 'Scan each asset tag or record its result manually.',
            'البنود غير المجرودة تُعتبر مفقودة عند الإغلاق.': 'Uncounted items are treated as missing on close.',
            'أتمّ الجرد للحصول على تقرير الفروقات.': 'Finish the audit to get the variance report.',
            'عنوان الجرد مطلوب': 'Audit title is required',
            'العنوان بين ٣ و ٢٠٠ حرف': 'Title must be between 3 and 200 characters',
            'تاريخ الجرد مطلوب': 'Audit date is required',
            'عنوان عملية الجرد': 'Audit Session Title',
            'الوصف / نطاق الجرد': 'Description / Audit Scope',
            'الموقع (اتركه فارغاً لجرد كل المواقع)': 'Location (leave empty to audit all locations)',
            'المسؤول عن الجرد': 'Audit Responsible',
            'تاريخ الجرد المخطَّط': 'Scheduled Audit Date',
            'الشركة': 'Company',

            // ── صفحة فتح تذكرة صيانة (نموذج الإنشاء)
            'التذاكر': 'Tickets',
            'فتح تذكرة صيانة': 'Open Maintenance Ticket',
            'اختر الأصل واشرح المشكلة — سيتم حساب مواعيد SLA تلقائياً حسب الأولوية.': 'Choose the asset and describe the issue — SLA dates are computed automatically based on priority.',
            'الأصل المتعلق بالمشكلة': 'Asset Related to the Issue',
            '— اختر الأصل —': '— Select Asset —',
            'يمكنك أيضاً': 'You can also',
            'مسح كود QR': 'scan the QR code',
            'للأصل ثم فتح تذكرة من صفحته.': 'for the asset, then open a ticket from its page.',
            'تفاصيل المشكلة': 'Issue Details',
            'موضوع المشكلة': 'Issue Subject',
            'وصف تفصيلي': 'Detailed Description',
            'التصنيف والأولوية': 'Classification & Priority',
            'الأولوية تحدد مواعيد الاستجابة والحل حسب سياسة SLA المعتمدة في شركتك.': 'Priority determines the response and resolution deadlines per your company SLA policy.',
            'الفئة الفنية للعطل — أساس تحليل أكثر أنواع الأعطال تكراراً.': 'The technical fault category — the basis for analyzing the most frequent fault types.',
            'الإنتاج وزمن التوقف': 'Production & Downtime',
            'خط الإنتاج / المنطقة': 'Production Line / Zone',
            '— يُحدَّد من موقع الماكينة —': '— Determined from the machine location —',
            'وقت توقف الماكينة': 'Machine Stop Time',
            'سجّل لحظة توقف الماكينة فعلاً. زمن التوقف سيُحسب تلقائياً عند تسجيل استئناف التشغيل.': 'Record the moment the machine actually stopped. Downtime is computed automatically when the restart is logged.',
            'العطل أوقف الإنتاج': 'Fault Stopped Production',
            'فتح التذكرة': 'Open Ticket',
            'يجب اختيار الأصل': 'You must select an asset',
            'الموضوع مطلوب': 'Subject is required',
            'الموضوع بين ٥ و ٢٠٠ حرف': 'Subject must be between 5 and 200 characters',
            'وصف المشكلة مطلوب': 'Issue description is required',
            'الوصف ١٠ أحرف على الأقل': 'Description must be at least 10 characters',

            // ── نموذج إضافة/تعديل أصل (نصوص كانت مبتترجمش أو مشوّهة)
            'المورد': 'Vendor',
            'المواصفات': 'Specifications',
            'اللون': 'Color',
            'رقم الفاتورة': 'Invoice Number',
            'بداية الضمان': 'Warranty Start Date',
            'نهاية الضمان': 'Warranty End Date',
            'جهة الضمان': 'Warranty Provider',
            '«في المخزن» للأصول غير المسلَّمة، و«نشط» للأصول قيد الاستخدام.': '"In Storage" for undelivered assets, "Active" for assets in use.',
            '— اختر الشركة أولاً —': '— Select the company first —',
            'لا فروع له': 'has no subcategories',
            'إضافة': 'Add',
            'إضافة الأصل': 'Add Asset',
            'يجب اختيار التصنيف.': 'Please select a category.',
            'اسم الأصل مطلوب': 'Asset name is required',
            'الاسم لا يزيد عن ٢٠٠ حرف': 'Name must not exceed 200 characters',
            'التصنيف مطلوب': 'Category is required',
            'قيمة غير صحيحة': 'Invalid value',
            'العمر الإنتاجي بين ١ و ٦٠ سنة': 'Useful life must be between 1 and 60 years',

            // ── لوحة التحكم الرئيسية (نصوص واجهة كانت مبتترجمش)
            'حالة الأصول': 'Asset Status',
            'حُلّت هذا الشهر': 'Resolved This Month',
            'ضمان ينتهي قريباً': 'Warranty Expiring Soon',
            'الرقم': 'No.',

            // ── صفحة تسليم/إسناد عهدة
            'تسليم عهدة': 'Assign Custody',
            'تسليم عهدة لموظف': 'Assign Custody to Employee',
            'يُرسَل الطلب للموظف ولا تُنقل العهدة فعلياً إلا بعد موافقته من صفحة «عهدي».': 'The request is sent to the employee; the custody is not actually transferred until they approve it from the "My Custody" page.',
            'بيانات التسليم': 'Assignment Details',
            'الموظف المستلِم': 'Receiving Employee',
            'سبب التسليم': 'Assignment Reason',
            '— اختر الموظف —': '— Select Employee —',
            'هذا الأصل في عهدة': 'This asset is in the custody of',
            'حالياً — سيتم تحويل العهدة بعد موافقة المستلِم الجديد.': 'currently — the custody will be transferred once the new recipient approves.',
            'ضوابط العهدة': 'Custody Rules',
            'لا يمكن تسليم أصل مستبعد أو مفقود.': 'A disposed or lost asset cannot be assigned.',
            'الموظف المستلِم يجب أن يكون من نفس شركة الأصل.': 'The receiving employee must belong to the same company as the asset.',
            'لا يُسمح بأكثر من طلب معلّق واحد على نفس الأصل.': 'Only one pending request per asset is allowed.',
            'الموافقة أو الرفض حق للموظف المستلِم فقط.': 'Only the receiving employee may approve or reject.',
            'إرسال طلب العهدة': 'Send Custody Request',
            'يجب اختيار الأصل': 'You must select an asset',
            'يجب اختيار الموظف': 'You must select an employee'
        },

        // قاموس التحويل من الإنجليزية إلى العربية
        en2ar: {}
    };

    var I18N = {
        'brand.name': { ar: 'نظام إدارة الأصول', en: 'Asset Management System' },
        'search.placeholder': { ar: 'ابحث بكود الأصل أو الاسم…', en: 'Search by asset code or name…' },
        'search.aria_label': { ar: 'بحث', en: 'Search' },
        'nav.home': { ar: 'الرئيسية', en: 'Home' },
        'nav.assets': { ar: 'الأصول', en: 'Assets' },
        'nav.warehouses': { ar: 'المخزون', en: 'Warehouse' },
        'nav.custody': { ar: 'العهد', en: 'Custody' },
        'nav.tickets': { ar: 'الدعم الفني', en: 'Support' },
        'nav.reports': { ar: 'التقارير', en: 'Reports' },
        'nav.admin': { ar: 'الإعدادات', en: 'Settings' },
        'nav.footer': { ar: 'نحو إدارة أذكى للأصول', en: 'Smarter Asset Management' },
        'nav.toggle': { ar: 'القائمة', en: 'Menu' },
        'role.admin': { ar: 'مدير النظام', en: 'System Admin' },
        'role.manager': { ar: 'مدير شركة', en: 'Company Manager' },
        'role.tech': { ar: 'فني دعم', en: 'Support Tech' },
        'role.employee': { ar: 'موظف', en: 'Employee' },
        'user.profile': { ar: 'الملف الشخصي', en: 'My Profile' },
        'user.change_pass': { ar: 'تغيير كلمة المرور', en: 'Change Password' },
        'user.logout': { ar: 'تسجيل الخروج', en: 'Sign Out' },
        'topbar.notifications': { ar: 'الإشعارات', en: 'Notifications' },
        'scan.qr': { ar: 'مسح QR', en: 'Scan QR' },
        'notif.mark_all': { ar: 'تحديد الكل كمقروء', en: 'Mark all as read' },
        'notif.loading': { ar: 'جارٍ التحميل…', en: 'Loading…' },
        'notif.view_all': { ar: 'عرض كل الإشعارات', en: 'View all notifications' },
        'notif.type': { ar: 'النوع', en: 'Type' },
        'notif.open': { ar: 'فتح', en: 'Open' },
        'notif.mark_read': { ar: 'تحديد كمقروء', en: 'Mark as read' },
        'pager.page': { ar: 'صفحة', en: 'Page' },
        'pager.of': { ar: 'من', en: 'of' },
        'pager.total': { ar: 'إجمالي', en: 'Total' },
        'pager.records': { ar: 'سجل', en: 'records' },
        'login.side_title': { ar: 'نظام إدارة وتتبع<br>الأصول والدعم الفني', en: 'Asset Tracking<br>& IT Support System' },
        'login.side_desc': { ar: 'حل متكامل لإدارة أصولك، تتبعها، وتقديم الدعم الفني بكفاءة ووضوح في مكان واحد', en: 'An integrated solution to manage, track and support your assets — clearly, in one place' },
        'login.app_desc': { ar: 'نظام إدارة وتتبع الأصول والدعم الفني', en: 'Asset Tracking & IT Support System' },
        'login.title': { ar: 'تسجيل الدخول', en: 'Sign In' },
        'login.subtitle': { ar: 'أدخل بياناتك للمتابعة', en: 'Enter your credentials to continue' },
        'login.email': { ar: 'البريد الإلكتروني', en: 'Email Address' },
        'login.password': { ar: 'كلمة المرور', en: 'Password' },
        'login.remember_me': { ar: 'تذكّرني على هذا الجهاز', en: 'Remember me on this device' },
        'login.submit': { ar: 'دخول', en: 'Sign In' },
        'login.demo_title': { ar: 'حسابات تجريبية (كلمة المرور: Admin@123)', en: 'Demo accounts (password: Admin@123)' },
        'login.feat.track': { ar: 'تتبع الأصول لحظياً', en: 'Real-time Asset Tracking' },
        'login.feat.vendor': { ar: 'إدارة العقود والموردين', en: 'Contract & Vendor Management' },
        'login.feat.tickets': { ar: 'نظام تذاكر الصيانة', en: 'Maintenance Ticket System' },
        'login.feat.alerts': { ar: 'تنبيهات ومواعيد دورية', en: 'Periodic Alerts & Audits' },
        'login.feat.reports': { ar: 'تقارير وإحصائيات متقدمة', en: 'Advanced Reports & Analytics' },
        'login.feat.audit': { ar: 'سجل تدقيق كامل للعمليات', en: 'Full Audit Logs' }
    };

    var SHORT_EXACT = { 'من': 1, 'إلى': 1, 'عرض': 1, 'الكل': 1, 'بحث': 1, 'إضافة': 1, 'حفظ': 1, 'نعم': 1, 'لا': 1, 'التالي': 1 };

    var arKeysSorted = Object.keys(T.ar2en).sort(function (a, b) { return b.length - a.length; });

    Object.keys(T.ar2en).forEach(function (ar) {
        T.en2ar[T.ar2en[ar]] = ar;
    });
    var enKeysSorted = Object.keys(T.en2ar).sort(function (a, b) { return b.length - a.length; });

    /* ──────────────── قراءة / حفظ الإعدادات ────────────────── */
    function getLang()  { return localStorage.getItem('ats_lang')  || 'ar'; }
    function getTheme() { return localStorage.getItem('ats_theme') || 'light'; }

    function setLang(lang) {
        localStorage.setItem('ats_lang', lang);
        applyLang(lang);
    }

    function setTheme(theme) {
        localStorage.setItem('ats_theme', theme);
        applyTheme(theme);
    }

    /* ─────────────── تطبيق الثيم ──────────────────────────── */
    function applyTheme(theme) {
        var html = document.documentElement;
        if (theme === 'dark') {
            html.setAttribute('data-theme', 'dark');
            html.classList.add('dark');
        } else {
            html.removeAttribute('data-theme');
            html.classList.remove('dark');
        }

        // تحديث جميع أزرار الثيم
        document.querySelectorAll('.ats-theme-toggle').forEach(function (btn) {
            var icon = btn.querySelector('i');
            if (icon) {
                icon.className = theme === 'dark'
                    ? 'fas fa-sun text-amber-400'
                    : 'fas fa-moon text-slate-600';
            }
            btn.title = theme === 'dark' ? 'الوضع الفاتح (Light Mode)' : 'الوضع الداكن (Dark Mode)';
        });
    }

    /* ─────────────── ترجمة نص فردي ────────────────────────── */
    function escRe(s) {
        return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function translateText(text, targetLang) {
        if (!text) return text;
        var trimmed = text.trim();
        if (!trimmed) return text;

        if (targetLang === 'en') {
            if (T.ar2en[trimmed]) return text.replace(trimmed, T.ar2en[trimmed]);

            if (trimmed.indexOf('مرحباً') === 0 || trimmed.indexOf('مرحبا') === 0) {
                return text.replace(/مرحب[اًا]\s*/g, 'Welcome ');
            }
            if (trimmed.indexOf('أهلاً') === 0 || trimmed.indexOf('اهلا') === 0) {
                return text.replace(/أهل[اًا]\s*/g, 'Welcome ');
            }
            text = text.replace(/تذكرة رقم/g, 'Ticket #');
            text = text.replace(/تنتظرك\s*(\d+)\s*مهام اليوم!?/g, 'You have $1 tasks waiting today!');
            text = text.replace(/متأخرة\s*(\d+)\s*ساعة/g, 'Overdue by $1 hrs');
            text = text.replace(/باقي\s*(\d+)\s*ساعة/g, '$1 hrs remaining');
            text = text.replace(/هناك\s*(\d+)\s*نواقص تحتاج لمراجعة في بعض الجلسات/g, 'There are $1 shortages that need review in some sessions');
            text = text.replace(/قبل\s*(\d+)\s*دقيقة/g, '$1 min ago');
            text = text.replace(/قبل\s*(\d+)\s*ساعة/g, '$1 hrs ago');
            text = text.replace(/قبل\s*(\d+)\s*يوم/g, '$1 days ago');
            text = text.replace(/قبل\s*(\d+)\s*شهر/g, '$1 months ago');
            text = text.replace(/قبل\s*(\d+)\s*سنة/g, '$1 years ago');
            text = text.replace(/الوسم\s+/g, 'Tag ');
            text = text.replace(/(\d)\s*م\s*$/g, '$1 PM');
            text = text.replace(/(\d)\s*ص\s*$/g, '$1 AM');

            var res = text;
            arKeysSorted.forEach(function (arW) {
                if (arW.length < 3 || SHORT_EXACT[arW]) return;
                if (res.indexOf(arW) === -1) return;
                // استبدال على حدود الكلمة فقط: لا نبدّل مفتاحًا داخل كلمة عربية
                // أطول — يمنع إفساد بيانات قاعدة البيانات (مثل «مكتبي» ← «Officeي»).
                var re = new RegExp('(?<![\\u0621-\\u064A\\u0671-\\u06D3])' + escRe(arW) + '(?![\\u0621-\\u064A\\u0671-\\u06D3])', 'g');
                res = res.replace(re, T.ar2en[arW]);
            });
            return res;
        }

        if (T.en2ar[trimmed]) return text.replace(trimmed, T.en2ar[trimmed]);
        if (trimmed.indexOf('Welcome ') === 0) {
            return text.replace(/Welcome\s+/g, 'مرحباً ');
        }
        text = text.replace(/Ticket\s*#/g, 'تذكرة رقم ');
        text = text.replace(/You have\s*(\d+)\s*tasks waiting today!?/g, 'تنتظرك $1 مهام اليوم!');
        var resAr = text;
        enKeysSorted.forEach(function (enW) {
            if (enW.length < 4) return;
            if (resAr.indexOf(enW) !== -1) {
                resAr = resAr.replace(new RegExp(escRe(enW), 'g'), T.en2ar[enW]);
            }
        });
        return resAr;
    }

    /* ─────────── ترجمة حرفية (مطابقة كاملة فقط، بلا استبدال جزئي) ───────────
       تُستخدم لعناصر <option>: تمنع إفساد أسماء البيانات القادمة من قاعدة
       البيانات (شركات/تصنيفات/أقسام) التي قد تحتوي بداخلها كلمات من القاموس،
       بينما تُترجم خيارات الواجهات الحقيقية لأنها كلها مفاتيح كاملة. */
    function translateExact(text, targetLang) {
        if (!text) return text;
        var trimmed = text.trim();
        if (!trimmed) return text;
        if (targetLang === 'en') {
            if (T.ar2en[trimmed]) return text.replace(trimmed, T.ar2en[trimmed]);
        } else {
            if (T.en2ar[trimmed]) return text.replace(trimmed, T.en2ar[trimmed]);
        }
        return text;
    }

    /* ─────────────── فحص وترجمة عقدة DOM ───────────────────── */
    function translateNodeTree(root, targetLang) {
        if (!root) return;

        var walker = document.createTreeWalker(
            root,
            NodeFilter.SHOW_TEXT,
            {
                acceptNode: function(node) {
                    var parent = node.parentElement;
                    if (!parent) return NodeFilter.FILTER_REJECT;
                    var tag = parent.tagName;
                    if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'CODE' || tag === 'OPTION') {
                        return NodeFilter.FILTER_REJECT;
                    }
                    if (parent.classList.contains('ats-lang-text') || parent.classList.contains('ats-lang-toggle')) {
                        return NodeFilter.FILTER_REJECT;
                    }
                    if (parent.closest('[data-notranslate], [data-i18n], [data-i18n-html]')) return NodeFilter.FILTER_REJECT;
                    var val = node.nodeValue.trim();
                    if (!val || /^\d+$/.test(val)) return NodeFilter.FILTER_SKIP;
                    return NodeFilter.FILTER_ACCEPT;
                }
            },
            false
        );

        var node;
        var nodesToTranslate = [];
        while ((node = walker.nextNode())) {
            nodesToTranslate.push(node);
        }

        nodesToTranslate.forEach(function (n) {
            if (!n._originalArabic) {
                n._originalArabic = n.nodeValue;
            }

            if (targetLang === 'en') {
                n.nodeValue = translateText(n._originalArabic, 'en');
            } else {
                n.nodeValue = n._originalArabic;
            }
        });

        // ترجمة حقول placeholder
        root.querySelectorAll('input, textarea').forEach(function (inp) {
            if (!inp._originalPlaceholder) {
                inp._originalPlaceholder = inp.placeholder;
            }
            if (inp._originalPlaceholder) {
                inp.placeholder = targetLang === 'en'
                    ? translateText(inp._originalPlaceholder, 'en')
                    : inp._originalPlaceholder;
            }
        });

        // ترجمة تلميحات title
        root.querySelectorAll('[title]').forEach(function (el) {
            if (el.classList.contains('ats-theme-toggle') || el.classList.contains('ats-lang-toggle')) return;
            if (!el._originalTitle) {
                el._originalTitle = el.title;
            }
            if (el._originalTitle) {
                el.title = targetLang === 'en'
                    ? translateText(el._originalTitle, 'en')
                    : el._originalTitle;
            }
        });

        root.querySelectorAll('select option').forEach(function (opt) {
            if (!opt._originalText) {
                opt._originalText = opt.textContent;
            }
            if (opt._originalText) {
                opt.textContent = targetLang === 'en'
                    ? translateExact(opt._originalText, 'en')
                    : opt._originalText;
            }
        });

        root.querySelectorAll('[aria-label]').forEach(function (el) {
            if (!el._originalAria) el._originalAria = el.getAttribute('aria-label');
            if (el._originalAria) {
                el.setAttribute('aria-label', targetLang === 'en'
                    ? translateText(el._originalAria, 'en')
                    : el._originalAria);
            }
        });

        root.querySelectorAll('input[type="submit"], input[type="button"], button[value]').forEach(function (el) {
            if (!el.value) return;
            if (!el._originalValue) el._originalValue = el.value;
            el.value = targetLang === 'en'
                ? translateText(el._originalValue, 'en')
                : el._originalValue;
        });
    }

    function applyKeyedI18n(lang) {
        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var tag = el.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA') return;
            var key = el.getAttribute('data-i18n');
            var pack = I18N[key];
            if (!pack) return;
            el.textContent = pack[lang] || pack.ar;
        });
        document.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-html');
            var pack = I18N[key];
            if (!pack) return;
            el.innerHTML = pack[lang] || pack.ar;
        });
        document.querySelectorAll('[data-i18n-title]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-title');
            var pack = I18N[key];
            if (!pack) return;
            el.title = pack[lang] || pack.ar;
        });
        document.querySelectorAll('[data-i18n-aria]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-aria');
            var pack = I18N[key];
            if (!pack) return;
            el.setAttribute('aria-label', pack[lang] || pack.ar);
        });
        document.querySelectorAll('input[data-i18n], textarea[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            var pack = I18N[key];
            if (!pack) return;
            el.placeholder = pack[lang] || pack.ar;
        });
    }

    var applyingLang = false;
    var langObserver = null;

    /* ─────────────── تطبيق اللغة ──────────────────────────── */
    function applyLang(lang) {
        applyingLang = true;
        if (langObserver) langObserver.disconnect();
        var html = document.documentElement;
        if (lang === 'en') {
            html.setAttribute('lang', 'en');
            html.setAttribute('dir', 'ltr');
        } else {
            html.setAttribute('lang', 'ar');
            html.setAttribute('dir', 'rtl');
        }

        applyKeyedI18n(lang);
        if (document.body) {
            translateNodeTree(document.body, lang);
        }

        // ترجمة عنوان الصفحة <title>
        if (!document._originalTitle) {
            document._originalTitle = document.title;
        }
        if (document._originalTitle) {
            document.title = lang === 'en'
                ? translateText(document._originalTitle, 'en')
                : document._originalTitle;
        }

        // تحديث جميع أزرار اللغة في الصفحة
        document.querySelectorAll('.ats-lang-toggle').forEach(function (btn) {
            var txt = btn.querySelector('.ats-lang-text');
            if (txt) {
                txt.textContent = lang === 'ar' ? 'EN' : 'عربي';
            } else {
                btn.textContent = lang === 'ar' ? 'EN' : 'عربي';
            }
            btn.title = lang === 'ar' ? 'Switch to English' : 'التبديل إلى العربية';
        });
        applyingLang = false;
        if (langObserver && document.body) {
            langObserver.observe(document.body, { childList: true, subtree: true });
        }
    }

    /* ─────── تطبيق الإعدادات فور تحميل الصفحة (بدون وميض) ── */
    applyTheme(getTheme());

    document.addEventListener('DOMContentLoaded', function () {
        applyTheme(getTheme());
        applyLang(getLang());
        bindButtons();

        var pending;
        langObserver = new MutationObserver(function () {
            if (applyingLang || getLang() !== 'en') return;
            clearTimeout(pending);
            pending = setTimeout(function () {
                if (applyingLang) return;
                applyLang(getLang());
            }, 120);
        });
        if (document.body) {
            langObserver.observe(document.body, { childList: true, subtree: true });
        }
    });

    /* ─────────────── ربط أزرار التبديل ───────────────────── */
    function bindButtons() {
        document.querySelectorAll('.ats-theme-toggle').forEach(function (themeBtn) {
            // منع تكرار الـ event listener
            if (themeBtn._boundTheme) return;
            themeBtn._boundTheme = true;
            themeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var newTheme = getTheme() === 'dark' ? 'light' : 'dark';
                setTheme(newTheme);
            });
        });

        document.querySelectorAll('.ats-lang-toggle').forEach(function (langBtn) {
            if (langBtn._boundLang) return;
            langBtn._boundLang = true;
            langBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var newLang = getLang() === 'ar' ? 'en' : 'ar';
                setLang(newLang);
            });
        });
    }

    // تصدير دوال ATS للاستخدام العام
    window.ATS = window.ATS || {};
    window.ATS.setLang  = setLang;
    window.ATS.setTheme = setTheme;
    window.ATS.getLang  = getLang;
    window.ATS.getTheme = getTheme;
    window.ATS.applyLang = applyLang;
    window.ATS.translateText = translateText;
})();
