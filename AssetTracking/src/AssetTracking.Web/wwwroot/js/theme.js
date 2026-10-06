/* ═══════════════════════════════════════════════════════════════
   نظام إدارة الأصول — تبديل الثيم (داكن/فاتح) واللغة (عربي/إنجليزي)
   يُخزَّن الإعداد في localStorage ويُطبَّق فور تحميل الصفحة
   ═══════════════════════════════════════════════════════════════ */

(function () {
    'use strict';

    /* ─────────────────────────── الترجمات ──────────────────────────── */
    var T = {
        ar: {
            // ── الشريط العلوي
            'brand.name':          'نظام إدارة الأصول',
            'search.placeholder':  'ابحث عن أصل أو موقع أو رقم...',
            'topbar.scan':         'مسح QR',
            'topbar.notifications':'الإشعارات',
            'topbar.mark_all':     'تحديد الكل كمقروء',
            'topbar.loading':      'جارٍ التحميل…',
            'topbar.view_all':     'عرض كل الإشعارات',
            // ── قائمة المستخدم
            'user.profile':        'الملف الشخصي',
            'user.change_pass':    'تغيير كلمة المرور',
            'user.logout':         'تسجيل الخروج',
            // ── التفضيلات
            'pref.language':       'اللغة',
            'pref.theme':          'المظهر',
            'pref.dark':           'داكن',
            'pref.light':          'فاتح',
            'pref.arabic':         'العربية',
            'pref.english':        'English',
            // ── التنقل
            'nav.home':            'الرئيسية',
            'nav.assets':          'الأصول',
            'nav.warehouses':      'المخزون',
            'nav.custody':         'العهد',
            'nav.tickets':         'الدعم الفني',
            'nav.reports':         'التقارير',
            'nav.admin':           'الإعدادات',
            'nav.footer':          'نحو إدارة أذكى للأصول',
            // ── الأدوار
            'role.admin':          'مدير النظام',
            'role.manager':        'مدير شركة',
            'role.tech':           'فني دعم',
            'role.employee':       'موظف',
            // ── صفحة تسجيل الدخول
            'login.title':         'تسجيل الدخول',
            'login.subtitle':      'أدخل بياناتك للمتابعة',
            'login.app_desc':      'نظام إدارة وتتبع الأصول والدعم الفني',
            'login.side_title':    'نظام إدارة وتتبع\nالأصول والدعم الفني',
            'login.side_desc':     'حل متكامل لإدارة أصولك، تتبعها، وتقديم الدعم الفني بكفاءة ووضوح في مكان واحد',
            'login.email':         'البريد الإلكتروني',
            'login.password':      'كلمة المرور',
            'login.remember_me':   'تذكّرني على هذا الجهاز',
            'login.submit':        'دخول',
            'login.demo_title':    'حسابات تجريبية (كلمة المرور: Admin@123)',
            'login.feat.track':    'تتبع الأصول لحظياً',
            'login.feat.vendor':   'إدارة العقود والموردين',
            'login.feat.tickets':  'نظام تذاكر الصيانة',
            'login.feat.alerts':   'تنبيهات ومواعيد دورية',
            'login.feat.reports':  'تقارير وإحصائيات متقدمة',
            'login.feat.audit':    'سجل تدقيق كامل للعمليات'
        },
        en: {
            // ── Top bar
            'brand.name':          'Asset Tracking',
            'search.placeholder':  'Search asset, location or number...',
            'topbar.scan':         'Scan QR',
            'topbar.notifications':'Notifications',
            'topbar.mark_all':     'Mark all as read',
            'topbar.loading':      'Loading…',
            'topbar.view_all':     'View all notifications',
            // ── User menu
            'user.profile':        'My Profile',
            'user.change_pass':    'Change Password',
            'user.logout':         'Sign Out',
            // ── Preferences
            'pref.language':       'Language',
            'pref.theme':          'Appearance',
            'pref.dark':           'Dark',
            'pref.light':          'Light',
            'pref.arabic':         'العربية',
            'pref.english':        'English',
            // ── Navigation
            'nav.home':            'Home',
            'nav.assets':          'Assets',
            'nav.warehouses':      'Warehouse',
            'nav.custody':         'Custody',
            'nav.tickets':         'Support Tickets',
            'nav.reports':         'Reports',
            'nav.admin':           'Settings',
            'nav.footer':          'Smarter Asset Management',
            // ── Roles
            'role.admin':          'System Admin',
            'role.manager':        'Company Manager',
            'role.tech':           'Support Tech',
            'role.employee':       'Employee',
            // ── Login page
            'login.title':         'Sign In',
            'login.subtitle':      'Enter your credentials to continue',
            'login.app_desc':      'Asset Management, Tracking & Support System',
            'login.side_title':    'Asset Tracking &\nSupport Management',
            'login.side_desc':     'An all-in-one solution for tracking assets, lifecycle management, and providing efficient IT support in one place',
            'login.email':         'Email Address',
            'login.password':      'Password',
            'login.remember_me':   'Remember me on this device',
            'login.submit':        'Sign In',
            'login.demo_title':    'Demo Accounts (Password: Admin@123)',
            'login.feat.track':    'Real-time Asset Tracking',
            'login.feat.vendor':   'Vendor & Contract Management',
            'login.feat.tickets':  'Maintenance & Support Tickets',
            'login.feat.alerts':   'Alerts & Scheduled Audits',
            'login.feat.reports':  'Advanced Analytics & Reports',
            'login.feat.audit':    'Comprehensive Audit Logs'
        }
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
        document.querySelectorAll('.ats-theme-toggle').forEach(function(btn) {
            var icon = btn.querySelector('i');
            if (icon) {
                icon.className = theme === 'dark'
                    ? 'fas fa-sun text-amber-400'
                    : 'fas fa-moon';
            }
            btn.title = theme === 'dark'
                ? t('pref.light')
                : t('pref.dark');
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

        // ترجمة كل العناصر التي تحمل data-i18n
        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            var val = T[lang] && T[lang][key];
            if (val !== undefined) {
                if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                    el.placeholder = val;
                } else if (el.tagName === 'TITLE') {
                    document.title = val;
                } else {
                    el.textContent = val;
                }
            }
        });

        // ترجمة العناصر مع أسطر جديدة (مثل العناوين الجانبية)
        document.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-html');
            var val = T[lang] && T[lang][key];
            if (val !== undefined) {
                el.innerHTML = val.replace(/\n/g, '<br>');
            }
        });

        // ترجمة data-i18n-title (title attribute)
        document.querySelectorAll('[data-i18n-title]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-title');
            var val = T[lang] && T[lang][key];
            if (val !== undefined) el.title = val;
        });

        // تحديث جميع أزرار اللغة
        document.querySelectorAll('.ats-lang-toggle').forEach(function(btn) {
            var txt = btn.querySelector('.ats-lang-text');
            if (txt) {
                txt.textContent = lang === 'ar' ? 'EN' : 'عربي';
            } else {
                btn.textContent = lang === 'ar' ? 'EN' : 'عربي';
            }
            btn.title = lang === 'ar' ? 'Switch to English' : 'التبديل إلى العربية';
        });
    }

    /* ─────────────── مساعد للترجمة ────────────────────────── */
    function t(key) {
        var lang = getLang();
        return (T[lang] && T[lang][key]) || key;
    }

    /* ─────── تطبيق الإعدادات فور تحميل الصفحة (بدون وميض) ── */
    applyTheme(getTheme());
    // اللغة تُطبَّق بعد تحميل DOM
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

    // تصدير دالة الترجمة للاستخدام الخارجي
    window.ATS = window.ATS || {};
    window.ATS.t = t;
    window.ATS.setLang  = setLang;
    window.ATS.setTheme = setTheme;
    window.ATS.getLang  = getLang;
    window.ATS.getTheme = getTheme;
})();
