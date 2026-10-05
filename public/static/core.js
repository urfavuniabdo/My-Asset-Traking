/* =====================================================================
   نظام إدارة وتتبع الأصول — النواة (core)
   يحتوي: API wrapper · helpers · router · shell · login · dashboard
   ===================================================================== */
(function () {
  'use strict'

  const A = (window.A = {
    user: null,
    unread: 0,
    routes: [],
    cache: {}
  })

  /* ------------------------------------------------------------------
     1. HELPERS: escaping / formatting
     ------------------------------------------------------------------ */
  A.esc = function (v) {
    if (v === null || v === undefined) return ''
    return String(v)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;')
  }

  A.money = function (n) {
    const v = Number(n || 0)
    return v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ج.م'
  }

  // Compact currency for narrow stat cards — Egyptian amounts reach millions and
  // would otherwise be clipped by `truncate` inside the card.
  A.moneyShort = function (n) {
    const v = Number(n || 0)
    const a = Math.abs(v)
    const sign = v < 0 ? '-' : ''
    const f = (x, s) => sign + x.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + ' ' + s
    if (a >= 1e9) return f(a / 1e9, 'مليار')
    if (a >= 1e6) return f(a / 1e6, 'مليون')
    if (a >= 1e5) return f(a / 1e3, 'ألف')
    return A.money(v)
  }

  A.num = function (n) {
    return Number(n || 0).toLocaleString('en-US')
  }

  A.date = function (s) {
    if (!s) return '—'
    const d = new Date(String(s).replace(' ', 'T') + (String(s).length <= 10 ? 'T00:00:00' : ''))
    if (isNaN(d)) return A.esc(s)
    return d.toLocaleDateString('en-GB')
  }

  A.dt = function (s) {
    if (!s) return '—'
    const d = new Date(String(s).replace(' ', 'T'))
    if (isNaN(d)) return A.esc(s)
    return d.toLocaleDateString('en-GB') + ' ' + d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
  }

  A.ago = function (s) {
    if (!s) return '—'
    const d = new Date(String(s).replace(' ', 'T'))
    if (isNaN(d)) return A.esc(s)
    const diff = (Date.now() - d.getTime()) / 1000
    if (diff < 60) return 'الآن'
    if (diff < 3600) return Math.floor(diff / 60) + ' دقيقة'
    if (diff < 86400) return Math.floor(diff / 3600) + ' ساعة'
    if (diff < 2592000) return Math.floor(diff / 86400) + ' يوم'
    return A.date(s)
  }

  /* ------------------------------------------------------------------
     2. Arabic label maps
     ------------------------------------------------------------------ */
  A.L = {
    assetStatus: {
      Active: 'نشط',
      UnderMaintenance: 'تحت الصيانة',
      Damaged: 'تالف',
      Disposed: 'مستبعد',
      Lost: 'مفقود',
      InStore: 'في المخزن'
    },
    assetStatusColor: {
      Active: 'green',
      UnderMaintenance: 'amber',
      Damaged: 'red',
      Disposed: 'slate',
      Lost: 'rose',
      InStore: 'blue'
    },
    ticketStatus: {
      Open: 'مفتوحة',
      Assigned: 'مُسندة',
      InProgress: 'قيد التنفيذ',
      WaitingParts: 'بانتظار قطع',
      Resolved: 'تم الحل',
      Closed: 'مغلقة',
      Cancelled: 'ملغاة'
    },
    ticketStatusColor: {
      Open: 'blue',
      Assigned: 'indigo',
      InProgress: 'amber',
      WaitingParts: 'orange',
      Resolved: 'green',
      Closed: 'slate',
      Cancelled: 'red'
    },
    priority: { Low: 'منخفضة', Medium: 'متوسطة', High: 'عالية', Critical: 'حرجة' },
    priorityColor: { Low: 'slate', Medium: 'blue', High: 'amber', Critical: 'red' },
    role: {
      Admin: 'مدير النظام',
      CompanyManager: 'مدير شركة',
      Technician: 'فني صيانة',
      Employee: 'موظف'
    },
    custodyStatus: { Pending: 'بانتظار القبول', Accepted: 'مقبولة', Rejected: 'مرفوضة' },
    custodyAction: { Assign: 'تسليم', Return: 'إرجاع', Transfer: 'نقل' },
    auditResult: {
      Expected: 'متوقع',
      Found: 'تم العثور',
      Missing: 'مفقود',
      WrongLocation: 'موقع خطأ',
      Damaged: 'تالف'
    },
    auditResultColor: {
      Expected: 'slate',
      Found: 'green',
      Missing: 'red',
      WrongLocation: 'amber',
      Damaged: 'orange'
    },
    auditStatus: { Draft: 'مسودة', InProgress: 'قيد التنفيذ', Completed: 'مكتمل' },
    recurrence: { Daily: 'يومي', Weekly: 'أسبوعي', Monthly: 'شهري', Quarterly: 'ربع سنوي', SemiAnnual: 'نصف سنوي', Annual: 'سنوي' },
    source: { Portal: 'البوابة', Phone: 'هاتف', Email: 'بريد', QR: 'مسح QR', Preventive: 'وقائي' }
  }

  A.tr = function (map, key) {
    return (A.L[map] && A.L[map][key]) || key || '—'
  }

  /* ------------------------------------------------------------------
     3. Badge / card / misc UI primitives
     ------------------------------------------------------------------ */
  const COLORS = {
    green: 'bg-green-100 text-green-800 border-green-200',
    red: 'bg-red-100 text-red-800 border-red-200',
    rose: 'bg-rose-100 text-rose-800 border-rose-200',
    amber: 'bg-amber-100 text-amber-800 border-amber-200',
    orange: 'bg-orange-100 text-orange-800 border-orange-200',
    blue: 'bg-blue-100 text-blue-800 border-blue-200',
    indigo: 'bg-indigo-100 text-indigo-800 border-indigo-200',
    slate: 'bg-slate-100 text-slate-700 border-slate-200',
    purple: 'bg-purple-100 text-purple-800 border-purple-200'
  }

  A.badge = function (text, color) {
    return `<span class="inline-block px-2 py-0.5 rounded-full text-xs font-semibold border ${
      COLORS[color] || COLORS.slate
    }">${A.esc(text)}</span>`
  }

  A.statusBadge = function (s) {
    return A.badge(A.tr('assetStatus', s), A.L.assetStatusColor[s])
  }
  A.ticketBadge = function (s) {
    return A.badge(A.tr('ticketStatus', s), A.L.ticketStatusColor[s])
  }
  A.prioBadge = function (s) {
    return A.badge(A.tr('priority', s), A.L.priorityColor[s])
  }

  A.statCard = function (o) {
    const href = o.href ? `href="${o.href}"` : ''
    const tag = o.href ? 'a' : 'div'
    return `<${tag} ${href} class="block bg-white rounded-xl shadow-sm border border-slate-200 p-4 ${
      o.href ? 'hover:shadow-md hover:border-brand-300 transition' : ''
    }">
      <div class="flex items-center justify-between gap-2">
        <div class="min-w-0 flex-1">
          <p class="text-xs text-slate-500 mb-1 truncate">${A.esc(o.label)}</p>
          <p class="${o.compact ? 'text-lg' : 'text-xl'} font-extrabold text-slate-800 truncate${
            o.tight ? ' tracking-tight' : ''
          }"${o.title ? ` title="${A.esc(o.title)}"` : ''}>${o.value}</p>
          ${o.sub ? `<p class="text-[11px] text-slate-400 mt-0.5 truncate"${o.subTitle ? ` title="${A.esc(o.subTitle)}"` : ''}>${A.esc(o.sub)}</p>` : ''}
        </div>
        <div class="${o.compact ? 'w-9 h-9' : 'w-11 h-11'} shrink-0 rounded-lg flex items-center justify-center ${
          COLORS[o.color] || COLORS.blue
        }">
          <i class="fas ${o.icon} ${o.compact ? 'text-base' : 'text-lg'}"></i>
        </div>
      </div>
    </${tag}>`
  }

  A.panel = function (title, bodyHtml, extra) {
    return `<section class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <header class="px-4 py-3 border-b border-slate-100 flex items-center justify-between gap-2">
        <h3 class="font-bold text-slate-700 text-sm"><i class="fas fa-circle-dot text-brand-500 text-[10px] ml-1"></i> ${A.esc(
          title
        )}</h3>
        <div>${extra || ''}</div>
      </header>
      <div>${bodyHtml}</div>
    </section>`
  }

  A.empty = function (msg, icon) {
    return `<div class="py-10 text-center text-slate-400">
      <i class="fas ${icon || 'fa-inbox'} text-3xl mb-2 block"></i>
      <p class="text-sm">${A.esc(msg || 'لا توجد بيانات')}</p>
    </div>`
  }

  A.spinner = function (msg) {
    return `<div class="py-16 text-center text-slate-400">
      <i class="fas fa-spinner fa-spin text-3xl text-brand-500"></i>
      <p class="mt-2 text-sm">${A.esc(msg || 'جارٍ التحميل...')}</p>
    </div>`
  }

  /**
   * جدول موحّد
   * cols: [{key, label, render(row), cls, width}]
   */
  A.table = function (cols, rows, opts) {
    opts = opts || {}
    if (!rows || !rows.length) return A.empty(opts.empty)
    const head = cols
      .map((c) => `<th class="px-3 py-2 text-right font-bold whitespace-nowrap ${c.thCls || ''}">${A.esc(c.label)}</th>`)
      .join('')
    const body = rows
      .map((r, i) => {
        const tds = cols
          .map((c) => {
            const v = c.render ? c.render(r, i) : A.esc(r[c.key])
            return `<td class="px-3 py-2 align-middle ${c.cls || ''}">${v === undefined || v === null || v === '' ? '—' : v}</td>`
          })
          .join('')
        const click = opts.rowHref ? ` onclick="location.hash='${opts.rowHref(r)}'" class="cursor-pointer hover:bg-brand-50/60 border-b border-slate-100"` : ' class="hover:bg-slate-50 border-b border-slate-100"'
        return `<tr${click}>${tds}</tr>`
      })
      .join('')
    return `<div class="overflow-x-auto"><table class="w-full text-sm">
      <thead class="bg-slate-50 text-slate-600 text-xs border-b border-slate-200"><tr>${head}</tr></thead>
      <tbody>${body}</tbody>
    </table></div>`
  }

  /* ------------------------------------------------------------------
     4. Form primitives — ONE canonical signature each: object arg
     ------------------------------------------------------------------ */
  const FIELD_WRAP = (o, inner) =>
    `<div class="${o.wrap || ''}">
      ${o.label ? `<label class="block text-xs font-semibold text-slate-600 mb-1">${A.esc(o.label)}${o.required ? ' <span class="text-red-500">*</span>' : ''}</label>` : ''}
      ${inner}
      ${o.hint ? `<p class="text-[11px] text-slate-400 mt-1">${A.esc(o.hint)}</p>` : ''}
    </div>`

  const INPUT_CLS =
    'w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400 focus:border-brand-400 bg-white disabled:bg-slate-100'

  A.inp = function (o) {
    o = o || {}
    return FIELD_WRAP(
      o,
      `<input class="${INPUT_CLS}" type="${o.type || 'text'}" name="${A.esc(o.name)}" id="${A.esc(o.id || o.name)}"
        value="${A.esc(o.value === null || o.value === undefined ? '' : o.value)}"
        placeholder="${A.esc(o.placeholder || '')}" ${o.required ? 'required' : ''} ${o.disabled ? 'disabled' : ''}
        ${o.step ? `step="${o.step}"` : ''} ${o.min !== undefined ? `min="${o.min}"` : ''} ${o.attrs || ''}>`
    )
  }

  A.txt = function (o) {
    o = o || {}
    return FIELD_WRAP(
      o,
      `<textarea class="${INPUT_CLS}" name="${A.esc(o.name)}" id="${A.esc(o.id || o.name)}" rows="${o.rows || 3}"
        placeholder="${A.esc(o.placeholder || '')}" ${o.required ? 'required' : ''} ${o.disabled ? 'disabled' : ''}>${A.esc(
        o.value || ''
      )}</textarea>`
    )
  }

  /** options: [{value,label}] أو [string] */
  A.sel = function (o) {
    o = o || {}
    const opts = (o.options || []).map((x) => (typeof x === 'object' ? x : { value: x, label: x }))
    const cur = o.value === null || o.value === undefined ? '' : String(o.value)
    const body = opts
      .map((x) => `<option value="${A.esc(x.value)}" ${String(x.value) === cur ? 'selected' : ''}>${A.esc(x.label)}</option>`)
      .join('')
    return FIELD_WRAP(
      o,
      `<select class="${INPUT_CLS}" name="${A.esc(o.name)}" id="${A.esc(o.id || o.name)}" ${o.required ? 'required' : ''} ${
        o.disabled ? 'disabled' : ''
      } ${o.attrs || ''}>
        ${o.empty === false ? '' : `<option value="">${A.esc(o.empty || '— اختر —')}</option>`}
        ${body}
      </select>`
    )
  }

  A.chk = function (o) {
    o = o || {}
    return `<label class="flex items-center gap-2 text-sm text-slate-700 cursor-pointer ${o.wrap || ''}">
      <input type="checkbox" name="${A.esc(o.name)}" id="${A.esc(o.id || o.name)}" ${o.value ? 'checked' : ''}
        class="w-4 h-4 rounded border-slate-300 text-brand-600 focus:ring-brand-400">
      <span>${A.esc(o.label)}</span>
    </label>`
  }

  A.btn = function (o) {
    o = o || {}
    const variants = {
      primary: 'bg-brand-600 hover:bg-brand-700 text-white',
      secondary: 'bg-white hover:bg-slate-50 text-slate-700 border border-slate-300',
      danger: 'bg-red-600 hover:bg-red-700 text-white',
      success: 'bg-green-600 hover:bg-green-700 text-white',
      warn: 'bg-amber-500 hover:bg-amber-600 text-white',
      ghost: 'bg-transparent hover:bg-slate-100 text-slate-600'
    }
    const size = o.size === 'sm' ? 'px-2.5 py-1.5 text-xs' : 'px-4 py-2 text-sm'
    const attrs = o.onclick ? `onclick="${o.onclick}"` : ''
    return `<button type="${o.type || 'button'}" ${o.id ? `id="${o.id}"` : ''} ${attrs} ${o.disabled ? 'disabled' : ''}
      class="inline-flex items-center gap-1.5 rounded-lg font-semibold transition ${size} ${
      variants[o.variant || 'primary']
    } disabled:opacity-50 disabled:cursor-not-allowed ${o.cls || ''}">
      ${o.icon ? `<i class="fas ${o.icon}"></i>` : ''}<span>${A.esc(o.label)}</span>
    </button>`
  }

  /** يقرأ نموذجاً إلى كائن عادي */
  A.formData = function (form) {
    const out = {}
    Array.prototype.forEach.call(form.elements, function (el) {
      if (!el.name) return
      if (el.type === 'checkbox') out[el.name] = el.checked
      else out[el.name] = el.value === '' ? null : el.value
    })
    return out
  }

  /* ------------------------------------------------------------------
     5. Toast
     ------------------------------------------------------------------ */
  A.toast = function (msg, type) {
    const wrap = document.getElementById('toast-wrap')
    if (!wrap) return
    const styles = {
      success: 'bg-green-600',
      error: 'bg-red-600',
      info: 'bg-slate-800',
      warn: 'bg-amber-500'
    }
    const icons = { success: 'fa-circle-check', error: 'fa-circle-exclamation', info: 'fa-circle-info', warn: 'fa-triangle-exclamation' }
    const t = type || 'info'
    const el = document.createElement('div')
    el.className = `${styles[t]} text-white px-4 py-2.5 rounded-lg shadow-lg text-sm font-semibold flex items-center gap-2 animate-[fadeIn_.15s_ease-out]`
    el.innerHTML = `<i class="fas ${icons[t]}"></i><span>${A.esc(msg)}</span>`
    wrap.appendChild(el)
    setTimeout(function () {
      el.style.opacity = '0'
      el.style.transition = 'opacity .3s'
      setTimeout(function () {
        el.remove()
      }, 300)
    }, 3200)
  }

  /* ------------------------------------------------------------------
     6. Modal — canonical: A.modal({title, body, size, footer, onSubmit})
     ------------------------------------------------------------------ */
  A.closeModal = function () {
    const r = document.getElementById('modal-root')
    if (r) r.innerHTML = ''
  }

  A.modal = function (o) {
    o = o || {}
    const root = document.getElementById('modal-root')
    const sizes = { sm: 'max-w-md', md: 'max-w-2xl', lg: 'max-w-4xl', xl: 'max-w-6xl' }
    root.innerHTML = `
      <div class="fixed inset-0 z-[9000] flex items-start justify-center p-4 overflow-y-auto bg-slate-900/50" id="modal-backdrop">
        <div class="bg-white rounded-xl shadow-2xl w-full ${sizes[o.size || 'md']} my-8" onclick="event.stopPropagation()">
          <header class="px-5 py-3.5 border-b border-slate-200 flex items-center justify-between">
            <h3 class="font-bold text-slate-800">${A.esc(o.title || '')}</h3>
            <button type="button" onclick="A.closeModal()" class="text-slate-400 hover:text-slate-700 text-lg leading-none px-1">
              <i class="fas fa-xmark"></i>
            </button>
          </header>
          <form id="modal-form" class="px-5 py-4">${o.body || ''}</form>
          <footer class="px-5 py-3 border-t border-slate-200 flex items-center justify-end gap-2 bg-slate-50 rounded-b-xl" id="modal-footer">
            ${
              o.footer !== undefined
                ? o.footer
                : `${A.btn({ label: 'إلغاء', variant: 'secondary', onclick: 'A.closeModal()' })}
                   ${o.onSubmit ? A.btn({ label: o.okLabel || 'حفظ', icon: 'fa-check', id: 'modal-ok' }) : ''}`
            }
          </footer>
        </div>
      </div>`
    document.getElementById('modal-backdrop').addEventListener('click', function (e) {
      if (e.target.id === 'modal-backdrop') A.closeModal()
    })
    const form = document.getElementById('modal-form')
    if (o.onSubmit) {
      const submit = async function (e) {
        if (e) e.preventDefault()
        const okBtn = document.getElementById('modal-ok')
        if (okBtn) {
          okBtn.disabled = true
          okBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i>'
        }
        try {
          await o.onSubmit(A.formData(form), form)
        } catch (err) {
          if (okBtn) {
            okBtn.disabled = false
            okBtn.innerHTML = '<i class="fas fa-check"></i><span>' + A.esc(o.okLabel || 'حفظ') + '</span>'
          }
        }
      }
      form.addEventListener('submit', submit)
      const okBtn = document.getElementById('modal-ok')
      if (okBtn) okBtn.addEventListener('click', submit)
    }
    if (o.onMount) o.onMount(form)
    const first = form.querySelector('input:not([type=hidden]),select,textarea')
    if (first) setTimeout(() => first.focus(), 50)
    return form
  }

  A.confirm = function (o) {
    return new Promise(function (resolve) {
      A.modal({
        title: o.title || 'تأكيد',
        size: 'sm',
        body: `<p class="text-sm text-slate-600 leading-relaxed">${A.esc(o.message || 'هل أنت متأكد؟')}</p>`,
        footer: `${A.btn({ label: 'إلغاء', variant: 'secondary', onclick: 'A.closeModal()' })}
                 ${A.btn({ label: o.okLabel || 'تأكيد', variant: o.danger ? 'danger' : 'primary', id: 'confirm-ok' })}`,
        onMount: function () {
          document.getElementById('confirm-ok').addEventListener('click', function () {
            A.closeModal()
            resolve(true)
          })
          document.getElementById('modal-backdrop').addEventListener('click', function (e) {
            if (e.target.id === 'modal-backdrop') resolve(false)
          })
        }
      })
    })
  }

  /* ------------------------------------------------------------------
     7. API wrapper
     ------------------------------------------------------------------ */
  A.api = async function (method, path, body) {
    try {
      const res = await axios({
        method: method,
        url: '/api' + path,
        data: body,
        headers: { 'Content-Type': 'application/json' },
        validateStatus: function (s) {
          return s < 500
        }
      })
      if (res.status === 401) {
        A.user = null
        if (location.hash !== '#/login') {
          location.hash = '#/login'
        }
        throw new Error('انتهت الجلسة، يرجى تسجيل الدخول')
      }
      if (res.status >= 400) {
        const msg = (res.data && (res.data.error || res.data.message)) || 'حدث خطأ (' + res.status + ')'
        throw new Error(msg)
      }
      return res.data
    } catch (err) {
      if (err.message && err.message.indexOf('Network') >= 0) throw new Error('تعذر الاتصال بالخادم')
      throw err
    }
  }

  /** استدعاء يعرض التوست تلقائياً عند الخطأ */
  A.call = async function (method, path, body) {
    try {
      return await A.api(method, path, body)
    } catch (e) {
      A.toast(e.message || 'خطأ غير معروف', 'error')
      throw e
    }
  }

  /* ------------------------------------------------------------------
     8. CSV export
     ------------------------------------------------------------------ */
  A.csv = function (filename, cols, rows) {
    const head = cols.map((c) => '"' + String(c.label).replace(/"/g, '""') + '"').join(',')
    const body = rows
      .map((r) =>
        cols
          .map((c) => {
            let v = c.value ? c.value(r) : r[c.key]
            if (v === null || v === undefined) v = ''
            return '"' + String(v).replace(/"/g, '""') + '"'
          })
          .join(',')
      )
      .join('\n')
    const blob = new Blob(['\uFEFF' + head + '\n' + body], { type: 'text/csv;charset=utf-8;' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = filename
    document.body.appendChild(a)
    a.click()
    a.remove()
    A.toast('تم تنزيل الملف', 'success')
  }

  /* ------------------------------------------------------------------
     9. Lookups cache (companies/departments/locations/categories/vendors/users)
     ------------------------------------------------------------------ */
  A.lookup = async function (name, force) {
    if (!force && A.cache[name]) return A.cache[name]
    const paths = {
      companies: '/companies',
      departments: '/departments',
      locations: '/locations',
      categories: '/categories',
      vendors: '/vendors',
      users: '/users',
      technicians: '/tickets/technicians'
    }
    const d = await A.api('get', paths[name])
    A.cache[name] = d.items || []
    return A.cache[name]
  }

  A.clearCache = function () {
    A.cache = {}
  }

  A.opt = function (list, valueKey, labelKey) {
    return (list || []).map((x) => ({ value: x[valueKey || 'id'], label: x[labelKey || 'name'] }))
  }

  /* ------------------------------------------------------------------
     10. Router
     ------------------------------------------------------------------ */
  A.route = function (pattern, handler, opts) {
    const keys = []
    const rx = new RegExp(
      '^' +
        pattern
          .replace(/\/:([A-Za-z0-9_]+)/g, function (_, k) {
            keys.push(k)
            return '/([^/]+)'
          })
          .replace(/\//g, '\\/') +
        '$'
    )
    A.routes.push({ rx: rx, keys: keys, handler: handler, opts: opts || {}, pattern: pattern })
  }

  A.go = function (h) {
    location.hash = h
  }

  A.currentPath = function () {
    const h = location.hash.replace(/^#/, '')
    return h === '' ? '/' : h.split('?')[0]
  }

  A.query = function () {
    const h = location.hash.replace(/^#/, '')
    const i = h.indexOf('?')
    const out = {}
    if (i < 0) return out
    h.substring(i + 1)
      .split('&')
      .forEach(function (p) {
        if (!p) return
        const kv = p.split('=')
        out[decodeURIComponent(kv[0])] = decodeURIComponent(kv[1] || '')
      })
    return out
  }

  A.setContent = function (html) {
    const el = document.getElementById('page')
    if (el) el.innerHTML = html
    else document.getElementById('root').innerHTML = html
  }

  A.pageHeader = function (title, subtitle, actionsHtml) {
    return `<div class="flex flex-wrap items-end justify-between gap-3 mb-4">
      <div>
        <h1 class="text-xl font-extrabold text-slate-800">${A.esc(title)}</h1>
        ${subtitle ? `<p class="text-xs text-slate-500 mt-0.5">${A.esc(subtitle)}</p>` : ''}
      </div>
      <div class="flex flex-wrap items-center gap-2">${actionsHtml || ''}</div>
    </div>`
  }

  /* ------------------------------------------------------------------
     11. Navigation definition (role filtered — master_plan §9.1)
     ------------------------------------------------------------------ */
  const ALL = ['Admin', 'CompanyManager', 'Technician', 'Employee']
  A.NAV = [
    { href: '#/', label: 'لوحة التحكم', icon: 'fa-gauge-high', roles: ALL },
    { href: '#/assets', label: 'الأصول', icon: 'fa-boxes-stacked', roles: ALL },
    { href: '#/scan', label: 'مسح QR', icon: 'fa-qrcode', roles: ALL },
    { href: '#/custody', label: 'العهد', icon: 'fa-hand-holding-hand', roles: ALL },
    { href: '#/tickets', label: 'تذاكر الصيانة', icon: 'fa-screwdriver-wrench', roles: ALL },
    { href: '#/schedules', label: 'الصيانة الوقائية', icon: 'fa-calendar-check', roles: ['Admin', 'CompanyManager', 'Technician'] },
    { href: '#/audits', label: 'الجرد الدوري', icon: 'fa-clipboard-list', roles: ['Admin', 'CompanyManager'] },
    { href: '#/reports', label: 'التقارير', icon: 'fa-chart-pie', roles: ALL },
    { href: '#/org', label: 'الهيكل التنظيمي', icon: 'fa-sitemap', roles: ['Admin', 'CompanyManager'] },
    { href: '#/users', label: 'المستخدمون', icon: 'fa-users', roles: ['Admin', 'CompanyManager'] },
    { href: '#/sla', label: 'سياسات SLA', icon: 'fa-stopwatch', roles: ['Admin'] },
    { href: '#/audit-log', label: 'سجل التدقيق', icon: 'fa-shield-halved', roles: ['Admin'] },
    { href: '#/notifications', label: 'الإشعارات', icon: 'fa-bell', roles: ALL },
    { href: '#/settings', label: 'الإعدادات', icon: 'fa-gear', roles: ['Admin'] }
  ]

  A.can = function (roles) {
    return !!A.user && roles.indexOf(A.user.role) >= 0
  }
  A.isManager = function () {
    return A.can(['Admin', 'CompanyManager'])
  }

  /* ------------------------------------------------------------------
     12. Shell
     ------------------------------------------------------------------ */
  let shellRendered = false

  A.renderShell = function () {
    const u = A.user
    const nav = A.NAV.filter((n) => n.roles.indexOf(u.role) >= 0)
      .map(
        (n) => `<a href="${n.href}" data-nav="${n.href}"
          class="nav-item flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm font-semibold text-slate-300 hover:bg-white/10 hover:text-white transition">
          <i class="fas ${n.icon} w-4 text-center"></i><span>${A.esc(n.label)}</span>
          ${n.href === '#/notifications' ? `<span id="nav-unread" class="mr-auto"></span>` : ''}
        </a>`
      )
      .join('')

    document.getElementById('root').innerHTML = `
      <div class="min-h-screen flex">
        <!-- Sidebar -->
        <aside id="sidebar" class="fixed lg:sticky top-0 right-0 h-screen w-64 shrink-0 bg-slate-900 text-white z-50 transition-transform translate-x-full lg:translate-x-0 flex flex-col">
          <div class="px-4 py-4 border-b border-white/10 flex items-center gap-2">
            <div class="w-9 h-9 rounded-lg bg-brand-600 flex items-center justify-center text-lg">📦</div>
            <div class="min-w-0">
              <p class="font-extrabold text-sm leading-tight">نظام الأصول</p>
              <p class="text-[11px] text-slate-400 truncate">${A.esc(u.company_name || 'كل الشركات')}</p>
            </div>
            <button onclick="A.toggleSidebar()" class="lg:hidden mr-auto text-slate-400"><i class="fas fa-xmark"></i></button>
          </div>
          <nav class="flex-1 overflow-y-auto p-2 space-y-1">${nav}</nav>
          <div class="p-3 border-t border-white/10">
            <a href="#/profile" class="flex items-center gap-2 mb-2 hover:bg-white/10 rounded-lg p-2 transition">
              <div class="w-8 h-8 rounded-full bg-brand-500 flex items-center justify-center text-xs font-bold">${A.esc(
                (u.full_name || '?').charAt(0)
              )}</div>
              <div class="min-w-0">
                <p class="text-xs font-bold truncate">${A.esc(u.full_name)}</p>
                <p class="text-[10px] text-slate-400">${A.esc(A.tr('role', u.role))}</p>
              </div>
            </a>
            <button onclick="A.logout()" class="w-full text-xs font-semibold text-red-300 hover:text-white hover:bg-red-600/80 rounded-lg py-2 transition">
              <i class="fas fa-right-from-bracket ml-1"></i> تسجيل الخروج
            </button>
          </div>
        </aside>
        <div id="sidebar-overlay" onclick="A.toggleSidebar()" class="fixed inset-0 bg-slate-900/50 z-40 hidden lg:hidden"></div>

        <!-- Main -->
        <div class="flex-1 min-w-0 flex flex-col">
          <header class="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-slate-200 px-4 py-2.5 flex items-center gap-3">
            <button onclick="A.toggleSidebar()" class="lg:hidden text-slate-600 text-lg"><i class="fas fa-bars"></i></button>
            <form onsubmit="A.quickSearch(event)" class="flex-1 max-w-md relative">
              <input id="quick-search" placeholder="بحث سريع: رقم الأصل / السيريال / التذكرة..."
                class="w-full bg-slate-100 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400">
              <i class="fas fa-magnifying-glass absolute right-3 top-2.5 text-slate-400 text-sm"></i>
            </form>
            <a href="#/scan" class="text-slate-500 hover:text-brand-600 px-2" title="مسح QR"><i class="fas fa-qrcode text-lg"></i></a>
            <a href="#/notifications" class="relative text-slate-500 hover:text-brand-600 px-2" title="الإشعارات">
              <i class="fas fa-bell text-lg"></i>
              <span id="bell-badge" class="hidden absolute -top-1 -left-1 bg-red-500 text-white text-[10px] rounded-full min-w-[16px] h-4 px-1 flex items-center justify-center font-bold"></span>
            </a>
          </header>
          <main id="page" class="flex-1 p-4 lg:p-6 max-w-[1600px] w-full mx-auto"></main>
          <footer class="text-center text-[11px] text-slate-400 py-3 border-t border-slate-200">
            نظام إدارة وتتبع الأصول والدعم الفني — الإصدار 2.0
          </footer>
        </div>
      </div>`
    shellRendered = true
    A.refreshUnread()
  }

  A.toggleSidebar = function () {
    const sb = document.getElementById('sidebar')
    const ov = document.getElementById('sidebar-overlay')
    if (!sb) return
    const hidden = sb.classList.contains('translate-x-full')
    sb.classList.toggle('translate-x-full', !hidden)
    ov.classList.toggle('hidden', !hidden)
  }

  A.highlightNav = function () {
    const path = A.currentPath()
    const base = '#/' + (path.split('/')[1] || '')
    document.querySelectorAll('[data-nav]').forEach(function (el) {
      const on = el.getAttribute('data-nav') === base || (base === '#/' && el.getAttribute('data-nav') === '#/')
      el.classList.toggle('bg-brand-600', on)
      el.classList.toggle('text-white', on)
      el.classList.toggle('text-slate-300', !on)
    })
  }

  A.quickSearch = function (e) {
    e.preventDefault()
    const v = document.getElementById('quick-search').value.trim()
    if (!v) return
    if (/^TKT-/i.test(v)) location.hash = '#/tickets?q=' + encodeURIComponent(v)
    else if (/^AST-/i.test(v)) location.hash = '#/scan-result/' + encodeURIComponent(v)
    else location.hash = '#/assets?q=' + encodeURIComponent(v)
    document.getElementById('quick-search').value = ''
  }

  A.refreshUnread = async function () {
    try {
      const d = await A.api('get', '/notifications')
      A.unread = d.unread || 0
      const b = document.getElementById('bell-badge')
      if (b) {
        b.textContent = A.unread > 99 ? '99+' : A.unread
        b.classList.toggle('hidden', !A.unread)
      }
      const n = document.getElementById('nav-unread')
      if (n) n.innerHTML = A.unread ? `<span class="bg-red-500 text-white text-[10px] rounded-full px-1.5 py-0.5 font-bold">${A.unread}</span>` : ''
    } catch (e) {}
  }

  A.logout = async function () {
    try {
      await A.api('post', '/auth/logout')
    } catch (e) {}
    A.user = null
    A.clearCache()
    shellRendered = false
    location.hash = '#/login'
    location.reload()
  }

  /* ------------------------------------------------------------------
     13. LOGIN page
     ------------------------------------------------------------------ */
  A.renderLogin = function () {
    shellRendered = false
    document.getElementById('root').innerHTML = `
      <div class="min-h-screen flex items-center justify-center p-4 bg-gradient-to-br from-slate-900 via-brand-900 to-slate-800">
        <div class="w-full max-w-4xl grid md:grid-cols-2 bg-white rounded-2xl shadow-2xl overflow-hidden">
          <div class="hidden md:flex flex-col justify-center gap-4 p-8 bg-gradient-to-br from-brand-700 to-brand-900 text-white">
            <div class="text-5xl">📦</div>
            <h2 class="text-2xl font-extrabold leading-snug">نظام إدارة وتتبع<br>الأصول والدعم الفني</h2>
            <p class="text-brand-100 text-sm leading-relaxed">تتبع الأصول بـ QR · إدارة العهد · تذاكر الصيانة مع SLA · الجرد الدوري · الإهلاك والتقارير</p>
            <ul class="text-xs text-brand-100 space-y-1.5 mt-2">
              <li><i class="fas fa-check-circle ml-1"></i> عزل كامل بين الشركات</li>
              <li><i class="fas fa-check-circle ml-1"></i> ٤ مستويات صلاحيات</li>
              <li><i class="fas fa-check-circle ml-1"></i> سجل تدقيق لكل عملية</li>
            </ul>
          </div>
          <div class="p-8">
            <h1 class="text-xl font-extrabold text-slate-800 mb-1">تسجيل الدخول</h1>
            <p class="text-xs text-slate-500 mb-5">أدخل بياناتك للمتابعة</p>
            <form id="login-form" class="space-y-3">
              ${A.inp({ name: 'email', label: 'البريد الإلكتروني', type: 'email', required: true, placeholder: 'admin@ats.eg' })}
              ${A.inp({ name: 'password', label: 'كلمة المرور', type: 'password', required: true, placeholder: '••••••' })}
              <div id="login-error" class="hidden text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2"></div>
              <button type="submit" id="login-btn" class="w-full bg-brand-600 hover:bg-brand-700 text-white font-bold py-2.5 rounded-lg transition">
                <i class="fas fa-right-to-bracket ml-1"></i> دخول
              </button>
            </form>
            <div class="mt-5 pt-4 border-t border-slate-200">
              <p class="text-[11px] font-bold text-slate-500 mb-2">حسابات تجريبية (كلمة المرور: 123456)</p>
              <div class="grid grid-cols-2 gap-1.5 text-[11px]">
                ${[
                  ['admin@ats.eg', 'مدير النظام'],
                  ['manager1@ats.eg', 'مدير شركة'],
                  ['tech1@ats.eg', 'فني صيانة'],
                  ['emp1@ats.eg', 'موظف']
                ]
                  .map(
                    (x) =>
                      `<button type="button" onclick="A.fillLogin('${x[0]}')"
                        class="text-right bg-slate-50 hover:bg-brand-50 border border-slate-200 rounded-lg px-2 py-1.5 transition">
                        <span class="block font-bold text-slate-700">${x[1]}</span>
                        <span class="block text-slate-400 text-[10px]">${x[0]}</span>
                      </button>`
                  )
                  .join('')}
              </div>
            </div>
          </div>
        </div>
      </div>`

    document.getElementById('login-form').addEventListener('submit', async function (e) {
      e.preventDefault()
      const btn = document.getElementById('login-btn')
      const err = document.getElementById('login-error')
      err.classList.add('hidden')
      btn.disabled = true
      btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i>'
      const d = A.formData(e.target)
      try {
        const r = await A.api('post', '/auth/login', { email: d.email, password: d.password })
        A.user = r.user
        A.clearCache()
        A.toast('مرحباً ' + r.user.full_name, 'success')
        location.hash = '#/'
        await A.boot()
      } catch (ex) {
        err.textContent = ex.message || 'فشل تسجيل الدخول'
        err.classList.remove('hidden')
        btn.disabled = false
        btn.innerHTML = '<i class="fas fa-right-to-bracket ml-1"></i> دخول'
      }
    })
  }

  A.fillLogin = function (email) {
    document.querySelector('#login-form [name=email]').value = email
    document.querySelector('#login-form [name=password]').value = '123456'
  }

  /* ------------------------------------------------------------------
     14. DASHBOARD
     ------------------------------------------------------------------ */
  const charts = []
  function destroyCharts() {
    while (charts.length) {
      try {
        charts.pop().destroy()
      } catch (e) {}
    }
  }

  A.chart = function (canvasId, config) {
    const el = document.getElementById(canvasId)
    if (!el || typeof Chart === 'undefined') return
    try {
      const c = new Chart(el, config)
      charts.push(c)
      return c
    } catch (e) {
      console.error('chart', canvasId, e)
    }
  }

  const PALETTE = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2', '#db2777', '#65a30d', '#ea580c', '#4b5563']

  A.renderDashboard = async function () {
    destroyCharts()
    A.setContent(A.spinner())
    const d = await A.call('get', '/dashboard')
    const c = d.cards || {}

    if (d.role === 'Employee') {
      A.setContent(`
        ${A.pageHeader('لوحة التحكم', 'مرحباً ' + A.user.full_name)}
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
          ${A.statCard({ label: 'أصولي (العهد)', value: A.num(c.my_assets), icon: 'fa-boxes-stacked', color: 'blue', href: '#/custody' })}
          ${A.statCard({ label: 'بانتظار قبولي', value: A.num(c.pending_custody), icon: 'fa-hourglass-half', color: 'amber', href: '#/custody' })}
          ${A.statCard({ label: 'تذاكري المفتوحة', value: A.num(c.my_open_tickets), icon: 'fa-screwdriver-wrench', color: 'orange', href: '#/tickets' })}
          ${A.statCard({ label: 'إجمالي تذاكري', value: A.num(c.my_tickets), icon: 'fa-list-check', color: 'slate', href: '#/tickets' })}
        </div>
        <div class="grid lg:grid-cols-2 gap-4">
          ${A.panel(
            'أصولي',
            A.table(
              [
                { label: 'الرقم', render: (r) => `<span class="font-mono text-xs text-brand-700">${A.esc(r.asset_tag)}</span>` },
                { label: 'الأصل', key: 'name' },
                { label: 'التصنيف', key: 'category_name' },
                { label: 'الحالة', render: (r) => A.statusBadge(r.status) }
              ],
              d.my_assets_list,
              { rowHref: (r) => '#/assets/' + r.id, empty: 'لا توجد أصول في عهدتك' }
            ),
            `<a href="#/custody" class="text-xs text-brand-600 font-bold">الكل</a>`
          )}
          ${A.panel(
            'أحدث تذاكري',
            A.table(
              [
                { label: 'الرقم', render: (r) => `<span class="font-mono text-xs">${A.esc(r.ticket_number)}</span>` },
                { label: 'الأصل', key: 'asset_name' },
                { label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
                { label: 'الحالة', render: (r) => A.ticketBadge(r.status) }
              ],
              d.recent_tickets,
              { rowHref: (r) => '#/tickets/' + r.id, empty: 'لا توجد تذاكر' }
            ),
            `<a href="#/tickets" class="text-xs text-brand-600 font-bold">الكل</a>`
          )}
        </div>`)
      return
    }

    if (d.role === 'Technician') {
      A.setContent(`
        ${A.pageHeader('لوحة الفني', 'مرحباً ' + A.user.full_name)}
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
          ${A.statCard({ label: 'تذاكر مُسندة لي', value: A.num(c.assigned_open), icon: 'fa-screwdriver-wrench', color: 'blue', href: '#/tickets' })}
          ${A.statCard({ label: 'حُلّت هذا الشهر', value: A.num(c.resolved_this_month), icon: 'fa-circle-check', color: 'green' })}
          ${A.statCard({ label: 'مخالفات SLA', value: A.num(c.sla_breached), icon: 'fa-triangle-exclamation', color: 'red' })}
          ${A.statCard({ label: 'تذاكر غير مُسندة', value: A.num(c.unassigned_pool), icon: 'fa-inbox', color: 'amber', href: '#/tickets?status=Open' })}
        </div>
        <div class="grid lg:grid-cols-3 gap-4">
          <div class="lg:col-span-2">
            ${A.panel(
              'تذاكري النشطة (حسب الأولوية)',
              A.table(
                [
                  { label: 'الرقم', render: (r) => `<span class="font-mono text-xs">${A.esc(r.ticket_number)}</span>` },
                  { label: 'الأصل', render: (r) => `${A.esc(r.asset_name)}<br><span class="text-[10px] text-slate-400 font-mono">${A.esc(r.asset_tag)}</span>` },
                  { label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
                  { label: 'الحالة', render: (r) => A.ticketBadge(r.status) },
                  { label: 'موعد الحل', render: (r) => A.slaCell(r) }
                ],
                d.my_tickets,
                { rowHref: (r) => '#/tickets/' + r.id, empty: 'لا توجد تذاكر نشطة' }
              )
            )}
          </div>
          ${A.panel('توزيع تذاكري', `<div class="p-4"><canvas id="ch-tech" height="220"></canvas></div>`)}
        </div>`)
      const bs = d.tickets_by_status || []
      A.chart('ch-tech', {
        type: 'doughnut',
        data: {
          labels: bs.map((x) => A.tr('ticketStatus', x.status)),
          datasets: [{ data: bs.map((x) => x.n), backgroundColor: PALETTE }]
        },
        options: { plugins: { legend: { position: 'bottom', labels: { font: { family: 'Cairo', size: 11 } } } } }
      })
      return
    }

    // Admin / CompanyManager
    A.setContent(`
      ${A.pageHeader('لوحة التحكم', A.user.role === 'Admin' ? 'نظرة عامة على كل الشركات' : 'نظرة عامة — ' + (A.user.company_name || ''))}
      <div class="grid grid-cols-2 lg:grid-cols-4 xl:grid-cols-6 gap-3 mb-5">
        ${A.statCard({ label: 'إجمالي الأصول', value: A.num(c.total_assets), icon: 'fa-boxes-stacked', color: 'blue', href: '#/assets' })}
        ${A.statCard({
          label: 'القيمة الدفترية',
          value: A.moneyShort(c.book_value) + ' <span class="text-xs font-bold text-slate-500">ج.م</span>',
          title: A.money(c.book_value),
          tight: true,
          icon: 'fa-sack-dollar',
          color: 'green',
          compact: true,
          sub: 'الشراء: ' + A.moneyShort(c.purchase_value) + ' ج.م',
          subTitle: 'الشراء: ' + A.money(c.purchase_value)
        })}
        ${A.statCard({ label: 'تذاكر مفتوحة', value: A.num(c.open_tickets), icon: 'fa-screwdriver-wrench', color: 'amber', href: '#/tickets' })}
        ${A.statCard({ label: 'مخالفات SLA', value: A.num(c.sla_breached), icon: 'fa-triangle-exclamation', color: 'red', href: '#/tickets?breached=1' })}
        ${A.statCard({ label: 'عهد بانتظار القبول', value: A.num(c.pending_custody), icon: 'fa-hourglass-half', color: 'orange', href: '#/custody' })}
        ${A.statCard({ label: 'المستخدمون', value: A.num(c.users_count), icon: 'fa-users', color: 'purple', href: '#/users' })}
      </div>

      <div class="grid lg:grid-cols-3 gap-4 mb-4">
        ${A.panel('الأصول حسب الحالة', `<div class="p-4"><canvas id="ch-astatus" height="200"></canvas></div>`)}
        ${A.panel('الأصول حسب التصنيف', `<div class="p-4"><canvas id="ch-acat" height="200"></canvas></div>`)}
        ${A.panel('التذاكر حسب الأولوية', `<div class="p-4"><canvas id="ch-tprio" height="200"></canvas></div>`)}
      </div>

      <div class="grid lg:grid-cols-2 gap-4 mb-4">
        ${A.panel('التذاكر شهرياً', `<div class="p-4"><canvas id="ch-monthly" height="160"></canvas></div>`)}
        ${A.panel('التذاكر حسب الحالة', `<div class="p-4"><canvas id="ch-tstatus" height="160"></canvas></div>`)}
      </div>

      <div class="grid lg:grid-cols-3 gap-4">
        <div class="lg:col-span-2">
          ${A.panel(
            'أحدث التذاكر',
            A.table(
              [
                { label: 'الرقم', render: (r) => `<span class="font-mono text-xs">${A.esc(r.ticket_number)}</span>${r.sla_breached ? ' <i class="fas fa-triangle-exclamation text-red-500 text-[10px]"></i>' : ''}` },
                { label: 'الأصل', key: 'asset_name' },
                { label: 'مقدم الطلب', key: 'requester_name' },
                { label: 'الفني', key: 'technician_name' },
                { label: 'الأولوية', render: (r) => A.prioBadge(r.priority) },
                { label: 'الحالة', render: (r) => A.ticketBadge(r.status) }
              ],
              d.recent_tickets,
              { rowHref: (r) => '#/tickets/' + r.id, empty: 'لا توجد تذاكر' }
            ),
            `<a href="#/tickets" class="text-xs text-brand-600 font-bold">الكل</a>`
          )}
        </div>
        <div class="space-y-4">
          ${A.panel(
            'ضمانات تنتهي قريباً',
            A.table(
              [
                { label: 'الأصل', render: (r) => `${A.esc(r.name)}<br><span class="text-[10px] font-mono text-slate-400">${A.esc(r.asset_tag)}</span>` },
                { label: 'الانتهاء', render: (r) => `<span class="text-xs text-amber-700 font-bold">${A.date(r.warranty_expiry_date)}</span>` }
              ],
              d.warranty_soon,
              { rowHref: (r) => '#/assets/' + r.id, empty: 'لا ضمانات قريبة' }
            )
          )}
          ${A.panel(
            'صيانة وقائية مستحقة',
            A.table(
              [
                { label: 'المهمة', render: (r) => `${A.esc(r.title)}<br><span class="text-[10px] text-slate-400">${A.esc(r.asset_name)}</span>` },
                { label: 'الاستحقاق', render: (r) => `<span class="text-xs font-bold ${new Date(r.next_due_date) < new Date() ? 'text-red-600' : 'text-slate-600'}">${A.date(r.next_due_date)}</span>` }
              ],
              d.due_schedules,
              { empty: 'لا مهام مستحقة' }
            ),
            `<a href="#/schedules" class="text-xs text-brand-600 font-bold">الكل</a>`
          )}
        </div>
      </div>`)

    const font = { family: 'Cairo', size: 11 }
    const legendBottom = { legend: { position: 'bottom', labels: { font: font, boxWidth: 12 } } }

    const st = d.assets_by_status || []
    A.chart('ch-astatus', {
      type: 'doughnut',
      data: { labels: st.map((x) => A.tr('assetStatus', x.status)), datasets: [{ data: st.map((x) => x.n), backgroundColor: PALETTE }] },
      options: { plugins: legendBottom }
    })

    const cat = d.assets_by_category || []
    A.chart('ch-acat', {
      type: 'bar',
      data: { labels: cat.map((x) => x.name || 'غير محدد'), datasets: [{ label: 'عدد', data: cat.map((x) => x.n), backgroundColor: '#2563eb', borderRadius: 4 }] },
      options: { indexAxis: 'y', plugins: { legend: { display: false } }, scales: { x: { ticks: { font: font } }, y: { ticks: { font: font } } } }
    })

    const tp = d.tickets_by_priority || []
    A.chart('ch-tprio', {
      type: 'pie',
      data: { labels: tp.map((x) => A.tr('priority', x.priority)), datasets: [{ data: tp.map((x) => x.n), backgroundColor: ['#64748b', '#2563eb', '#f59e0b', '#dc2626'] }] },
      options: { plugins: legendBottom }
    })

    const mt = d.monthly_tickets || []
    A.chart('ch-monthly', {
      type: 'line',
      data: { labels: mt.map((x) => x.m), datasets: [{ label: 'تذاكر', data: mt.map((x) => x.n), borderColor: '#2563eb', backgroundColor: 'rgba(37,99,235,.12)', fill: true, tension: 0.35 }] },
      options: { plugins: { legend: { display: false } }, scales: { x: { ticks: { font: font } }, y: { beginAtZero: true, ticks: { font: font, precision: 0 } } } }
    })

    const ts = d.tickets_by_status || []
    A.chart('ch-tstatus', {
      type: 'bar',
      data: { labels: ts.map((x) => A.tr('ticketStatus', x.status)), datasets: [{ label: 'عدد', data: ts.map((x) => x.n), backgroundColor: '#16a34a', borderRadius: 4 }] },
      options: { plugins: { legend: { display: false } }, scales: { x: { ticks: { font: font } }, y: { beginAtZero: true, ticks: { font: font, precision: 0 } } } }
    })
  }

  /** خلية SLA مشتركة */
  A.slaCell = function (t) {
    if (!t.sla_resolution_due_at) return '—'
    const due = new Date(String(t.sla_resolution_due_at).replace(' ', 'T'))
    const done = ['Resolved', 'Closed', 'Cancelled'].indexOf(t.status) >= 0
    if (t.sla_breached) return `<span class="text-xs font-bold text-red-600"><i class="fas fa-triangle-exclamation"></i> مخالفة</span>`
    if (done) return `<span class="text-xs text-green-600 font-bold">ملتزم</span>`
    const h = (due - Date.now()) / 3600000
    if (h < 0) return `<span class="text-xs font-bold text-red-600">متأخر</span>`
    const color = h < 4 ? 'text-red-600' : h < 24 ? 'text-amber-600' : 'text-slate-600'
    const label = h < 24 ? Math.round(h) + ' ساعة' : Math.round(h / 24) + ' يوم'
    return `<span class="text-xs font-bold ${color}">${label}</span>`
  }

  /* ------------------------------------------------------------------
     15. Profile page
     ------------------------------------------------------------------ */
  A.renderProfile = function () {
    const u = A.user
    A.setContent(`
      ${A.pageHeader('حسابي')}
      <div class="grid lg:grid-cols-2 gap-4">
        ${A.panel(
          'البيانات',
          `<dl class="p-4 text-sm space-y-2">
            ${[
              ['الاسم', u.full_name],
              ['البريد', u.email],
              ['الدور', A.tr('role', u.role)],
              ['المسمى الوظيفي', u.job_title || '—'],
              ['الشركة', u.company_name || 'كل الشركات']
            ]
              .map(
                (x) =>
                  `<div class="flex justify-between border-b border-slate-100 pb-1.5"><dt class="text-slate-500">${A.esc(
                    x[0]
                  )}</dt><dd class="font-semibold">${A.esc(x[1])}</dd></div>`
              )
              .join('')}
          </dl>`
        )}
        ${A.panel(
          'تغيير كلمة المرور',
          `<form id="pw-form" class="p-4 space-y-3">
            ${A.inp({ name: 'old_password', label: 'كلمة المرور الحالية', type: 'password', required: true })}
            ${A.inp({ name: 'new_password', label: 'كلمة المرور الجديدة', type: 'password', required: true, hint: '٦ أحرف على الأقل' })}
            ${A.btn({ label: 'تحديث', icon: 'fa-key', type: 'submit' })}
          </form>`
        )}
      </div>`)
    document.getElementById('pw-form').addEventListener('submit', async function (e) {
      e.preventDefault()
      const d = A.formData(e.target)
      try {
        await A.call('post', '/auth/change-password', d)
        A.toast('تم تحديث كلمة المرور', 'success')
        e.target.reset()
      } catch (ex) {}
    })
  }

  /* ------------------------------------------------------------------
     16. Boot + dispatch
     ------------------------------------------------------------------ */
  A.notFound = function () {
    A.setContent(`<div class="py-20 text-center">
      <i class="fas fa-map-signs text-5xl text-slate-300 mb-3 block"></i>
      <h2 class="text-lg font-bold text-slate-600">الصفحة غير موجودة</h2>
      <a href="#/" class="text-brand-600 text-sm font-bold mt-2 inline-block">العودة للوحة التحكم</a>
    </div>`)
  }

  A.denied = function () {
    A.setContent(`<div class="py-20 text-center">
      <i class="fas fa-lock text-5xl text-red-300 mb-3 block"></i>
      <h2 class="text-lg font-bold text-slate-600">لا تملك صلاحية الوصول لهذه الصفحة</h2>
      <a href="#/" class="text-brand-600 text-sm font-bold mt-2 inline-block">العودة للوحة التحكم</a>
    </div>`)
  }

  A.dispatch = async function () {
    const path = A.currentPath()

    if (!A.user) {
      A.renderLogin()
      return
    }
    if (path === '/login') {
      location.hash = '#/'
      return
    }
    if (!shellRendered) A.renderShell()
    A.highlightNav()
    A.closeModal()
    window.scrollTo(0, 0)

    let match = null
    for (let i = 0; i < A.routes.length; i++) {
      const m = path.match(A.routes[i].rx)
      if (m) {
        match = { r: A.routes[i], m: m }
        break
      }
    }
    if (!match) {
      A.notFound()
      return
    }
    const roles = match.r.opts.roles
    if (roles && roles.indexOf(A.user.role) < 0) {
      A.denied()
      return
    }
    const params = {}
    match.r.keys.forEach(function (k, i) {
      params[k] = decodeURIComponent(match.m[i + 1])
    })
    try {
      await match.r.handler(params, A.query())
    } catch (e) {
      console.error('route error', path, e)
      const nf = /غير موجود|not found/i.test(e.message || '')
      A.setContent(`<div class="py-16 text-center">
        <i class="fas ${nf ? 'fa-folder-open text-slate-300' : 'fa-circle-exclamation text-red-400'} text-4xl mb-3 block"></i>
        <h2 class="text-base font-bold text-slate-700 mb-1">${
          nf ? 'السجل غير موجود أو لا تملك صلاحية الوصول إليه' : 'تعذر تحميل الصفحة'
        }</h2>
        <p class="text-xs text-slate-500 mb-3">${
          nf ? 'قد يكون السجل تابعاً لشركة أو مستخدم آخر' : A.esc(e.message || '')
        }</p>
        <div class="flex gap-2 justify-center">
          ${A.btn({ label: 'إعادة المحاولة', icon: 'fa-rotate', variant: 'secondary', onclick: 'A.dispatch()' })}
          ${A.btn({ label: 'لوحة التحكم', icon: 'fa-gauge-high', onclick: "A.go('#/')" })}
        </div>
      </div>`)
    }
  }

  A.boot = async function () {
    try {
      const d = await A.api('get', '/auth/me')
      A.user = d.user
      A.unread = d.unread || 0
    } catch (e) {
      A.user = null
    }
    await A.dispatch()
  }

  /* base routes (modules register their own) */
  A.route('/', A.renderDashboard)
  A.route('/profile', A.renderProfile)

  window.addEventListener('hashchange', function () {
    A.dispatch()
  })
})()
