import { Hono } from 'hono'
import {
  type Env,
  requireRole,
  companyScope,
  audit,
  notify,
  toInt,
  toNum,
  nn,
  nowSql,
  addDaysSql,
  monthlyDepreciation
} from '../lib'

const misc = new Hono<Env>()

/* ==================================================================
   DASHBOARD
   ================================================================== */
misc.get('/dashboard', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const cScope = cid === null ? '' : ` AND company_id=${cid}`
  const D = c.env.DB

  if (u.role === 'Employee') {
    const myAssets = await D.prepare(
      `SELECT COUNT(*) AS n FROM assets WHERE current_custody_user_id=? AND is_deleted=0`
    )
      .bind(u.id)
      .first<any>()
    const myTickets = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE requester_user_id=?`
    )
      .bind(u.id)
      .first<any>()
    const openTickets = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE requester_user_id=? AND status NOT IN ('Closed','Cancelled')`
    )
      .bind(u.id)
      .first<any>()
    const pending = await D.prepare(
      `SELECT COUNT(*) AS n FROM custody_logs WHERE new_user_id=? AND acceptance_status='Pending'`
    )
      .bind(u.id)
      .first<any>()
    const recentTickets = await D.prepare(
      `SELECT t.id, t.ticket_number, t.status, t.priority, t.created_at, a.name AS asset_name
       FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
       WHERE t.requester_user_id=? ORDER BY t.id DESC LIMIT 5`
    )
      .bind(u.id)
      .all()
    const myList = await D.prepare(
      `SELECT a.id, a.asset_tag, a.name, a.status, ct.name AS category_name
       FROM assets a LEFT JOIN categories ct ON ct.id=a.category_id
       WHERE a.current_custody_user_id=? AND a.is_deleted=0 ORDER BY a.id DESC LIMIT 8`
    )
      .bind(u.id)
      .all()
    return c.json({
      role: u.role,
      cards: {
        my_assets: myAssets?.n ?? 0,
        my_tickets: myTickets?.n ?? 0,
        my_open_tickets: openTickets?.n ?? 0,
        pending_custody: pending?.n ?? 0
      },
      recent_tickets: recentTickets.results || [],
      my_assets_list: myList.results || []
    })
  }

  if (u.role === 'Technician') {
    const assigned = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE assigned_technician_id=? AND status NOT IN ('Closed','Cancelled')`
    )
      .bind(u.id)
      .first<any>()
    const resolvedMonth = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE assigned_technician_id=?
        AND resolved_at IS NOT NULL AND resolved_at >= date('now','start of month')`
    )
      .bind(u.id)
      .first<any>()
    const breached = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE assigned_technician_id=? AND sla_breached=1`
    )
      .bind(u.id)
      .first<any>()
    const unassigned = await D.prepare(
      `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE status='Open'${cScope}`
    ).first<any>()
    const myTickets = await D.prepare(
      `SELECT t.id, t.ticket_number, t.status, t.priority, t.created_at, t.sla_resolution_due_at,
              a.name AS asset_name, a.asset_tag
       FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
       WHERE t.assigned_technician_id=? AND t.status NOT IN ('Closed','Cancelled')
       ORDER BY CASE t.priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END, t.id DESC
       LIMIT 10`
    )
      .bind(u.id)
      .all()
    const byStatus = await D.prepare(
      `SELECT status, COUNT(*) AS n FROM maintenance_tickets WHERE assigned_technician_id=? GROUP BY status`
    )
      .bind(u.id)
      .all()
    return c.json({
      role: u.role,
      cards: {
        assigned_open: assigned?.n ?? 0,
        resolved_this_month: resolvedMonth?.n ?? 0,
        sla_breached: breached?.n ?? 0,
        unassigned_pool: unassigned?.n ?? 0
      },
      my_tickets: myTickets.results || [],
      tickets_by_status: byStatus.results || []
    })
  }

  // Admin / CompanyManager
  const totalAssets = await D.prepare(
    `SELECT COUNT(*) AS n FROM assets WHERE is_deleted=0${cScope}`
  ).first<any>()
  const totalValue = await D.prepare(
    `SELECT COALESCE(SUM(book_value),0) AS v, COALESCE(SUM(purchase_cost),0) AS p
       FROM assets WHERE is_deleted=0 AND status NOT IN ('Disposed')${cScope}`
  ).first<any>()
  const openTickets = await D.prepare(
    `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE status NOT IN ('Closed','Cancelled')${cScope}`
  ).first<any>()
  const breached = await D.prepare(
    `SELECT COUNT(*) AS n FROM maintenance_tickets WHERE sla_breached=1${cScope}`
  ).first<any>()
  const usersCount = await D.prepare(
    `SELECT COUNT(*) AS n FROM users WHERE is_deleted=0${cScope}`
  ).first<any>()
  const pendingCustody = await D.prepare(
    `SELECT COUNT(*) AS n FROM custody_logs cl JOIN assets a ON a.id=cl.asset_id
      WHERE cl.acceptance_status='Pending'${cid === null ? '' : ` AND a.company_id=${cid}`}`
  ).first<any>()

  const byStatus = await D.prepare(
    `SELECT status, COUNT(*) AS n FROM assets WHERE is_deleted=0${cScope} GROUP BY status`
  ).all()
  const byCategory = await D.prepare(
    `SELECT ct.name, COUNT(*) AS n, COALESCE(SUM(a.book_value),0) AS value
       FROM assets a LEFT JOIN categories ct ON ct.id=a.category_id
      WHERE a.is_deleted=0${cid === null ? '' : ` AND a.company_id=${cid}`}
      GROUP BY ct.name ORDER BY n DESC LIMIT 8`
  ).all()
  const ticketsByStatus = await D.prepare(
    `SELECT status, COUNT(*) AS n FROM maintenance_tickets WHERE 1=1${cScope} GROUP BY status`
  ).all()
  const ticketsByPriority = await D.prepare(
    `SELECT priority, COUNT(*) AS n FROM maintenance_tickets WHERE 1=1${cScope} GROUP BY priority`
  ).all()
  const monthlyTickets = await D.prepare(
    `SELECT strftime('%Y-%m', created_at) AS m, COUNT(*) AS n
       FROM maintenance_tickets WHERE 1=1${cScope}
      GROUP BY m ORDER BY m DESC LIMIT 6`
  ).all()
  const recentTickets = await D.prepare(
    `SELECT t.id, t.ticket_number, t.status, t.priority, t.created_at, t.sla_breached,
            a.name AS asset_name, rq.full_name AS requester_name, tech.full_name AS technician_name
     FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
     LEFT JOIN users rq ON rq.id=t.requester_user_id
     LEFT JOIN users tech ON tech.id=t.assigned_technician_id
     WHERE 1=1${cid === null ? '' : ` AND t.company_id=${cid}`}
     ORDER BY t.id DESC LIMIT 8`
  ).all()
  const warrantySoon = await D.prepare(
    `SELECT id, asset_tag, name, warranty_expiry_date FROM assets
      WHERE is_deleted=0 AND warranty_expiry_date IS NOT NULL
        AND warranty_expiry_date BETWEEN date('now') AND date('now','+60 day')${cScope}
      ORDER BY warranty_expiry_date LIMIT 8`
  ).all()
  const dueSchedules = await D.prepare(
    `SELECT ms.id, ms.title, ms.next_due_date, a.name AS asset_name, a.asset_tag
       FROM maintenance_schedules ms JOIN assets a ON a.id=ms.asset_id
      WHERE ms.is_active=1 AND ms.next_due_date <= date('now','+14 day')
        ${cid === null ? '' : ` AND a.company_id=${cid}`}
      ORDER BY ms.next_due_date LIMIT 8`
  ).all()

  return c.json({
    role: u.role,
    cards: {
      total_assets: totalAssets?.n ?? 0,
      book_value: totalValue?.v ?? 0,
      purchase_value: totalValue?.p ?? 0,
      open_tickets: openTickets?.n ?? 0,
      sla_breached: breached?.n ?? 0,
      users_count: usersCount?.n ?? 0,
      pending_custody: pendingCustody?.n ?? 0
    },
    assets_by_status: byStatus.results || [],
    assets_by_category: byCategory.results || [],
    tickets_by_status: ticketsByStatus.results || [],
    tickets_by_priority: ticketsByPriority.results || [],
    monthly_tickets: (monthlyTickets.results || []).reverse(),
    recent_tickets: recentTickets.results || [],
    warranty_soon: warrantySoon.results || [],
    due_schedules: dueSchedules.results || []
  })
})

/* ==================================================================
   NOTIFICATIONS
   ================================================================== */
misc.get('/notifications', async (c) => {
  const u = c.get('user')
  const { results } = await c.env.DB.prepare(
    `SELECT * FROM notifications WHERE user_id=? ORDER BY id DESC LIMIT 60`
  )
    .bind(u.id)
    .all()
  const unread = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM notifications WHERE user_id=? AND is_read=0`
  )
    .bind(u.id)
    .first<any>()
  return c.json({ items: results || [], unread: unread?.n ?? 0 })
})

misc.post('/notifications/:id/read', async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  await c.env.DB.prepare(`UPDATE notifications SET is_read=1 WHERE id=? AND user_id=?`)
    .bind(id, u.id)
    .run()
  return c.json({ ok: true })
})

misc.post('/notifications/read-all', async (c) => {
  const u = c.get('user')
  await c.env.DB.prepare(`UPDATE notifications SET is_read=1 WHERE user_id=?`).bind(u.id).run()
  return c.json({ ok: true })
})

/* ==================================================================
   MAINTENANCE SCHEDULES (preventive)
   ================================================================== */
misc.get('/schedules', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const params: any[] = []
  let where = 'WHERE 1=1'
  if (cid !== null) {
    where += ' AND a.company_id=?'
    params.push(cid)
  }
  if (u.role === 'Employee') return c.json({ items: [] })
  const { results } = await c.env.DB.prepare(
    `SELECT ms.*, a.name AS asset_name, a.asset_tag, a.status AS asset_status,
            l.name AS location_name, co.name AS company_name,
            CASE WHEN ms.next_due_date < date('now') THEN 1 ELSE 0 END AS is_overdue
     FROM maintenance_schedules ms
     JOIN assets a ON a.id=ms.asset_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN companies co ON co.id=a.company_id
     ${where} ORDER BY ms.is_active DESC, ms.next_due_date`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

misc.post('/schedules', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const assetId = toInt(b.asset_id)
  if (!assetId || !b.title || !b.next_due_date)
    return c.json({ error: 'الأصل والعنوان وتاريخ الاستحقاق مطلوبة' }, 400)
  const cid = companyScope(u)
  const asset = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(assetId)
    .first<any>()
  if (!asset || (cid !== null && asset.company_id !== cid))
    return c.json({ error: 'غير موجود' }, 404)

  let checklist = b.checklist_json
  if (Array.isArray(b.checklist)) checklist = JSON.stringify(b.checklist)
  else if (typeof b.checklist === 'string' && b.checklist.trim())
    checklist = JSON.stringify(
      b.checklist.split('\n').map((s: string) => s.trim()).filter(Boolean)
    )

  const r = await c.env.DB.prepare(
    `INSERT INTO maintenance_schedules (asset_id, title, recurrence_type, next_due_date, checklist_json)
     VALUES (?,?,?,?,?)`
  )
    .bind(
      assetId,
      b.title,
      ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual'].includes(b.recurrence_type)
        ? b.recurrence_type
        : 'Quarterly',
      b.next_due_date,
      nn(checklist)
    )
    .run()
  await audit(c, 'MaintenanceSchedule', r.meta.last_row_id, 'Create', b)
  return c.json({ ok: true, id: r.meta.last_row_id })
})

misc.put('/schedules/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(
    `SELECT ms.*, a.company_id FROM maintenance_schedules ms JOIN assets a ON a.id=ms.asset_id WHERE ms.id=?`
  )
    .bind(id)
    .first<any>()
  if (!row || (cid !== null && row.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)

  await c.env.DB.prepare(
    `UPDATE maintenance_schedules SET title=?, recurrence_type=?, next_due_date=?, is_active=? WHERE id=?`
  )
    .bind(
      b.title ?? row.title,
      b.recurrence_type ?? row.recurrence_type,
      b.next_due_date ?? row.next_due_date,
      b.is_active === undefined ? row.is_active : b.is_active ? 1 : 0,
      id
    )
    .run()
  await audit(c, 'MaintenanceSchedule', id, 'Update', b)
  return c.json({ ok: true })
})

misc.delete('/schedules/:id', requireRole('Admin', 'CompanyManager'), async (c) => {
  const id = toInt(c.req.param('id'))
  await c.env.DB.prepare(`DELETE FROM maintenance_schedules WHERE id=?`).bind(id).run()
  await audit(c, 'MaintenanceSchedule', id, 'Delete')
  return c.json({ ok: true })
})

/* ==================================================================
   INVENTORY AUDITS
   ================================================================== */
misc.get('/audits', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const params: any[] = []
  let where = 'WHERE 1=1'
  if (cid !== null) {
    where += ' AND ia.company_id=?'
    params.push(cid)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT ia.*, co.name AS company_name, l.name AS location_name, cb.full_name AS created_by_name,
      (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id) AS items_count,
      (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id AND i.result='Found') AS found_count,
      (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id AND i.result='Missing') AS missing_count
     FROM inventory_audits ia
     LEFT JOIN companies co ON co.id=ia.company_id
     LEFT JOIN locations l ON l.id=ia.location_id
     LEFT JOIN users cb ON cb.id=ia.created_by_user_id
     ${where} ORDER BY ia.id DESC`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

misc.post('/audits', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const companyId = u.role === 'Admin' ? toInt(b.company_id) ?? u.company_id : u.company_id
  const locationId = toInt(b.location_id)
  if (!companyId || !locationId || !b.title)
    return c.json({ error: 'الشركة والموقع والعنوان مطلوبون' }, 400)

  const r = await c.env.DB.prepare(
    `INSERT INTO inventory_audits (company_id, location_id, title, status, created_by_user_id, started_at)
     VALUES (?,?,?,'InProgress',?,datetime('now'))`
  )
    .bind(companyId, locationId, b.title, u.id)
    .run()
  const auditId = r.meta.last_row_id

  // snapshot expected assets at this location
  const { results } = await c.env.DB.prepare(
    `SELECT id FROM assets WHERE location_id=? AND is_deleted=0 AND status NOT IN ('Disposed')`
  )
    .bind(locationId)
    .all<any>()
  for (const a of results || [])
    await c.env.DB.prepare(
      `INSERT INTO inventory_audit_items (inventory_audit_id, asset_id, result) VALUES (?,?,'Expected')`
    )
      .bind(auditId, a.id)
      .run()

  await audit(c, 'InventoryAudit', auditId, 'Create', b)
  return c.json({ ok: true, id: auditId, expected: (results || []).length })
})

misc.get('/audits/:id', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const cid = companyScope(u)
  const item = await c.env.DB.prepare(
    `SELECT ia.*, co.name AS company_name, l.name AS location_name, cb.full_name AS created_by_name
     FROM inventory_audits ia
     LEFT JOIN companies co ON co.id=ia.company_id
     LEFT JOIN locations l ON l.id=ia.location_id
     LEFT JOIN users cb ON cb.id=ia.created_by_user_id
     WHERE ia.id=?`
  )
    .bind(id)
    .first<any>()
  if (!item || (cid !== null && item.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)

  const items = await c.env.DB.prepare(
    `SELECT i.*, a.asset_tag, a.name AS asset_name, a.status AS asset_status,
            l.name AS asset_location_name, sb.full_name AS scanned_by_name
     FROM inventory_audit_items i
     JOIN assets a ON a.id=i.asset_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN users sb ON sb.id=i.scanned_by_user_id
     WHERE i.inventory_audit_id=? ORDER BY i.result, a.asset_tag`
  )
    .bind(id)
    .all()

  return c.json({ item, items: items.results || [] })
})

/* scan an asset inside an audit session */
misc.post('/audits/:id/scan', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const tag = String(b.asset_tag || '').trim()
  if (!tag) return c.json({ error: 'رقم الأصل مطلوب' }, 400)

  const cid = companyScope(u)
  const a = await c.env.DB.prepare(
    `SELECT ia.* FROM inventory_audits ia WHERE ia.id=?`
  )
    .bind(id)
    .first<any>()
  if (!a || (cid !== null && a.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  if (a.status === 'Completed') return c.json({ error: 'جلسة الجرد مكتملة' }, 400)

  const asset = await c.env.DB.prepare(
    `SELECT * FROM assets WHERE (asset_tag=? OR barcode=? OR serial_number=?) AND is_deleted=0`
  )
    .bind(tag, tag, tag)
    .first<any>()
  if (!asset) return c.json({ error: 'لم يتم العثور على أصل بهذا الرقم' }, 404)
  if (asset.company_id !== a.company_id)
    return c.json({ error: 'هذا الأصل تابع لشركة أخرى' }, 400)

  const result = asset.location_id === a.location_id ? 'Found' : 'WrongLocation'
  const existing = await c.env.DB.prepare(
    `SELECT * FROM inventory_audit_items WHERE inventory_audit_id=? AND asset_id=?`
  )
    .bind(id, asset.id)
    .first<any>()

  if (existing) {
    await c.env.DB.prepare(
      `UPDATE inventory_audit_items SET result=?, scanned_by_user_id=?, scanned_at=datetime('now'), notes=? WHERE id=?`
    )
      .bind(result, u.id, nn(b.notes), existing.id)
      .run()
  } else {
    await c.env.DB.prepare(
      `INSERT INTO inventory_audit_items (inventory_audit_id, asset_id, result, scanned_by_user_id, scanned_at, notes)
       VALUES (?,?,?,?,datetime('now'),?)`
    )
      .bind(id, asset.id, result, u.id, nn(b.notes) || 'أصل غير متوقع بهذا الموقع')
      .run()
  }

  return c.json({
    ok: true,
    result,
    asset: { id: asset.id, asset_tag: asset.asset_tag, name: asset.name }
  })
})

/* complete the audit — remaining Expected become Missing */
misc.post('/audits/:id/complete', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const cid = companyScope(u)
  const a = await c.env.DB.prepare(`SELECT * FROM inventory_audits WHERE id=?`)
    .bind(id)
    .first<any>()
  if (!a || (cid !== null && a.company_id !== cid)) return c.json({ error: 'غير موجود' }, 404)
  if (a.status === 'Completed') return c.json({ error: 'الجلسة مكتملة بالفعل' }, 400)

  await c.env.DB.prepare(
    `UPDATE inventory_audit_items SET result='Missing' WHERE inventory_audit_id=? AND result='Expected'`
  )
    .bind(id)
    .run()
  await c.env.DB.prepare(
    `UPDATE inventory_audits SET status='Completed', completed_at=datetime('now') WHERE id=?`
  )
    .bind(id)
    .run()

  const missing = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM inventory_audit_items WHERE inventory_audit_id=? AND result='Missing'`
  )
    .bind(id)
    .first<any>()

  await audit(c, 'InventoryAudit', id, 'Update', { status: 'Completed', missing: missing?.n })
  return c.json({ ok: true, missing: missing?.n ?? 0 })
})

/* ==================================================================
   AUDIT LOGS (Admin only)
   ================================================================== */
misc.get('/audit-logs', requireRole('Admin'), async (c) => {
  const entity = c.req.query('entity') || ''
  const action = c.req.query('action') || ''
  const page = Math.max(1, toInt(c.req.query('page')) ?? 1)
  const size = Math.min(200, toInt(c.req.query('size')) ?? 50)
  const params: any[] = []
  let where = 'WHERE 1=1'
  if (entity) {
    where += ' AND al.entity_name=?'
    params.push(entity)
  }
  if (action) {
    where += ' AND al.action=?'
    params.push(action)
  }
  const total = await c.env.DB.prepare(`SELECT COUNT(*) AS n FROM audit_logs al ${where}`)
    .bind(...params)
    .first<any>()
  const { results } = await c.env.DB.prepare(
    `SELECT al.*, x.full_name AS user_name, x.role AS user_role
     FROM audit_logs al LEFT JOIN users x ON x.id=al.user_id
     ${where} ORDER BY al.id DESC LIMIT ? OFFSET ?`
  )
    .bind(...params, size, (page - 1) * size)
    .all()
  return c.json({ items: results || [], total: total?.n ?? 0, page, size })
})

/* ==================================================================
   REPORTS
   ================================================================== */
misc.get('/reports/:type', async (c) => {
  const u = c.get('user')
  const type = c.req.param('type')
  const cid = companyScope(u)
  const cA = cid === null ? '' : ` AND a.company_id=${cid}`
  const cT = cid === null ? '' : ` AND t.company_id=${cid}`
  const D = c.env.DB

  if (u.role === 'Employee' && !['my-assets', 'my-tickets'].includes(type))
    return c.json({ error: 'ليس لديك صلاحية لهذا التقرير' }, 403)

  switch (type) {
    case 'assets-by-company': {
      const { results } = await D.prepare(
        `SELECT co.name AS company_name, COUNT(a.id) AS assets_count,
                COALESCE(SUM(a.purchase_cost),0) AS purchase_total,
                COALESCE(SUM(a.book_value),0) AS book_total,
                COALESCE(SUM(a.accumulated_depreciation),0) AS depreciation_total
         FROM assets a LEFT JOIN companies co ON co.id=a.company_id
         WHERE a.is_deleted=0${cA} GROUP BY co.name ORDER BY assets_count DESC`
      ).all()
      return c.json({ title: 'تقرير الأصول حسب الشركة', items: results || [] })
    }
    case 'assets-by-status': {
      const { results } = await D.prepare(
        `SELECT a.status, COUNT(*) AS assets_count, COALESCE(SUM(a.book_value),0) AS book_total
         FROM assets a WHERE a.is_deleted=0${cA} GROUP BY a.status ORDER BY assets_count DESC`
      ).all()
      return c.json({ title: 'تقرير الأصول حسب الحالة', items: results || [] })
    }
    case 'assets-by-location': {
      const { results } = await D.prepare(
        `SELECT l.name AS location_name, l.type AS location_type, co.name AS company_name,
                COUNT(a.id) AS assets_count, COALESCE(SUM(a.book_value),0) AS book_total
         FROM assets a LEFT JOIN locations l ON l.id=a.location_id
         LEFT JOIN companies co ON co.id=a.company_id
         WHERE a.is_deleted=0${cA} GROUP BY l.id ORDER BY assets_count DESC`
      ).all()
      return c.json({ title: 'تقرير توزيع الأصول على المواقع', items: results || [] })
    }
    case 'custody': {
      const { results } = await D.prepare(
        `SELECT x.full_name AS user_name, x.employee_number, d.name AS department_name,
                co.name AS company_name, COUNT(a.id) AS assets_count,
                COALESCE(SUM(a.book_value),0) AS book_total
         FROM assets a JOIN users x ON x.id=a.current_custody_user_id
         LEFT JOIN departments d ON d.id=x.department_id
         LEFT JOIN companies co ON co.id=a.company_id
         WHERE a.is_deleted=0${cA} GROUP BY x.id ORDER BY assets_count DESC`
      ).all()
      return c.json({ title: 'تقرير العهد حسب الموظف', items: results || [] })
    }
    case 'maintenance-cost': {
      const { results } = await D.prepare(
        `SELECT a.asset_tag, a.name AS asset_name, ct.name AS category_name,
                COUNT(t.id) AS tickets_count,
                COALESCE(SUM(t.labor_cost),0) AS labor_total,
                COALESCE(SUM(t.parts_cost),0) AS parts_total,
                COALESCE(SUM(t.total_cost),0) AS cost_total
         FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
         LEFT JOIN categories ct ON ct.id=a.category_id
         WHERE 1=1${cT} GROUP BY a.id HAVING cost_total > 0 OR tickets_count > 0
         ORDER BY cost_total DESC LIMIT 100`
      ).all()
      return c.json({ title: 'تقرير تكاليف الصيانة لكل أصل', items: results || [] })
    }
    case 'technician-performance': {
      const { results } = await D.prepare(
        `SELECT x.full_name AS technician_name,
                COUNT(t.id) AS total_tickets,
                SUM(CASE WHEN t.status IN ('Resolved','Closed') THEN 1 ELSE 0 END) AS resolved_count,
                SUM(CASE WHEN t.status NOT IN ('Resolved','Closed','Cancelled') THEN 1 ELSE 0 END) AS open_count,
                SUM(CASE WHEN t.sla_breached=1 THEN 1 ELSE 0 END) AS breached_count,
                COALESCE(SUM(t.total_cost),0) AS cost_total
         FROM maintenance_tickets t JOIN users x ON x.id=t.assigned_technician_id
         WHERE 1=1${cT} GROUP BY x.id ORDER BY resolved_count DESC`
      ).all()
      return c.json({ title: 'تقرير أداء الفنيين', items: results || [] })
    }
    case 'sla': {
      const { results } = await D.prepare(
        `SELECT t.priority, COUNT(*) AS total,
                SUM(CASE WHEN t.sla_breached=1 THEN 1 ELSE 0 END) AS breached,
                SUM(CASE WHEN t.sla_breached=0 THEN 1 ELSE 0 END) AS compliant
         FROM maintenance_tickets t WHERE 1=1${cT} GROUP BY t.priority`
      ).all()
      const agg = await D.prepare(
        `SELECT COUNT(*) AS total, SUM(CASE WHEN sla_breached=1 THEN 1 ELSE 0 END) AS breached
         FROM maintenance_tickets t WHERE 1=1${cT}`
      ).first<any>()
      const total = agg?.total ?? 0
      const breached = agg?.breached ?? 0
      const compliance = total ? Math.round(((total - breached) / total) * 100) : 100
      return c.json({ title: 'تقرير الالتزام بـ SLA', items: results || [], compliance })
    }
    case 'depreciation': {
      const { results } = await D.prepare(
        `SELECT a.asset_tag, a.name AS asset_name, ct.name AS category_name,
                a.purchase_cost, a.accumulated_depreciation, a.book_value,
                a.useful_life_years, a.purchase_date
         FROM assets a LEFT JOIN categories ct ON ct.id=a.category_id
         WHERE a.is_deleted=0 AND a.purchase_cost > 0${cA}
         ORDER BY a.accumulated_depreciation DESC LIMIT 100`
      ).all()
      return c.json({ title: 'تقرير الإهلاك والقيمة الدفترية', items: results || [] })
    }
    case 'warranty': {
      const { results } = await D.prepare(
        `SELECT a.asset_tag, a.name AS asset_name, a.brand, a.warranty_expiry_date,
                v.name AS vendor_name, co.name AS company_name,
                CAST(julianday(a.warranty_expiry_date) - julianday('now') AS INTEGER) AS days_left
         FROM assets a LEFT JOIN vendors v ON v.id=a.vendor_id
         LEFT JOIN companies co ON co.id=a.company_id
         WHERE a.is_deleted=0 AND a.warranty_expiry_date IS NOT NULL${cA}
         ORDER BY a.warranty_expiry_date LIMIT 100`
      ).all()
      return c.json({ title: 'تقرير الضمانات', items: results || [] })
    }
    case 'inventory': {
      const { results } = await D.prepare(
        `SELECT ia.title, ia.status, l.name AS location_name, co.name AS company_name,
                ia.started_at, ia.completed_at,
                (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id) AS items_count,
                (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id AND i.result='Found') AS found_count,
                (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id AND i.result='Missing') AS missing_count,
                (SELECT COUNT(*) FROM inventory_audit_items i WHERE i.inventory_audit_id=ia.id AND i.result='WrongLocation') AS wrong_count
         FROM inventory_audits ia
         LEFT JOIN locations l ON l.id=ia.location_id
         LEFT JOIN companies co ON co.id=ia.company_id
         WHERE 1=1${cid === null ? '' : ` AND ia.company_id=${cid}`} ORDER BY ia.id DESC`
      ).all()
      return c.json({ title: 'تقرير جلسات الجرد', items: results || [] })
    }
    case 'my-assets': {
      const { results } = await D.prepare(
        `SELECT a.asset_tag, a.name AS asset_name, ct.name AS category_name, a.status,
                l.name AS location_name, a.purchase_date, a.book_value
         FROM assets a LEFT JOIN categories ct ON ct.id=a.category_id
         LEFT JOIN locations l ON l.id=a.location_id
         WHERE a.current_custody_user_id=? AND a.is_deleted=0 ORDER BY a.id DESC`
      )
        .bind(u.id)
        .all()
      return c.json({ title: 'تقرير عهدي', items: results || [] })
    }
    case 'my-tickets': {
      const { results } = await D.prepare(
        `SELECT t.ticket_number, a.name AS asset_name, t.status, t.priority, t.created_at,
                t.resolved_at, tech.full_name AS technician_name, t.total_cost
         FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
         LEFT JOIN users tech ON tech.id=t.assigned_technician_id
         WHERE t.requester_user_id=? ORDER BY t.id DESC`
      )
        .bind(u.id)
        .all()
      return c.json({ title: 'تقرير تذاكري', items: results || [] })
    }
    default:
      return c.json({ error: 'نوع تقرير غير معروف' }, 400)
  }
})

/* ==================================================================
   BACKGROUND JOBS (manual triggers — Hangfire equivalent)
   ================================================================== */

/* SLA breach scan */
misc.post('/jobs/sla-check', requireRole('Admin'), async (c) => {
  const D = c.env.DB
  const { results } = await D.prepare(
    `SELECT t.*, a.name AS asset_name FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id
      WHERE t.sla_breached=0 AND t.status NOT IN ('Closed','Cancelled','Resolved')
        AND t.sla_resolution_due_at IS NOT NULL AND t.sla_resolution_due_at < datetime('now')`
  ).all<any>()

  let flagged = 0
  for (const t of results || []) {
    await D.prepare(`UPDATE maintenance_tickets SET sla_breached=1 WHERE id=?`).bind(t.id).run()
    await D.prepare(
      `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, old_value, new_value, notes)
       VALUES (?,?,'Escalated','0','1','تجاوز زمن الحل المستهدف (SLA)')`
    )
      .bind(t.id, c.get('user').id)
      .run()
    if (t.assigned_technician_id)
      await notify(
        D,
        t.assigned_technician_id,
        'تجاوز SLA',
        `التذكرة ${t.ticket_number} تجاوزت زمن الحل المستهدف`,
        'SLA',
        `#/tickets/${t.id}`
      )
    flagged++
  }
  await audit(c, 'Job', null, 'Update', { job: 'sla-check', flagged })
  return c.json({ ok: true, flagged })
})

/* monthly depreciation posting */
misc.post('/jobs/depreciation', requireRole('Admin'), async (c) => {
  const D = c.env.DB
  const period = new Date().toISOString().slice(0, 10)
  const { results } = await D.prepare(
    `SELECT * FROM assets WHERE is_deleted=0 AND status NOT IN ('Disposed')
       AND purchase_cost > 0 AND useful_life_years > 0`
  ).all<any>()

  let posted = 0
  for (const a of results || []) {
    const already = await D.prepare(
      `SELECT 1 FROM depreciation_entries WHERE asset_id=? AND strftime('%Y-%m',period_date)=strftime('%Y-%m',?)`
    )
      .bind(a.id, period)
      .first()
    if (already) continue

    const amount = monthlyDepreciation(a.purchase_cost, a.salvage_value, a.useful_life_years)
    if (amount <= 0) continue
    const newAcc = Math.min(
      (a.accumulated_depreciation || 0) + amount,
      a.purchase_cost - (a.salvage_value || 0)
    )
    const newBook = a.purchase_cost - newAcc
    if (newAcc <= (a.accumulated_depreciation || 0)) continue

    await D.prepare(
      `INSERT INTO depreciation_entries (asset_id, period_date, amount, book_value_after, method)
       VALUES (?,?,?,?,'StraightLine')`
    )
      .bind(a.id, period, Math.round(amount * 100) / 100, Math.round(newBook * 100) / 100)
      .run()
    await D.prepare(`UPDATE assets SET accumulated_depreciation=?, book_value=? WHERE id=?`)
      .bind(Math.round(newAcc * 100) / 100, Math.round(newBook * 100) / 100, a.id)
      .run()
    posted++
  }
  await audit(c, 'Job', null, 'Update', { job: 'depreciation', posted })
  return c.json({ ok: true, posted })
})

/* generate preventive maintenance tickets for due schedules */
misc.post('/jobs/generate-schedules', requireRole('Admin'), async (c) => {
  const D = c.env.DB
  const u = c.get('user')
  const { results } = await D.prepare(
    `SELECT ms.*, a.company_id, a.name AS asset_name FROM maintenance_schedules ms
      JOIN assets a ON a.id=ms.asset_id
     WHERE ms.is_active=1 AND ms.next_due_date <= date('now') AND a.is_deleted=0`
  ).all<any>()

  const { nextTicketNumber, addHoursSql } = await import('../lib')
  let created = 0
  for (const s of results || []) {
    const sla = await D.prepare(
      `SELECT * FROM sla_policies WHERE priority='Medium' AND is_active=1 LIMIT 1`
    ).first<any>()
    const number = await nextTicketNumber(D)
    const r = await D.prepare(
      `INSERT INTO maintenance_tickets (ticket_number, asset_id, company_id, requester_user_id,
          sla_policy_id, status, priority, source, issue_description, sla_response_due_at, sla_resolution_due_at)
       VALUES (?,?,?,?,?,'Open','Medium','Preventive',?,?,?)`
    )
      .bind(
        number,
        s.asset_id,
        s.company_id,
        u.id,
        sla?.id ?? null,
        `صيانة وقائية مجدولة: ${s.title}`,
        sla ? addHoursSql(sla.response_time_hours) : null,
        sla ? addHoursSql(sla.resolution_time_hours) : null
      )
      .run()
    await D.prepare(
      `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, new_value, notes)
       VALUES (?,?,'Created','Open','تم إنشاؤها تلقائياً من الصيانة الوقائية')`
    )
      .bind(r.meta.last_row_id, u.id)
      .run()

    // advance next due date
    const months =
      s.recurrence_type === 'Monthly'
        ? 1
        : s.recurrence_type === 'Quarterly'
        ? 3
        : s.recurrence_type === 'SemiAnnual'
        ? 6
        : 12
    const d = new Date(s.next_due_date)
    d.setMonth(d.getMonth() + months)
    await D.prepare(`UPDATE maintenance_schedules SET next_due_date=? WHERE id=?`)
      .bind(d.toISOString().slice(0, 10), s.id)
      .run()
    created++
  }
  await audit(c, 'Job', null, 'Update', { job: 'generate-schedules', created })
  return c.json({ ok: true, created })
})

/* warranty expiry alerts */
misc.post('/jobs/warranty-alerts', requireRole('Admin'), async (c) => {
  const D = c.env.DB
  const { results } = await D.prepare(
    `SELECT a.id, a.asset_tag, a.name, a.warranty_expiry_date, a.company_id
       FROM assets a WHERE a.is_deleted=0 AND a.warranty_expiry_date IS NOT NULL
        AND a.warranty_expiry_date BETWEEN date('now') AND date('now','+30 day')`
  ).all<any>()
  let sent = 0
  for (const a of results || []) {
    const { results: mgrs } = await D.prepare(
      `SELECT id FROM users WHERE is_deleted=0 AND is_active=1
        AND (role='Admin' OR (role='CompanyManager' AND company_id=?))`
    )
      .bind(a.company_id)
      .all<any>()
    for (const m of mgrs || []) {
      await notify(
        D,
        m.id,
        'انتهاء ضمان قريب',
        `ضمان ${a.name} (${a.asset_tag}) ينتهي بتاريخ ${a.warranty_expiry_date}`,
        'Warranty',
        `#/assets/${a.id}`
      )
      sent++
    }
  }
  await audit(c, 'Job', null, 'Update', { job: 'warranty-alerts', sent })
  return c.json({ ok: true, sent })
})

/* ==================================================================
   SYSTEM SETTINGS
   ================================================================== */
misc.get('/settings', requireRole('Admin'), async (c) => {
  const { results } = await c.env.DB.prepare(
    `SELECT * FROM system_settings ORDER BY id`
  ).all()
  return c.json({ items: results || [] })
})

misc.put('/settings', requireRole('Admin'), async (c) => {
  const b = await c.req.json().catch(() => ({}))
  const items: any[] = Array.isArray(b.items) ? b.items : []
  for (const s of items)
    await c.env.DB.prepare(`UPDATE system_settings SET setting_value=? WHERE setting_key=?`)
      .bind(String(s.setting_value ?? ''), s.setting_key)
      .run()
  await audit(c, 'SystemSettings', null, 'Update', b)
  return c.json({ ok: true })
})

export default misc
