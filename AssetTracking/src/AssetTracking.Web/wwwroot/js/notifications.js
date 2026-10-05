/* ═══════════════════════════════════════════════════════
   الإشعارات الفورية عبر SignalR + جرس الإشعارات
   ═══════════════════════════════════════════════════════ */
(function () {
    'use strict';

    var bell = document.getElementById('notif-bell');
    if (!bell) return;

    var listEl = document.getElementById('notif-list');
    var countEl = document.getElementById('notif-count');
    var markAllBtn = document.getElementById('notif-mark-all');
    var loaded = false;

    // ── عرض عدد غير المقروء ───────────────────────────────
    function setCount(n) {
        if (!countEl) return;
        if (n > 0) {
            countEl.textContent = n > 99 ? '99+' : String(n);
            countEl.classList.remove('d-none');
        } else {
            countEl.classList.add('d-none');
        }
    }

    function timeAgo(iso) {
        var d = new Date(iso);
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) return 'الآن';
        if (s < 3600) return 'قبل ' + Math.floor(s / 60) + ' دقيقة';
        if (s < 86400) return 'قبل ' + Math.floor(s / 3600) + ' ساعة';
        if (s < 2592000) return 'قبل ' + Math.floor(s / 86400) + ' يوم';
        return d.toLocaleDateString('en-GB');
    }

    function esc(t) {
        var d = document.createElement('div');
        d.textContent = t == null ? '' : t;
        return d.innerHTML;
    }

    function itemHtml(n) {
        var href = n.url || '/Notifications';
        return '<a class="notif-item' + (n.isRead ? '' : ' unread') + '" href="' + esc(href) +
            '" data-id="' + n.id + '">' +
            '<i class="bi ' + esc(n.icon || 'bi-bell') + ' notif-ico"></i>' +
            '<div class="notif-body">' +
            '<p class="notif-title">' + esc(n.title) + '</p>' +
            '<p class="notif-msg">' + esc(n.message) + '</p>' +
            '</div>' +
            '<span class="notif-time">' + timeAgo(n.createdAt) + '</span>' +
            '</a>';
    }

    function render(items) {
        if (!listEl) return;
        if (!items || !items.length) {
            listEl.innerHTML = '<div class="notif-empty">' +
                '<i class="bi bi-bell-slash"></i> لا توجد إشعارات</div>';
            return;
        }
        listEl.innerHTML = items.map(itemHtml).join('');
    }

    // ── تحميل الإشعارات من الـAPI ─────────────────────────
    function load() {
        fetch('/Notifications/Recent', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                render(data.items);
                setCount(data.unreadCount);
                loaded = true;
            })
            .catch(function () {
                if (listEl && !loaded) {
                    listEl.innerHTML = '<div class="notif-empty">' +
                        '<i class="bi bi-wifi-off"></i> تعذّر تحميل الإشعارات</div>';
                }
            });
    }

    // تحميل العدّاد فوراً، والقائمة عند أول فتح
    load();
    bell.addEventListener('click', function () { load(); });

    // ── تحديد الكل كمقروء ─────────────────────────────────
    if (markAllBtn) {
        markAllBtn.addEventListener('click', function () {
            var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
            var body = new FormData();
            if (tokenEl) body.append('__RequestVerificationToken', tokenEl.value);
            fetch('/Notifications/MarkAllRead', {
                method: 'POST',
                body: body,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            }).then(function () {
                setCount(0);
                document.querySelectorAll('.notif-item.unread')
                    .forEach(function (el) { el.classList.remove('unread'); });
            });
        });
    }

    // ── الاتصال الفوري ────────────────────────────────────
    if (!window.signalR) return;

    var conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    conn.on('ReceiveNotification', function (n) {
        // زيادة العدّاد
        var cur = 0;
        if (countEl && !countEl.classList.contains('d-none')) {
            cur = parseInt(countEl.textContent, 10) || 0;
        }
        setCount(cur + 1);

        // إضافة العنصر لأعلى القائمة
        if (listEl) {
            var empty = listEl.querySelector('.notif-empty');
            if (empty) listEl.innerHTML = '';
            n.isRead = false;
            listEl.insertAdjacentHTML('afterbegin', itemHtml(n));
        }

        // تنبيه بصري على الجرس
        bell.classList.add('text-danger');
        setTimeout(function () { bell.classList.remove('text-danger'); }, 2500);

        // إشعار المتصفح إن كان مسموحاً
        if (window.Notification && Notification.permission === 'granted') {
            try { new Notification(n.title, { body: n.message }); } catch (e) { }
        }
    });

    conn.start().catch(function () { /* الاتصال غير متاح — الجرس يعمل بالتحديث اليدوي */ });
})();
