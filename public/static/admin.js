/* ==================================================================
   admin.js — الهيكل التنظيمي / المستخدمون / SLA / سجل التدقيق /
              الإشعارات / الإعدادات
   يعتمد على core.js (window.A)
   ================================================================== */
(function () {
  const A = window.A

  /* static color maps (Tailwind CDN JIT can't compile dynamic classes) */
  const ICON_BG = {
    blue: 'bg-blue-100 text-blue-700',
    green: 'bg-green-100 text-green-700',
    amber: 'bg-amber-100 text-amber-700',
    red: 'bg-red-100 text-red-700',
    purple: 'bg-purple-100 text-purple-700',
    indigo: 'bg-indigo-100 text-indigo-700',
    cyan: 'bg-cyan-100 text-cyan-700',
    slate: 'bg-slate-100 text-slate-700'
  }
  const bgc = (c) => ICON_BG[c] || ICON_BG.slate

  /* ==================================================================
     1) الهيكل التنظيمي  #/org?tab=companies|departments|locations|categories|vendors
     ================================================================== */
  const TABS = [
    { key: 'companies', label: 'الشركات', icon: 'fa-building', adminOnly: true },
    { key: 'departments', label: 'الإدارات', icon: 'fa-diagram-project' },
    { key: 'locations', label: 'المواقع', icon: 'fa-location-dot' },
    { key: 'categories', label: 'التصنيفات', icon: 'fa-tags', adminOnly: true },
    { key: 'vendors', label: 'الموردون', icon: 'fa-truck-field' }
  ]

  const ENTITY_TITLES = {
    companies: 'شركة',
    departments: 'إدارة',
    locations: 'موقع',
    categories: 'تصنيف',
    vendors: 'مورّد'
  }

  const LOC_TYPES = [
    { value: 'Office', label: 'مكتب / مقر إداري' },
    { value: 'Factory', label: 'مصنع' },
    { value: 'Warehouse', label: 'مخزن / مستودع' },
    { value: 'Building', label: 'مبنى / موقع مشروع' },
    { value: 'Apartment', label: 'شقة / سكن' },
    { value: 'Branch', label: 'فرع' },
    { value: 'Other', label: 'أخرى' }
  ]
  const locType = (t) => (LOC_TYPES.find((x) => x.value === t) || { label: t || '—' }).label

  function setOrgBody(html) {
    const el = document.getElementById('org-body')
    if (el) el.innerHTML = html
  }

  function rowActions(entity, id, canEdit, canDelete) {
    let h = '<div class="flex gap-1 justify-end">'
    if (canEdit)
      h += `<button onclick="A.orgForm('${entity}',${id})" class="w-8 h-8 rounded-lg hover:bg-brand-50 text-brand-600" title="تعديل"><i class="fas fa-pen"></i></button>`
    if (canDelete)
      h += `<button onclick="A.orgDelete('${entity}',${id})" class="w-8 h-8 rounded-lg hover:bg-red-50 text-red-600" title="حذف"><i class="fas fa-trash"></i></button>`
    return h + '</div>'
  }

  A.renderOrg = async function (params, query) {
    const isAdmin = A.user.role === 'Admin'
    const avail = TABS.filter((t) => !t.adminOnly || isAdmin)
    let tab = (query && query.tab) || avail[0].key
    if (!avail.some((t) => t.key === tab)) tab = avail[0].key

    const tabsHtml = avail
      .map(
        (t) => `<a href="#/org?tab=${t.key}"
        class="px-4 py-2.5 rounded-xl text-sm font-semibold whitespace-nowrap transition ${
          t.key === tab
            ? 'bg-brand-600 text-white shadow-sm'
            : 'bg-white text-slate-600 hover:bg-slate-100 border border-slate-200'
        }">
        <i class="fas ${t.icon} ml-1.5"></i>${t.label}</a>`
      )
      .join('')

    A.setContent(`
      ${A.pageHeader(
        'الهيكل التنظيمي',
        'إدارة الشركات والإدارات والمواقع والتصنيفات والموردين',
        A.btn({
          label: 'إضافة ' + (ENTITY_TITLES[tab] || ''),
          icon: 'fa-plus',
          onclick: `A.orgForm('${tab}')`
        })
      )}
      <div class="flex gap-2 overflow-x-auto pb-1 mb-5">${tabsHtml}</div>
      <div id="org-body">${A.spinner()}</div>
    `)
    A.highlightNav()

    try {
      if (tab === 'companies') await orgCompanies()
      else if (tab === 'departments') await orgDepartments()
      else if (tab === 'locations') await orgLocations()
      else if (tab === 'categories') await orgCategories()
      else await orgVendors()
    } catch (e) {
      setOrgBody(A.empty(e.message || 'تعذّر تحميل البيانات', 'fa-triangle-exclamation'))
    }
  }

  async function orgCompanies() {
    const d = await A.api('get', '/companies')
    A.cache.companies = d.items || []
    setOrgBody(
      A.panel(
        `الشركات (${(d.items || []).length})`,
        A.table(
          [
            { key: 'name', label: 'الاسم', render: (r) => `<span class="font-semibold text-slate-800">${A.esc(r.name)}</span>${r.name_en ? `<div class="text-xs text-slate-400">${A.esc(r.name_en)}</div>` : ''}` },
            { key: 'commercial_no', label: 'السجل التجاري', render: (r) => A.esc(r.commercial_no || '—') },
            { key: 'tax_number', label: 'الرقم الضريبي', render: (r) => A.esc(r.tax_number || '—') },
            { key: 'phone', label: 'الهاتف', render: (r) => A.esc(r.phone || '—') },
            { key: 'email', label: 'البريد', render: (r) => A.esc(r.email || '—') },
            { key: 'is_active', label: 'الحالة', render: (r) => A.badge(r.is_active ? 'نشطة' : 'موقوفة', r.is_active ? 'green' : 'slate') },
            { key: 'a', label: '', render: (r) => rowActions('companies', r.id, true, true) }
          ],
          d.items || [],
          { empty: 'لا توجد شركات' }
        )
      )
    )
  }

  async function orgDepartments() {
    const d = await A.api('get', '/departments')
    setOrgBody(
      A.panel(
        `الإدارات (${(d.items || []).length})`,
        A.table(
          [
            { key: 'name', label: 'الاسم', render: (r) => `<span class="font-semibold text-slate-800">${A.esc(r.name)}</span>` },
            { key: 'code', label: 'الرمز', render: (r) => r.code ? `<code class="text-xs bg-slate-100 px-2 py-0.5 rounded">${A.esc(r.code)}</code>` : '—' },
            { key: 'company_name', label: 'الشركة', render: (r) => A.esc(r.company_name || '—') },
            { key: 'manager_name', label: 'المدير', render: (r) => A.esc(r.manager_name || '—') },
            { key: 'users_count', label: 'المستخدمون', render: (r) => A.num(r.users_count || 0) },
            { key: 'a', label: '', render: (r) => rowActions('departments', r.id, true, true) }
          ],
          d.items || [],
          { empty: 'لا توجد إدارات' }
        )
      )
    )
  }

  async function orgLocations() {
    const d = await A.api('get', '/locations')
    setOrgBody(
      A.panel(
        `المواقع (${(d.items || []).length})`,
        A.table(
          [
            { key: 'name', label: 'الاسم', render: (r) => `<span class="font-semibold text-slate-800">${A.esc(r.name)}</span>` },
            { key: 'type', label: 'النوع', render: (r) => A.badge(locType(r.type), 'indigo') },
            { key: 'company_name', label: 'الشركة', render: (r) => A.esc(r.company_name || '—') },
            { key: 'address_details', label: 'التفاصيل', render: (r) => A.esc(r.address_details || '—') },
            { key: 'gps_coordinates', label: 'GPS', render: (r) => r.gps_coordinates ? `<a target="_blank" class="text-brand-600 hover:underline" href="https://maps.google.com/?q=${encodeURIComponent(r.gps_coordinates)}"><i class="fas fa-map-location-dot"></i></a>` : '—' },
            { key: 'assets_count', label: 'الأصول', render: (r) => A.num(r.assets_count || 0) },
            { key: 'a', label: '', render: (r) => rowActions('locations', r.id, true, true) }
          ],
          d.items || [],
          { empty: 'لا توجد مواقع' }
        )
      )
    )
  }

  async function orgCategories() {
    const d = await A.api('get', '/categories')
    setOrgBody(
      A.panel(
        `التصنيفات (${(d.items || []).length})`,
        A.table(
          [
            { key: 'name', label: 'الاسم', render: (r) => `<span class="font-semibold text-slate-800">${r.parent_name ? '<i class="fas fa-turn-up fa-rotate-90 text-slate-300 ml-1"></i>' : ''}${A.esc(r.name)}</span>` },
            { key: 'code', label: 'الرمز', render: (r) => r.code ? `<code class="text-xs bg-slate-100 px-2 py-0.5 rounded">${A.esc(r.code)}</code>` : '—' },
            { key: 'parent_name', label: 'التصنيف الأب', render: (r) => A.esc(r.parent_name || '—') },
            { key: 'default_useful_life_years', label: 'العمر الافتراضي', render: (r) => r.default_useful_life_years ? A.num(r.default_useful_life_years) + ' سنة' : '—' },
            { key: 'default_salvage_rate', label: 'نسبة الإنقاذ', render: (r) => r.default_salvage_rate != null ? (Number(r.default_salvage_rate) * 100).toFixed(0) + '%' : '—' },
            { key: 'assets_count', label: 'الأصول', render: (r) => A.num(r.assets_count || 0) },
            { key: 'a', label: '', render: (r) => rowActions('categories', r.id, true, true) }
          ],
          d.items || [],
          { empty: 'لا توجد تصنيفات' }
        )
      )
    )
  }

  async function orgVendors() {
    const d = await A.api('get', '/vendors')
    setOrgBody(
      A.panel(
        `الموردون (${(d.items || []).length})`,
        A.table(
          [
            { key: 'name', label: 'الاسم', render: (r) => `<span class="font-semibold text-slate-800">${A.esc(r.name)}</span>` },
            { key: 'contact_person', label: 'مسؤول التواصل', render: (r) => A.esc(r.contact_person || '—') },
            { key: 'phone', label: 'الهاتف', render: (r) => A.esc(r.phone || '—') },
            { key: 'email', label: 'البريد', render: (r) => A.esc(r.email || '—') },
            { key: 'company_name', label: 'الشركة', render: (r) => A.esc(r.company_name || '—') },
            { key: 'assets_count', label: 'الأصول', render: (r) => A.num(r.assets_count || 0) },
            { key: 'a', label: '', render: (r) => rowActions('vendors', r.id, true, true) }
          ],
          d.items || [],
          { empty: 'لا يوجد موردون' }
        )
      )
    )
  }

  /* ---------------- org create/edit modal ---------------- */
  A.orgForm = async function (entity, id) {
    const isAdmin = A.user.role === 'Admin'
    let row = {}
    if (id) {
      const list = A.cache['_' + entity] || (await A.api('get', '/' + entity)).items || []
      A.cache['_' + entity] = list
      row = list.find((x) => x.id === id) || {}
    }

    let companies = []
    if (isAdmin && entity !== 'companies' && entity !== 'categories')
      companies = await A.lookup('companies')

    let body = ''
    if (entity === 'companies') {
      body =
        A.inp({ name: 'name', label: 'اسم الشركة', value: row.name, required: true }) +
        A.inp({ name: 'name_en', label: 'الاسم بالإنجليزية', value: row.name_en }) +
        `<div class="grid md:grid-cols-2 gap-4">
          ${A.inp({ name: 'commercial_no', label: 'السجل التجاري', value: row.commercial_no, wrap: false })}
          ${A.inp({ name: 'tax_number', label: 'الرقم الضريبي', value: row.tax_number, wrap: false })}
          ${A.inp({ name: 'phone', label: 'الهاتف', value: row.phone, wrap: false })}
          ${A.inp({ name: 'email', label: 'البريد الإلكتروني', type: 'email', value: row.email, wrap: false })}
        </div>` +
        A.txt({ name: 'address', label: 'العنوان', value: row.address, rows: 2 }) +
        A.chk({ name: 'is_active', label: 'شركة نشطة', value: id ? !!row.is_active : true })
    } else if (entity === 'departments') {
      body =
        A.inp({ name: 'name', label: 'اسم الإدارة', value: row.name, required: true }) +
        A.inp({ name: 'code', label: 'الرمز', value: row.code, placeholder: 'IT / HR / FIN' }) +
        (isAdmin
          ? A.sel({
              name: 'company_id',
              label: 'الشركة',
              value: row.company_id,
              options: A.opt(companies),
              empty: '— اختر الشركة —',
              required: true
            })
          : '') +
        A.sel({
          name: 'manager_user_id',
          label: 'مدير الإدارة',
          value: row.manager_user_id,
          options: A.opt(await A.lookup('users'), 'id', 'full_name'),
          empty: '— بدون —'
        })
    } else if (entity === 'locations') {
      body =
        A.inp({ name: 'name', label: 'اسم الموقع', value: row.name, required: true }) +
        A.sel({ name: 'type', label: 'النوع', value: row.type || 'Room', options: LOC_TYPES, required: true }) +
        (isAdmin
          ? A.sel({
              name: 'company_id',
              label: 'الشركة',
              value: row.company_id,
              options: A.opt(companies),
              empty: '— اختر الشركة —',
              required: true
            })
          : '') +
        A.txt({ name: 'address_details', label: 'تفاصيل العنوان', value: row.address_details, rows: 2 }) +
        A.inp({
          name: 'gps_coordinates',
          label: 'إحداثيات GPS',
          value: row.gps_coordinates,
          placeholder: '24.7136,46.6753',
          hint: 'خط العرض,خط الطول'
        })
    } else if (entity === 'categories') {
      const cats = (await A.lookup('categories')).filter((x) => x.id !== id)
      body =
        A.inp({ name: 'name', label: 'اسم التصنيف', value: row.name, required: true }) +
        A.inp({ name: 'code', label: 'الرمز', value: row.code, placeholder: 'LAPTOP' }) +
        A.sel({
          name: 'parent_category_id',
          label: 'التصنيف الأب',
          value: row.parent_category_id,
          options: A.opt(cats),
          empty: '— تصنيف رئيسي —'
        }) +
        `<div class="grid md:grid-cols-2 gap-4">
          ${A.inp({ name: 'default_useful_life_years', label: 'العمر الافتراضي (سنة)', type: 'number', min: 1, value: row.default_useful_life_years, wrap: false })}
          ${A.inp({ name: 'default_salvage_rate', label: 'نسبة قيمة الإنقاذ', type: 'number', step: '0.01', min: 0, value: row.default_salvage_rate, placeholder: '0.10', wrap: false })}
        </div>`
    } else {
      body =
        A.inp({ name: 'name', label: 'اسم المورّد', value: row.name, required: true }) +
        A.inp({ name: 'contact_person', label: 'مسؤول التواصل', value: row.contact_person }) +
        `<div class="grid md:grid-cols-2 gap-4">
          ${A.inp({ name: 'phone', label: 'الهاتف', value: row.phone, wrap: false })}
          ${A.inp({ name: 'email', label: 'البريد الإلكتروني', type: 'email', value: row.email, wrap: false })}
        </div>` +
        (isAdmin
          ? A.sel({
              name: 'company_id',
              label: 'الشركة',
              value: row.company_id,
              options: A.opt(companies),
              empty: '— اختر الشركة —',
              required: true
            })
          : '') +
        A.txt({ name: 'address', label: 'العنوان', value: row.address, rows: 2 })
    }

    A.modal({
      title: (id ? 'تعديل ' : 'إضافة ') + ENTITY_TITLES[entity],
      body,
      size: 'md',
      okLabel: id ? 'حفظ التعديلات' : 'إضافة',
      onSubmit: async (data) => {
        await A.call(id ? 'put' : 'post', '/' + entity + (id ? '/' + id : ''), data)
        A.toast(id ? 'تم حفظ التعديلات' : 'تمت الإضافة بنجاح', 'success')
        A.clearCache()
        A.closeModal()
        A.dispatch()
      }
    })
  }

  A.orgDelete = async function (entity, id) {
    const ok = await A.confirm({
      title: 'تأكيد الحذف',
      message: `هل تريد حذف هذا الـ${ENTITY_TITLES[entity]}؟ لن يمكن التراجع.`,
      okLabel: 'حذف',
      danger: true
    })
    if (!ok) return
    try {
      await A.api('delete', '/' + entity + '/' + id)
      A.toast('تم الحذف بنجاح', 'success')
      A.clearCache()
      A.dispatch()
    } catch (e) {
      A.toast(e.message || 'تعذّر الحذف', 'error')
    }
  }

  /* ==================================================================
     2) المستخدمون  #/users
     ================================================================== */
  let USERS = []

  A.renderUsers = async function () {
    A.setContent(
      A.pageHeader(
        'المستخدمون',
        'إدارة حسابات المستخدمين والأدوار والصلاحيات',
        A.btn({ label: 'مستخدم جديد', icon: 'fa-user-plus', onclick: 'A.userForm()' }) +
          A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'ghost', onclick: 'A.exportUsers()' })
      ) + `<div id="users-body">${A.spinner()}</div>`
    )
    A.highlightNav()
    try {
      const d = await A.api('get', '/users')
      USERS = d.items || []
      const roleCount = (r) => USERS.filter((u) => u.role === r).length
      const stats = `<div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
        ${A.statCard({ label: 'الإجمالي', value: A.num(USERS.length), icon: 'fa-users', color: 'blue' })}
        ${A.statCard({ label: 'مديرو شركات', value: A.num(roleCount('CompanyManager')), icon: 'fa-user-tie', color: 'purple' })}
        ${A.statCard({ label: 'فنيون', value: A.num(roleCount('Technician')), icon: 'fa-screwdriver-wrench', color: 'amber' })}
        ${A.statCard({ label: 'موظفون', value: A.num(roleCount('Employee')), icon: 'fa-user', color: 'green' })}
      </div>`

      document.getElementById('users-body').innerHTML =
        stats +
        A.panel(
          'قائمة المستخدمين',
          A.table(
            [
              {
                key: 'full_name',
                label: 'المستخدم',
                render: (r) => `<div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-full bg-brand-100 text-brand-700 flex items-center justify-center font-bold text-sm shrink-0">${A.esc((r.full_name || '?').charAt(0))}</div>
                  <div><div class="font-semibold text-slate-800">${A.esc(r.full_name)}</div>
                  <div class="text-xs text-slate-400" dir="ltr">${A.esc(r.email)}</div></div></div>`
              },
              { key: 'role', label: 'الدور', render: (r) => A.badge(A.tr('role', r.role), r.role === 'Admin' ? 'red' : r.role === 'CompanyManager' ? 'purple' : r.role === 'Technician' ? 'amber' : 'blue') },
              { key: 'company_name', label: 'الشركة', render: (r) => A.esc(r.company_name || '—') },
              { key: 'department_name', label: 'الإدارة', render: (r) => A.esc(r.department_name || '—') },
              { key: 'job_title', label: 'المسمى الوظيفي', render: (r) => A.esc(r.job_title || '—') },
              { key: 'custody_count', label: 'العهد', render: (r) => A.num(r.custody_count || 0) },
              { key: 'last_login_at', label: 'آخر دخول', render: (r) => r.last_login_at ? `<span class="text-xs">${A.ago(r.last_login_at)}</span>` : '<span class="text-xs text-slate-400">لم يدخل</span>' },
              { key: 'is_active', label: 'الحالة', render: (r) => A.badge(r.is_active ? 'نشط' : 'موقوف', r.is_active ? 'green' : 'slate') },
              {
                key: 'a',
                label: '',
                render: (r) => `<div class="flex gap-1 justify-end">
                  <button onclick="A.userForm(${r.id})" class="w-8 h-8 rounded-lg hover:bg-brand-50 text-brand-600" title="تعديل"><i class="fas fa-pen"></i></button>
                  ${r.id === A.user.id ? '' : `<button onclick="A.userDelete(${r.id})" class="w-8 h-8 rounded-lg hover:bg-red-50 text-red-600" title="حذف"><i class="fas fa-trash"></i></button>`}
                </div>`
              }
            ],
            USERS,
            { empty: 'لا يوجد مستخدمون' }
          )
        )
    } catch (e) {
      document.getElementById('users-body').innerHTML = A.empty(e.message, 'fa-triangle-exclamation')
    }
  }

  A.exportUsers = function () {
    A.csv(
      'users',
      [
        { label: 'الاسم', key: 'full_name' },
        { label: 'البريد', key: 'email' },
        { label: 'الدور', value: (r) => A.tr('role', r.role) },
        { label: 'الشركة', key: 'company_name' },
        { label: 'الإدارة', key: 'department_name' },
        { label: 'المسمى', key: 'job_title' },
        { label: 'الرقم الوظيفي', key: 'employee_number' },
        { label: 'الهاتف', key: 'phone_number' },
        { label: 'العهد', key: 'custody_count' },
        { label: 'الحالة', value: (r) => (r.is_active ? 'نشط' : 'موقوف') }
      ],
      USERS
    )
  }

  A.userForm = async function (id) {
    const row = id ? USERS.find((x) => x.id === id) || {} : {}
    const isAdmin = A.user.role === 'Admin'
    const roles = [
      { value: 'Employee', label: 'موظف' },
      { value: 'Technician', label: 'فني صيانة' },
      { value: 'CompanyManager', label: 'مدير شركة' }
    ]
    if (isAdmin) roles.push({ value: 'Admin', label: 'مدير النظام' })
    const companies = isAdmin ? await A.lookup('companies') : []
    const deps = await A.lookup('departments')

    A.modal({
      title: id ? 'تعديل المستخدم' : 'مستخدم جديد',
      size: 'md',
      okLabel: id ? 'حفظ التعديلات' : 'إنشاء الحساب',
      body:
        A.inp({ name: 'full_name', label: 'الاسم الكامل', value: row.full_name, required: true }) +
        `<div class="grid md:grid-cols-2 gap-4">
          ${A.inp({ name: 'email', label: 'البريد الإلكتروني', type: 'email', value: row.email, required: true, wrap: false })}
          ${A.inp({ name: 'password', label: id ? 'كلمة مرور جديدة' : 'كلمة المرور', type: 'password', required: !id, hint: id ? 'اتركها فارغة للإبقاء على الحالية' : '6 أحرف على الأقل', wrap: false })}
        </div>` +
        `<div class="grid md:grid-cols-2 gap-4">
          ${A.sel({ name: 'role', label: 'الدور', value: row.role || 'Employee', options: roles, required: true, wrap: false })}
          ${isAdmin ? A.sel({ name: 'company_id', label: 'الشركة', value: row.company_id, options: A.opt(companies), empty: '— بدون —', wrap: false }) : ''}
          ${A.sel({ name: 'department_id', label: 'الإدارة', value: row.department_id, options: A.opt(deps), empty: '— بدون —', wrap: false })}
          ${A.inp({ name: 'job_title', label: 'المسمى الوظيفي', value: row.job_title, wrap: false })}
          ${A.inp({ name: 'employee_number', label: 'الرقم الوظيفي', value: row.employee_number, wrap: false })}
          ${A.inp({ name: 'phone_number', label: 'رقم الهاتف', value: row.phone_number, wrap: false })}
        </div>` +
        A.chk({ name: 'is_active', label: 'حساب نشط', value: id ? !!row.is_active : true }),
      onSubmit: async (data) => {
        if (id && !data.password) delete data.password
        await A.call(id ? 'put' : 'post', '/users' + (id ? '/' + id : ''), data)
        A.toast(id ? 'تم حفظ التعديلات' : 'تم إنشاء الحساب', 'success')
        A.clearCache()
        A.closeModal()
        A.renderUsers()
      }
    })
  }

  A.userDelete = async function (id) {
    const u = USERS.find((x) => x.id === id) || {}
    const ok = await A.confirm({
      title: 'تأكيد الحذف',
      message: `حذف المستخدم "${u.full_name || ''}"؟ لن يمكن التراجع.`,
      okLabel: 'حذف',
      danger: true
    })
    if (!ok) return
    try {
      await A.api('delete', '/users/' + id)
      A.toast('تم حذف المستخدم', 'success')
      A.clearCache()
      A.renderUsers()
    } catch (e) {
      A.toast(e.message || 'تعذّر الحذف', 'error')
    }
  }

  /* ==================================================================
     3) سياسات SLA + المهام المجدولة  #/sla
     ================================================================== */
  let SLAS = []

  const JOBS = [
    { key: 'sla-check', label: 'فحص تجاوزات SLA', desc: 'وسم التذاكر التي تجاوزت زمن الحل المستهدف وإشعار المسؤولين', icon: 'fa-stopwatch', color: 'red' },
    { key: 'depreciation', label: 'احتساب الإهلاك الشهري', desc: 'تسجيل قسط الإهلاك وتحديث القيمة الدفترية لكل أصل', icon: 'fa-chart-line-down', color: 'purple' },
    { key: 'generate-schedules', label: 'توليد تذاكر الصيانة الوقائية', desc: 'إنشاء تذاكر من جداول الصيانة المستحقة اليوم', icon: 'fa-calendar-plus', color: 'blue' },
    { key: 'warranty-alerts', label: 'تنبيهات انتهاء الضمان', desc: 'إشعار المسؤولين بالأصول التي ينتهي ضمانها قريباً', icon: 'fa-shield-halved', color: 'amber' }
  ]

  A.renderSla = async function () {
    A.setContent(
      A.pageHeader('سياسات SLA والمهام المجدولة', 'أزمنة الاستجابة والحل حسب الأولوية، وتشغيل المهام الخلفية يدوياً') +
        `<div id="sla-body">${A.spinner()}</div>`
    )
    A.highlightNav()
    try {
      const d = await A.api('get', '/tickets/sla/policies')
      SLAS = d.items || []
      const jobsHtml = `<div class="grid md:grid-cols-2 gap-4">
        ${JOBS.map(
          (j) => `<div class="bg-white rounded-2xl border border-slate-200 p-5">
          <div class="flex items-start gap-3">
            <div class="w-11 h-11 rounded-xl ${bgc(j.color)} flex items-center justify-center shrink-0"><i class="fas ${j.icon}"></i></div>
            <div class="flex-1 min-w-0">
              <div class="font-bold text-slate-800">${j.label}</div>
              <div class="text-xs text-slate-500 mt-1 leading-relaxed">${j.desc}</div>
              <div class="mt-3">${A.btn({ label: 'تشغيل الآن', icon: 'fa-play', size: 'sm', variant: 'outline', onclick: `A.runJob('${j.key}')` })}</div>
              <div id="job-${j.key}" class="mt-2 text-xs"></div>
            </div>
          </div></div>`
        ).join('')}
      </div>`

      document.getElementById('sla-body').innerHTML =
        A.panel(
          'سياسات مستوى الخدمة',
          A.table(
            [
              { key: 'name', label: 'السياسة', render: (r) => `<span class="font-semibold text-slate-800">${A.esc(r.name)}</span>` },
              { key: 'priority', label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
              { key: 'response_time_hours', label: 'زمن الاستجابة', render: (r) => `<span class="font-semibold">${A.num(r.response_time_hours)}</span> ساعة` },
              { key: 'resolution_time_hours', label: 'زمن الحل', render: (r) => `<span class="font-semibold">${A.num(r.resolution_time_hours)}</span> ساعة` },
              { key: 'tickets_count', label: 'التذاكر المرتبطة', render: (r) => A.num(r.tickets_count || 0) },
              { key: 'is_active', label: 'الحالة', render: (r) => A.badge(r.is_active ? 'مفعّلة' : 'معطّلة', r.is_active ? 'green' : 'slate') },
              {
                key: 'a',
                label: '',
                render: (r) => `<div class="flex justify-end"><button onclick="A.slaForm(${r.id})" class="w-8 h-8 rounded-lg hover:bg-brand-50 text-brand-600" title="تعديل"><i class="fas fa-pen"></i></button></div>`
              }
            ],
            SLAS,
            { empty: 'لا توجد سياسات' }
          )
        ) +
        `<h3 class="text-lg font-bold text-slate-800 mt-8 mb-4"><i class="fas fa-robot text-brand-500 ml-2"></i>المهام الخلفية</h3>` +
        jobsHtml
    } catch (e) {
      document.getElementById('sla-body').innerHTML = A.empty(e.message, 'fa-triangle-exclamation')
    }
  }

  A.slaForm = function (id) {
    const row = SLAS.find((x) => x.id === id) || {}
    A.modal({
      title: 'تعديل سياسة SLA — ' + A.tr('priority', row.priority),
      size: 'sm',
      okLabel: 'حفظ',
      body:
        A.inp({ name: 'name', label: 'اسم السياسة', value: row.name, required: true }) +
        A.inp({ name: 'response_time_hours', label: 'زمن الاستجابة (ساعة)', type: 'number', min: 1, value: row.response_time_hours, required: true }) +
        A.inp({ name: 'resolution_time_hours', label: 'زمن الحل (ساعة)', type: 'number', min: 1, value: row.resolution_time_hours, required: true, hint: 'يجب أن يكون أكبر من أو يساوي زمن الاستجابة' }) +
        A.chk({ name: 'is_active', label: 'سياسة مفعّلة', value: !!row.is_active }),
      onSubmit: async (data) => {
        await A.call('put', '/tickets/sla/policies/' + id, data)
        A.toast('تم تحديث السياسة', 'success')
        A.closeModal()
        A.renderSla()
      }
    })
  }

  A.runJob = async function (job) {
    const out = document.getElementById('job-' + job)
    if (out) out.innerHTML = '<i class="fas fa-spinner fa-spin text-brand-500"></i> جارٍ التشغيل…'
    try {
      const r = await A.api('post', '/jobs/' + job)
      const n =
        r.flagged != null ? r.flagged : r.processed != null ? r.processed : r.created != null ? r.created : r.sent != null ? r.sent : 0
      const msg =
        job === 'sla-check' ? `تم وسم ${A.num(n)} تذكرة متجاوزة`
        : job === 'depreciation' ? `تم احتساب الإهلاك لـ ${A.num(n)} أصل`
        : job === 'generate-schedules' ? `تم إنشاء ${A.num(n)} تذكرة وقائية`
        : `تم إرسال ${A.num(n)} تنبيه ضمان`
      if (out) out.innerHTML = `<span class="text-green-600 font-semibold"><i class="fas fa-circle-check"></i> ${msg}</span>`
      A.toast(msg, 'success')
      A.refreshUnread()
    } catch (e) {
      if (out) out.innerHTML = `<span class="text-red-600"><i class="fas fa-circle-xmark"></i> ${A.esc(e.message || 'فشل التشغيل')}</span>`
      A.toast(e.message || 'فشل تشغيل المهمة', 'error')
    }
  }

  /* ==================================================================
     4) سجل التدقيق  #/audit-log
     ================================================================== */
  let LOGS = []
  const ACTIONS = [
    { value: 'Create', label: 'إنشاء' },
    { value: 'Update', label: 'تحديث' },
    { value: 'Delete', label: 'حذف' },
    { value: 'Login', label: 'دخول' },
    { value: 'Export', label: 'تصدير' }
  ]
  const ACTION_COLOR = { Create: 'green', Update: 'blue', Delete: 'red', Login: 'purple', Export: 'amber' }
  const actLabel = (a) => (ACTIONS.find((x) => x.value === a) || { label: a || '—' }).label

  const ENTITY_AR = {
    Company: 'شركة', Department: 'إدارة', Location: 'موقع', Category: 'تصنيف',
    Vendor: 'مورّد', User: 'مستخدم', Asset: 'أصل', Custody: 'عهدة',
    Ticket: 'تذكرة', SlaPolicy: 'سياسة SLA', Schedule: 'جدول صيانة',
    Audit: 'جرد', Job: 'مهمة خلفية', Settings: 'إعدادات', Auth: 'مصادقة'
  }

  A.renderAuditLog = async function (params, query) {
    const q = query || {}
    const page = parseInt(q.page || '1', 10) || 1
    A.setContent(
      A.pageHeader('سجل التدقيق', 'كل عملية إنشاء أو تحديث أو حذف مسجّلة مع المستخدم والوقت وعنوان IP',
        A.btn({ label: 'تصدير CSV', icon: 'fa-file-csv', variant: 'ghost', onclick: 'A.exportAuditLog()' })) +
        `<div class="bg-white rounded-2xl border border-slate-200 p-4 mb-5 grid md:grid-cols-3 gap-3">
          ${A.sel({ name: 'f-entity', id: 'f-entity', label: 'الكيان', value: q.entity, options: Object.keys(ENTITY_AR).map((k) => ({ value: k, label: ENTITY_AR[k] })), empty: 'كل الكيانات', wrap: false })}
          ${A.sel({ name: 'f-action', id: 'f-action', label: 'نوع العملية', value: q.action, options: ACTIONS, empty: 'كل العمليات', wrap: false })}
          <div class="flex items-end">${A.btn({ label: 'تطبيق', icon: 'fa-filter', onclick: 'A.applyAuditFilter()', cls: 'w-full' })}</div>
        </div>
        <div id="log-body">${A.spinner()}</div>`
    )
    A.highlightNav()
    try {
      const qs = []
      if (q.entity) qs.push('entity=' + encodeURIComponent(q.entity))
      if (q.action) qs.push('action=' + encodeURIComponent(q.action))
      qs.push('page=' + page, 'size=50')
      const d = await A.api('get', '/audit-logs?' + qs.join('&'))
      LOGS = d.items || []
      const pages = Math.max(1, Math.ceil((d.total || 0) / (d.size || 50)))
      const base = '#/audit-log?' + (q.entity ? 'entity=' + encodeURIComponent(q.entity) + '&' : '') + (q.action ? 'action=' + encodeURIComponent(q.action) + '&' : '')
      const pager =
        pages > 1
          ? `<div class="flex items-center justify-between px-4 py-3 border-t border-slate-100 text-sm">
              <span class="text-slate-500">صفحة ${A.num(page)} من ${A.num(pages)} — ${A.num(d.total)} سجل</span>
              <div class="flex gap-2">
                ${page > 1 ? `<a href="${base}page=${page - 1}" class="px-3 py-1.5 rounded-lg border border-slate-200 hover:bg-slate-50">السابق</a>` : ''}
                ${page < pages ? `<a href="${base}page=${page + 1}" class="px-3 py-1.5 rounded-lg border border-slate-200 hover:bg-slate-50">التالي</a>` : ''}
              </div></div>`
          : ''

      document.getElementById('log-body').innerHTML = A.panel(
        `السجلات (${A.num(d.total || 0)})`,
        A.table(
          [
            { key: 'created_at', label: 'الوقت', render: (r) => `<div class="text-xs"><div class="font-semibold text-slate-700">${A.dt(r.created_at)}</div><div class="text-slate-400">${A.ago(r.created_at)}</div></div>` },
            { key: 'user_name', label: 'المستخدم', render: (r) => r.user_name ? `<div><div class="font-semibold text-slate-800 text-sm">${A.esc(r.user_name)}</div><div class="text-xs text-slate-400">${A.tr('role', r.user_role)}</div></div>` : '<span class="text-slate-400">النظام</span>' },
            { key: 'action', label: 'العملية', render: (r) => A.badge(actLabel(r.action), ACTION_COLOR[r.action] || 'slate') },
            { key: 'entity_name', label: 'الكيان', render: (r) => `${A.esc(ENTITY_AR[r.entity_name] || r.entity_name || '—')}${r.entity_id ? `<span class="text-xs text-slate-400"> #${A.esc(r.entity_id)}</span>` : ''}` },
            { key: 'ip_address', label: 'IP', render: (r) => `<code class="text-xs text-slate-500" dir="ltr">${A.esc(r.ip_address || '—')}</code>` },
            { key: 'a', label: 'التفاصيل', render: (r) => r.changes_json ? `<button onclick="A.showChanges(${r.id})" class="text-brand-600 hover:underline text-xs font-semibold"><i class="fas fa-code ml-1"></i>عرض</button>` : '<span class="text-xs text-slate-300">—</span>' }
          ],
          LOGS,
          { empty: 'لا توجد سجلات مطابقة' }
        ) + pager
      )
    } catch (e) {
      document.getElementById('log-body').innerHTML = A.empty(e.message, 'fa-triangle-exclamation')
    }
  }

  A.applyAuditFilter = function () {
    const e = document.getElementById('f-entity')
    const a = document.getElementById('f-action')
    const qs = []
    if (e && e.value) qs.push('entity=' + encodeURIComponent(e.value))
    if (a && a.value) qs.push('action=' + encodeURIComponent(a.value))
    A.go('#/audit-log' + (qs.length ? '?' + qs.join('&') : ''))
  }

  A.showChanges = function (id) {
    const r = LOGS.find((x) => x.id === id)
    if (!r) return
    let pretty = r.changes_json
    try {
      pretty = JSON.stringify(JSON.parse(r.changes_json), null, 2)
    } catch (e) {}
    A.modal({
      title: 'تفاصيل التغيير — ' + (ENTITY_AR[r.entity_name] || r.entity_name || ''),
      size: 'lg',
      okLabel: '',
      body: `<div class="text-xs text-slate-500 mb-3">
          <span class="font-semibold">${A.esc(r.user_name || 'النظام')}</span> · ${A.dt(r.created_at)} · ${A.badge(actLabel(r.action), ACTION_COLOR[r.action] || 'slate')}
        </div>
        <pre dir="ltr" class="bg-slate-900 text-emerald-300 text-xs p-4 rounded-xl overflow-auto max-h-96 leading-relaxed">${A.esc(pretty || '')}</pre>`
    })
  }

  A.exportAuditLog = function () {
    A.csv(
      'audit-log',
      [
        { label: 'الوقت', key: 'created_at' },
        { label: 'المستخدم', key: 'user_name' },
        { label: 'الدور', value: (r) => A.tr('role', r.user_role) },
        { label: 'العملية', value: (r) => actLabel(r.action) },
        { label: 'الكيان', value: (r) => ENTITY_AR[r.entity_name] || r.entity_name },
        { label: 'رقم الكيان', key: 'entity_id' },
        { label: 'IP', key: 'ip_address' },
        { label: 'التغييرات', key: 'changes_json' }
      ],
      LOGS
    )
  }

  /* ==================================================================
     5) الإشعارات  #/notifications
     ================================================================== */
  const NTYPE = {
    Ticket: { label: 'تذكرة', icon: 'fa-screwdriver-wrench', color: 'blue' },
    Custody: { label: 'عهدة', icon: 'fa-hand-holding-hand', color: 'green' },
    Warranty: { label: 'ضمان', icon: 'fa-shield-halved', color: 'amber' },
    SLA: { label: 'SLA', icon: 'fa-stopwatch', color: 'red' },
    System: { label: 'النظام', icon: 'fa-bell', color: 'slate' }
  }

  A.renderNotifications = async function () {
    A.setContent(
      A.pageHeader('الإشعارات', 'كل التنبيهات الخاصة بك',
        A.btn({ label: 'تعليم الكل كمقروء', icon: 'fa-check-double', variant: 'ghost', onclick: 'A.notifyReadAll()' })) +
        `<div id="notif-body">${A.spinner()}</div>`
    )
    A.highlightNav()
    try {
      const d = await A.api('get', '/notifications')
      A.unread = d.unread || 0
      const items = d.items || []
      const body = items.length
        ? `<div class="bg-white rounded-2xl border border-slate-200 divide-y divide-slate-100 overflow-hidden">
            ${items
              .map((n) => {
                const t = NTYPE[n.type] || NTYPE.System
                return `<div class="flex items-start gap-3 p-4 ${n.is_read ? '' : 'bg-brand-50/40'}">
                  <div class="w-10 h-10 rounded-xl ${bgc(t.color)} flex items-center justify-center shrink-0"><i class="fas ${t.icon}"></i></div>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 flex-wrap">
                      <span class="font-bold text-slate-800">${A.esc(n.title)}</span>
                      ${A.badge(t.label, t.color)}
                      ${n.is_read ? '' : '<span class="w-2 h-2 rounded-full bg-brand-500 inline-block"></span>'}
                    </div>
                    ${n.message ? `<div class="text-sm text-slate-600 mt-1 leading-relaxed">${A.esc(n.message)}</div>` : ''}
                    <div class="text-xs text-slate-400 mt-1.5">${A.dt(n.created_at)} · ${A.ago(n.created_at)}</div>
                  </div>
                  <div class="flex flex-col gap-1 shrink-0">
                    ${n.target_url ? `<a href="${A.esc(n.target_url)}" class="px-3 py-1.5 text-xs rounded-lg bg-brand-600 text-white hover:bg-brand-700 whitespace-nowrap">فتح</a>` : ''}
                    ${n.is_read ? '' : `<button onclick="A.notifyRead(${n.id})" class="px-3 py-1.5 text-xs rounded-lg border border-slate-200 hover:bg-slate-50 whitespace-nowrap">مقروء</button>`}
                  </div>
                </div>`
              })
              .join('')}
          </div>`
        : A.empty('لا توجد إشعارات', 'fa-bell-slash')
      document.getElementById('notif-body').innerHTML = body
    } catch (e) {
      document.getElementById('notif-body').innerHTML = A.empty(e.message, 'fa-triangle-exclamation')
    }
  }

  A.notifyRead = async function (id) {
    try {
      await A.api('post', '/notifications/' + id + '/read')
      A.renderNotifications()
      A.refreshUnread()
    } catch (e) {
      A.toast(e.message || 'خطأ', 'error')
    }
  }

  A.notifyReadAll = async function () {
    try {
      await A.api('post', '/notifications/read-all')
      A.toast('تم تعليم كل الإشعارات كمقروءة', 'success')
      A.renderNotifications()
      A.refreshUnread()
    } catch (e) {
      A.toast(e.message || 'خطأ', 'error')
    }
  }

  /* ==================================================================
     6) الإعدادات  #/settings
     ================================================================== */
  A.renderSettings = async function () {
    A.setContent(
      A.pageHeader('إعدادات النظام', 'القيم العامة المؤثرة على سلوك النظام') +
        `<div id="set-body">${A.spinner()}</div>`
    )
    A.highlightNav()
    try {
      const d = await A.api('get', '/settings')
      const items = d.items || []
      const fields = items
        .map(
          (s) => `<div class="grid md:grid-cols-3 gap-3 items-center py-3 border-b border-slate-100 last:border-0">
            <div class="md:col-span-2">
              <div class="font-semibold text-slate-800 text-sm">${A.esc(s.description || s.setting_key)}</div>
              <code class="text-xs text-slate-400" dir="ltr">${A.esc(s.setting_key)}</code>
            </div>
            <input name="s_${A.esc(s.setting_key)}" value="${A.esc(s.setting_value == null ? '' : s.setting_value)}"
              class="w-full px-3 py-2 rounded-xl border border-slate-200 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none text-sm" dir="ltr"/>
          </div>`
        )
        .join('')

      document.getElementById('set-body').innerHTML = items.length
        ? `<form id="settings-form" onsubmit="A.saveSettings(event)">
            ${A.panel('القيم', fields)}
            <div class="mt-4">${A.btn({ label: 'حفظ الإعدادات', icon: 'fa-floppy-disk', type: 'submit' })}</div>
           </form>`
        : A.empty('لا توجد إعدادات', 'fa-gear')
    } catch (e) {
      document.getElementById('set-body').innerHTML = A.empty(e.message, 'fa-triangle-exclamation')
    }
  }

  A.saveSettings = async function (ev) {
    ev.preventDefault()
    const form = ev.target
    const items = []
    Array.prototype.forEach.call(form.querySelectorAll('input[name^="s_"]'), (i) => {
      items.push({ setting_key: i.name.slice(2), setting_value: i.value })
    })
    try {
      await A.api('put', '/settings', { items })
      A.toast('تم حفظ الإعدادات', 'success')
    } catch (e) {
      A.toast(e.message || 'تعذّر الحفظ', 'error')
    }
  }

  /* ==================================================================
     Routes
     ================================================================== */
  A.route('/org', A.renderOrg, { roles: ['Admin', 'CompanyManager'] })
  A.route('/users', A.renderUsers, { roles: ['Admin', 'CompanyManager'] })
  A.route('/sla', A.renderSla, { roles: ['Admin'] })
  A.route('/audit-log', A.renderAuditLog, { roles: ['Admin'] })
  A.route('/notifications', A.renderNotifications)
  A.route('/settings', A.renderSettings, { roles: ['Admin'] })
})()
