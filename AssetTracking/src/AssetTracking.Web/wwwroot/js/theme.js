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
        } else {
            html.removeAttribute('data-theme');
        }
        // تحديث أيقونة الزر
        var btn = document.getElementById('ats-theme-btn');
        if (btn) {
            var icon = btn.querySelector('i');
            if (icon) {
                icon.className = theme === 'dark'
                    ? 'fas fa-sun text-amber-400'
                    : 'fas fa-moon';
            }
            btn.title = theme === 'dark'
                ? t('pref.light')
                : t('pref.dark');
        }
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
                } else {
                    el.textContent = val;
                }
            }
        });

        // ترجمة data-i18n-title (title attribute)
        document.querySelectorAll('[data-i18n-title]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-title');
            var val = T[lang] && T[lang][key];
            if (val !== undefined) el.title = val;
        });

        // تحديث زر اللغة
        var btn = document.getElementById('ats-lang-btn');
        if (btn) {
            btn.textContent = lang === 'ar' ? 'EN' : 'عربي';
            btn.title       = lang === 'ar' ? 'Switch to English' : 'التبديل إلى العربية';
        }
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
        var themeBtn = document.getElementById('ats-theme-btn');
        if (themeBtn) {
            themeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                setTheme(getTheme() === 'dark' ? 'light' : 'dark');
            });
        }

        var langBtn = document.getElementById('ats-lang-btn');
        if (langBtn) {
            langBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                setLang(getLang() === 'ar' ? 'en' : 'ar');
            });
        }
    }

    // تصدير دالة الترجمة للاستخدام الخارجي
    window.ATS = window.ATS || {};
    window.ATS.t = t;
    window.ATS.setLang  = setLang;
    window.ATS.setTheme = setTheme;
    window.ATS.getLang  = getLang;
    window.ATS.getTheme = getTheme;
})();
