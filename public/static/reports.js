/* =====================================================================
   وحدة التقارير — ١٢ تقريراً + تصدير CSV + رسوم بيانية
   ===================================================================== */
(function () {
  'use strict'
  const A = window.A

  const ICON_BG = {
    blue: 'bg-blue-100 text-blue-700',
    green: 'bg-green-100 text-green-700',
    amber: 'bg-amber-100 text-amber-700',
    orange: 'bg-orange-100 text-orange-700',
    red: 'bg-red-100 text-red-700',
    purple: 'bg-purple-100 text-purple-700',
    indigo: 'bg-indigo-100 text-indigo-700',
    cyan: 'bg-cyan-100 text-cyan-700',
    teal: 'bg-teal-100 text-teal-700',
    slate: 'bg-slate-100 text-slate-700'
  }

  const M = A.money
  const N = A.num

  const DEFS = {
    'assets-by-company': {
      label: 'الأصول حسب الشركة',
      icon: 'fa-building',
      color: 'blue',
      roles: ['Admin', 'CompanyManager', 'Technician'],
      cols: [
        { label: 'الشركة', key: 'company_name' },
        { label: 'عدد الأصول', render: (r) => `<b>${N(r.assets_count)}</b>`, csv: (r) => r.assets_count },
        { label: 'إجمالي الشراء', render: (r) => M(r.purchase_total), csv: (r) => r.purchase_total },
        { label: 'مجمع الإهلاك', render: (r) => `<span class="text-red-600">${M(r.depreciation_total)}</span>`, csv: (r) => r.depreciation_total },
        { label: 'القيمة الدفترية', render: (r) => `<span class="text-green-700 font-bold">${M(r.book_total)}</span>`, csv: (r) => r.book_total }
      ],
      chart: (rows) => ({ type: 'bar', labels: rows.map((r) => r.company_name || '—'), data: rows.map((r) => r.assets_count), title: 'عدد الأصول' })
    },
    'assets-by-status': {
      label: 'الأصول حسب الحالة',
      icon: 'fa-chart-pie',
      color: 'green',
      roles: ['Admin', 'CompanyManager', 'Technician'],
      cols: [
        { label: 'الحالة', render: (r) => A.statusBadge(r.status), csv: (r) => A.tr('assetStatus', r.status) },
        { label: 'عدد الأصول', render: (r) => `<b>${N(r.assets_count)}</b>`, csv: (r) => r.assets_count },
        { label: 'القيمة الدفترية', render: (r) => M(r.book_total), csv: (r) => r.book_total }
      ],
      chart: (rows) => ({ type: 'doughnut', labels: rows.map((r) => A.tr('assetStatus', r.status)), data: rows.map((r) => r.assets_count), title: 'الحالة' })
    },
    'assets-by-location': {
      label: 'توزيع الأصول على المواقع',
      icon: 'fa-location-dot',
      color: 'indigo',
      roles: ['Admin', 'CompanyManager', 'Technician'],
      cols: [
        { label: 'الموقع', key: 'location_name' },
        { label: 'النوع', key: 'location_type' },
        { label: 'الشركة', key: 'company_name' },
        { label: 'عدد الأصول', render: (r) => `<b>${N(r.assets_count)}</b>`, csv: (r) => r.assets_count },
        { label: 'القيمة الدفترية', render: (r) => M(r.book_total), csv: (r) => r.book_total }
      ],
      chart: (rows) => ({ type: 'bar', labels: rows.map((r) => r.location_name || '—'), data: rows.map((r) => r.assets_count), title: 'عدد الأصول', horizontal: true })
    },
    custody: {
      label: 'العهد حسب الموظف',
      icon: 'fa-hand-holding-hand',
      color: 'purple',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'الموظف', key: 'user_name' },
        { label: 'الرقم الوظيفي', key: 'employee_number' },
        { label: 'الإدارة', key: 'department_name' },
        { label: 'الشركة', key: 'company_name' },
        { label: 'عدد الأصول', render: (r) => `<b>${N(r.assets_count)}</b>`, csv: (r) => r.assets_count },
        { label: 'القيمة الدفترية', render: (r) => M(r.book_total), csv: (r) => r.book_total }
      ],
      chart: (rows) => ({ type: 'bar', labels: rows.slice(0, 10).map((r) => r.user_name), data: rows.slice(0, 10).map((r) => r.assets_count), title: 'عدد الأصول', horizontal: true })
    },
    'maintenance-cost': {
      label: 'تكاليف الصيانة لكل أصل',
      icon: 'fa-coins',
      color: 'amber',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'رقم الأصل', render: (r) => `<span class="font-mono text-xs">${A.esc(r.asset_tag)}</span>`, csv: (r) => r.asset_tag },
        { label: 'الأصل', key: 'asset_name' },
        { label: 'التصنيف', key: 'category_name' },
        { label: 'عدد التذاكر', render: (r) => N(r.tickets_count), csv: (r) => r.tickets_count },
        { label: 'العمالة', render: (r) => M(r.labor_total), csv: (r) => r.labor_total },
        { label: 'القطع', render: (r) => M(r.parts_total), csv: (r) => r.parts_total },
        { label: 'الإجمالي', render: (r) => `<b class="text-slate-800">${M(r.cost_total)}</b>`, csv: (r) => r.cost_total }
      ],
      chart: (rows) => ({ type: 'bar', labels: rows.slice(0, 10).map((r) => r.asset_name), data: rows.slice(0, 10).map((r) => r.cost_total), title: 'التكلفة (ج.م)', horizontal: true })
    },
    'technician-performance': {
      label: 'أداء الفنيين',
      icon: 'fa-user-gear',
      color: 'blue',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'الفني', key: 'technician_name' },
        { label: 'إجمالي التذاكر', render: (r) => N(r.total_tickets), csv: (r) => r.total_tickets },
        { label: 'تم حلها', render: (r) => `<span class="text-green-700 font-bold">${N(r.resolved_count)}</span>`, csv: (r) => r.resolved_count },
        { label: 'مفتوحة', render: (r) => `<span class="text-amber-700">${N(r.open_count)}</span>`, csv: (r) => r.open_count },
        { label: 'مخالفات SLA', render: (r) => `<span class="${Number(r.breached_count) ? 'text-red-600 font-bold' : 'text-slate-400'}">${N(r.breached_count)}</span>`, csv: (r) => r.breached_count },
        {
          label: 'نسبة الحل',
          render: (r) => {
            const pct = r.total_tickets ? Math.round((Number(r.resolved_count) / Number(r.total_tickets)) * 100) : 0
            return `<div class="w-24"><div class="h-1.5 bg-slate-100 rounded-full overflow-hidden"><div class="h-full ${pct >= 80 ? 'bg-green-500' : pct >= 50 ? 'bg-amber-500' : 'bg-red-500'}" style="width:${pct}%"></div></div><span class="text-[10px] text-slate-500">${pct}%</span></div>`
          },
          csv: (r) => (r.total_tickets ? Math.round((Number(r.resolved_count) / Number(r.total_tickets)) * 100) + '%' : '0%')
        },
        { label: 'إجمالي التكلفة', render: (r) => M(r.cost_total), csv: (r) => r.cost_total }
      ],
      chart: (rows) => ({
        type: 'bar',
        labels: rows.map((r) => r.technician_name),
        data: rows.map((r) => r.resolved_count),
        title: 'تذاكر تم حلها'
      })
    },
    sla: {
      label: 'الالتزام بـ SLA',
      icon: 'fa-stopwatch',
      color: 'red',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'الأولوية', render: (r) => A.prioBadge(r.priority), csv: (r) => A.tr('priority', r.priority) },
        { label: 'إجمالي التذاكر', render: (r) => N(r.total), csv: (r) => r.total },
        { label: 'ملتزمة', render: (r) => `<span class="text-green-700 font-bold">${N(r.compliant)}</span>`, csv: (r) => r.compliant },
        { label: 'مخالفة', render: (r) => `<span class="text-red-600 font-bold">${N(r.breached)}</span>`, csv: (r) => r.breached },
        {
          label: 'نسبة الالتزام',
          render: (r) => {
            const pct = r.total ? Math.round((Number(r.compliant) / Number(r.total)) * 100) : 100
            return `<b class="${pct >= 90 ? 'text-green-700' : pct >= 70 ? 'text-amber-700' : 'text-red-600'}">${pct}%</b>`
          },
          csv: (r) => (r.total ? Math.round((Number(r.compliant) / Number(r.total)) * 100) + '%' : '100%')
        }
      ],
      chart: (rows) => ({
        type: 'bar',
        labels: rows.map((r) => A.tr('priority', r.priority)),
        data: rows.map((r) => r.breached),
        title: 'عدد المخالفات'
      })
    },
    depreciation: {
      label: 'الإهلاك والقيمة الدفترية',
      icon: 'fa-arrow-trend-down',
      color: 'orange',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'رقم الأصل', render: (r) => `<span class="font-mono text-xs">${A.esc(r.asset_tag)}</span>`, csv: (r) => r.asset_tag },
        { label: 'الأصل', key: 'asset_name' },
        { label: 'التصنيف', key: 'category_name' },
        { label: 'تكلفة الشراء', render: (r) => M(r.purchase_cost), csv: (r) => r.purchase_cost },
        { label: 'العمر (سنة)', key: 'useful_life_years' },
        { label: 'مجمع الإهلاك', render: (r) => `<span class="text-red-600">${M(r.accumulated_depreciation)}</span>`, csv: (r) => r.accumulated_depreciation },
        { label: 'القيمة الدفترية', render: (r) => `<b class="text-green-700">${M(r.book_value)}</b>`, csv: (r) => r.book_value },
        {
          label: 'نسبة الإهلاك',
          render: (r) => {
            const pct = r.purchase_cost ? Math.round((Number(r.accumulated_depreciation) / Number(r.purchase_cost)) * 100) : 0
            return `<div class="w-20"><div class="h-1.5 bg-slate-100 rounded-full overflow-hidden"><div class="h-full bg-amber-500" style="width:${Math.min(100, pct)}%"></div></div><span class="text-[10px] text-slate-500">${pct}%</span></div>`
          },
          csv: (r) => (r.purchase_cost ? Math.round((Number(r.accumulated_depreciation) / Number(r.purchase_cost)) * 100) + '%' : '0%')
        }
      ]
    },
    warranty: {
      label: 'الضمانات',
      icon: 'fa-shield-halved',
      color: 'green',
      roles: ['Admin', 'CompanyManager', 'Technician'],
      cols: [
        { label: 'رقم الأصل', render: (r) => `<span class="font-mono text-xs">${A.esc(r.asset_tag)}</span>`, csv: (r) => r.asset_tag },
        { label: 'الأصل', key: 'asset_name' },
        { label: 'الماركة', key: 'brand' },
        { label: 'المورّد', key: 'vendor_name' },
        { label: 'الشركة', key: 'company_name' },
        { label: 'انتهاء الضمان', render: (r) => A.date(r.warranty_expiry_date), csv: (r) => r.warranty_expiry_date },
        {
          label: 'المتبقي',
          render: (r) => {
            const d = Number(r.days_left)
            if (d < 0) return A.badge('منتهي', 'red')
            if (d <= 30) return A.badge(d + ' يوم', 'amber')
            if (d <= 90) return A.badge(d + ' يوم', 'blue')
            return A.badge(d + ' يوم', 'green')
          },
          csv: (r) => r.days_left
        }
      ]
    },
    inventory: {
      label: 'جلسات الجرد',
      icon: 'fa-clipboard-check',
      color: 'purple',
      roles: ['Admin', 'CompanyManager'],
      cols: [
        { label: 'الجلسة', key: 'title' },
        { label: 'الموقع', key: 'location_name' },
        { label: 'الشركة', key: 'company_name' },
        { label: 'الحالة', render: (r) => A.badge(A.tr('auditStatus', r.status), r.status === 'Completed' ? 'green' : 'amber'), csv: (r) => A.tr('auditStatus', r.status) },
        { label: 'المتوقع', render: (r) => N(r.items_count), csv: (r) => r.items_count },
        { label: 'تم العثور', render: (r) => `<span class="text-green-700 font-bold">${N(r.found_count)}</span>`, csv: (r) => r.found_count },
        { label: 'موقع خطأ', render: (r) => `<span class="text-amber-700">${N(r.wrong_count)}</span>`, csv: (r) => r.wrong_count },
        { label: 'مفقود', render: (r) => `<span class="text-red-600 font-bold">${N(r.missing_count)}</span>`, csv: (r) => r.missing_count },
        { label: 'البدء', render: (r) => A.dt(r.started_at), csv: (r) => r.started_at },
        { label: 'الإنهاء', render: (r) => (r.completed_at ? A.dt(r.completed_at) : '—'), csv: (r) => r.completed_at }
      ]
    },
    'my-assets': {
      label: 'عهدي',
      icon: 'fa-boxes-stacked',
      color: 'blue',
      roles: ['Admin', 'CompanyManager', 'Technician', 'Employee'],
      cols: [
        { label: 'رقم الأصل', render: (r) => `<span class="font-mono text-xs">${A.esc(r.asset_tag)}</span>`, csv: (r) => r.asset_tag },
        { label: 'الأصل', key: 'asset_name' },
        { label: 'التصنيف', key: 'category_name' },
        { label: 'الموقع', key: 'location_name' },
        { label: 'الحالة', render: (r) => A.statusBadge(r.status), csv: (r) => A.tr('assetStatus', r.status) },
        { label: 'تاريخ الشراء', render: (r) => A.date(r.purchase_date), csv: (r) => r.purchase_date },
        { label: 'القيمة الدفترية', render: (r) => M(r.book_value), csv: (r) => r.book_value }
      ]
    },
    'my-tickets': {
      label: 'تذاكري',
      icon: 'fa-screwdriver-wrench',
      color: 'amber',
      roles: ['Admin', 'CompanyManager', 'Technician', 'Employee'],
      cols: [
        { label: 'رقم التذكرة', render: (r) => `<span class="font-mono text-xs">${A.esc(r.ticket_number)}</span>`, csv: (r) => r.ticket_number },
        { label: 'الأصل', key: 'asset_name' },
        { label: 'الفني', key: 'technician_name' },
        { label: 'الأولوية', render: (r) => A.prioBadge(r.priority), csv: (r) => A.tr('priority', r.priority) },
        { label: 'الحالة', render: (r) => A.ticketBadge(r.status), csv: (r) => A.tr('ticketStatus', r.status) },
        { label: 'التكلفة', render: (r) => M(r.total_cost), csv: (r) => r.total_cost },
        { label: 'الإنشاء', render: (r) => A.dt(r.created_at), csv: (r) => r.created_at },
        { label: 'الحل', render: (r) => (r.resolved_at ? A.dt(r.resolved_at) : '—'), csv: (r) => r.resolved_at }
      ]
    }
  }

  A.REPORT_DEFS = DEFS

  /* ==================================================================
     صفحة قائمة التقارير
     ================================================================== */
  A.renderReports = function () {
    const keys = Object.keys(DEFS).filter((k) => DEFS[k].roles.indexOf(A.user.role) >= 0)
    A.setContent(`
      ${A.pageHeader('التقارير', 'اختر تقريراً لعرضه وتصديره')}
      <div class="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
        ${keys
          .map(function (k) {
            const r = DEFS[k]
            return `<a href="#/reports/${k}" class="bg-white rounded-xl border border-slate-200 p-4 hover:shadow-md hover:border-brand-300 transition group">
              <div class="flex items-start gap-3">
                <div class="w-10 h-10 rounded-lg flex items-center justify-center shrink-0 ${ICON_BG[r.color] || ICON_BG.slate}">
                  <i class="fas ${r.icon}"></i>
                </div>
                <div class="min-w-0">
                  <p class="font-bold text-sm text-slate-800 group-hover:text-brand-700">${A.esc(r.label)}</p>
                  <p class="text-[11px] text-slate-400 mt-0.5">${r.cols.length} عمود · تصدير CSV</p>
                </div>
              </div>
            </a>`
          })
          .join('')}
      </div>
      <p class="text-xs text-slate-400 mt-4"><i class="fas fa-circle-info"></i> التقارير محدودة بنطاق شركتك وصلاحياتك تلقائياً.</p>`)
  }

  /* ==================================================================
     صفحة تقرير محدد
     ================================================================== */
  A.renderReport = async function (params) {
    const type = params.type
    const def = DEFS[type]
    if (!def) {
      A.notFound()
      return
    }
    if (def.roles.indexOf(A.user.role) < 0) {
      A.denied()
      return
    }
    A.setContent(A.spinner())
    const d = await A.call('get', '/reports/' + type)
    const rows = d.items || []
    A.cache._report = { type: type, rows: rows }

    const hasChart = !!def.chart && rows.length > 0

    A.setContent(`
      ${A.pageHeader(
        d.title || def.label,
        'عدد السجلات: ' + rows.length,
        `${A.btn({ label: 'رجوع', icon: 'fa-arrow-right', variant: 'secondary', size: 'sm', onclick: "location.hash='#/reports'" })}
         ${A.btn({ label: 'طباعة', icon: 'fa-print', variant: 'secondary', size: 'sm', onclick: 'window.print()' })}
         ${A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', size: 'sm', onclick: 'A.exportReport()' })}`
      )}

      ${
        type === 'sla'
          ? `<div class="bg-white rounded-xl border border-slate-200 p-5 mb-4 flex flex-wrap items-center gap-6">
              <div>
                <p class="text-xs text-slate-500 mb-1">نسبة الالتزام الكلية بـ SLA</p>
                <p class="text-4xl font-extrabold ${
                  d.compliance >= 90 ? 'text-green-600' : d.compliance >= 70 ? 'text-amber-600' : 'text-red-600'
                }">${N(d.compliance)}%</p>
              </div>
              <div class="flex-1 min-w-[200px]">
                <div class="h-3 bg-slate-100 rounded-full overflow-hidden">
                  <div class="h-full ${d.compliance >= 90 ? 'bg-green-500' : d.compliance >= 70 ? 'bg-amber-500' : 'bg-red-500'}" style="width:${d.compliance}%"></div>
                </div>
                <p class="text-[11px] text-slate-400 mt-1">الهدف: ٩٠٪ أو أعلى</p>
              </div>
             </div>`
          : ''
      }

      ${hasChart ? `<div class="bg-white rounded-xl border border-slate-200 p-4 mb-4"><canvas id="report-chart" height="${(def.chart(rows).horizontal ? Math.max(140, rows.length * 26) : 220)}"></canvas></div>` : ''}

      ${A.panel(
        'البيانات',
        A.table(
          def.cols.map((c) => ({ label: c.label, key: c.key, render: c.render })),
          rows,
          { empty: 'لا توجد بيانات لهذا التقرير' }
        )
      )}`)

    if (hasChart) {
      const cfg = def.chart(rows)
      const font = { family: 'Cairo', size: 11 }
      const PAL = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2', '#db2777', '#65a30d', '#ea580c', '#4b5563']
      if (cfg.type === 'doughnut' || cfg.type === 'pie') {
        A.chart('report-chart', {
          type: cfg.type,
          data: { labels: cfg.labels, datasets: [{ data: cfg.data, backgroundColor: PAL }] },
          options: { maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { font: font } } } }
        })
      } else {
        A.chart('report-chart', {
          type: 'bar',
          data: { labels: cfg.labels, datasets: [{ label: cfg.title, data: cfg.data, backgroundColor: '#2563eb', borderRadius: 4 }] },
          options: {
            maintainAspectRatio: false,
            indexAxis: cfg.horizontal ? 'y' : 'x',
            plugins: { legend: { display: false } },
            scales: { x: { ticks: { font: font } }, y: { ticks: { font: font } } }
          }
        })
      }
    }
  }

  A.exportReport = function () {
    const cur = A.cache._report
    if (!cur || !cur.rows || !cur.rows.length) return A.toast('لا توجد بيانات للتصدير', 'warn')
    const def = DEFS[cur.type]
    A.csv(
      cur.type + '.csv',
      def.cols.map((c) => ({ label: c.label, key: c.key, value: c.csv })),
      cur.rows
    )
  }

  A.route('/reports', A.renderReports)
  A.route('/reports/:type', A.renderReport)
})()
