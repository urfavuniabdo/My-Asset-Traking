/* ═══════════════════════════════════════════════════════
   نظام إدارة الأصول — سكربتات عامة
   ═══════════════════════════════════════════════════════ */
(function () {
    'use strict';

    // ── القائمة الجانبية على الشاشات الصغيرة ──────────────
    var toggle = document.getElementById('sidebar-toggle');
    var sidebar = document.getElementById('sidebar');
    var backdrop = document.getElementById('sidebar-backdrop');

    function closeSidebar() {
        if (sidebar) sidebar.classList.remove('open');
        if (backdrop) backdrop.classList.remove('show');
    }

    if (toggle && sidebar) {
        toggle.addEventListener('click', function () {
            sidebar.classList.toggle('open');
            if (backdrop) backdrop.classList.toggle('show');
        });
    }
    if (backdrop) backdrop.addEventListener('click', closeSidebar);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeSidebar();
    });

    // ── إخفاء التنبيهات تلقائياً بعد 6 ثوانٍ ───────────────
    setTimeout(function () {
        document.querySelectorAll('.alert-dismissible').forEach(function (el) {
            if (window.bootstrap && bootstrap.Alert) {
                var a = bootstrap.Alert.getOrCreateInstance(el);
                if (a) a.close();
            }
        });
    }, 6000);

    // ── تأكيد قبل التنفيذ: data-confirm="نص السؤال" ────────
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var msg = form.getAttribute && form.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) {
            e.preventDefault();
            e.stopPropagation();
        }
    }, true);

    document.addEventListener('click', function (e) {
        var el = e.target.closest('[data-confirm-link]');
        if (el && !window.confirm(el.getAttribute('data-confirm-link'))) {
            e.preventDefault();
        }
    });

    // ── إرسال النموذج تلقائياً عند تغيير الفلتر ────────────
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () {
            var f = el.closest('form');
            if (f) f.submit();
        });
    });

    // ── منع الإرسال المزدوج ───────────────────────────────
    document.querySelectorAll('form[data-once]').forEach(function (f) {
        f.addEventListener('submit', function () {
            var btns = f.querySelectorAll('button[type=submit]');
            btns.forEach(function (b) {
                b.disabled = true;
                b.insertAdjacentHTML('afterbegin',
                    '<span class="spinner-border spinner-border-sm me-1"></span>');
            });
        });
    });

    // ── تعبئة بيانات الدخول التجريبية ─────────────────────
    document.querySelectorAll('.demo-fill').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var email = btn.getAttribute('data-email');
            var pass = btn.getAttribute('data-pass') || 'Admin@123';
            var eIn = document.getElementById('Email');
            var pIn = document.getElementById('Password');
            if (eIn) eIn.value = email;
            if (pIn) pIn.value = pass;
            if (eIn) eIn.focus();
        });
    });

    // ── تحديد الكل في الجداول (العمليات المجمّعة) ──────────
    var selectAll = document.getElementById('select-all');
    if (selectAll) {
        selectAll.addEventListener('change', function () {
            document.querySelectorAll('.row-check').forEach(function (c) {
                c.checked = selectAll.checked;
            });
            updateBulkBar();
        });
        document.querySelectorAll('.row-check').forEach(function (c) {
            c.addEventListener('change', updateBulkBar);
        });
    }

    function updateBulkBar() {
        var n = document.querySelectorAll('.row-check:checked').length;
        var bar = document.getElementById('bulk-bar');
        var cnt = document.getElementById('bulk-count');
        if (cnt) cnt.textContent = n;
        if (bar) bar.classList.toggle('d-none', n === 0);
    }

    // ── نسخ للحافظة ───────────────────────────────────────
    document.addEventListener('click', function (e) {
        var el = e.target.closest('[data-copy]');
        if (!el) return;
        e.preventDefault();
        var txt = el.getAttribute('data-copy');
        if (navigator.clipboard) {
            navigator.clipboard.writeText(txt).then(function () {
                var old = el.innerHTML;
                el.innerHTML = '<i class="bi bi-check2"></i>';
                setTimeout(function () { el.innerHTML = old; }, 1400);
            });
        }
    });

    // ── تفعيل التلميحات ───────────────────────────────────
    if (window.bootstrap && bootstrap.Tooltip) {
        document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    }

    // ── مساعد عام لبناء رسم بياني ─────────────────────────
    window.ATS = window.ATS || {};

    ATS.palette = ['#0f766e', '#0891b2', '#7c3aed', '#db2777', '#ea580c',
                   '#65a30d', '#0284c7', '#9333ea', '#dc2626', '#ca8a04'];

    ATS.fmtMoney = function (v) {
        if (v === null || v === undefined) return '—';
        return Number(v).toLocaleString('en-US', {
            minimumFractionDigits: 2, maximumFractionDigits: 2
        }) + ' ج.م';
    };

    ATS.doughnut = function (canvasId, labels, values, colors) {
        var el = document.getElementById(canvasId);
        if (!el || !window.Chart || !labels.length) return null;
        return new Chart(el, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors || ATS.palette,
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                animation: { duration: 420 },
                maintainAspectRatio: false,
                cutout: '58%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            font: { family: 'Cairo', size: 12 },
                            padding: 12,
                            usePointStyle: true,
                            boxWidth: 8
                        }
                    }
                }
            }
        });
    };

    ATS.bar = function (canvasId, labels, values, label, horizontal) {
        var el = document.getElementById(canvasId);
        if (!el || !window.Chart || !labels.length) return null;
        return new Chart(el, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: label || '',
                    data: values,
                    backgroundColor: ATS.palette.map(function (c) { return c + 'cc'; }),
                    borderRadius: 6,
                    maxBarThickness: 46
                }]
            },
            options: {
                indexAxis: horizontal ? 'y' : 'x',
                responsive: true,
                animation: { duration: 420 },
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: {
                        ticks: { font: { family: 'Cairo', size: 11 } },
                        grid: { display: !horizontal }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: { font: { family: 'Cairo', size: 11 }, precision: 0 },
                        grid: { color: '#eef2f6' }
                    }
                }
            }
        });
    };

    ATS.line = function (canvasId, labels, values, label) {
        var el = document.getElementById(canvasId);
        if (!el || !window.Chart || !labels.length) return null;
        return new Chart(el, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: label || '',
                    data: values,
                    borderColor: '#0f766e',
                    backgroundColor: 'rgba(15,118,110,.12)',
                    fill: true,
                    tension: .35,
                    pointRadius: 3,
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                animation: { duration: 420 },
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { ticks: { font: { family: 'Cairo', size: 11 } } },
                    y: {
                        beginAtZero: true,
                        ticks: { font: { family: 'Cairo', size: 11 }, precision: 0 },
                        grid: { color: '#eef2f6' }
                    }
                }
            }
        });
    };
})();
