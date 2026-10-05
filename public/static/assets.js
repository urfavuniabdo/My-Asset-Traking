/* =====================================================================
   وحدة الأصول: القائمة · التفاصيل · النموذج · QR · المسح
   ===================================================================== */
(function () {
  'use strict'
  const A = window.A

  const STATUSES = ['Active', 'UnderMaintenance', 'Damaged', 'Disposed', 'Lost', 'InStore']
  const statusOptions = STATUSES.map((s) => ({ value: s, label: A.L.assetStatus[s] }))

  /* ==================================================================
     1. قائمة الأصول
     ================================================================== */
  let LST = { q: '', status: '', category_id: '', location_id: '', company_id: '', page: 1, size: 25, sort: 'id', dir: 'desc' }

  A.renderAssets = async function (params, query) {
    LST = Object.assign({ q: '', status: '', category_id: '', location_id: '', company_id: '', page: 1, size: 25, sort: 'id', dir: 'desc' }, query || {})
    LST.page = Number(LST.page) || 1

    const isMgr = A.isManager()
    const lookups = { categories: [], locations: [], companies: [] }
    try {
      lookups.categories = await A.lookup('categories')
      lookups.locations = await A.lookup('locations')
      if (A.user.role === 'Admin') lookups.companies = await A.lookup('companies')
    } catch (e) {}

    A.setContent(`
      ${A.pageHeader(
        'الأصول',
        A.user.role === 'Employee' ? 'الأصول المسجلة في عهدتك' : 'إدارة وتتبع جميع الأصول',
        `${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'secondary', size: 'sm', onclick: 'A.exportAssets()' })}
         ${isMgr ? A.btn({ label: 'استيراد جماعي', icon: 'fa-file-import', variant: 'secondary', size: 'sm', onclick: 'A.bulkImport()' }) : ''}
         ${isMgr ? A.btn({ label: 'أصل جديد', icon: 'fa-plus', size: 'sm', onclick: "A.assetForm(null)" }) : ''}`
      )}

      <div class="bg-white rounded-xl border border-slate-200 p-3 mb-4">
        <form id="asset-filters" class="grid md:grid-cols-5 gap-2 items-end">
          ${A.inp({ name: 'q', label: 'بحث', value: LST.q, placeholder: 'رقم / اسم / سيريال / موديل' })}
          ${A.sel({ name: 'status', label: 'الحالة', value: LST.status, options: statusOptions, empty: 'كل الحالات' })}
          ${A.sel({ name: 'category_id', label: 'التصنيف', value: LST.category_id, options: A.opt(lookups.categories), empty: 'كل التصنيفات' })}
          ${A.sel({ name: 'location_id', label: 'الموقع', value: LST.location_id, options: A.opt(lookups.locations), empty: 'كل المواقع' })}
          ${
            A.user.role === 'Admin'
              ? A.sel({ name: 'company_id', label: 'الشركة', value: LST.company_id, options: A.opt(lookups.companies), empty: 'كل الشركات' })
              : `<div class="flex gap-2">${A.btn({ label: 'تصفية', icon: 'fa-filter', type: 'submit', cls: 'flex-1' })}
                 ${A.btn({ label: '', icon: 'fa-rotate-left', variant: 'secondary', onclick: "location.hash='#/assets'" })}</div>`
          }
          ${
            A.user.role === 'Admin'
              ? `<div class="md:col-span-5 flex gap-2">${A.btn({ label: 'تصفية', icon: 'fa-filter', type: 'submit' })}
                 ${A.btn({ label: 'إفراغ', icon: 'fa-rotate-left', variant: 'secondary', onclick: "location.hash='#/assets'" })}</div>`
              : ''
          }
        </form>
      </div>

      <div id="assets-result">${A.spinner()}</div>`)

    document.getElementById('asset-filters').addEventListener('submit', function (e) {
      e.preventDefault()
      const d = A.formData(e.target)
      const qs = []
      Object.keys(d).forEach(function (k) {
        if (d[k]) qs.push(k + '=' + encodeURIComponent(d[k]))
      })
      location.hash = '#/assets' + (qs.length ? '?' + qs.join('&') : '')
    })

    await loadAssetList()
  }

  async function loadAssetList() {
    const qs = Object.keys(LST)
      .filter((k) => LST[k] !== '' && LST[k] !== null && LST[k] !== undefined)
      .map((k) => k + '=' + encodeURIComponent(LST[k]))
      .join('&')
    const d = await A.call('get', '/assets?' + qs)
    A.cache._lastAssets = d.items

    const sortLink = function (key, label) {
      const on = LST.sort === key
      const dir = on && LST.dir === 'asc' ? 'desc' : 'asc'
      return `<button onclick="A.sortAssets('${key}','${dir}')" class="font-bold hover:text-brand-600">
        ${label} ${on ? `<i class="fas fa-sort-${LST.dir === 'asc' ? 'up' : 'down'} text-[10px]"></i>` : '<i class="fas fa-sort text-[9px] text-slate-300"></i>'}
      </button>`
    }

    const cols = [
      { label: 'الرقم', thCls: '', render: (r) => `<span class="font-mono text-xs font-bold text-brand-700">${A.esc(r.asset_tag)}</span>` },
      { label: 'الأصل', render: (r) => `<span class="font-semibold">${A.esc(r.name)}</span>${r.brand || r.model ? `<br><span class="text-[10px] text-slate-400">${A.esc([r.brand, r.model].filter(Boolean).join(' · '))}</span>` : ''}` },
      { label: 'التصنيف', key: 'category_name' },
      { label: 'الموقع', key: 'location_name' },
      { label: 'العهدة', render: (r) => (r.custody_user_name ? A.esc(r.custody_user_name) : '<span class="text-slate-400">غير مسلّم</span>') },
      { label: 'الحالة', render: (r) => A.statusBadge(r.status) },
      { label: 'القيمة الدفترية', render: (r) => `<span class="text-xs">${A.money(r.book_value)}</span>` },
      { label: '', render: (r) => `<a href="#/assets/${r.id}" class="text-brand-600 hover:text-brand-800" title="عرض"><i class="fas fa-arrow-left"></i></a>` }
    ]
    if (A.user.role === 'Admin') cols.splice(4, 0, { label: 'الشركة', key: 'company_name' })

    // header with sorting for main columns
    const tableHtml = A.table(cols, d.items, { rowHref: (r) => '#/assets/' + r.id, empty: 'لا توجد أصول مطابقة' })

    document.getElementById('assets-result').innerHTML = `
      <div class="bg-white rounded-xl border border-slate-200 overflow-hidden">
        <div class="px-4 py-2.5 border-b border-slate-100 flex flex-wrap items-center justify-between gap-2 text-xs">
          <span class="text-slate-600">النتائج: <b class="text-slate-800">${A.num(d.total)}</b> أصل</span>
          <div class="flex items-center gap-1.5">
            <span class="text-slate-500">ترتيب:</span>
            ${['id:الأحدث', 'asset_tag:الرقم', 'name:الاسم', 'purchase_cost:التكلفة', 'book_value:القيمة', 'purchase_date:تاريخ الشراء']
              .map(function (p) {
                const kv = p.split(':')
                return `<button onclick="A.sortAssets('${kv[0]}','${LST.sort === kv[0] && LST.dir === 'desc' ? 'asc' : 'desc'}')"
                  class="px-2 py-1 rounded border ${LST.sort === kv[0] ? 'bg-brand-600 text-white border-brand-600' : 'bg-white border-slate-200 text-slate-600'}">
                  ${kv[1]}${LST.sort === kv[0] ? (LST.dir === 'asc' ? ' ↑' : ' ↓') : ''}</button>`
              })
              .join('')}
          </div>
        </div>
        ${tableHtml}
        ${pager(d)}
      </div>`
  }

  function pager(d) {
    if (!d.pages || d.pages <= 1) return ''
    const cur = d.page
    const btns = []
    const add = (p, label, on) =>
      btns.push(
        `<button onclick="A.pageAssets(${p})" class="min-w-[32px] px-2 py-1 rounded border text-xs ${
          on ? 'bg-brand-600 text-white border-brand-600 font-bold' : 'bg-white border-slate-200 hover:bg-slate-50'
        }">${label}</button>`
      )
    add(Math.max(1, cur - 1), '‹', false)
    const start = Math.max(1, cur - 2)
    const end = Math.min(d.pages, start + 4)
    for (let p = start; p <= end; p++) add(p, p, p === cur)
    add(Math.min(d.pages, cur + 1), '›', false)
    return `<div class="px-4 py-3 border-t border-slate-100 flex items-center justify-between gap-2">
      <span class="text-xs text-slate-500">صفحة ${cur} من ${d.pages}</span>
      <div class="flex gap-1">${btns.join('')}</div>
    </div>`
  }

  A.sortAssets = function (sort, dir) {
    LST.sort = sort
    LST.dir = dir
    LST.page = 1
    document.getElementById('assets-result').innerHTML = A.spinner()
    loadAssetList()
  }

  A.pageAssets = function (p) {
    LST.page = p
    document.getElementById('assets-result').innerHTML = A.spinner()
    loadAssetList()
  }

  A.exportAssets = function () {
    const rows = A.cache._lastAssets || []
    if (!rows.length) return A.toast('لا توجد بيانات للتصدير', 'warn')
    A.csv('assets.csv', [
      { label: 'رقم الأصل', key: 'asset_tag' },
      { label: 'الاسم', key: 'name' },
      { label: 'التصنيف', key: 'category_name' },
      { label: 'الموقع', key: 'location_name' },
      { label: 'الشركة', key: 'company_name' },
      { label: 'العهدة', key: 'custody_user_name' },
      { label: 'الحالة', value: (r) => A.tr('assetStatus', r.status) },
      { label: 'السيريال', key: 'serial_number' },
      { label: 'الماركة', key: 'brand' },
      { label: 'الموديل', key: 'model' },
      { label: 'تكلفة الشراء', key: 'purchase_cost' },
      { label: 'تاريخ الشراء', key: 'purchase_date' },
      { label: 'انتهاء الضمان', key: 'warranty_expiry_date' },
      { label: 'القيمة الدفترية', key: 'book_value' }
    ], rows)
  }

  /* ==================================================================
     2. نموذج إضافة/تعديل أصل
     ================================================================== */
  A.assetForm = async function (id) {
    let item = {}
    if (id) {
      const d = await A.call('get', '/assets/' + id)
      item = d.item
    }
    const [cats, locs, vends, users] = await Promise.all([
      A.lookup('categories'),
      A.lookup('locations'),
      A.lookup('vendors'),
      A.lookup('users')
    ])
    let comps = []
    if (A.user.role === 'Admin') comps = await A.lookup('companies')

    A.modal({
      title: id ? 'تعديل الأصل ' + (item.asset_tag || '') : 'تسجيل أصل جديد',
      size: 'lg',
      okLabel: id ? 'تحديث' : 'حفظ',
      body: `
        <div class="grid md:grid-cols-3 gap-3">
          ${A.inp({ name: 'name', label: 'اسم الأصل', required: true, value: item.name, wrap: 'md:col-span-2' })}
          ${A.user.role === 'Admin' ? A.sel({ name: 'company_id', label: 'الشركة', required: true, value: item.company_id, options: A.opt(comps) }) : ''}
          ${A.sel({ name: 'category_id', label: 'التصنيف', value: item.category_id, options: A.opt(cats) })}
          ${A.sel({ name: 'location_id', label: 'الموقع', value: item.location_id, options: A.opt(locs) })}
          ${A.sel({ name: 'vendor_id', label: 'المورّد', value: item.vendor_id, options: A.opt(vends) })}
          ${A.inp({ name: 'serial_number', label: 'الرقم التسلسلي', value: item.serial_number })}
          ${A.inp({ name: 'barcode', label: 'الباركود', value: item.barcode })}
          ${A.sel({ name: 'status', label: 'الحالة', value: item.status || 'Active', options: statusOptions, empty: false })}
          ${A.inp({ name: 'brand', label: 'الماركة', value: item.brand })}
          ${A.inp({ name: 'model', label: 'الموديل', value: item.model })}
          ${A.sel({ name: 'current_custody_user_id', label: 'العهدة (المستخدم)', value: item.current_custody_user_id, options: A.opt(users, 'id', 'full_name'), empty: '— بدون عهدة —' })}
          ${A.inp({ name: 'purchase_cost', label: 'تكلفة الشراء', type: 'number', step: '0.01', min: 0, value: item.purchase_cost })}
          ${A.inp({ name: 'purchase_date', label: 'تاريخ الشراء', type: 'date', value: item.purchase_date })}
          ${A.inp({ name: 'warranty_expiry_date', label: 'انتهاء الضمان', type: 'date', value: item.warranty_expiry_date })}
          ${A.inp({ name: 'useful_life_years', label: 'العمر الإنتاجي (سنة)', type: 'number', min: 1, value: item.useful_life_years || 5 })}
          ${A.inp({ name: 'salvage_value', label: 'القيمة التخريدية', type: 'number', step: '0.01', min: 0, value: item.salvage_value || 0 })}
          ${A.txt({ name: 'notes', label: 'ملاحظات', value: item.notes, wrap: 'md:col-span-3', rows: 2 })}
        </div>`,
      onSubmit: async function (d) {
        if (!d.name) {
          A.toast('اسم الأصل مطلوب', 'error')
          throw new Error('validation')
        }
        if (id) await A.call('put', '/assets/' + id, d)
        else {
          const r = await A.call('post', '/assets', d)
          A.toast('تم إنشاء الأصل ' + r.asset_tag, 'success')
        }
        if (id) A.toast('تم التحديث', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ==================================================================
     3. تفاصيل الأصل
     ================================================================== */
  A.renderAssetDetail = async function (params) {
    const id = params.id
    A.setContent(A.spinner())
    const d = await A.call('get', '/assets/' + id)
    const a = d.item
    const isMgr = A.isManager()
    const canTicket = true

    const kv = (label, value, cls) =>
      `<div class="flex justify-between gap-2 py-1.5 border-b border-slate-100 last:border-0">
        <span class="text-xs text-slate-500 shrink-0">${A.esc(label)}</span>
        <span class="text-xs font-semibold text-left ${cls || ''}">${value === null || value === undefined || value === '' ? '—' : value}</span>
      </div>`

    A.setContent(`
      ${A.pageHeader(
        a.name,
        a.asset_tag + (a.serial_number ? ' · سيريال: ' + a.serial_number : ''),
        `${A.btn({ label: 'رجوع', icon: 'fa-arrow-right', variant: 'secondary', size: 'sm', onclick: "history.length>1?history.back():location.hash='#/assets'" })}
         ${A.btn({ label: 'طباعة ملصق QR', icon: 'fa-print', variant: 'secondary', size: 'sm', onclick: `A.printLabel(${a.id})` })}
         ${A.btn({ label: 'تذكرة صيانة', icon: 'fa-screwdriver-wrench', variant: 'warn', size: 'sm', onclick: `A.ticketForm(${a.id})` })}
         ${isMgr ? A.btn({ label: 'تغيير الحالة', icon: 'fa-exchange-alt', variant: 'secondary', size: 'sm', onclick: `A.assetStatus(${a.id},'${a.status}')` }) : ''}
         ${isMgr ? A.btn({ label: 'تسليم عهدة', icon: 'fa-hand-holding-hand', variant: 'success', size: 'sm', onclick: `A.custodyAssign(${a.id})` }) : ''}
         ${isMgr ? A.btn({ label: 'نقل موقع', icon: 'fa-truck-fast', variant: 'secondary', size: 'sm', onclick: `A.custodyMoveLocation(${a.id})` }) : ''}
         ${isMgr ? A.btn({ label: 'تعديل', icon: 'fa-pen', size: 'sm', onclick: `A.assetForm(${a.id})` }) : ''}
         ${A.user.role === 'Admin' ? A.btn({ label: 'حذف', icon: 'fa-trash', variant: 'danger', size: 'sm', onclick: `A.assetDelete(${a.id})` }) : ''}`
      )}

      <div class="grid lg:grid-cols-3 gap-4 mb-4">
        <div class="lg:col-span-2 grid md:grid-cols-2 gap-4">
          ${A.panel(
            'البيانات الأساسية',
            `<div class="p-4">
              ${kv('رقم الأصل', `<span class="font-mono text-brand-700">${A.esc(a.asset_tag)}</span>`)}
              ${kv('الحالة', A.statusBadge(a.status))}
              ${kv('التصنيف', A.esc(a.category_name))}
              ${kv('الشركة', A.esc(a.company_name))}
              ${kv('الموقع', A.esc(a.location_name))}
              ${kv('العهدة الحالية', a.custody_user_name ? A.esc(a.custody_user_name) + (a.custody_department_name ? ` <span class="text-slate-400">(${A.esc(a.custody_department_name)})</span>` : '') : '<span class="text-slate-400">غير مسلّم</span>')}
              ${kv('الماركة / الموديل', A.esc([a.brand, a.model].filter(Boolean).join(' · ')))}
              ${kv('الرقم التسلسلي', a.serial_number ? `<span class="font-mono">${A.esc(a.serial_number)}</span>` : '')}
              ${kv('الباركود', a.barcode ? `<span class="font-mono">${A.esc(a.barcode)}</span>` : '')}
              ${kv('المورّد', A.esc(a.vendor_name))}
              ${kv('ملاحظات', A.esc(a.notes))}
            </div>`
          )}
          ${A.panel(
            'المالية والإهلاك',
            `<div class="p-4">
              ${kv('تكلفة الشراء', A.money(a.purchase_cost))}
              ${kv('تاريخ الشراء', A.date(a.purchase_date))}
              ${kv('العمر الإنتاجي', (a.useful_life_years || 0) + ' سنة')}
              ${kv('القيمة التخريدية', A.money(a.salvage_value))}
              ${kv('مجمع الإهلاك', `<span class="text-red-600">${A.money(a.accumulated_depreciation)}</span>`)}
              ${kv('القيمة الدفترية', `<span class="text-green-700 font-bold">${A.money(a.book_value)}</span>`)}
              ${kv(
                'انتهاء الضمان',
                a.warranty_expiry_date
                  ? `<span class="${new Date(a.warranty_expiry_date) < new Date() ? 'text-red-600' : 'text-green-700'}">${A.date(a.warranty_expiry_date)}${
                      new Date(a.warranty_expiry_date) < new Date() ? ' (منتهي)' : ''
                    }</span>`
                  : ''
              )}
              ${kv('تاريخ التسجيل', A.dt(a.created_at))}
              <div class="mt-3">
                <div class="flex justify-between text-[11px] text-slate-500 mb-1"><span>نسبة الإهلاك</span><span>${
                  a.purchase_cost ? Math.round((Number(a.accumulated_depreciation || 0) / Number(a.purchase_cost)) * 100) : 0
                }%</span></div>
                <div class="h-2 bg-slate-100 rounded-full overflow-hidden">
                  <div class="h-full bg-gradient-to-l from-red-400 to-amber-400" style="width:${
                    a.purchase_cost ? Math.min(100, Math.round((Number(a.accumulated_depreciation || 0) / Number(a.purchase_cost)) * 100)) : 0
                  }%"></div>
                </div>
              </div>
            </div>`
          )}
        </div>
        ${A.panel(
          'رمز QR',
          `<div class="p-4 text-center">
            <div id="qr-box" class="inline-block bg-white p-2 border border-slate-200 rounded-lg"></div>
            <p class="font-mono text-xs font-bold text-slate-700 mt-2">${A.esc(a.asset_tag)}</p>
            <p class="text-[10px] text-slate-400 mt-1 break-all">${location.origin}/a/${A.esc(a.asset_tag)}</p>
            <div class="mt-3 flex flex-col gap-2">
              ${A.btn({ label: 'طباعة الملصق', icon: 'fa-print', variant: 'secondary', size: 'sm', onclick: `A.printLabel(${a.id})` })}
            </div>
          </div>`
        )}
      </div>

      <div class="grid lg:grid-cols-2 gap-4">
        ${A.panel(
          'تذاكر الصيانة (' + d.tickets.length + ')',
          A.table(
            [
              { label: 'الرقم', render: (r) => `<span class="font-mono text-xs">${A.esc(r.ticket_number)}</span>` },
              { label: 'المشكلة', render: (r) => `<span class="text-xs">${A.esc((r.issue_description || '').substring(0, 50))}</span>` },
              { label: 'الفني', key: 'technician_name' },
              { label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
              { label: 'الحالة', render: (r) => A.ticketBadge(r.status) },
              { label: 'التكلفة', render: (r) => `<span class="text-xs">${A.money(r.total_cost)}</span>` }
            ],
            d.tickets,
            { rowHref: (r) => '#/tickets/' + r.id, empty: 'لا توجد تذاكر' }
          )
        )}
        ${A.panel(
          'سجل العهد (' + d.custody.length + ')',
          A.table(
            [
              { label: 'الإجراء', render: (r) => A.badge(A.tr('custodyAction', r.action_type), r.action_type === 'Return' ? 'amber' : 'blue') },
              { label: 'من', render: (r) => A.esc(r.previous_user_name || '—') },
              { label: 'إلى', render: (r) => A.esc(r.new_user_name || '—') },
              { label: 'الحالة', render: (r) => A.badge(A.tr('custodyStatus', r.acceptance_status), r.acceptance_status === 'Accepted' ? 'green' : r.acceptance_status === 'Rejected' ? 'red' : 'amber') },
              { label: 'التاريخ', render: (r) => `<span class="text-xs">${A.dt(r.transfer_date)}</span>` }
            ],
            d.custody,
            { empty: 'لا يوجد سجل عهد' }
          )
        )}
        ${A.panel(
          'سجل المواقع (' + d.locations.length + ')',
          A.table(
            [
              { label: 'من', render: (r) => A.esc(r.previous_location_name || '—') },
              { label: 'إلى', render: (r) => A.esc(r.new_location_name) },
              { label: 'بواسطة', key: 'moved_by_name' },
              { label: 'السبب', render: (r) => `<span class="text-xs text-slate-500">${A.esc(r.reason)}</span>` },
              { label: 'التاريخ', render: (r) => `<span class="text-xs">${A.dt(r.transfer_date)}</span>` }
            ],
            d.locations,
            { empty: 'لا يوجد سجل نقل' }
          )
        )}
        ${A.panel(
          'قيود الإهلاك (' + d.depreciation.length + ')',
          A.table(
            [
              { label: 'الفترة', render: (r) => A.date(r.period_date) },
              { label: 'المبلغ', render: (r) => `<span class="text-xs">${A.money(r.amount)}</span>` },
              { label: 'المجمع', render: (r) => `<span class="text-xs">${A.money(r.accumulated_after)}</span>` },
              { label: 'الدفترية', render: (r) => `<span class="text-xs font-bold">${A.money(r.book_value_after)}</span>` }
            ],
            d.depreciation,
            { empty: 'لا توجد قيود إهلاك' }
          )
        )}
        ${A.panel(
          'الصيانة الوقائية (' + d.schedules.length + ')',
          A.table(
            [
              { label: 'المهمة', key: 'title' },
              { label: 'التكرار', render: (r) => A.tr('recurrence', r.recurrence_type) },
              { label: 'الاستحقاق', render: (r) => `<span class="text-xs ${new Date(r.next_due_date) < new Date() ? 'text-red-600 font-bold' : ''}">${A.date(r.next_due_date)}</span>` },
              { label: 'نشط', render: (r) => (r.is_active ? A.badge('نعم', 'green') : A.badge('لا', 'slate')) }
            ],
            d.schedules,
            { empty: 'لا توجد جدولة' }
          )
        )}
      </div>`)

    // QR render
    try {
      const box = document.getElementById('qr-box')
      if (box && typeof QRCode !== 'undefined') {
        box.innerHTML = ''
        new QRCode(box, { text: location.origin + '/a/' + a.asset_tag, width: 150, height: 150, correctLevel: QRCode.CorrectLevel.M })
      }
    } catch (e) {
      console.error('qr', e)
    }
  }

  A.assetDelete = async function (id) {
    const ok = await A.confirm({ title: 'حذف الأصل', message: 'سيتم حذف الأصل حذفاً منطقياً (يمكن استعادته من قاعدة البيانات). هل تريد المتابعة؟', danger: true, okLabel: 'حذف' })
    if (!ok) return
    try {
      await A.call('delete', '/assets/' + id)
      A.toast('تم الحذف', 'success')
      location.hash = '#/assets'
    } catch (e) {}
  }

  A.assetStatus = function (id, current) {
    A.modal({
      title: 'تغيير حالة الأصل',
      size: 'sm',
      okLabel: 'تحديث الحالة',
      body: `${A.sel({ name: 'status', label: 'الحالة الجديدة', value: current, options: statusOptions, empty: false, required: true })}
             ${A.txt({ name: 'notes', label: 'ملاحظات / سبب التغيير', rows: 3 })}
             <p class="text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-2 mt-2">
               <i class="fas fa-circle-info"></i> التكهين (مستبعد) والفقدان يحتاجان صلاحية مدير النظام، وسيتم تحرير العهدة تلقائياً.
             </p>`,
      onSubmit: async function (d) {
        await A.call('post', '/assets/' + id + '/status', d)
        A.toast('تم تحديث الحالة', 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ==================================================================
     4. طباعة ملصق QR
     ================================================================== */
  A.printLabel = async function (id) {
    const d = await A.call('get', '/assets/' + id)
    const a = d.item
    const w = window.open('', '_blank', 'width=420,height=560')
    if (!w) return A.toast('يرجى السماح بالنوافذ المنبثقة', 'warn')
    w.document.write(`<!DOCTYPE html><html dir="rtl" lang="ar"><head><meta charset="utf-8">
      <title>ملصق ${A.esc(a.asset_tag)}</title>
      <script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"><\/script>
      <style>
        body{font-family:'Cairo',Tahoma,sans-serif;margin:0;padding:16px;display:flex;justify-content:center}
        .label{width:340px;border:2px solid #111;border-radius:10px;padding:12px;text-align:center}
        .co{font-size:11px;color:#555;margin-bottom:4px}
        .nm{font-size:16px;font-weight:800;margin:4px 0 8px}
        .tag{font-family:monospace;font-size:15px;font-weight:800;letter-spacing:1px;margin-top:8px}
        .meta{font-size:10px;color:#555;margin-top:6px;line-height:1.6}
        #q{display:flex;justify-content:center;margin:8px 0}
        @media print{body{padding:0}}
      </style></head><body>
      <div class="label">
        <div class="co">${A.esc(a.company_name || '')}</div>
        <div class="nm">${A.esc(a.name)}</div>
        <div id="q"></div>
        <div class="tag">${A.esc(a.asset_tag)}</div>
        <div class="meta">
          ${a.serial_number ? 'السيريال: ' + A.esc(a.serial_number) + '<br>' : ''}
          ${a.location_name ? 'الموقع: ' + A.esc(a.location_name) + '<br>' : ''}
          امسح الرمز للإبلاغ عن مشكلة
        </div>
      </div>
      <script>
        new QRCode(document.getElementById('q'),{text:'${location.origin}/a/${A.esc(a.asset_tag)}',width:150,height:150});
        setTimeout(function(){window.print()},600);
      <\/script></body></html>`)
    w.document.close()
  }

  /* ==================================================================
     5. الاستيراد الجماعي
     ================================================================== */
  A.bulkImport = function () {
    A.modal({
      title: 'استيراد أصول جماعي',
      size: 'lg',
      okLabel: 'استيراد',
      body: `
        <p class="text-xs text-slate-600 mb-2">الصق البيانات بصيغة CSV (سطر لكل أصل). الأعمدة بالترتيب:</p>
        <code class="block bg-slate-900 text-green-300 text-[11px] p-2 rounded-lg mb-3 overflow-x-auto" dir="ltr">name,serial_number,brand,model,purchase_cost,purchase_date,category_id,location_id,useful_life_years</code>
        ${A.txt({ name: 'csv', label: 'البيانات', rows: 10, placeholder: 'لابتوب Dell,SN-9001,Dell,Latitude 5540,4500,2026-01-15,6,1,5' })}
        <p class="text-[11px] text-slate-400 mt-1">الحد الأقصى ٥٠٠ صف. الاسم مطلوب في كل صف.</p>`,
      onSubmit: async function (d) {
        const lines = String(d.csv || '')
          .split('\n')
          .map((x) => x.trim())
          .filter(Boolean)
        if (!lines.length) {
          A.toast('لا توجد بيانات', 'error')
          throw new Error('v')
        }
        const rows = lines.map(function (ln) {
          const p = ln.split(',').map((x) => x.trim())
          return {
            name: p[0],
            serial_number: p[1] || null,
            brand: p[2] || null,
            model: p[3] || null,
            purchase_cost: p[4] || 0,
            purchase_date: p[5] || null,
            category_id: p[6] || null,
            location_id: p[7] || null,
            useful_life_years: p[8] || 5
          }
        })
        const r = await A.call('post', '/assets/bulk', { rows: rows })
        A.toast('تم إنشاء ' + r.created + ' أصل' + (r.errors && r.errors.length ? ' — ' + r.errors.length + ' أخطاء' : ''), r.errors && r.errors.length ? 'warn' : 'success')
        A.closeModal()
        A.dispatch()
      }
    })
  }

  /* ==================================================================
     6. صفحة المسح QR
     ================================================================== */
  let scanner = null

  A.renderScan = function () {
    A.setContent(`
      ${A.pageHeader('مسح رمز QR', 'امسح ملصق الأصل أو أدخل رقمه يدوياً')}
      <div class="grid lg:grid-cols-2 gap-4">
        ${A.panel(
          'الكاميرا',
          `<div class="p-4">
            <div id="qr-reader" class="rounded-lg overflow-hidden bg-slate-900 min-h-[240px] flex items-center justify-center text-slate-400 text-sm">
              <div class="text-center p-6">
                <i class="fas fa-camera text-3xl mb-2 block"></i>
                <p>اضغط "تشغيل الكاميرا" للبدء</p>
              </div>
            </div>
            <div class="flex gap-2 mt-3">
              ${A.btn({ label: 'تشغيل الكاميرا', icon: 'fa-camera', onclick: 'A.startScan()', id: 'scan-start' })}
              ${A.btn({ label: 'إيقاف', icon: 'fa-stop', variant: 'secondary', onclick: 'A.stopScan()' })}
            </div>
            <p id="scan-msg" class="text-xs text-slate-500 mt-2"></p>
          </div>`
        )}
        ${A.panel(
          'إدخال يدوي',
          `<div class="p-4">
            <form id="manual-scan" class="space-y-3">
              ${A.inp({ name: 'tag', label: 'رقم الأصل / السيريال / الباركود', required: true, placeholder: 'AST-2026-00001' })}
              ${A.btn({ label: 'بحث', icon: 'fa-magnifying-glass', type: 'submit' })}
            </form>
            <div class="mt-4 pt-3 border-t border-slate-100">
              <p class="text-[11px] font-bold text-slate-500 mb-2">تجربة سريعة</p>
              <div class="flex flex-wrap gap-1.5">
                ${['AST-2026-00001', 'AST-2026-00002', 'AST-2026-00003']
                  .map((t) => `<button onclick="location.hash='#/scan-result/${t}'" class="text-[11px] font-mono bg-slate-100 hover:bg-brand-100 border border-slate-200 rounded px-2 py-1">${t}</button>`)
                  .join('')}
              </div>
            </div>
          </div>`
        )}
      </div>`)

    document.getElementById('manual-scan').addEventListener('submit', function (e) {
      e.preventDefault()
      const v = A.formData(e.target).tag
      if (v) location.hash = '#/scan-result/' + encodeURIComponent(v.trim())
    })
  }

  A.startScan = function () {
    const msg = document.getElementById('scan-msg')
    if (typeof Html5Qrcode === 'undefined') {
      msg.textContent = 'مكتبة المسح غير متاحة — استخدم الإدخال اليدوي'
      return
    }
    A.stopScan()
    try {
      document.getElementById('qr-reader').innerHTML = ''
      scanner = new Html5Qrcode('qr-reader')
      scanner
        .start(
          { facingMode: 'environment' },
          { fps: 10, qrbox: { width: 220, height: 220 } },
          function (text) {
            A.stopScan()
            let tag = text
            const m = String(text).match(/\/a\/([^/?#]+)/)
            if (m) tag = decodeURIComponent(m[1])
            location.hash = '#/scan-result/' + encodeURIComponent(tag)
          },
          function () {}
        )
        .then(function () {
          msg.textContent = 'الكاميرا تعمل — وجّهها نحو الرمز'
        })
        .catch(function (e) {
          msg.textContent = 'تعذر تشغيل الكاميرا: ' + (e.message || e) + ' — استخدم الإدخال اليدوي'
        })
    } catch (e) {
      msg.textContent = 'خطأ: ' + e.message
    }
  }

  A.stopScan = function () {
    if (scanner) {
      try {
        scanner.stop().then(function () {
          try {
            scanner.clear()
          } catch (e) {}
          scanner = null
        })
      } catch (e) {
        scanner = null
      }
    }
  }

  /* ==================================================================
     7. نتيجة المسح
     ================================================================== */
  A.renderScanResult = async function (params) {
    const tag = params.tag
    A.setContent(A.spinner('جارٍ البحث عن ' + tag))
    let a = null
    try {
      const d = await A.api('get', '/assets/by-tag/' + encodeURIComponent(tag))
      a = d.item
    } catch (e) {
      A.setContent(`
        ${A.pageHeader('نتيجة المسح', tag)}
        <div class="bg-white rounded-xl border border-red-200 p-8 text-center">
          <i class="fas fa-circle-xmark text-4xl text-red-400 mb-3 block"></i>
          <h2 class="font-bold text-slate-700 mb-1">لم يتم العثور على الأصل</h2>
          <p class="text-sm text-slate-500 mb-4">${A.esc(e.message)}</p>
          ${A.btn({ label: 'مسح مرة أخرى', icon: 'fa-qrcode', onclick: "location.hash='#/scan'" })}
        </div>`)
      return
    }

    A.setContent(`
      ${A.pageHeader('نتيجة المسح', 'تم التعرف على الأصل بنجاح')}
      <div class="max-w-3xl">
        <div class="bg-white rounded-xl border border-green-200 overflow-hidden mb-4">
          <div class="bg-green-50 border-b border-green-200 px-4 py-3 flex items-center gap-2">
            <i class="fas fa-circle-check text-green-600 text-xl"></i>
            <div>
              <p class="font-extrabold text-slate-800">${A.esc(a.name)}</p>
              <p class="text-xs font-mono text-slate-500">${A.esc(a.asset_tag)}</p>
            </div>
            <div class="mr-auto">${A.statusBadge(a.status)}</div>
          </div>
          <div class="p-4 grid sm:grid-cols-2 gap-y-2 gap-x-6 text-sm">
            ${[
              ['التصنيف', a.category_name],
              ['الموقع', a.location_name],
              ['الشركة', a.company_name],
              ['العهدة', a.custody_user_name || 'غير مسلّم'],
              ['الماركة/الموديل', [a.brand, a.model].filter(Boolean).join(' · ')],
              ['السيريال', a.serial_number],
              ['تاريخ الشراء', a.purchase_date ? A.date(a.purchase_date) : null],
              ['انتهاء الضمان', a.warranty_expiry_date ? A.date(a.warranty_expiry_date) : null]
            ]
              .map(
                (x) =>
                  `<div class="flex justify-between border-b border-slate-100 pb-1"><span class="text-xs text-slate-500">${A.esc(
                    x[0]
                  )}</span><span class="text-xs font-semibold">${A.esc(x[1] || '—')}</span></div>`
              )
              .join('')}
          </div>
          <div class="px-4 py-3 bg-slate-50 border-t border-slate-100 flex flex-wrap gap-2">
            ${A.btn({ label: 'الإبلاغ عن مشكلة', icon: 'fa-triangle-exclamation', variant: 'warn', onclick: `A.ticketForm(${a.id})` })}
            ${A.btn({ label: 'التفاصيل الكاملة', icon: 'fa-circle-info', variant: 'secondary', onclick: `location.hash='#/assets/${a.id}'` })}
            ${A.btn({ label: 'مسح آخر', icon: 'fa-qrcode', variant: 'secondary', onclick: "location.hash='#/scan'" })}
          </div>
        </div>
      </div>`)
  }

  /* routes */
  A.route('/assets', A.renderAssets)
  A.route('/assets/:id', A.renderAssetDetail)
  A.route('/scan', A.renderScan)
  A.route('/scan-result/:tag', A.renderScanResult)
})()
