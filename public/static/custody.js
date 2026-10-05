/* =====================================================================
   وحدة العهد: عهدي · الإقرار · الإرجاع · التسليم · سجل العهد
   ===================================================================== */
(function () {
  'use strict'
  const A = window.A

  A.renderCustody = async function () {
    const isMgr = A.isManager()
    A.setContent(A.spinner())
    const d = await A.call('get', '/custody/my')

    const pendingBlock = d.pending && d.pending.length
      ? `<div class="bg-amber-50 border border-amber-300 rounded-xl overflow-hidden mb-4">
          <header class="px-4 py-3 border-b border-amber-200 flex items-center gap-2">
            <i class="fas fa-hourglass-half text-amber-600"></i>
            <h3 class="font-bold text-amber-800 text-sm">عهد بانتظار إقرارك (${d.pending.length})</h3>
          </header>
          <div class="divide-y divide-amber-200">
            ${d.pending
              .map(
                (p) => `<div class="p-4 flex flex-wrap items-center gap-3">
                  <div class="flex-1 min-w-[200px]">
                    <p class="font-bold text-slate-800 text-sm">${A.esc(p.asset_name)}</p>
                    <p class="text-xs font-mono text-slate-500">${A.esc(p.asset_tag)}</p>
                    <p class="text-[11px] text-slate-500 mt-1">
                      سُلّمت بواسطة: <b>${A.esc(p.assigned_by_name || '—')}</b> · ${A.dt(p.transfer_date)}
                      ${p.reason ? '<br>السبب: ' + A.esc(p.reason) : ''}
                    </p>
                  </div>
                  <div class="flex gap-2">
                    ${A.btn({ label: 'أقبل العهدة', icon: 'fa-check', variant: 'success', size: 'sm', onclick: `A.custodyRespond(${p.id},true)` })}
                    ${A.btn({ label: 'أرفض', icon: 'fa-xmark', variant: 'danger', size: 'sm', onclick: `A.custodyRespond(${p.id},false)` })}
                  </div>
                </div>`
              )
              .join('')}
          </div>
        </div>`
      : ''

    A.setContent(`
      ${A.pageHeader(
        'العهد',
        'الأصول المسجلة في عهدتك وسجل التسليم والاستلام',
        isMgr ? A.btn({ label: 'سجل العهد الكامل', icon: 'fa-list', variant: 'secondary', size: 'sm', onclick: "location.hash='#/custody/logs'" }) : ''
      )}
      ${pendingBlock}
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        ${A.statCard({ label: 'أصول في عهدتي', value: A.num((d.current || []).length), icon: 'fa-boxes-stacked', color: 'blue' })}
        ${A.statCard({ label: 'بانتظار الإقرار', value: A.num((d.pending || []).length), icon: 'fa-hourglass-half', color: 'amber' })}
        ${A.statCard({
          label: 'القيمة الدفترية',
          value: A.money((d.current || []).reduce((s, x) => s + Number(x.book_value || 0), 0)),
          icon: 'fa-sack-dollar',
          color: 'green'
        })}
        ${A.statCard({ label: 'حركات سابقة', value: A.num((d.history || []).length), icon: 'fa-clock-rotate-left', color: 'slate' })}
      </div>

      ${A.panel(
        'الأصول في عهدتي (' + (d.current || []).length + ')',
        A.table(
          [
            { label: 'الرقم', render: (r) => `<span class="font-mono text-xs font-bold text-brand-700">${A.esc(r.asset_tag)}</span>` },
            { label: 'الأصل', render: (r) => `<span class="font-semibold">${A.esc(r.name)}</span>${r.brand || r.model ? `<br><span class="text-[10px] text-slate-400">${A.esc([r.brand, r.model].filter(Boolean).join(' · '))}</span>` : ''}` },
            { label: 'التصنيف', key: 'category_name' },
            { label: 'الموقع', key: 'location_name' },
            { label: 'الحالة', render: (r) => A.statusBadge(r.status) },
            { label: 'القيمة', render: (r) => `<span class="text-xs">${A.money(r.book_value)}</span>` },
            {
              label: 'إجراءات',
              render: (r) =>
                `<div class="flex gap-1">
                  <button onclick="event.stopPropagation();A.ticketForm(${r.id})" class="text-amber-600 hover:text-amber-800 px-1" title="الإبلاغ عن مشكلة"><i class="fas fa-triangle-exclamation"></i></button>
                  <button onclick="event.stopPropagation();A.custodyReturn(${r.id},'${A.esc(r.name)}')" class="text-red-600 hover:text-red-800 px-1" title="إرجاع العهدة"><i class="fas fa-rotate-left"></i></button>
                  <a href="#/assets/${r.id}" onclick="event.stopPropagation()" class="text-brand-600 px-1" title="التفاصيل"><i class="fas fa-circle-info"></i></a>
                </div>`
            }
          ],
          d.current || [],
          { empty: 'لا توجد أصول في عهدتك' }
        ),
        `${A.btn({ label: 'تصدير', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportMyCustody()' })}`
      )}

      <div class="mt-4">
        ${A.panel(
          'سجل حركات عهدتي (' + (d.history || []).length + ')',
          A.table(
            [
              { label: 'الأصل', render: (r) => `${A.esc(r.asset_name)}<br><span class="text-[10px] font-mono text-slate-400">${A.esc(r.asset_tag)}</span>` },
              { label: 'الإجراء', render: (r) => A.badge(A.tr('custodyAction', r.action_type), r.action_type === 'Return' ? 'amber' : r.action_type === 'Transfer' ? 'purple' : 'blue') },
              { label: 'من', render: (r) => A.esc(r.previous_user_name || '—') },
              { label: 'إلى', render: (r) => A.esc(r.new_user_name || '—') },
              {
                label: 'حالة الإقرار',
                render: (r) =>
                  A.badge(A.tr('custodyStatus', r.acceptance_status), r.acceptance_status === 'Accepted' ? 'green' : r.acceptance_status === 'Rejected' ? 'red' : 'amber')
              },
              { label: 'السبب', render: (r) => `<span class="text-xs text-slate-500">${A.esc(r.reason || '')}</span>` },
              { label: 'التاريخ', render: (r) => `<span class="text-xs">${A.dt(r.transfer_date)}</span>` }
            ],
            d.history || [],
            { empty: 'لا يوجد سجل' }
          )
        )}
      </div>`)

    A.cache._myCustody = d.current || []
  }

  A.exportMyCustody = function () {
    const rows = A.cache._myCustody || []
    if (!rows.length) return A.toast('لا توجد بيانات', 'warn')
    A.csv('my-custody.csv', [
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الاسم', key: 'name' },
      { label: 'التصنيف', key: 'category_name' },
      { label: 'الموقع', key: 'location_name' },
      { label: 'الحالة', value: (r) => A.tr('assetStatus', r.status) },
      { label: 'القيمة الدفترية', key: 'book_value' }
    ], rows)
  }

  /* ------------------------------------------------------------------
     الإقرار بالقبول / الرفض
     ------------------------------------------------------------------ */
  A.custodyRespond = function (logId, accept) {
    A.modal({
      title: accept ? 'إقرار قبول العهدة' : 'رفض العهدة',
      size: 'sm',
      okLabel: accept ? 'أقر بالاستلام' : 'تأكيد الرفض',
      body: accept
        ? `<p class="text-sm text-slate-600 mb-3 leading-relaxed">بالإقرار أنت تتحمل مسؤولية العهدة والمحافظة عليها وفق سياسات الشركة.</p>
           ${A.txt({ name: 'note', label: 'ملاحظة (اختياري)', rows: 2 })}`
        : `<p class="text-sm text-slate-600 mb-3">يرجى ذكر سبب الرفض:</p>
           ${A.txt({ name: 'note', label: 'سبب الرفض', rows: 3, required: true })}`,
      onSubmit: async function (d) {
        if (!accept && !d.note) {
          A.toast('سبب الرفض مطلوب', 'error')
          throw new Error('v')
        }
        await A.call('post', '/custody/' + logId + '/respond', { accept: accept, note: d.note })
        A.toast(accept ? 'تم الإقرار بالاستلام' : 'تم رفض العهدة', 'success')
        A.closeModal()
        A.refreshUnread()
        A.dispatch()
      }
    })
  }

  /* ------------------------------------------------------------------
     تسليم عهدة (مدير)
     ------------------------------------------------------------------ */
  A.custodyAssign = async function (assetId) {
    const users = await A.lookup('users')
    A.modal({
      title: 'تسليم عهدة',
      size: 'sm',
      okLabel: 'إرسال للإقرار',
      body: `${A.sel({ name: 'new_user_id', label: 'المستخدم المستلم', required: true, options: A.opt(users, 'id', 'full_name') })}
             ${A.txt({ name: 'reason', label: 'السبب / ملاحظات', rows: 3, placeholder: 'تسليم لأغراض العمل' })}
             <p class="text-[11px] text-blue-700 bg-blue-50 border border-blue-200 rounded-lg p-2 mt-2">
               <i class="fas fa-circle-info"></i> سيصل إشعار للمستخدم ولن تُسجَّل العهدة إلا بعد إقراره بالاستلام.
             </p>`,
      onSubmit: async function (d) {
        if (!d.new_user_id) {
          A.toast('اختر المستخدم', 'error')
          throw new Error('v')
        }
        await A.call('post', '/custody/assign', { asset_id: assetId, new_user_id: d.new_user_id, reason: d.reason })
        A.toast('تم إرسال العهدة للإقرار', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ------------------------------------------------------------------
     إرجاع عهدة
     ------------------------------------------------------------------ */
  A.custodyReturn = function (assetId, name) {
    A.modal({
      title: 'إرجاع العهدة',
      size: 'sm',
      okLabel: 'تأكيد الإرجاع',
      body: `<p class="text-sm text-slate-600 mb-3">إرجاع: <b>${A.esc(name || '')}</b></p>
             ${A.sel({ name: 'condition', label: 'حالة الأصل عند الإرجاع', options: [
               { value: 'Active', label: 'سليم — نشط' },
               { value: 'InStore', label: 'سليم — إلى المخزن' },
               { value: 'Damaged', label: 'تالف' }
             ], value: 'InStore', empty: false })}
             ${A.txt({ name: 'reason', label: 'السبب / ملاحظات', rows: 3, required: true })}`,
      onSubmit: async function (d) {
        if (!d.reason) {
          A.toast('السبب مطلوب', 'error')
          throw new Error('v')
        }
        await A.call('post', '/custody/return', { asset_id: assetId, reason: d.reason, condition: d.condition })
        A.toast('تم إرجاع العهدة', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ------------------------------------------------------------------
     نقل موقع
     ------------------------------------------------------------------ */
  A.custodyMoveLocation = async function (assetId) {
    const locs = await A.lookup('locations')
    A.modal({
      title: 'نقل الأصل لموقع آخر',
      size: 'sm',
      okLabel: 'نقل',
      body: `${A.sel({ name: 'new_location_id', label: 'الموقع الجديد', required: true, options: A.opt(locs) })}
             ${A.txt({ name: 'reason', label: 'سبب النقل', rows: 3, required: true })}`,
      onSubmit: async function (d) {
        if (!d.new_location_id || !d.reason) {
          A.toast('الموقع والسبب مطلوبان', 'error')
          throw new Error('v')
        }
        await A.call('post', '/custody/transfer-location', { asset_id: assetId, new_location_id: d.new_location_id, reason: d.reason })
        A.toast('تم نقل الأصل', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ------------------------------------------------------------------
     سجل العهد الكامل (مدير)
     ------------------------------------------------------------------ */
  A.renderCustodyLogs = async function () {
    A.setContent(A.spinner())
    const d = await A.call('get', '/custody/logs')
    const rows = d.items || []
    A.cache._custodyLogs = rows

    const counts = { Pending: 0, Accepted: 0, Rejected: 0 }
    rows.forEach((r) => {
      if (counts[r.acceptance_status] !== undefined) counts[r.acceptance_status]++
    })

    A.setContent(`
      ${A.pageHeader(
        'سجل العهد الكامل',
        'جميع حركات تسليم واستلام العهد',
        `${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportCustodyLogs()' })}
         ${A.btn({ label: 'رجوع', icon: 'fa-arrow-right', variant: 'secondary', size: 'sm', onclick: "location.hash='#/custody'" })}`
      )}
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        ${A.statCard({ label: 'إجمالي الحركات', value: A.num(rows.length), icon: 'fa-list', color: 'blue' })}
        ${A.statCard({ label: 'بانتظار الإقرار', value: A.num(counts.Pending), icon: 'fa-hourglass-half', color: 'amber' })}
        ${A.statCard({ label: 'مقبولة', value: A.num(counts.Accepted), icon: 'fa-circle-check', color: 'green' })}
        ${A.statCard({ label: 'مرفوضة', value: A.num(counts.Rejected), icon: 'fa-circle-xmark', color: 'red' })}
      </div>
      ${A.panel(
        'الحركات',
        A.table(
          [
            { label: '#', key: 'id', cls: 'text-slate-400 text-xs' },
            { label: 'الأصل', render: (r) => `${A.esc(r.asset_name)}<br><span class="text-[10px] font-mono text-slate-400">${A.esc(r.asset_tag)}</span>` },
            { label: 'الإجراء', render: (r) => A.badge(A.tr('custodyAction', r.action_type), r.action_type === 'Return' ? 'amber' : r.action_type === 'Transfer' ? 'purple' : 'blue') },
            { label: 'من', render: (r) => A.esc(r.previous_user_name || '—') },
            { label: 'إلى', render: (r) => A.esc(r.new_user_name || '—') },
            { label: 'بواسطة', key: 'assigned_by_name' },
            {
              label: 'الإقرار',
              render: (r) =>
                A.badge(A.tr('custodyStatus', r.acceptance_status), r.acceptance_status === 'Accepted' ? 'green' : r.acceptance_status === 'Rejected' ? 'red' : 'amber') +
                (r.rejection_reason ? `<br><span class="text-[10px] text-red-500">${A.esc(r.rejection_reason)}</span>` : '')
            },
            { label: 'التاريخ', render: (r) => `<span class="text-xs">${A.dt(r.transfer_date)}</span>` }
          ],
          rows,
          { empty: 'لا يوجد سجل' }
        )
      )}`)
  }

  A.exportCustodyLogs = function () {
    const rows = A.cache._custodyLogs || []
    if (!rows.length) return A.toast('لا توجد بيانات', 'warn')
    A.csv('custody-logs.csv', [
      { label: 'م', key: 'id' },
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الأصل', key: 'asset_name' },
      { label: 'الإجراء', value: (r) => A.tr('custodyAction', r.action_type) },
      { label: 'من', key: 'previous_user_name' },
      { label: 'إلى', key: 'new_user_name' },
      { label: 'بواسطة', key: 'assigned_by_name' },
      { label: 'حالة الإقرار', value: (r) => A.tr('custodyStatus', r.acceptance_status) },
      { label: 'السبب', key: 'reason' },
      { label: 'التاريخ', key: 'transfer_date' }
    ], rows)
  }

  A.route('/custody', A.renderCustody)
  A.route('/custody/logs', A.renderCustodyLogs, { roles: ['Admin', 'CompanyManager'] })
})()
