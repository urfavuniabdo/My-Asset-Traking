/* ═══════════════════════════════════════════════════════════════
   نظام إدارة الأصول — تبديل الثيم (داكن/فاتح) واللغة (عربي/إنجليزي)
   يُخزَّن الإعداد في localStorage ويُطبَّق فور تحميل الصفحة
   ترجمة شاملة وتلقائية لكافة النصوص الثابتة في كافة الصفحات
   ═══════════════════════════════════════════════════════════════ */

(function () {
    'use strict';

    /* ─────────────────────────── قاموس الترجمات ──────────────────────────── */
    var T = {
        // قاموس التحويل من العربية إلى الإنجليزية
        ar2en: {
            // ── لوحة التحكم والإحصائيات
            'طلبات الدعم الفني': 'Support Tickets',
            'في انتظار المعالجة': 'Pending Processing',
            'الأصول المستحقة للصيانة': 'Assets Due for Maintenance',
            'خلال 7 أيام قادمة': 'Within Next 7 Days',
            'الأصول النشطة': 'Active Assets',
            'قيد الاستخدام': 'In Use',
            'إجمالي الأصول': 'Total Assets',
            'جميع الفئات': 'All Categories',
            'لا مهام عالقة اليوم — يوم مثير!': 'No pending tasks today — Great job!',
            'لا مهام قادمة — كل شيء تحت السيطرة': 'No upcoming tasks — Everything under control',
            'إجراءات سريعة': 'Quick Actions',
            'إصدار عهدة': 'Issue Custody',
            'إضافة أصل جديد': 'Add New Asset',
            'طلب دعم فني': 'Request Support',
            'استعلام عن أصل': 'Scan / Lookup Asset',
            'آخر الأصول المضافة': 'Recently Added Assets',
            'عرض الكل': 'View All',
            'اسم الأصل': 'Asset Name',
            'النوع': 'Category',
            'تاريخ الإضافة': 'Date Added',
            'لا توجد أصول بعد': 'No assets yet',
            'المهام والمواعيد القادمة': 'Upcoming Tasks & Due Dates',
            'معلومات سريعة': 'Quick Info',
            'أصول تحتاج صيانة': 'Assets Needing Maintenance',
            'أصول خارج الخدمة': 'Out of Service Assets',
            'عمليات منتهية': 'Completed Operations',
            'مفقود': 'Lost',
            'متهلك / تالف': 'Damaged / Depreciated',
            'أحدث طلبات الدعم الفني': 'Recent Support Tickets',
            'رقم التذكرة': 'Ticket No',
            'العنوان': 'Title',
            'الحالة': 'Status',
            'تاريخ الإنشاء': 'Created Date',
            'لا توجد تذاكر بعد': 'No tickets yet',
            'لوحة المعلومات': 'Dashboard',
            'نظام إدارة الأصول': 'Asset Management System',
            'نحو إدارة أذكى للأصول': 'Smarter Asset Management',
            'الرئيسية': 'Home',
            'الأصول': 'Assets',
            'المخزون': 'Warehouse',
            'العهد': 'Custody',
            'الدعم الفني': 'Support',
            'التقارير': 'Reports',
            'الإعدادات': 'Settings',
            'الملف الشخصي': 'My Profile',
            'تغيير كلمة المرور': 'Change Password',
            'تسجيل الخروج': 'Sign Out',
            'مدير النظام': 'System Admin',
            'مدير شركة': 'Company Manager',
            'فني دعم': 'Support Tech',
            'موظف': 'Employee',
            'تصفية': 'Filter',
            'بحث': 'Search',
            'إعادة تعيين': 'Reset',
            'إغلاق': 'Close',
            'حفظ': 'Save',
            'إلغاء': 'Cancel',
            'تعديل': 'Edit',
            'حذف': 'Delete',
            'تفاصيل': 'Details',
            'إضافة': 'Add',
            'تصدير Excel': 'Export Excel',
            'طباعة': 'Print',
            'الإشعارات': 'Notifications',
            'تحديد الكل كمقروء': 'Mark all as read',
            'عرض كل الإشعارات': 'View all notifications',
            'مسح QR': 'Scan QR',
            'إدارة العهد': 'Custody Management',
            'عهد الموظفين': 'Employee Custodies',
            'تسليم عهدة': 'Assign Custody',
            'استرجاع عهدة': 'Return Custody',
            'مقبولة': 'Accepted',
            'مرفوضة': 'Rejected',
            'تم الإرجاع': 'Returned',
            'بانتظار الموافقة': 'Pending Approval',
            'قيد التنفيذ': 'In Progress',
            'تم الحل': 'Resolved',
            'مغلقة': 'Closed',
            'ملغاة': 'Cancelled',
            'مفتوحة': 'Open',
            'مُكلَّف بها': 'Assigned',
            'بانتظار قطع غيار': 'Waiting for Parts',
            'تسجيل الدخول': 'Sign In',
            'أدخل بياناتك للمتابعة': 'Enter your credentials to continue',
            'البريد الإلكتروني': 'Email Address',
            'كلمة المرور': 'Password',
            'تذكّرني على هذا الجهاز': 'Remember me on this device',
            'دخول': 'Sign In',
            'نظام إدارة وتتبع الأصول والدعم الفني': 'Asset Tracking & IT Support System',
            'تتبع الأصول لحظياً': 'Real-time Asset Tracking',
            'إدارة العقود والموردين': 'Contract & Vendor Management',
            'نظام تذاكر الصيانة': 'Maintenance Ticket System',
            'تنبيهات ومواعيد دورية': 'Periodic Alerts & Audits',
            'تقارير وإحصائيات متقدمة': 'Advanced Reports & Analytics',
            'سجل تدقيق كامل للعمليات': 'Full Audit Logs'
        },

        // قاموس التحويل من الإنجليزية إلى العربية
        en2ar: {}
    };

    // إنشاء القاموس العكسي تلقائياً
    Object.keys(T.ar2en).forEach(function (ar) {
        T.en2ar[T.ar2en[ar]] = ar;
    });

    // أسماء الأيام والشهور العربية
    var arDays = {
        'Sunday': 'الأحد', 'Monday': 'الإثنين', 'Tuesday': 'الثلاثاء',
        'Wednesday': 'الأربعاء', 'Thursday': 'الخميس', 'Friday': 'الجمعة', 'Saturday': 'السبت'
    };
    var enDays = {
        'الأحد': 'Sunday', 'الإثنين': 'Monday', 'الثلاثاء': 'Tuesday',
        'الأربعاء': 'Wednesday', 'الخميس': 'Thursday', 'الجمعة': 'Friday', 'السبت': 'Saturday'
    };
    var arMonths = {
        'January': 'يناير', 'February': 'فبراير', 'March': 'مارس', 'April': 'أبريل',
        'May': 'مايو', 'June': 'يونيو', 'July': 'يوليو', 'August': 'أغسطس',
        'September': 'سبتمبر', 'October': 'أكتوبر', 'November': 'نوفمبر', 'December': 'ديسمبر'
    };
    var enMonths = {
        'يناير': 'January', 'فبراير': 'February', 'مارس': 'March', 'أبريل': 'April',
        'مايو': 'May', 'يونيو': 'June', 'يوليو': 'July', 'أغسطس': 'August',
        'سبتمبر': 'September', 'أكتوبر': 'October', 'نوفمبر': 'November', 'ديسمبر': 'December'
    };

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
    function translateText(text, targetLang) {
        if (!text) return text;
        var trimmed = text.trim();
        if (!trimmed) return text;

        if (targetLang === 'en') {
            // فحص القاموس المباشر
            if (T.ar2en[trimmed]) return text.replace(trimmed, T.ar2en[trimmed]);

            // مرحبا فلان
            if (trimmed.indexOf('مرحباً') === 0 || trimmed.indexOf('مرحبا') === 0) {
                return text.replace(/مرحب[اًا]\s*/g, 'Welcome ');
            }
            // تذكرة رقم
            if (trimmed.indexOf('تذكرة رقم') !== -1) {
                return text.replace(/تذكرة رقم/g, 'Ticket #');
            }
            // تنتظرك X مهام اليوم
            if (trimmed.indexOf('تنتظرك') !== -1 && trimmed.indexOf('مهام') !== -1) {
                return text.replace(/تنتظرك\s*(\d+)\s*مهام اليوم!?/g, 'You have $1 tasks waiting today!');
            }

            // ترجمة التواريخ (مثل: الثلاثاء / 06 أكتوبر 2026)
            var res = text;
            Object.keys(enDays).forEach(function(arD) {
                if (res.indexOf(arD) !== -1) res = res.replace(new RegExp(arD, 'g'), enDays[arD]);
            });
            Object.keys(enMonths).forEach(function(arM) {
                if (res.indexOf(arM) !== -1) res = res.replace(new RegExp(arM, 'g'), enMonths[arM]);
            });
            return res;
        } else {
            // إلى العربية
            if (T.en2ar[trimmed]) return text.replace(trimmed, T.en2ar[trimmed]);

            if (trimmed.indexOf('Welcome ') === 0) {
                return text.replace(/Welcome\s+/g, 'مرحباً ');
            }
            if (trimmed.indexOf('Ticket #') !== -1) {
                return text.replace(/Ticket\s*#/g, 'تذكرة رقم ');
            }
            if (trimmed.indexOf('You have') !== -1 && trimmed.indexOf('tasks waiting') !== -1) {
                return text.replace(/You have\s*(\d+)\s*tasks waiting today!?/g, 'تنتظرك $1 مهام اليوم!');
            }

            var resAr = text;
            Object.keys(arDays).forEach(function(enD) {
                if (resAr.indexOf(enD) !== -1) resAr = resAr.replace(new RegExp(enD, 'g'), arDays[enD]);
            });
            Object.keys(arMonths).forEach(function(enM) {
                if (resAr.indexOf(enM) !== -1) resAr = resAr.replace(new RegExp(enM, 'g'), arMonths[enM]);
            });
            return resAr;
        }
    }

    /* ─────────────── فحص وترجمة عقدة DOM ───────────────────── */
    function translateNodeTree(root, targetLang) {
        if (!root) return;

        // ترجمة النصوص داخل Text Nodes مباشرة
        var walker = document.createTreeWalker(
            root,
            NodeFilter.SHOW_TEXT,
            {
                acceptNode: function(node) {
                    var parent = node.parentElement;
                    if (!parent) return NodeFilter.FILTER_REJECT;
                    var tag = parent.tagName;
                    if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'CODE') {
                        return NodeFilter.FILTER_REJECT;
                    }
                    if (parent.classList.contains('ats-lang-text') || parent.classList.contains('ats-lang-toggle')) {
                        return NodeFilter.FILTER_REJECT;
                    }
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
            // حفظ النص العربي الأصلي أول مرة في خاصية على العنصر
            if (!n._originalArabic) {
                n._originalArabic = n.nodeValue;
            }

            if (targetLang === 'en') {
                n.nodeValue = translateText(n._originalArabic, 'en');
            } else {
                n.nodeValue = n._originalArabic;
            }
        });

        // ترجمة حقول placeholder و title
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
    }

    /* ─────────────── تطبيق اللغة ──────────────────────────── */
    function applyLang(lang) {
        var html = document.documentElement;
        if (lang === 'en') {
            html.setAttribute('lang', 'en');
            html.setAttribute('dir', 'ltr');
        } else {
            html.setAttribute('lang', 'ar');
            html.setAttribute('dir', 'rtl');
        }

        // ترجمة كل نصوص الصفحة في الـ body
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
    }

    /* ─────── تطبيق الإعدادات فور تحميل الصفحة (بدون وميض) ── */
    applyTheme(getTheme());

    document.addEventListener('DOMContentLoaded', function () {
        applyLang(getLang());
        bindButtons();
    });

    /* ─────────────── ربط أزرار التبديل ───────────────────── */
    function bindButtons() {
        document.querySelectorAll('.ats-theme-toggle').forEach(function (themeBtn) {
            themeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                setTheme(getTheme() === 'dark' ? 'light' : 'dark');
            });
        });

        document.querySelectorAll('.ats-lang-toggle').forEach(function (langBtn) {
            langBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                setLang(getLang() === 'ar' ? 'en' : 'ar');
            });
        });
    }

    // تصدير دوال ATS للاستخدام العام
    window.ATS = window.ATS || {};
    window.ATS.setLang  = setLang;
    window.ATS.setTheme = setTheme;
    window.ATS.getLang  = getLang;
    window.ATS.getTheme = getTheme;
    window.ATS.translateText = translateText;
})();
