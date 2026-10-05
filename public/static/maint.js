/* =====================================================================
   وحدة الصيانة الوقائية والجرد الدوري
   ===================================================================== */
(function () {
  'use strict'
  const A = window.A

  const RECUR = ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual']
  const recurOpts = RECUR.map((r) => ({ value: r, label: A.L.recurrence[r] }))

  /* ==================================================================
     1. الصيانة الوقائية
     ================================================================== */
  A.renderSchedules = async function () {
    A.setContent(A.spinner())
    const d = await A.call('get', '/schedules')
    const rows = d.items || []
    A.cache._schedules = rows
    const isMgr = A.isManager()

    const overdue = rows.filter((r) => r.is_overdue && r.is_active).length
    const soon = rows.filter((r) => !r.is_overdue && r.is_active && new Date(r.next_due_date) <= new Date(Date.now() + 14 * 864e5)).length
    const active = rows.filter((r) => r.is_active).length

    A.setContent(`
      ${A.pageHeader(
        'الصيانة الوقائية',
        'جدولة مهام الصيانة الدورية للأصول',
        `${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportSchedules()' })}
         ${isMgr ? A.btn({ label: 'توليد التذاكر المستحقة', icon: 'fa-bolt', variant: 'warn', size: 'sm', onclick: "A.runJob('generate-schedules')" }) : ''}
         ${isMgr ? A.btn({ label: 'جدولة جديدة', icon: 'fa-plus', size: 'sm', onclick: 'A.scheduleForm(null)' }) : ''}`
      )}
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        ${A.statCard({ label: 'إجمالي الجدولات', value: A.num(rows.length), icon: 'fa-calendar-days', color: 'blue' })}
        ${A.statCard({ label: 'نشطة', value: A.num(active), icon: 'fa-circle-check', color: 'green' })}
        ${A.statCard({ label: 'متأخرة', value: A.num(overdue), icon: 'fa-triangle-exclamation', color: 'red' })}
        ${A.statCard({ label: 'مستحقة خلال ١٤ يوم', value: A.num(soon), icon: 'fa-clock', color: 'amber' })}
      </div>
      ${A.panel(
        'الجدولات',
        A.table(
          [
            { label: 'المهمة', render: (r) => `<span class="font-semibold">${A.esc(r.title)}</span>` },
            { label: 'الأصل', render: (r) => `<a href="#/assets/${r.asset_id}" class="text-brand-600 hover:underline">${A.esc(r.asset_name)}</a><br><span class="text-[10px] font-mono text-slate-400">${A.esc(r.asset_tag)}</span>` },
            { label: 'الموقع', key: 'location_name' },
            { label: 'التكرار', render: (r) => A.badge(A.tr('recurrence', r.recurrence_type), 'purple') },
            {
              label: 'الاستحقاق القادم',
              render: (r) =>
                `<span class="text-xs font-bold ${r.is_overdue ? 'text-red-600' : 'text-slate-700'}">${A.date(r.next_due_date)}</span>${
                  r.is_overdue ? ' <i class="fas fa-triangle-exclamation text-red-500 text-[10px]"></i>' : ''
                }`
            },
            { label: 'آخر تنفيذ', render: (r) => `<span class="text-xs text-slate-500">${r.last_generated_date ? A.date(r.last_generated_date) : '—'}</span>` },
            { label: 'الحالة', render: (r) => (r.is_active ? A.badge('نشطة', 'green') : A.badge('موقوفة', 'slate')) },
            ...(isMgr
              ? [
                  {
                    label: 'إجراءات',
                    render: (r) =>
                      `<div class="flex gap-1">
                        <button onclick="A.scheduleForm(${r.id})" class="text-brand-600 hover:text-brand-800 px-1" title="تعديل"><i class="fas fa-pen text-xs"></i></button>
                        <button onclick="A.scheduleToggle(${r.id},${r.is_active ? 0 : 1})" class="text-amber-600 hover:text-amber-800 px-1" title="${r.is_active ? 'إيقاف' : 'تنشيط'}"><i class="fas fa-power-off text-xs"></i></button>
                        <button onclick="A.scheduleDelete(${r.id})" class="text-red-600 hover:text-red-800 px-1" title="حذف"><i class="fas fa-trash text-xs"></i></button>
                      </div>`
                  }
                ]
              : [])
          ],
          rows,
          { empty: 'لا توجد جدولات صيانة وقائية' }
        )
      )}`)
  }

  A.exportSchedules = function () {
    const rows = A.cache._schedules || []
    if (!rows.length) return A.toast('لا توجد بيانات', 'warn')
    A.csv('schedules.csv', [
      { label: 'المهمة', key: 'title' },
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الأصل', key: 'asset_name' },
      { label: 'الموقع', key: 'location_name' },
      { label: 'التكرار', value: (r) => A.tr('recurrence', r.recurrence_type) },
      { label: 'الاستحقاق', key: 'next_due_date' },
      { label: 'آخر تنفيذ', key: 'last_generated_date' },
      { label: 'نشطة', value: (r) => (r.is_active ? 'نعم' : 'لا') }
    ], rows)
  }

  A.scheduleForm = async function (id) {
    let item = {}
    if (id) {
      const rows = A.cache._schedules || []
      item = rows.filter((x) => x.id === Number(id))[0] || {}
    }
    let assetOpts = []
    if (!id) {
      const ad = await A.call('get', '/assets?size=100')
      assetOpts = (ad.items || []).map((x) => ({ value: x.id, label: x.asset_tag + ' — ' + x.name }))
    }
    let checklist = ''
    try {
      if (item.checklist_json) checklist = JSON.parse(item.checklist_json).join('\n')
    } catch (e) {
      checklist = item.checklist_json || ''
    }

    A.modal({
      title: id ? 'تعديل الجدولة' : 'جدولة صيانة وقائية جديدة',
      size: 'md',
      okLabel: id ? 'تحديث' : 'حفظ',
      body: `
        ${id ? `<div class="bg-slate-50 border border-slate-200 rounded-lg p-3 mb-3 text-sm"><b>${A.esc(item.asset_name || '')}</b> <span class="font-mono text-xs text-slate-500">${A.esc(item.asset_tag || '')}</span></div>` : A.sel({ name: 'asset_id', label: 'الأصل', required: true, options: assetOpts })}
        ${A.inp({ name: 'title', label: 'عنوان المهمة', required: true, value: item.title, placeholder: 'صيانة دورية شاملة' })}
        <div class="grid md:grid-cols-2 gap-3">
          ${A.sel({ name: 'recurrence_type', label: 'التكرار', required: true, empty: false, value: item.recurrence_type || 'Quarterly', options: recurOpts })}
          ${A.inp({ name: 'next_due_date', label: 'تاريخ الاستحقاق القادم', type: 'date', required: true, value: item.next_due_date })}
        </div>
        ${id ? A.chk({ name: 'is_active', label: 'نشطة', value: item.is_active ? true : false, wrap: 'mt-2' }) : ''}
        ${!id ? A.txt({ name: 'checklist', label: 'قائمة الفحص (بند في كل سطر)', rows: 4, value: checklist, placeholder: 'تنظيف المراوح\nفحص الكابلات\nتحديث النظام' }) : ''}`,
      onSubmit: async function (dd) {
        if (id) {
          await A.call('put', '/schedules/' + id, dd)
          A.toast('تم التحديث', 'success')
        } else {
          if (!dd.asset_id || !dd.title || !dd.next_due_date) {
            A.toast('الأصل والعنوان والتاريخ مطلوبة', 'error')
            throw new Error('v')
          }
          await A.call('post', '/schedules', dd)
          A.toast('تم إنشاء الجدولة', 'success')
        }
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.scheduleToggle = async function (id, active) {
    const rows = A.cache._schedules || []
    const item = rows.filter((x) => x.id === Number(id))[0] || {}
    try {
      await A.call('put', '/schedules/' + id, {
        title: item.title,
        recurrence_type: item.recurrence_type,
        next_due_date: item.next_due_date,
        is_active: active
      })
      A.toast(active ? 'تم التنشيط' : 'تم الإيقاف', 'success')
      A.dispatch()
    } catch (e) {}
  }

  A.scheduleDelete = async function (id) {
    const ok = await A.confirm({ title: 'حذف الجدولة', message: 'هل تريد حذف جدولة الصيانة؟', danger: true, okLabel: 'حذف' })
    if (!ok) return
    try {
      await A.call('delete', '/schedules/' + id)
      A.toast('تم الحذف', 'success')
      A.dispatch()
    } catch (e) {}
  }

  /* ==================================================================
     2. الجرد الدوري — القائمة
     ================================================================== */
  A.renderAudits = async function () {
    A.setContent(A.spinner())
    const d = await A.call('get', '/audits')
    const rows = d.items || []
    A.cache._audits = rows
    const isMgr = A.isManager()

    A.setContent(`
      ${A.pageHeader(
        'الجرد الدوري',
        'جلسات الجرد بالمسح الميداني ومطابقة المواقع',
        isMgr ? A.btn({ label: 'جلسة جرد جديدة', icon: 'fa-plus', size: 'sm', onclick: 'A.auditForm()' }) : ''
      )}
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        ${A.statCard({ label: 'إجمالي الجلسات', value: A.num(rows.length), icon: 'fa-clipboard-list', color: 'blue' })}
        ${A.statCard({ label: 'قيد التنفيذ', value: A.num(rows.filter((r) => r.status === 'InProgress').length), icon: 'fa-spinner', color: 'amber' })}
        ${A.statCard({ label: 'مكتملة', value: A.num(rows.filter((r) => r.status === 'Completed').length), icon: 'fa-circle-check', color: 'green' })}
        ${A.statCard({ label: 'أصول مفقودة', value: A.num(rows.reduce((s, r) => s + Number(r.missing_count || 0), 0)), icon: 'fa-triangle-exclamation', color: 'red' })}
      </div>
      ${A.panel(
        'جلسات الجرد',
        A.table(
          [
            { label: '#', key: 'id', cls: 'text-slate-400 text-xs' },
            { label: 'العنوان', render: (r) => `<span class="font-semibold">${A.esc(r.title)}</span>` },
            { label: 'الموقع', key: 'location_name' },
            ...(A.user.role === 'Admin' ? [{ label: 'الشركة', key: 'company_name' }] : []),
            { label: 'المتوقع', render: (r) => `<span class="text-xs font-bold">${A.num(r.items_count)}</span>` },
            { label: 'تم العثور', render: (r) => `<span class="text-xs font-bold text-green-700">${A.num(r.found_count)}</span>` },
            { label: 'مفقود', render: (r) => `<span class="text-xs font-bold ${Number(r.missing_count) ? 'text-red-600' : 'text-slate-400'}">${A.num(r.missing_count)}</span>` },
            {
              label: 'النسبة',
              render: (r) => {
                const pct = r.items_count ? Math.round((Number(r.found_count) / Number(r.items_count)) * 100) : 0
                return `<div class="w-20"><div class="h-1.5 bg-slate-100 rounded-full overflow-hidden"><div class="h-full ${pct >= 90 ? 'bg-green-500' : pct >= 60 ? 'bg-amber-500' : 'bg-red-500'}" style="width:${pct}%"></div></div><span class="text-[10px] text-slate-500">${pct}%</span></div>`
              }
            },
            { label: 'الحالة', render: (r) => A.badge(A.tr('auditStatus', r.status), r.status === 'Completed' ? 'green' : r.status === 'InProgress' ? 'amber' : 'slate') },
            { label: 'أنشأها', key: 'created_by_name' },
            { label: '', render: (r) => `<a href="#/audits/${r.id}" class="text-brand-600 hover:text-brand-800"><i class="fas fa-arrow-left"></i></a>` }
          ],
          rows,
          { rowHref: (r) => '#/audits/' + r.id, empty: 'لا توجد جلسات جرد' }
        )
      )}`)
  }

  A.auditForm = async function () {
    const locs = await A.lookup('locations')
    let comps = []
    if (A.user.role === 'Admin') comps = await A.lookup('companies')
    A.modal({
      title: 'جلسة جرد جديدة',
      size: 'sm',
      okLabel: 'بدء الجلسة',
      body: `${A.inp({ name: 'title', label: 'عنوان الجلسة', required: true, placeholder: 'جرد الربع الأول — المقر الرئيسي' })}
             ${A.user.role === 'Admin' ? A.sel({ name: 'company_id', label: 'الشركة', required: true, options: A.opt(comps) }) : ''}
             ${A.sel({ name: 'location_id', label: 'الموقع', required: true, options: A.opt(locs) })}
             <p class="text-[11px] text-blue-700 bg-blue-50 border border-blue-200 rounded-lg p-2 mt-2">
               <i class="fas fa-circle-info"></i> سيتم أخذ لقطة (snapshot) لكل الأصول المسجلة في هذا الموقع كأصول متوقعة.
             </p>`,
      onSubmit: async function (d) {
        if (!d.title || !d.location_id || (A.user.role === 'Admin' && !d.company_id)) {
          A.toast('العنوان والموقع والشركة مطلوبة', 'error')
          throw new Error('v')
        }
        const r = await A.call('post', '/audits', d)
        A.toast('تم إنشاء الجلسة — ' + r.expected + ' أصل متوقع', 'success')
        A.closeModal()
        location.hash = '#/audits/' + r.id
      }
    })
  }

  /* ==================================================================
     3. جلسة الجرد — التفاصيل والمسح
     ================================================================== */
  let auditScanner = null

  A.renderAuditDetail = async function (params) {
    const id = params.id
    A.setContent(A.spinner())
    const d = await A.call('get', '/audits/' + id)
    const a = d.item
    const items = d.items || []
    A.cache._auditItems = items
    const isMgr = A.isManager()
    const open = a.status !== 'Completed'

    const g = { Expected: 0, Found: 0, Missing: 0, WrongLocation: 0, Damaged: 0 }
    items.forEach((x) => {
      if (g[x.result] !== undefined) g[x.result]++
    })

    A.setContent(`
      ${A.pageHeader(
        a.title,
        (a.location_name || '') + ' · ' + (a.company_name || '') + ' · ' + A.tr('auditStatus', a.status),
        `${A.btn({ label: 'رجوع', icon: 'fa-arrow-right', variant: 'secondary', size: 'sm', onclick: "location.hash='#/audits'" })}
         ${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportAuditItems()' })}
         ${open && isMgr ? A.btn({ label: 'إنهاء الجلسة', icon: 'fa-flag-checkered', variant: 'danger', size: 'sm', onclick: `A.auditComplete(${a.id})` }) : ''}`
      )}

      <div class="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-4">
        ${A.statCard({ label: 'إجمالي المتوقع', value: A.num(items.length), icon: 'fa-list', color: 'blue' })}
        ${A.statCard({ label: 'لم يُمسح بعد', value: A.num(g.Expected), icon: 'fa-hourglass', color: 'slate' })}
        ${A.statCard({ label: 'تم العثور', value: A.num(g.Found), icon: 'fa-circle-check', color: 'green' })}
        ${A.statCard({ label: 'موقع خطأ', value: A.num(g.WrongLocation), icon: 'fa-location-crosshairs', color: 'amber' })}
        ${A.statCard({ label: 'مفقود', value: A.num(g.Missing), icon: 'fa-circle-xmark', color: 'red' })}
      </div>

      ${
        open
          ? `<div class="grid lg:grid-cols-2 gap-4 mb-4">
              ${A.panel(
                'مسح سريع',
                `<div class="p-4">
                  <form id="audit-scan-form" class="flex gap-2 items-end">
                    <div class="flex-1">${A.inp({ name: 'asset_tag', label: 'رقم الأصل / السيريال', required: true, placeholder: 'AST-2026-00001', attrs: 'autocomplete="off"' })}</div>
                    ${A.btn({ label: 'تسجيل', icon: 'fa-check', type: 'submit' })}
                  </form>
                  <div id="audit-scan-log" class="mt-3 space-y-1.5 max-h-40 overflow-y-auto"></div>
                </div>`
              )}
              ${A.panel(
                'المسح بالكاميرا',
                `<div class="p-4">
                  <div id="audit-reader" class="rounded-lg bg-slate-900 min-h-[180px] flex items-center justify-center text-slate-400 text-sm">
                    <div class="text-center p-4"><i class="fas fa-camera text-2xl mb-2 block"></i><p>اضغط للتشغيل</p></div>
                  </div>
                  <div class="flex gap-2 mt-3">
                    ${A.btn({ label: 'تشغيل', icon: 'fa-camera', size: 'sm', onclick: `A.auditStartScan(${a.id})` })}
                    ${A.btn({ label: 'إيقاف', icon: 'fa-stop', variant: 'secondary', size: 'sm', onclick: 'A.auditStopScan()' })}
                  </div>
                  <p id="audit-cam-msg" class="text-xs text-slate-500 mt-2"></p>
                </div>`
              )}
             </div>`
          : `<div class="bg-green-50 border border-green-300 rounded-xl px-4 py-3 mb-4 flex items-center gap-2">
              <i class="fas fa-circle-check text-green-600"></i>
              <p class="text-sm font-bold text-green-800">الجلسة مكتملة — تم إنهاؤها في ${A.dt(a.completed_at)}</p>
             </div>`
      }

      ${A.panel(
        'بنود الجرد (' + items.length + ')',
        A.table(
          [
            { label: 'رقم الأصل', render: (r) => `<span class="font-mono text-xs font-bold">${A.esc(r.asset_tag)}</span>` },
            { label: 'الأصل', render: (r) => `<a href="#/assets/${r.asset_id}" class="text-brand-600 hover:underline">${A.esc(r.asset_name)}</a>` },
            { label: 'الموقع المسجل', key: 'asset_location_name' },
            { label: 'حالة الأصل', render: (r) => A.statusBadge(r.asset_status) },
            { label: 'نتيجة الجرد', render: (r) => A.badge(A.tr('auditResult', r.result), A.L.auditResultColor[r.result]) },
            { label: 'مسحه', key: 'scanned_by_name' },
            { label: 'وقت المسح', render: (r) => `<span class="text-xs">${r.scanned_at ? A.dt(r.scanned_at) : '—'}</span>` },
            { label: 'ملاحظات', render: (r) => `<span class="text-xs text-slate-500">${A.esc(r.notes || '')}</span>` }
          ],
          items,
          { empty: 'لا توجد بنود' }
        )
      )}`)

    const f = document.getElementById('audit-scan-form')
    if (f) {
      f.addEventListener('submit', async function (e) {
        e.preventDefault()
        const inp = f.querySelector('[name=asset_tag]')
        const tag = (inp.value || '').trim()
        if (!tag) return
        inp.value = ''
        await A.auditScan(id, tag)
      })
    }
  }

  A.auditScan = async function (auditId, tag) {
    const log = document.getElementById('audit-scan-log')
    try {
      const r = await A.api('post', '/audits/' + auditId + '/scan', { asset_tag: tag })
      if (log) {
        log.insertAdjacentHTML(
          'afterbegin',
          `<div class="text-xs ${r.result === 'Found' ? 'bg-green-50 border-green-200' : 'bg-amber-50 border-amber-200'} border rounded-lg px-2.5 py-1.5 flex items-center gap-2">
            <i class="fas fa-circle-check ${r.result === 'Found' ? 'text-green-600' : 'text-amber-600'}"></i>
            <span class="font-mono font-bold">${A.esc(r.asset && r.asset.asset_tag)}</span>
            <span>${A.esc(r.asset && r.asset.name)}</span>
            <span class="mr-auto">${A.badge(A.tr('auditResult', r.result), A.L.auditResultColor[r.result])}</span>
          </div>`
        )
      }
      A.toast('تم تسجيل: ' + A.tr('auditResult', r.result), r.result === 'Found' ? 'success' : 'warn')
      // refresh counts silently
      setTimeout(function () {
        if (A.currentPath() === '/audits/' + auditId) A.dispatch()
      }, 700)
    } catch (e) {
      if (log) {
        log.insertAdjacentHTML(
          'afterbegin',
          `<div class="text-xs bg-red-50 border border-red-200 rounded-lg px-2.5 py-1.5 flex items-center gap-2">
            <i class="fas fa-circle-xmark text-red-600"></i><span class="font-mono">${A.esc(tag)}</span><span class="mr-auto">${A.esc(e.message)}</span>
          </div>`
        )
      }
      A.toast(e.message, 'error')
    }
  }

  A.auditStartScan = function (auditId) {
    const msg = document.getElementById('audit-cam-msg')
    if (typeof Html5Qrcode === 'undefined') {
      msg.textContent = 'مكتبة المسح غير متاحة — استخدم الإدخال اليدوي'
      return
    }
    A.auditStopScan()
    try {
      document.getElementById('audit-reader').innerHTML = ''
      auditScanner = new Html5Qrcode('audit-reader')
      auditScanner
        .start({ facingMode: 'environment' }, { fps: 10, qrbox: { width: 200, height: 200 } }, function (text) {
          let tag = text
          const m = String(text).match(/\/a\/([^/?#]+)/)
          if (m) tag = decodeURIComponent(m[1])
          A.auditScan(auditId, tag)
        })
        .then(function () {
          msg.textContent = 'الكاميرا تعمل — امسح الأصول تتابعاً'
        })
        .catch(function (e) {
          msg.textContent = 'تعذر تشغيل الكاميرا: ' + (e.message || e)
        })
    } catch (e) {
      msg.textContent = 'خطأ: ' + e.message
    }
  }

  A.auditStopScan = function () {
    if (auditScanner) {
      try {
        auditScanner.stop().then(function () {
          try {
            auditScanner.clear()
          } catch (e) {}
          auditScanner = null
        })
      } catch (e) {
        auditScanner = null
      }
    }
  }

  A.auditComplete = async function (id) {
    const ok = await A.confirm({
      title: 'إنهاء جلسة الجرد',
      message: 'سيتم تعليم كل الأصول التي لم تُمسح كـ "مفقودة" ولا يمكن التعديل بعد الإنهاء. هل تريد المتابعة؟',
      danger: true,
      okLabel: 'إنهاء'
    })
    if (!ok) return
    try {
      const r = await A.call('post', '/audits/' + id + '/complete')
      A.toast('تم إنهاء الجلسة — ' + r.missing + ' أصل مفقود', r.missing ? 'warn' : 'success')
      A.dispatch()
    } catch (e) {}
  }

  A.exportAuditItems = function () {
    const rows = A.cache._auditItems || []
    if (!rows.length) return A.toast('لا توجد بيانات', 'warn')
    A.csv('audit-items.csv', [
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الأصل', key: 'asset_name' },
      { label: 'الموقع المسجل', key: 'asset_location_name' },
      { label: 'حالة الأصل', value: (r) => A.tr('assetStatus', r.asset_status) },
      { label: 'نتيجة الجرد', value: (r) => A.tr('auditResult', r.result) },
      { label: 'مسحه', key: 'scanned_by_name' },
      { label: 'وقت المسح', key: 'scanned_at' },
      { label: 'ملاحظات', key: 'notes' }
    ], rows)
  }

  A.route('/schedules', A.renderSchedules, { roles: ['Admin', 'CompanyManager', 'Technician'] })
  A.route('/audits', A.renderAudits, { roles: ['Admin', 'CompanyManager', 'Technician'] })
  A.route('/audits/:id', A.renderAuditDetail, { roles: ['Admin', 'CompanyManager', 'Technician'] })
})()
