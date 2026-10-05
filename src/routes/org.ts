import { Hono } from 'hono'
import {
  type Env,
  requireRole,
  companyScope,
  audit,
  hashPassword,
  toInt,
  nn
} from '../lib'

const org = new Hono<Env>()

/* ==================== COMPANIES ==================== */
org.get('/companies', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const sql = `SELECT c.*,
      (SELECT COUNT(*) FROM assets a WHERE a.company_id=c.id AND a.is_deleted=0) AS assets_count,
      (SELECT COUNT(*) FROM users x WHERE x.company_id=c.id AND x.is_deleted=0) AS users_count
      FROM companies c WHERE c.is_deleted=0 ${cid === null ? '' : 'AND c.id = ?'}
      ORDER BY c.name`
  const st = cid === null ? c.env.DB.prepare(sql) : c.env.DB.prepare(sql).bind(cid)
  const { results } = await st.all()
  return c.json({ items: results || [] })
})

org.post('/companies', requireRole('Admin'), async (c) => {
  const b = await c.req.json().catch(() => ({}))
  if (!b.name) return c.json({ error: 'اسم الشركة مطلوب' }, 400)
  const r = await c.env.DB.prepare(
    `INSERT INTO companies (name, name_en, commercial_no, tax_number, address) VALUES (?,?,?,?,?)`
  )
    .bind(b.name, nn(b.name_en), nn(b.commercial_no), nn(b.tax_number), nn(b.address))
    .run()
  const id = r.meta.last_row_id
  await audit(c, 'Company', id, 'Create', b)
  return c.json({ ok: true, id })
})

org.put('/companies/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  await c.env.DB.prepare(
    `UPDATE companies SET name=?, name_en=?, commercial_no=?, tax_number=?, address=?, is_active=? WHERE id=?`
  )
    .bind(
      b.name,
      nn(b.name_en),
      nn(b.commercial_no),
      nn(b.tax_number),
      nn(b.address),
      b.is_active ? 1 : 0,
      id
    )
    .run()
  await audit(c, 'Company', id, 'Update', b)
  return c.json({ ok: true })
})

org.delete('/companies/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const cnt = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM assets WHERE company_id=? AND is_deleted=0`
  )
    .bind(id)
    .first<{ n: number }>()
  if ((cnt?.n ?? 0) > 0)
    return c.json({ error: 'لا يمكن حذف شركة تملك أصولاً مسجلة' }, 400)
  await c.env.DB.prepare(`UPDATE companies SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Company', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================== DEPARTMENTS ==================== */
org.get('/departments', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const qCompany = toInt(c.req.query('company_id'))
  const params: any[] = []
  let where = 'WHERE d.is_deleted=0'
  if (cid !== null) {
    where += ' AND d.company_id=?'
    params.push(cid)
  } else if (qCompany) {
    where += ' AND d.company_id=?'
    params.push(qCompany)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT d.*, co.name AS company_name, mu.full_name AS manager_name,
       (SELECT COUNT(*) FROM users x WHERE x.department_id=d.id AND x.is_deleted=0) AS users_count
     FROM departments d
     LEFT JOIN companies co ON co.id=d.company_id
     LEFT JOIN users mu ON mu.id=d.manager_user_id
     ${where} ORDER BY co.name, d.name`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

org.post('/departments', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const companyId = u.role === 'Admin' ? toInt(b.company_id) : u.company_id
  if (!b.name || !companyId) return c.json({ error: 'اسم الإدارة والشركة مطلوبان' }, 400)
  const r = await c.env.DB.prepare(
    `INSERT INTO departments (company_id, name, code, manager_user_id) VALUES (?,?,?,?)`
  )
    .bind(companyId, b.name, nn(b.code), toInt(b.manager_user_id))
    .run()
  await audit(c, 'Department', r.meta.last_row_id, 'Create', b)
  return c.json({ ok: true, id: r.meta.last_row_id })
})

org.put('/departments/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM departments WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  await c.env.DB.prepare(`UPDATE departments SET name=?, code=?, manager_user_id=? WHERE id=?`)
    .bind(b.name, nn(b.code), toInt(b.manager_user_id), id)
    .run()
  await audit(c, 'Department', id, 'Update', b)
  return c.json({ ok: true })
})

org.delete('/departments/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM departments WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  await c.env.DB.prepare(`UPDATE departments SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Department', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================== LOCATIONS ==================== */
org.get('/locations', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const qCompany = toInt(c.req.query('company_id'))
  const params: any[] = []
  let where = 'WHERE l.is_deleted=0'
  if (cid !== null) {
    where += ' AND l.company_id=?'
    params.push(cid)
  } else if (qCompany) {
    where += ' AND l.company_id=?'
    params.push(qCompany)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT l.*, co.name AS company_name,
       (SELECT COUNT(*) FROM assets a WHERE a.location_id=l.id AND a.is_deleted=0) AS assets_count
     FROM locations l LEFT JOIN companies co ON co.id=l.company_id
     ${where} ORDER BY co.name, l.name`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

org.post('/locations', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const companyId = u.role === 'Admin' ? toInt(b.company_id) : u.company_id
  if (!b.name || !companyId) return c.json({ error: 'اسم الموقع والشركة مطلوبان' }, 400)
  const r = await c.env.DB.prepare(
    `INSERT INTO locations (company_id, name, type, address_details, gps_coordinates) VALUES (?,?,?,?,?)`
  )
    .bind(companyId, b.name, b.type || 'Office', nn(b.address_details), nn(b.gps_coordinates))
    .run()
  await audit(c, 'Location', r.meta.last_row_id, 'Create', b)
  return c.json({ ok: true, id: r.meta.last_row_id })
})

org.put('/locations/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM locations WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  await c.env.DB.prepare(
    `UPDATE locations SET name=?, type=?, address_details=?, gps_coordinates=? WHERE id=?`
  )
    .bind(b.name, b.type || 'Office', nn(b.address_details), nn(b.gps_coordinates), id)
    .run()
  await audit(c, 'Location', id, 'Update', b)
  return c.json({ ok: true })
})

org.delete('/locations/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM locations WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  const cnt = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM assets WHERE location_id=? AND is_deleted=0`
  )
    .bind(id)
    .first<{ n: number }>()
  if ((cnt?.n ?? 0) > 0) return c.json({ error: 'لا يمكن حذف موقع يحتوي أصولاً' }, 400)
  await c.env.DB.prepare(`UPDATE locations SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Location', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================== VENDORS ==================== */
org.get('/vendors', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const params: any[] = []
  let where = 'WHERE v.is_deleted=0'
  if (cid !== null) {
    where += ' AND (v.company_id=? OR v.company_id IS NULL)'
    params.push(cid)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT v.*, co.name AS company_name,
       (SELECT COUNT(*) FROM assets a WHERE a.vendor_id=v.id AND a.is_deleted=0) AS assets_count
     FROM vendors v LEFT JOIN companies co ON co.id=v.company_id
     ${where} ORDER BY v.name`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

org.post('/vendors', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  if (!b.name) return c.json({ error: 'اسم المورد مطلوب' }, 400)
  const companyId = u.role === 'Admin' ? toInt(b.company_id) : u.company_id
  const r = await c.env.DB.prepare(
    `INSERT INTO vendors (company_id, name, contact_person, phone, email, address) VALUES (?,?,?,?,?,?)`
  )
    .bind(companyId, b.name, nn(b.contact_person), nn(b.phone), nn(b.email), nn(b.address))
    .run()
  await audit(c, 'Vendor', r.meta.last_row_id, 'Create', b)
  return c.json({ ok: true, id: r.meta.last_row_id })
})

org.put('/vendors/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  await c.env.DB.prepare(
    `UPDATE vendors SET name=?, contact_person=?, phone=?, email=?, address=? WHERE id=?`
  )
    .bind(b.name, nn(b.contact_person), nn(b.phone), nn(b.email), nn(b.address), id)
    .run()
  await audit(c, 'Vendor', id, 'Update', b)
  return c.json({ ok: true })
})

org.delete('/vendors/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const id = toInt(c.req.param('id'))
  await c.env.DB.prepare(`UPDATE vendors SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Vendor', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================== CATEGORIES ==================== */
org.get('/categories', async (c) => {
  const { results } = await c.env.DB.prepare(
    `SELECT ct.*, p.name AS parent_name,
       (SELECT COUNT(*) FROM assets a WHERE a.category_id=ct.id AND a.is_deleted=0) AS assets_count
     FROM categories ct LEFT JOIN categories p ON p.id=ct.parent_category_id
     WHERE ct.is_deleted=0 ORDER BY COALESCE(p.name, ct.name), ct.parent_category_id IS NOT NULL, ct.name`
  ).all()
  return c.json({ items: results || [] })
})

org.post('/categories', requireRole('Admin'), async (c) => {
  const b = await c.req.json().catch(() => ({}))
  if (!b.name) return c.json({ error: 'اسم التصنيف مطلوب' }, 400)
  const r = await c.env.DB.prepare(
    `INSERT INTO categories (parent_category_id, name, code, default_useful_life_years, default_salvage_rate)
     VALUES (?,?,?,?,?)`
  )
    .bind(
      toInt(b.parent_category_id),
      b.name,
      nn(b.code),
      toInt(b.default_useful_life_years) ?? 5,
      Number(b.default_salvage_rate ?? 0.1)
    )
    .run()
  await audit(c, 'Category', r.meta.last_row_id, 'Create', b)
  return c.json({ ok: true, id: r.meta.last_row_id })
})

org.put('/categories/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  await c.env.DB.prepare(
    `UPDATE categories SET parent_category_id=?, name=?, code=?, default_useful_life_years=?, default_salvage_rate=? WHERE id=?`
  )
    .bind(
      toInt(b.parent_category_id),
      b.name,
      nn(b.code),
      toInt(b.default_useful_life_years) ?? 5,
      Number(b.default_salvage_rate ?? 0.1),
      id
    )
    .run()
  await audit(c, 'Category', id, 'Update', b)
  return c.json({ ok: true })
})

org.delete('/categories/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const cnt = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM assets WHERE category_id=? AND is_deleted=0`
  )
    .bind(id)
    .first<{ n: number }>()
  if ((cnt?.n ?? 0) > 0) return c.json({ error: 'لا يمكن حذف تصنيف مستخدم في أصول' }, 400)
  await c.env.DB.prepare(`UPDATE categories SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Category', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================== USERS ==================== */
org.get('/users', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const role = c.req.query('role')
  const params: any[] = []
  let where = 'WHERE x.is_deleted=0'
  if (cid !== null) {
    where += ' AND x.company_id=?'
    params.push(cid)
  }
  if (role) {
    where += ' AND x.role=?'
    params.push(role)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT x.id, x.company_id, x.department_id, x.full_name, x.email, x.role, x.phone_number,
            x.job_title, x.employee_number, x.is_active, x.last_login_at,
            co.name AS company_name, d.name AS department_name,
            (SELECT COUNT(*) FROM assets a WHERE a.current_custody_user_id=x.id AND a.is_deleted=0) AS custody_count
     FROM users x
     LEFT JOIN companies co ON co.id=x.company_id
     LEFT JOIN departments d ON d.id=x.department_id
     ${where} ORDER BY x.full_name`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

org.post('/users', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const email = String(b.email || '').trim().toLowerCase()
  if (!b.full_name || !email || !b.password)
    return c.json({ error: 'الاسم والبريد وكلمة المرور مطلوبة' }, 400)
  if (String(b.password).length < 6)
    return c.json({ error: 'كلمة المرور يجب أن تكون 6 أحرف على الأقل' }, 400)

  let role = b.role || 'Employee'
  if (u.role === 'CompanyManager' && (role === 'Admin' || role === 'CompanyManager'))
    return c.json({ error: 'لا يمكنك إنشاء حساب بهذه الصلاحية' }, 403)

  const companyId = u.role === 'Admin' ? toInt(b.company_id) : u.company_id
  const dup = await c.env.DB.prepare(`SELECT 1 FROM users WHERE lower(email)=?`).bind(email).first()
  if (dup) return c.json({ error: 'البريد الإلكتروني مستخدم بالفعل' }, 400)

  const r = await c.env.DB.prepare(
    `INSERT INTO users (company_id, department_id, full_name, email, password_hash, role,
        phone_number, job_title, employee_number) VALUES (?,?,?,?,?,?,?,?,?)`
  )
    .bind(
      companyId,
      toInt(b.department_id),
      b.full_name,
      email,
      await hashPassword(String(b.password)),
      role,
      nn(b.phone_number),
      nn(b.job_title),
      nn(b.employee_number)
    )
    .run()
  await audit(c, 'User', r.meta.last_row_id, 'Create', { ...b, password: '***' })
  return c.json({ ok: true, id: r.meta.last_row_id })
})

org.put('/users/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM users WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  if (u.role === 'CompanyManager' && (row.role === 'Admin' || b.role === 'Admin'))
    return c.json({ error: 'لا يمكنك تعديل حساب بهذه الصلاحية' }, 403)

  await c.env.DB.prepare(
    `UPDATE users SET full_name=?, department_id=?, role=?, phone_number=?, job_title=?,
        employee_number=?, is_active=? WHERE id=?`
  )
    .bind(
      b.full_name ?? row.full_name,
      toInt(b.department_id),
      b.role ?? row.role,
      nn(b.phone_number),
      nn(b.job_title),
      nn(b.employee_number),
      b.is_active === undefined ? row.is_active : b.is_active ? 1 : 0,
      id
    )
    .run()

  if (b.password) {
    if (String(b.password).length < 6)
      return c.json({ error: 'كلمة المرور يجب أن تكون 6 أحرف على الأقل' }, 400)
    await c.env.DB.prepare(`UPDATE users SET password_hash=? WHERE id=?`)
      .bind(await hashPassword(String(b.password)), id)
      .run()
  }
  await audit(c, 'User', id, 'Update', { ...b, password: b.password ? '***' : undefined })
  return c.json({ ok: true })
})

org.delete('/users/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  if (id === u.id) return c.json({ error: 'لا يمكنك حذف حسابك الخاص' }, 400)
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(`SELECT * FROM users WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  const cnt = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM assets WHERE current_custody_user_id=? AND is_deleted=0`
  )
    .bind(id)
    .first<{ n: number }>()
  if ((cnt?.n ?? 0) > 0)
    return c.json({ error: 'لا يمكن حذف مستخدم لديه عهد — يجب إرجاع العهد أولاً' }, 400)
  await c.env.DB.prepare(`UPDATE users SET is_deleted=1, is_active=0 WHERE id=?`).bind(id).run()
  await audit(c, 'User', id, 'Delete')
  return c.json({ ok: true })
})

export default org
