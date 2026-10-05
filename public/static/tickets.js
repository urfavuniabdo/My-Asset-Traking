/* =====================================================================
   وحدة تذاكر الصيانة: القائمة · التفاصيل · الإسناد · الحالة · التعليقات · القطع
   ===================================================================== */
(function () {
  'use strict'
  const A = window.A

  const T_STATUS = ['Open', 'Assigned', 'InProgress', 'WaitingParts', 'Resolved', 'Closed', 'Cancelled']
  const PRIOS = ['Low', 'Medium', 'High', 'Critical']
  const statusOpts = T_STATUS.map((s) => ({ value: s, label: A.L.ticketStatus[s] }))
  const prioOpts = PRIOS.map((s) => ({ value: s, label: A.L.priority[s] }))

  let TL = { q: '', status: '', priority: '', technician_id: '', breached: '', page: 1, size: 25 }

  /* ==================================================================
     1. قائمة التذاكر
     ================================================================== */
  A.renderTickets = async function (params, query) {
    TL = Object.assign({ q: '', status: '', priority: '', technician_id: '', breached: '', page: 1, size: 25 }, query || {})
    TL.page = Number(TL.page) || 1
    const isMgr = A.isManager()

    let techs = []
    if (isMgr) {
      try {
        techs = await A.lookup('technicians')
      } catch (e) {}
    }

    A.setContent(`
      ${A.pageHeader(
        'تذاكر الصيانة',
        A.user.role === 'Employee' ? 'التذاكر التي قدّمتها' : A.user.role === 'Technician' ? 'التذاكر المُسندة لي والتذاكر المتاحة' : 'إدارة جميع تذاكر الصيانة',
        `${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportTickets()' })}
         ${A.btn({ label: 'تذكرة جديدة', icon: 'fa-plus', size: 'sm', onclick: 'A.ticketForm(null)' })}`
      )}

      <div class="bg-white rounded-xl border border-slate-200 p-3 mb-4">
        <form id="ticket-filters" class="grid md:grid-cols-5 gap-2 items-end">
          ${A.inp({ name: 'q', label: 'بحث', value: TL.q, placeholder: 'رقم التذكرة / وصف المشكلة' })}
          ${A.sel({ name: 'status', label: 'الحالة', value: TL.status, options: statusOpts, empty: 'كل الحالات' })}
          ${A.sel({ name: 'priority', label: 'الأولوية', value: TL.priority, options: prioOpts, empty: 'كل الأولويات' })}
          ${isMgr ? A.sel({ name: 'technician_id', label: 'الفني', value: TL.technician_id, options: A.opt(techs, 'id', 'full_name'), empty: 'كل الفنيين' }) : ''}
          ${A.sel({ name: 'breached', label: 'مخالفة SLA', value: TL.breached, options: [{ value: '1', label: 'مخالفة فقط' }], empty: 'الكل' })}
          <div class="md:col-span-5 flex gap-2">
            ${A.btn({ label: 'تصفية', icon: 'fa-filter', type: 'submit' })}
            ${A.btn({ label: 'إفراغ', icon: 'fa-rotate-left', variant: 'secondary', onclick: "location.hash='#/tickets'" })}
          </div>
        </form>
      </div>
      <div id="tickets-result">${A.spinner()}</div>`)

    document.getElementById('ticket-filters').addEventListener('submit', function (e) {
      e.preventDefault()
      const d = A.formData(e.target)
      const qs = []
      Object.keys(d).forEach((k) => {
        if (d[k]) qs.push(k + '=' + encodeURIComponent(d[k]))
      })
      location.hash = '#/tickets' + (qs.length ? '?' + qs.join('&') : '')
    })

    await loadTickets()
  }

  async function loadTickets() {
    const qs = Object.keys(TL)
      .filter((k) => TL[k] !== '' && TL[k] !== null && TL[k] !== undefined)
      .map((k) => k + '=' + encodeURIComponent(TL[k]))
      .join('&')
    const d = await A.call('get', '/tickets?' + qs)
    A.cache._lastTickets = d.items

    const cols = [
      { label: 'الرقم', render: (r) => `<span class="font-mono text-xs font-bold text-brand-700">${A.esc(r.ticket_number)}</span>${r.sla_breached ? ' <i class="fas fa-triangle-exclamation text-red-500 text-[10px]" title="مخالفة SLA"></i>' : ''}` },
      { label: 'الأصل', render: (r) => `<span class="font-semibold">${A.esc(r.asset_name)}</span><br><span class="text-[10px] font-mono text-slate-400">${A.esc(r.asset_tag)}</span>` },
      { label: 'المشكلة', render: (r) => `<span class="text-xs text-slate-600">${A.esc(String(r.issue_description || '').substring(0, 60))}</span>` },
      { label: 'مقدم الطلب', key: 'requester_name' },
      { label: 'الفني', render: (r) => (r.technician_name ? A.esc(r.technician_name) : '<span class="text-amber-600 text-xs font-bold">غير مُسندة</span>') },
      { label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
      { label: 'الحالة', render: (r) => A.ticketBadge(r.status) },
      { label: 'SLA', render: (r) => A.slaCell(r) },
      { label: 'التاريخ', render: (r) => `<span class="text-xs text-slate-500">${A.ago(r.created_at)}</span>` }
    ]
    if (A.user.role === 'Admin') cols.splice(4, 0, { label: 'الشركة', key: 'company_name' })

    document.getElementById('tickets-result').innerHTML = `
      <div class="bg-white rounded-xl border border-slate-200 overflow-hidden">
        <div class="px-4 py-2.5 border-b border-slate-100 text-xs text-slate-600">
          النتائج: <b class="text-slate-800">${A.num(d.total)}</b> تذكرة
        </div>
        ${A.table(cols, d.items, { rowHref: (r) => '#/tickets/' + r.id, empty: 'لا توجد تذاكر مطابقة' })}
        ${tPager(d)}
      </div>`
  }

  function tPager(d) {
    if (!d.pages || d.pages <= 1) return ''
    const btns = []
    const start = Math.max(1, d.page - 2)
    const end = Math.min(d.pages, start + 4)
    for (let p = start; p <= end; p++) {
      btns.push(
        `<button onclick="A.pageTickets(${p})" class="min-w-[32px] px-2 py-1 rounded border text-xs ${
          p === d.page ? 'bg-brand-600 text-white border-brand-600 font-bold' : 'bg-white border-slate-200 hover:bg-slate-50'
        }">${p}</button>`
      )
    }
    return `<div class="px-4 py-3 border-t border-slate-100 flex items-center justify-between">
      <span class="text-xs text-slate-500">صفحة ${d.page} من ${d.pages}</span>
      <div class="flex gap-1">${btns.join('')}</div></div>`
  }

  A.pageTickets = function (p) {
    TL.page = p
    document.getElementById('tickets-result').innerHTML = A.spinner()
    loadTickets()
  }

  A.exportTickets = function () {
    const rows = A.cache._lastTickets || []
    if (!rows.length) return A.toast('لا توجد بيانات', 'warn')
    A.csv('tickets.csv', [
      { label: 'رقم التذكرة', key: 'ticket_number' },
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الأصل', key: 'asset_name' },
      { label: 'المشكلة', key: 'issue_description' },
      { label: 'مقدم الطلب', key: 'requester_name' },
      { label: 'الفني', key: 'technician_name' },
      { label: 'الأولوية', value: (r) => A.tr('priority', r.priority) },
      { label: 'الحالة', value: (r) => A.tr('ticketStatus', r.status) },
      { label: 'مخالفة SLA', value: (r) => (r.sla_breached ? 'نعم' : 'لا') },
      { label: 'التكلفة', key: 'total_cost' },
      { label: 'تاريخ الإنشاء', key: 'created_at' }
    ], rows)
  }

  /* ==================================================================
     2. نموذج تذكرة جديدة
     ================================================================== */
  A.ticketForm = async function (assetId) {
    let assetOptions = []
    let fixedAsset = null
    if (assetId) {
      try {
        const d = await A.api('get', '/assets/' + assetId)
        fixedAsset = d.item
      } catch (e) {}
    }
    if (!fixedAsset) {
      const d = await A.call('get', '/assets?size=100')
      assetOptions = (d.items || []).map((x) => ({ value: x.id, label: x.asset_tag + ' — ' + x.name }))
    }
    let users = []
    if (A.isManager()) {
      try {
        users = await A.lookup('users')
      } catch (e) {}
    }

    A.modal({
      title: 'تذكرة صيانة جديدة',
      size: 'md',
      okLabel: 'إنشاء التذكرة',
      body: `
        ${
          fixedAsset
            ? `<div class="bg-brand-50 border border-brand-200 rounded-lg p-3 mb-3">
                 <p class="text-xs text-slate-500">الأصل</p>
                 <p class="font-bold text-slate-800">${A.esc(fixedAsset.name)} <span class="font-mono text-xs text-brand-700">${A.esc(fixedAsset.asset_tag)}</span></p>
               </div>
               <input type="hidden" name="asset_id" value="${fixedAsset.id}">`
            : A.sel({ name: 'asset_id', label: 'الأصل', required: true, options: assetOptions })
        }
        ${A.txt({ name: 'issue_description', label: 'وصف المشكلة', required: true, rows: 4, placeholder: 'اشرح المشكلة بالتفصيل...' })}
        <div class="grid md:grid-cols-2 gap-3">
          ${A.sel({ name: 'priority', label: 'الأولوية', value: 'Medium', empty: false, options: prioOpts })}
          ${A.sel({ name: 'source', label: 'مصدر الطلب', value: 'Portal', empty: false, options: Object.keys(A.L.source).map((k) => ({ value: k, label: A.L.source[k] })) })}
          ${A.isManager() ? A.sel({ name: 'requester_user_id', label: 'مقدم الطلب (نيابةً عن)', options: A.opt(users, 'id', 'full_name'), empty: '— أنا —', wrap: 'md:col-span-2' }) : ''}
        </div>
        <p class="text-[11px] text-slate-500 mt-2 bg-slate-50 border border-slate-200 rounded-lg p-2">
          <i class="fas fa-stopwatch text-brand-500"></i> سيتم حساب موعد الاستجابة والحل تلقائياً حسب سياسة SLA المرتبطة بالأولوية.
        </p>`,
      onSubmit: async function (d) {
        if (!d.asset_id || !d.issue_description) {
          A.toast('الأصل ووصف المشكلة مطلوبان', 'error')
          throw new Error('v')
        }
        const r = await A.call('post', '/tickets', d)
        A.toast('تم إنشاء التذكرة ' + r.ticket_number, 'success')
        A.closeModal()
        location.hash = '#/tickets/' + r.id
        if (A.currentPath() === '/tickets/' + r.id) A.dispatch()
      }
    })
  }

  /* ==================================================================
     3. تفاصيل التذكرة
     ================================================================== */
  A.renderTicketDetail = async function (params) {
    const id = params.id
    A.setContent(A.spinner())
    const d = await A.call('get', '/tickets/' + id)
    const t = d.item
    const isMgr = A.isManager()
    const isMyTech = A.user.role === 'Technician' && t.assigned_technician_id === A.user.id
    const canWork = isMgr || isMyTech
    const closed = ['Closed', 'Cancelled'].indexOf(t.status) >= 0

    const actions = []
    if (isMgr && !closed) actions.push(A.btn({ label: t.assigned_technician_id ? 'إعادة الإسناد' : 'إسناد لفني', icon: 'fa-user-check', variant: 'secondary', size: 'sm', onclick: `A.ticketAssign(${t.id})` }))
    if (canWork && !closed) {
      if (t.status === 'Assigned' || t.status === 'Open') actions.push(A.btn({ label: 'بدء التنفيذ', icon: 'fa-play', variant: 'warn', size: 'sm', onclick: `A.ticketStatus(${t.id},'InProgress')` }))
      if (t.status === 'InProgress') actions.push(A.btn({ label: 'بانتظار قطع', icon: 'fa-boxes-packing', variant: 'secondary', size: 'sm', onclick: `A.ticketStatus(${t.id},'WaitingParts')` }))
      if (t.status === 'WaitingParts') actions.push(A.btn({ label: 'متابعة التنفيذ', icon: 'fa-play', variant: 'warn', size: 'sm', onclick: `A.ticketStatus(${t.id},'InProgress')` }))
      if (['InProgress', 'WaitingParts', 'Assigned'].indexOf(t.status) >= 0) actions.push(A.btn({ label: 'تم الحل', icon: 'fa-circle-check', variant: 'success', size: 'sm', onclick: `A.ticketResolve(${t.id})` }))
    }
    if (isMgr && !closed) {
      if (t.status === 'Resolved') actions.push(A.btn({ label: 'إغلاق التذكرة', icon: 'fa-lock', size: 'sm', onclick: `A.ticketStatus(${t.id},'Closed')` }))
      actions.push(A.btn({ label: 'إلغاء', icon: 'fa-ban', variant: 'danger', size: 'sm', onclick: `A.ticketStatus(${t.id},'Cancelled')` }))
    }

    const kv = (l, v) =>
      `<div class="flex justify-between gap-2 py-1.5 border-b border-slate-100 last:border-0">
        <span class="text-xs text-slate-500 shrink-0">${A.esc(l)}</span>
        <span class="text-xs font-semibold text-left">${v === null || v === undefined || v === '' ? '—' : v}</span></div>`

    A.setContent(`
      ${A.pageHeader(
        'تذكرة ' + t.ticket_number,
        t.asset_name + ' · ' + t.asset_tag,
        `${A.btn({ label: 'رجوع', icon: 'fa-arrow-right', variant: 'secondary', size: 'sm', onclick: "location.hash='#/tickets'" })} ${actions.join(' ')}`
      )}

      ${
        t.sla_breached
          ? `<div class="bg-red-50 border border-red-300 rounded-xl px-4 py-3 mb-4 flex items-center gap-2">
              <i class="fas fa-triangle-exclamation text-red-600 text-lg"></i>
              <p class="text-sm font-bold text-red-800">تم تجاوز اتفاقية مستوى الخدمة (SLA) لهذه التذكرة</p>
             </div>`
          : ''
      }

      <div class="grid lg:grid-cols-3 gap-4 mb-4">
        <div class="lg:col-span-2 space-y-4">
          ${A.panel(
            'وصف المشكلة',
            `<div class="p-4">
              <div class="flex flex-wrap items-center gap-2 mb-3">
                ${A.ticketBadge(t.status)} ${A.prioBadge(t.priority)}
                ${A.badge(A.tr('source', t.source), 'slate')}
              </div>
              <p class="text-sm text-slate-700 leading-relaxed whitespace-pre-wrap">${A.esc(t.issue_description)}</p>
              ${
                t.resolution_report
                  ? `<div class="mt-4 pt-3 border-t border-slate-100">
                      <p class="text-xs font-bold text-green-700 mb-1"><i class="fas fa-circle-check"></i> تقرير الحل</p>
                      <p class="text-sm text-slate-700 leading-relaxed whitespace-pre-wrap">${A.esc(t.resolution_report)}</p>
                     </div>`
                  : ''
              }
            </div>`
          )}

          ${A.panel(
            'التعليقات (' + d.comments.length + ')',
            `<div class="divide-y divide-slate-100 max-h-[420px] overflow-y-auto">
              ${
                d.comments.length
                  ? d.comments
                      .map(
                        (cm) => `<div class="p-3.5 ${cm.is_internal ? 'bg-amber-50/60' : ''}">
                          <div class="flex items-center gap-2 mb-1">
                            <div class="w-7 h-7 rounded-full bg-brand-100 text-brand-700 flex items-center justify-center text-[11px] font-bold">${A.esc(
                              (cm.user_name || '?').charAt(0)
                            )}</div>
                            <span class="text-xs font-bold text-slate-700">${A.esc(cm.user_name)}</span>
                            <span class="text-[10px] text-slate-400">${A.esc(A.tr('role', cm.user_role))}</span>
                            ${cm.is_internal ? A.badge('داخلي', 'amber') : ''}
                            <span class="mr-auto text-[10px] text-slate-400">${A.dt(cm.created_at)}</span>
                          </div>
                          <p class="text-sm text-slate-700 leading-relaxed whitespace-pre-wrap pr-9">${A.esc(cm.comment_text)}</p>
                        </div>`
                      )
                      .join('')
                  : A.empty('لا توجد تعليقات', 'fa-comments')
              }
            </div>
            ${
              closed
                ? ''
                : `<form id="comment-form" class="p-3 border-t border-slate-200 bg-slate-50">
                    ${A.txt({ name: 'comment_text', rows: 2, placeholder: 'أضف تعليقاً...', required: true })}
                    <div class="flex items-center justify-between mt-2">
                      ${canWork ? A.chk({ name: 'is_internal', label: 'تعليق داخلي (لا يظهر للموظف)' }) : '<span></span>'}
                      ${A.btn({ label: 'إرسال', icon: 'fa-paper-plane', size: 'sm', type: 'submit' })}
                    </div>
                  </form>`
            }`
          )}

          ${A.panel(
            'قطع الغيار (' + d.parts.length + ')',
            A.table(
              [
                { label: 'القطعة', key: 'part_name' },
                { label: 'الكمية', key: 'quantity' },
                { label: 'سعر الوحدة', render: (r) => `<span class="text-xs">${A.money(r.unit_cost)}</span>` },
                { label: 'الإجمالي', render: (r) => `<span class="text-xs font-bold">${A.money(r.total_cost)}</span>` },
                { label: 'المورّد', key: 'supplier_name' },
                ...(canWork && !closed
                  ? [{ label: '', render: (r) => `<button onclick="A.ticketPartDelete(${t.id},${r.id})" class="text-red-500 hover:text-red-700 px-1"><i class="fas fa-trash text-xs"></i></button>` }]
                  : [])
              ],
              d.parts,
              { empty: 'لم تُضف قطع غيار' }
            ),
            canWork && !closed ? A.btn({ label: 'إضافة قطعة', icon: 'fa-plus', variant: 'secondary', size: 'sm', onclick: `A.ticketPartAdd(${t.id})` }) : ''
          )}
        </div>

        <div class="space-y-4">
          ${A.panel(
            'البيانات',
            `<div class="p-4">
              ${kv('رقم التذكرة', `<span class="font-mono text-brand-700">${A.esc(t.ticket_number)}</span>`)}
              ${kv('الأصل', `<a href="#/assets/${t.asset_id}" class="text-brand-600 hover:underline">${A.esc(t.asset_name)}</a>`)}
              ${kv('رقم الأصل', `<span class="font-mono">${A.esc(t.asset_tag)}</span>`)}
              ${kv('حالة الأصل', A.statusBadge(t.asset_status))}
              ${kv('الموقع', A.esc(t.location_name))}
              ${kv('الشركة', A.esc(t.company_name))}
              ${kv('مقدم الطلب', A.esc(t.requester_name))}
              ${kv('الفني المسؤول', t.technician_name ? A.esc(t.technician_name) : '<span class="text-amber-600">غير مُسندة</span>')}
              ${kv('تاريخ الإنشاء', A.dt(t.created_at))}
              ${kv('أول استجابة', t.first_response_at ? A.dt(t.first_response_at) : '<span class="text-slate-400">لم تحدث</span>')}
              ${kv('تاريخ الحل', t.resolved_at ? A.dt(t.resolved_at) : '—')}
              ${kv('تاريخ الإغلاق', t.closed_at ? A.dt(t.closed_at) : '—')}
            </div>`
          )}

          ${A.panel(
            'اتفاقية مستوى الخدمة',
            `<div class="p-4">
              ${kv('السياسة', A.esc(t.sla_name))}
              ${kv('مدة الاستجابة', t.response_time_hours ? t.response_time_hours + ' ساعة' : '—')}
              ${kv('مدة الحل', t.resolution_time_hours ? t.resolution_time_hours + ' ساعة' : '—')}
              ${kv('موعد الاستجابة', A.dt(t.sla_response_due_at))}
              ${kv('موعد الحل', A.dt(t.sla_resolution_due_at))}
              ${kv('المتبقي', A.slaCell(t))}
              ${kv('الالتزام', t.sla_breached ? A.badge('مخالفة', 'red') : A.badge('ملتزم', 'green'))}
            </div>`
          )}

          ${A.panel(
            'التكاليف',
            `<div class="p-4">
              ${kv('تكلفة العمالة', A.money(t.labor_cost))}
              ${kv('تكلفة القطع', A.money(t.parts_cost))}
              ${kv('الإجمالي', `<span class="text-base font-extrabold text-slate-800">${A.money(t.total_cost)}</span>`)}
            </div>`
          )}

          ${A.panel(
            'سجل الإجراءات (' + d.logs.length + ')',
            `<div class="p-4 max-h-[340px] overflow-y-auto">
              ${
                d.logs.length
                  ? `<ol class="relative border-r-2 border-slate-200 pr-4 space-y-3">
                      ${d.logs
                        .map(
                          (lg) => `<li class="relative">
                            <span class="absolute -right-[21px] top-1 w-3 h-3 rounded-full bg-brand-500 border-2 border-white"></span>
                            <p class="text-xs font-bold text-slate-700">${A.esc(lg.action_type)}
                              ${lg.old_value || lg.new_value ? `<span class="font-normal text-slate-500">${A.esc(lg.old_value || '')} ${lg.old_value ? '→' : ''} ${A.esc(lg.new_value || '')}</span>` : ''}
                            </p>
                            ${lg.notes ? `<p class="text-[11px] text-slate-500">${A.esc(lg.notes)}</p>` : ''}
                            <p class="text-[10px] text-slate-400">${A.esc(lg.user_name || '')} · ${A.dt(lg.created_at)}</p>
                          </li>`
                        )
                        .join('')}
                     </ol>`
                  : A.empty('لا يوجد سجل', 'fa-clock-rotate-left')
              }
            </div>`
          )}
        </div>
      </div>`)

    const cf = document.getElementById('comment-form')
    if (cf) {
      cf.addEventListener('submit', async function (e) {
        e.preventDefault()
        const dd = A.formData(e.target)
        if (!dd.comment_text) return
        try {
          await A.call('post', '/tickets/' + id + '/comments', dd)
          A.toast('تم إضافة التعليق', 'success')
          A.dispatch()
        } catch (ex) {}
      })
    }
  }

  /* ==================================================================
     4. الإجراءات
     ================================================================== */
  A.ticketAssign = async function (id) {
    const techs = await A.call('get', '/tickets/technicians')
    const opts = (techs.items || []).map((x) => ({
      value: x.id,
      label: x.full_name + ' (' + (x.open_tickets || 0) + ' تذكرة مفتوحة)'
    }))
    A.modal({
      title: 'إسناد التذكرة لفني',
      size: 'sm',
      okLabel: 'إسناد',
      body: `${A.sel({ name: 'technician_id', label: 'الفني', required: true, options: opts })}
             ${A.txt({ name: 'notes', label: 'ملاحظات', rows: 2 })}`,
      onSubmit: async function (d) {
        if (!d.technician_id) {
          A.toast('اختر الفني', 'error')
          throw new Error('v')
        }
        await A.call('post', '/tickets/' + id + '/assign', d)
        A.toast('تم الإسناد', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.ticketStatus = function (id, status) {
    const needConfirm = status === 'Cancelled' || status === 'Closed'
    A.modal({
      title: 'تحديث الحالة إلى: ' + A.tr('ticketStatus', status),
      size: 'sm',
      okLabel: 'تأكيد',
      body: `${needConfirm ? `<p class="text-sm text-slate-600 mb-3">${status === 'Cancelled' ? 'سيتم إلغاء التذكرة نهائياً.' : 'سيتم إغلاق التذكرة نهائياً.'}</p>` : ''}
             ${A.txt({ name: 'notes', label: 'ملاحظات', rows: 3 })}`,
      onSubmit: async function (d) {
        await A.call('post', '/tickets/' + id + '/status', { status: status, notes: d.notes })
        A.toast('تم تحديث الحالة', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.ticketResolve = function (id) {
    A.modal({
      title: 'إغلاق فني — تم الحل',
      size: 'md',
      okLabel: 'تسجيل الحل',
      body: `${A.txt({ name: 'resolution_report', label: 'تقرير الحل', required: true, rows: 4, placeholder: 'اشرح ما تم عمله لحل المشكلة...' })}
             <div class="grid md:grid-cols-2 gap-3">
               ${A.inp({ name: 'labor_cost', label: 'تكلفة العمالة', type: 'number', step: '0.01', min: 0, value: 0 })}
               <div class="flex items-end pb-2">${A.chk({ name: 'asset_unrepairable', label: 'الأصل غير قابل للإصلاح (تعليمه كتالف)' })}</div>
             </div>
             ${A.txt({ name: 'notes', label: 'ملاحظات إضافية', rows: 2 })}`,
      onSubmit: async function (d) {
        if (!d.resolution_report) {
          A.toast('تقرير الحل مطلوب', 'error')
          throw new Error('v')
        }
        await A.call('post', '/tickets/' + id + '/status', {
          status: 'Resolved',
          resolution_report: d.resolution_report,
          labor_cost: d.labor_cost,
          asset_unrepairable: d.asset_unrepairable,
          notes: d.notes
        })
        A.toast('تم تسجيل الحل', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.ticketPartAdd = function (id) {
    A.modal({
      title: 'إضافة قطعة غيار',
      size: 'sm',
      okLabel: 'إضافة',
      body: `${A.inp({ name: 'part_name', label: 'اسم القطعة', required: true })}
             <div class="grid grid-cols-2 gap-3">
               ${A.inp({ name: 'quantity', label: 'الكمية', type: 'number', min: 1, value: 1, required: true })}
               ${A.inp({ name: 'unit_cost', label: 'سعر الوحدة', type: 'number', step: '0.01', min: 0, value: 0, required: true })}
             </div>
             ${A.inp({ name: 'supplier_name', label: 'المورّد' })}`,
      onSubmit: async function (d) {
        if (!d.part_name) {
          A.toast('اسم القطعة مطلوب', 'error')
          throw new Error('v')
        }
        await A.call('post', '/tickets/' + id + '/parts', d)
        A.toast('تمت إضافة القطعة', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.ticketPartDelete = async function (id, partId) {
    const ok = await A.confirm({ title: 'حذف القطعة', message: 'هل تريد حذف هذه القطعة؟', danger: true, okLabel: 'حذف' })
    if (!ok) return
    try {
      await A.call('delete', '/tickets/' + id + '/parts/' + partId)
      A.toast('تم الحذف', 'success')
      A.dispatch()
    } catch (e) {}
  }

  A.route('/tickets', A.renderTickets)
  A.route('/tickets/:id', A.renderTicketDetail)
})()
