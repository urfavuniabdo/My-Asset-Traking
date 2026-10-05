import { Hono } from 'hono'
import {
  type Env,
  requireRole,
  companyScope,
  audit,
  nextAssetTag,
  toInt,
  toNum,
  nn,
  nowSql,
  notify
} from '../lib'

const assets = new Hono<Env>()

const STATUSES = ['Active', 'UnderMaintenance', 'Damaged', 'Disposed', 'Lost', 'InStore']

/* ==================== LIST ==================== */
assets.get('/', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const q = (c.req.query('q') || '').trim()
  const status = c.req.query('status') || ''
  const categoryId = toInt(c.req.query('category_id'))
  const locationId = toInt(c.req.query('location_id'))
  const companyId = toInt(c.req.query('company_id'))
  const custodyUserId = toInt(c.req.query('custody_user_id'))
  const page = Math.max(1, toInt(c.req.query('page')) ?? 1)
  const size = Math.min(100, Math.max(5, toInt(c.req.query('size')) ?? 25))
  const sort = c.req.query('sort') || 'id'
  const dir = (c.req.query('dir') || 'desc').toLowerCase() === 'asc' ? 'ASC' : 'DESC'
  const sortable: Record<string, string> = {
    id: 'a.id',
    asset_tag: 'a.asset_tag',
    name: 'a.name',
    status: 'a.status',
    purchase_cost: 'a.purchase_cost',
    book_value: 'a.book_value',
    purchase_date: 'a.purchase_date'
  }
  const orderBy = sortable[sort] || 'a.id'

  const params: any[] = []
  let where = 'WHERE a.is_deleted=0'

  // company isolation
  if (cid !== null) {
    where += ' AND a.company_id=?'
    params.push(cid)
  } else if (companyId) {
    where += ' AND a.company_id=?'
    params.push(companyId)
  }
  // Employees only see their own custody
  if (u.role === 'Employee') {
    where += ' AND a.current_custody_user_id=?'
    params.push(u.id)
  } else if (custodyUserId) {
    where += ' AND a.current_custody_user_id=?'
    params.push(custodyUserId)
  }
  if (q) {
    where +=
      ' AND (a.name LIKE ? OR a.asset_tag LIKE ? OR a.serial_number LIKE ? OR a.brand LIKE ? OR a.model LIKE ? OR a.barcode LIKE ?)'
    const like = `%${q}%`
    params.push(like, like, like, like, like, like)
  }
  if (status) {
    where += ' AND a.status=?'
    params.push(status)
  }
  if (categoryId) {
    where += ' AND a.category_id=?'
    params.push(categoryId)
  }
  if (locationId) {
    where += ' AND a.location_id=?'
    params.push(locationId)
  }

  const totalRow = await c.env.DB.prepare(`SELECT COUNT(*) AS n FROM assets a ${where}`)
    .bind(...params)
    .first<{ n: number }>()
  const total = totalRow?.n ?? 0

  const { results } = await c.env.DB.prepare(
    `SELECT a.*, ct.name AS category_name, l.name AS location_name, co.name AS company_name,
            usr.full_name AS custody_user_name, v.name AS vendor_name,
            (SELECT COUNT(*) FROM maintenance_tickets t WHERE t.asset_id=a.id) AS tickets_count
     FROM assets a
     LEFT JOIN categories ct ON ct.id=a.category_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN companies co ON co.id=a.company_id
     LEFT JOIN users usr ON usr.id=a.current_custody_user_id
     LEFT JOIN vendors v ON v.id=a.vendor_id
     ${where} ORDER BY ${orderBy} ${dir} LIMIT ? OFFSET ?`
  )
    .bind(...params, size, (page - 1) * size)
    .all()

  return c.json({ items: results || [], total, page, size, pages: Math.ceil(total / size) })
})

/* ==================== BY TAG (QR scan) ==================== */
assets.get('/by-tag/:tag', async (c) => {
  const u = c.get('user')
  const tag = c.req.param('tag')
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(
    `SELECT a.*, ct.name AS category_name, l.name AS location_name, co.name AS company_name,
            usr.full_name AS custody_user_name, v.name AS vendor_name
     FROM assets a
     LEFT JOIN categories ct ON ct.id=a.category_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN companies co ON co.id=a.company_id
     LEFT JOIN users usr ON usr.id=a.current_custody_user_id
     LEFT JOIN vendors v ON v.id=a.vendor_id
     WHERE (a.asset_tag=? OR a.barcode=? OR a.serial_number=?) AND a.is_deleted=0`
  )
    .bind(tag, tag, tag)
    .first<any>()

  if (!row) return c.json({ error: 'لم يتم العثور على أصل بهذا الرقم' }, 404)
  if (cid !== null && row.company_id !== cid) return c.json({ error: 'غير موجود' }, 404)
  if (u.role === 'Employee' && row.current_custody_user_id !== u.id)
    return c.json({ error: 'هذا الأصل ليس في عهدتك' }, 403)
  return c.json({ item: row })
})

/* ==================== DETAIL ==================== */
assets.get('/:id', async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const cid = companyScope(u)
  const item = await c.env.DB.prepare(
    `SELECT a.*, ct.name AS category_name, ct.default_useful_life_years, l.name AS location_name,
            co.name AS company_name, usr.full_name AS custody_user_name, v.name AS vendor_name
     FROM assets a
     LEFT JOIN categories ct ON ct.id=a.category_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN companies co ON co.id=a.company_id
     LEFT JOIN users usr ON usr.id=a.current_custody_user_id
     LEFT JOIN vendors v ON v.id=a.vendor_id
     WHERE a.id=? AND a.is_deleted=0`
  )
    .bind(id)
    .first<any>()

  if (!item) return c.json({ error: 'غير موجود' }, 404)
  if (cid !== null && item.company_id !== cid) return c.json({ error: 'غير موجود' }, 404)
  if (u.role === 'Employee' && item.current_custody_user_id !== u.id)
    return c.json({ error: 'غير موجود' }, 404)

  const custody = await c.env.DB.prepare(
    `SELECT cl.*, pu.full_name AS previous_user_name, nu.full_name AS new_user_name,
            ab.full_name AS assigned_by_name
     FROM custody_logs cl
     LEFT JOIN users pu ON pu.id=cl.previous_user_id
     LEFT JOIN users nu ON nu.id=cl.new_user_id
     LEFT JOIN users ab ON ab.id=cl.assigned_by_user_id
     WHERE cl.asset_id=? ORDER BY cl.id DESC`
  )
    .bind(id)
    .all()

  const locations = await c.env.DB.prepare(
    `SELECT ll.*, pl.name AS previous_location_name, nl.name AS new_location_name,
            mb.full_name AS moved_by_name
     FROM location_logs ll
     LEFT JOIN locations pl ON pl.id=ll.previous_location_id
     LEFT JOIN locations nl ON nl.id=ll.new_location_id
     LEFT JOIN users mb ON mb.id=ll.moved_by_user_id
     WHERE ll.asset_id=? ORDER BY ll.id DESC`
  )
    .bind(id)
    .all()

  const tickets = await c.env.DB.prepare(
    `SELECT t.id, t.ticket_number, t.status, t.priority, t.issue_description, t.total_cost,
            t.created_at, t.closed_at, tech.full_name AS technician_name, rq.full_name AS requester_name
     FROM maintenance_tickets t
     LEFT JOIN users tech ON tech.id=t.assigned_technician_id
     LEFT JOIN users rq ON rq.id=t.requester_user_id
     WHERE t.asset_id=? ORDER BY t.id DESC`
  )
    .bind(id)
    .all()

  const depreciation = await c.env.DB.prepare(
    `SELECT * FROM depreciation_entries WHERE asset_id=? ORDER BY period_date DESC LIMIT 24`
  )
    .bind(id)
    .all()

  const schedules = await c.env.DB.prepare(
    `SELECT * FROM maintenance_schedules WHERE asset_id=? ORDER BY next_due_date`
  )
    .bind(id)
    .all()

  return c.json({
    item,
    custody: custody.results || [],
    locations: locations.results || [],
    tickets: tickets.results || [],
    depreciation: depreciation.results || [],
    schedules: schedules.results || []
  })
})

/* ==================== CREATE ==================== */
assets.post('/', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const companyId = u.role === 'Admin' ? toInt(b.company_id) : u.company_id
  if (!b.name || !companyId) return c.json({ error: 'اسم الأصل والشركة مطلوبان' }, 400)

  const tag = await nextAssetTag(c.env.DB)
  const cost = toNum(b.purchase_cost)
  const life = toInt(b.useful_life_years) ?? 5
  const salvage = toNum(b.salvage_value)

  const r = await c.env.DB.prepare(
    `INSERT INTO assets (asset_tag, company_id, category_id, location_id, vendor_id,
        current_custody_user_id, name, serial_number, barcode, model, brand, status,
        purchase_cost, purchase_date, warranty_expiry_date, useful_life_years, salvage_value,
        accumulated_depreciation, book_value, notes)
     VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?,?)`
  )
    .bind(
      tag,
      companyId,
      toInt(b.category_id),
      toInt(b.location_id),
      toInt(b.vendor_id),
      toInt(b.current_custody_user_id),
      b.name,
      nn(b.serial_number),
      nn(b.barcode),
      nn(b.model),
      nn(b.brand),
      STATUSES.includes(b.status) ? b.status : 'Active',
      cost,
      nn(b.purchase_date),
      nn(b.warranty_expiry_date),
      life,
      salvage,
      cost,
      nn(b.notes)
    )
    .run()

  const id = r.meta.last_row_id

  if (toInt(b.current_custody_user_id)) {
    await c.env.DB.prepare(
      `INSERT INTO custody_logs (asset_id, new_user_id, action_type, acceptance_status,
          transfer_date, reason, assigned_by_user_id) VALUES (?,?,'Assign','Pending',?,?,?)`
    )
      .bind(id, toInt(b.current_custody_user_id), nowSql(), 'تسليم عند تسجيل الأصل', u.id)
      .run()
    await notify(
      c.env.DB,
      toInt(b.current_custody_user_id),
      'إقرار استلام عهدة',
      `لديك عهدة بانتظار الإقرار: ${b.name}`,
      'Custody',
      '#/custody'
    )
  }
  if (toInt(b.location_id)) {
    await c.env.DB.prepare(
      `INSERT INTO location_logs (asset_id, new_location_id, transfer_date, reason, moved_by_user_id)
       VALUES (?,?,?,?,?)`
    )
      .bind(id, toInt(b.location_id), nowSql(), 'الموقع الأولي عند التسجيل', u.id)
      .run()
  }

  await audit(c, 'Asset', id, 'Create', { ...b, asset_tag: tag })
  return c.json({ ok: true, id, asset_tag: tag })
})

/* ==================== UPDATE ==================== */
assets.put('/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const cid = companyScope(u)
  const old = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!old || (cid !== null && old.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)

  const cost = b.purchase_cost === undefined ? old.purchase_cost : toNum(b.purchase_cost)
  const life = toInt(b.useful_life_years) ?? old.useful_life_years
  const salvage = b.salvage_value === undefined ? old.salvage_value : toNum(b.salvage_value)
  const newLocation = toInt(b.location_id)

  await c.env.DB.prepare(
    `UPDATE assets SET category_id=?, location_id=?, vendor_id=?, name=?, serial_number=?,
        barcode=?, model=?, brand=?, purchase_cost=?, purchase_date=?, warranty_expiry_date=?,
        useful_life_years=?, salvage_value=?, notes=?, book_value=?, updated_at=datetime('now')
     WHERE id=?`
  )
    .bind(
      toInt(b.category_id),
      newLocation,
      toInt(b.vendor_id),
      b.name ?? old.name,
      nn(b.serial_number),
      nn(b.barcode),
      nn(b.model),
      nn(b.brand),
      cost,
      nn(b.purchase_date),
      nn(b.warranty_expiry_date),
      life,
      salvage,
      nn(b.notes),
      Math.max(0, cost - (old.accumulated_depreciation || 0)),
      id
    )
    .run()

  if (newLocation && newLocation !== old.location_id) {
    await c.env.DB.prepare(
      `INSERT INTO location_logs (asset_id, previous_location_id, new_location_id, transfer_date, reason, moved_by_user_id)
       VALUES (?,?,?,?,?,?)`
    )
      .bind(id, old.location_id, newLocation, nowSql(), b.location_reason || 'تعديل بيانات الأصل', u.id)
      .run()
  }

  await audit(c, 'Asset', id, 'Update', { before: old, after: b })
  return c.json({ ok: true })
})

/* ==================== CHANGE STATUS ==================== */
assets.post('/:id/status', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const status = b.status
  if (!STATUSES.includes(status)) return c.json({ error: 'حالة غير صحيحة' }, 400)

  const cid = companyScope(u)
  const old = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!old || (cid !== null && old.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)

  // Disposed / Lost require Admin (final approval per plan §9)
  if ((status === 'Disposed' || status === 'Lost') && u.role !== 'Admin')
    return c.json({ error: 'التكهين أو الفقدان يحتاج موافقة مدير النظام' }, 403)

  await c.env.DB.prepare(
    `UPDATE assets SET status=?, notes=COALESCE(?, notes), updated_at=datetime('now') WHERE id=?`
  )
    .bind(status, nn(b.notes), id)
    .run()

  // Disposed assets release custody
  if (status === 'Disposed' || status === 'Lost') {
    if (old.current_custody_user_id) {
      await c.env.DB.prepare(
        `INSERT INTO custody_logs (asset_id, previous_user_id, action_type, acceptance_status,
            transfer_date, reason, assigned_by_user_id) VALUES (?,?,'Return','Accepted',?,?,?)`
      )
        .bind(id, old.current_custody_user_id, nowSql(), `تحرير العهدة بسبب: ${status}`, u.id)
        .run()
      await c.env.DB.prepare(`UPDATE assets SET current_custody_user_id=NULL WHERE id=?`)
        .bind(id)
        .run()
    }
  }

  await audit(c, 'Asset', id, 'Update', { status: { old: old.status, new: status }, notes: b.notes })
  return c.json({ ok: true })
})

/* ==================== DELETE (soft) ==================== */
assets.delete('/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const row = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(id)
    .first<any>()
  if (!row) return c.json({ error: 'غير موجود' }, 404)
  const open = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE asset_id=? AND status NOT IN ('Closed','Cancelled')`
  )
    .bind(id)
    .first<{ n: number }>()
  if ((open?.n ?? 0) > 0) return c.json({ error: 'لا يمكن حذف أصل له تذاكر مفتوحة' }, 400)
  await c.env.DB.prepare(`UPDATE assets SET is_deleted=1 WHERE id=?`).bind(id).run()
  await audit(c, 'Asset', id, 'Delete', row)
  return c.json({ ok: true })
})

/* ==================== BULK IMPORT (CSV rows) ==================== */
assets.post('/bulk', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const rows: any[] = Array.isArray(b.rows) ? b.rows : []
  if (!rows.length) return c.json({ error: 'لا توجد صفوف للاستيراد' }, 400)
  if (rows.length > 500) return c.json({ error: 'الحد الأقصى 500 صف في المرة الواحدة' }, 400)

  let created = 0
  const errors: string[] = []
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i]
    try {
      const companyId = u.role === 'Admin' ? toInt(r.company_id) ?? u.company_id : u.company_id
      if (!r.name || !companyId) {
        errors.push(`صف ${i + 1}: الاسم أو الشركة مفقود`)
        continue
      }
      const tag = await nextAssetTag(c.env.DB)
      const cost = toNum(r.purchase_cost)
      await c.env.DB.prepare(
        `INSERT INTO assets (asset_tag, company_id, category_id, location_id, vendor_id, name,
            serial_number, brand, model, status, purchase_cost, purchase_date, warranty_expiry_date,
            useful_life_years, salvage_value, accumulated_depreciation, book_value, notes)
         VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?,?)`
      )
        .bind(
          tag,
          companyId,
          toInt(r.category_id),
          toInt(r.location_id),
          toInt(r.vendor_id),
          r.name,
          nn(r.serial_number),
          nn(r.brand),
          nn(r.model),
          STATUSES.includes(r.status) ? r.status : 'Active',
          cost,
          nn(r.purchase_date),
          nn(r.warranty_expiry_date),
          toInt(r.useful_life_years) ?? 5,
          toNum(r.salvage_value),
          cost,
          nn(r.notes)
        )
        .run()
      created++
    } catch (e: any) {
      errors.push(`صف ${i + 1}: ${e.message || 'خطأ غير معروف'}`)
    }
  }
  await audit(c, 'Asset', null, 'Create', { bulk: true, created, errors: errors.length })
  return c.json({ ok: true, created, errors })
})

export default assets
