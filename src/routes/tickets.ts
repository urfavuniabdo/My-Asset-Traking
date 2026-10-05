import { Hono } from 'hono'
import {
  type Env,
  requireRole,
  companyScope,
  audit,
  notify,
  nextTicketNumber,
  toInt,
  toNum,
  nn,
  nowSql,
  addHoursSql
} from '../lib'

const tickets = new Hono<Env>()

const STATUSES = [
  'Open',
  'Assigned',
  'InProgress',
  'WaitingParts',
  'Resolved',
  'Closed',
  'Cancelled'
]
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']

/** Access check on a ticket for the current user. Returns row or null. */
async function loadTicket(c: any, id: number) {
  const u = c.get('user')
  const cid = companyScope(u)
  const row = await c.env.DB.prepare(
    `SELECT t.*, a.name AS asset_name, a.asset_tag, a.status AS asset_status,
            rq.full_name AS requester_name, rq.email AS requester_email,
            tech.full_name AS technician_name,
            co.name AS company_name, sp.name AS sla_name,
            sp.response_time_hours, sp.resolution_time_hours,
            l.name AS location_name
     FROM maintenance_tickets t
     JOIN assets a ON a.id=t.asset_id
     LEFT JOIN users rq ON rq.id=t.requester_user_id
     LEFT JOIN users tech ON tech.id=t.assigned_technician_id
     LEFT JOIN companies co ON co.id=t.company_id
     LEFT JOIN sla_policies sp ON sp.id=t.sla_policy_id
     LEFT JOIN locations l ON l.id=a.location_id
     WHERE t.id=?`
  )
    .bind(id)
    .first<any>()
  if (!row) return null
  if (cid !== null && row.company_id !== cid) return null
  if (u.role === 'Employee' && row.requester_user_id !== u.id) return null
  if (u.role === 'Technician' && row.assigned_technician_id !== u.id && row.status !== 'Open')
    return null
  return row
}

/* ==================== LIST ==================== */
tickets.get('/', async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const q = (c.req.query('q') || '').trim()
  const status = c.req.query('status') || ''
  const priority = c.req.query('priority') || ''
  const techId = toInt(c.req.query('technician_id'))
  const assetId = toInt(c.req.query('asset_id'))
  const breached = c.req.query('breached')
  const page = Math.max(1, toInt(c.req.query('page')) ?? 1)
  const size = Math.min(100, Math.max(5, toInt(c.req.query('size')) ?? 25))

  const params: any[] = []
  let where = 'WHERE 1=1'
  if (cid !== null) {
    where += ' AND t.company_id=?'
    params.push(cid)
  }
  if (u.role === 'Employee') {
    where += ' AND t.requester_user_id=?'
    params.push(u.id)
  } else if (u.role === 'Technician') {
    where += ' AND (t.assigned_technician_id=? OR t.status=?)'
    params.push(u.id, 'Open')
  }
  if (q) {
    where += ' AND (t.ticket_number LIKE ? OR t.issue_description LIKE ? OR a.name LIKE ? OR a.asset_tag LIKE ?)'
    const like = `%${q}%`
    params.push(like, like, like, like)
  }
  if (status) {
    where += ' AND t.status=?'
    params.push(status)
  }
  if (priority) {
    where += ' AND t.priority=?'
    params.push(priority)
  }
  if (techId) {
    where += ' AND t.assigned_technician_id=?'
    params.push(techId)
  }
  if (assetId) {
    where += ' AND t.asset_id=?'
    params.push(assetId)
  }
  if (breached === '1') where += ' AND t.sla_breached=1'

  const totalRow = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM maintenance_tickets t JOIN assets a ON a.id=t.asset_id ${where}`
  )
    .bind(...params)
    .first<{ n: number }>()

  const { results } = await c.env.DB.prepare(
    `SELECT t.*, a.name AS asset_name, a.asset_tag,
            rq.full_name AS requester_name, tech.full_name AS technician_name,
            co.name AS company_name, sp.name AS sla_name
     FROM maintenance_tickets t
     JOIN assets a ON a.id=t.asset_id
     LEFT JOIN users rq ON rq.id=t.requester_user_id
     LEFT JOIN users tech ON tech.id=t.assigned_technician_id
     LEFT JOIN companies co ON co.id=t.company_id
     LEFT JOIN sla_policies sp ON sp.id=t.sla_policy_id
     ${where} ORDER BY
       CASE t.priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END,
       t.id DESC
     LIMIT ? OFFSET ?`
  )
    .bind(...params, size, (page - 1) * size)
    .all()

  return c.json({
    items: results || [],
    total: totalRow?.n ?? 0,
    page,
    size,
    pages: Math.ceil((totalRow?.n ?? 0) / size)
  })
})

/* ==================== TECHNICIANS (for assignment dropdown) ==================== */
tickets.get('/technicians', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const params: any[] = []
  let where = `WHERE x.is_deleted=0 AND x.is_active=1 AND x.role='Technician'`
  if (cid !== null) {
    where += ' AND x.company_id=?'
    params.push(cid)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT x.id, x.full_name, x.email, x.job_title, co.name AS company_name,
       (SELECT COUNT(*) FROM maintenance_tickets t WHERE t.assigned_technician_id=x.id
          AND t.status NOT IN ('Closed','Cancelled')) AS open_tickets
     FROM users x LEFT JOIN companies co ON co.id=x.company_id
     ${where} ORDER BY x.full_name`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

/* ==================== SLA POLICIES ==================== */
tickets.get('/sla/policies', async (c) => {
  const { results } = await c.env.DB.prepare(
    `SELECT sp.*, (SELECT COUNT(*) FROM maintenance_tickets t WHERE t.sla_policy_id=sp.id) AS tickets_count
     FROM sla_policies sp ORDER BY
       CASE sp.priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END`
  ).all()
  return c.json({ items: results || [] })
})

tickets.put('/sla/policies/:id', requireRole('Admin'), async (c) => {
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const resp = toInt(b.response_time_hours)
  const reso = toInt(b.resolution_time_hours)
  if (!resp || !reso || resp < 1 || reso < 1)
    return c.json({ error: 'أزمنة الاستجابة والحل يجب أن تكون أرقاماً صحيحة أكبر من صفر' }, 400)
  if (resp > reso) return c.json({ error: 'زمن الاستجابة يجب أن يكون أقل من أو يساوي زمن الحل' }, 400)
  await c.env.DB.prepare(
    `UPDATE sla_policies SET name=?, response_time_hours=?, resolution_time_hours=?, is_active=? WHERE id=?`
  )
    .bind(b.name, resp, reso, b.is_active === false ? 0 : 1, id)
    .run()
  await audit(c, 'SlaPolicy', id, 'Update', b)
  return c.json({ ok: true })
})

/* ==================== DETAIL ==================== */
tickets.get('/:id', async (c) => {
  const id = toInt(c.req.param('id'))
  const item = await loadTicket(c, id!)
  if (!item) return c.json({ error: 'غير موجود' }, 404)
  const u = c.get('user')

  const internalFilter = u.role === 'Employee' ? ' AND tc.is_internal=0' : ''
  const comments = await c.env.DB.prepare(
    `SELECT tc.*, x.full_name AS user_name, x.role AS user_role
     FROM ticket_comments tc LEFT JOIN users x ON x.id=tc.user_id
     WHERE tc.ticket_id=? ${internalFilter} ORDER BY tc.id ASC`
  )
    .bind(id)
    .all()

  const logs = await c.env.DB.prepare(
    `SELECT tl.*, x.full_name AS user_name FROM ticket_logs tl
     LEFT JOIN users x ON x.id=tl.action_user_id
     WHERE tl.ticket_id=? ORDER BY tl.id ASC`
  )
    .bind(id)
    .all()

  const parts = await c.env.DB.prepare(`SELECT * FROM ticket_parts WHERE ticket_id=? ORDER BY id`)
    .bind(id)
    .all()

  return c.json({
    item,
    comments: comments.results || [],
    logs: logs.results || [],
    parts: parts.results || []
  })
})

/* ==================== CREATE ==================== */
tickets.post('/', async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const assetId = toInt(b.asset_id)
  if (!assetId || !b.issue_description)
    return c.json({ error: 'الأصل ووصف المشكلة مطلوبان' }, 400)
  if (u.role === 'Technician')
    return c.json({ error: 'الفنيون لا يفتحون تذاكر — يتم إسنادها إليهم' }, 403)

  const cid = companyScope(u)
  const asset = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(assetId)
    .first<any>()
  if (!asset) return c.json({ error: 'الأصل غير موجود' }, 404)
  if (cid !== null && asset.company_id !== cid) return c.json({ error: 'غير موجود' }, 404)
  if (u.role === 'Employee' && asset.current_custody_user_id !== u.id)
    return c.json({ error: 'يمكنك فتح تذاكر لأصول عهدتك فقط' }, 403)
  if (['Disposed'].includes(asset.status))
    return c.json({ error: 'لا يمكن فتح تذكرة لأصل مكهّن' }, 400)

  const priority = PRIORITIES.includes(b.priority) ? b.priority : 'Medium'
  const sla = await c.env.DB.prepare(
    `SELECT * FROM sla_policies WHERE priority=? AND is_active=1 LIMIT 1`
  )
    .bind(priority)
    .first<any>()

  const number = await nextTicketNumber(c.env.DB)
  const requesterId = u.role === 'Employee' ? u.id : toInt(b.requester_user_id) ?? u.id
  const source = ['Manual', 'QRScan', 'Preventive'].includes(b.source) ? b.source : 'Manual'

  const r = await c.env.DB.prepare(
    `INSERT INTO maintenance_tickets (ticket_number, asset_id, company_id, requester_user_id,
        sla_policy_id, status, priority, source, issue_description,
        sla_response_due_at, sla_resolution_due_at)
     VALUES (?,?,?,?,?,'Open',?,?,?,?,?)`
  )
    .bind(
      number,
      assetId,
      asset.company_id,
      requesterId,
      sla?.id ?? null,
      priority,
      source,
      b.issue_description,
      sla ? addHoursSql(sla.response_time_hours) : null,
      sla ? addHoursSql(sla.resolution_time_hours) : null
    )
    .run()

  const id = r.meta.last_row_id
  await c.env.DB.prepare(
    `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, new_value, notes)
     VALUES (?,?,'Created','Open',?)`
  )
    .bind(id, u.id, `تم فتح التذكرة (${source === 'QRScan' ? 'مسح QR' : 'يدوي'})`)
    .run()

  // asset goes under maintenance for high/critical
  if (['High', 'Critical'].includes(priority) && asset.status === 'Active')
    await c.env.DB.prepare(`UPDATE assets SET status='UnderMaintenance' WHERE id=?`)
      .bind(assetId)
      .run()

  // notify managers/admin
  const { results } = await c.env.DB.prepare(
    `SELECT id FROM users WHERE is_deleted=0 AND is_active=1
      AND (role='Admin' OR (role='CompanyManager' AND company_id=?))`
  )
    .bind(asset.company_id)
    .all<any>()
  for (const m of results || [])
    await notify(
      c.env.DB,
      m.id,
      'تذكرة صيانة جديدة',
      `${number} — ${asset.name}: ${String(b.issue_description).slice(0, 60)}`,
      'Ticket',
      `#/tickets/${id}`
    )

  await audit(c, 'Ticket', id, 'Create', { ...b, ticket_number: number })
  return c.json({ ok: true, id, ticket_number: number })
})

/* ==================== ASSIGN TECHNICIAN ==================== */
tickets.post('/:id/assign', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const techId = toInt(b.technician_id)
  if (!techId) return c.json({ error: 'الفني مطلوب' }, 400)

  const t = await loadTicket(c, id!)
  if (!t) return c.json({ error: 'غير موجود' }, 404)
  if (['Closed', 'Cancelled'].includes(t.status))
    return c.json({ error: 'لا يمكن تعيين فني لتذكرة مغلقة أو ملغاة' }, 400)

  const tech = await c.env.DB.prepare(
    `SELECT * FROM users WHERE id=? AND role='Technician' AND is_deleted=0 AND is_active=1`
  )
    .bind(techId)
    .first<any>()
  if (!tech) return c.json({ error: 'الفني غير موجود' }, 404)

  const oldStatus = t.status
  const newStatus = oldStatus === 'Open' ? 'Assigned' : oldStatus
  await c.env.DB.prepare(
    `UPDATE maintenance_tickets SET assigned_technician_id=?, status=?, assigned_at=COALESCE(assigned_at, datetime('now'))
     WHERE id=?`
  )
    .bind(techId, newStatus, id)
    .run()

  await c.env.DB.prepare(
    `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, old_value, new_value, notes)
     VALUES (?,?,'Assigned',?,?,?)`
  )
    .bind(id, u.id, t.technician_name || oldStatus, tech.full_name, nn(b.notes) || 'تعيين فني')
    .run()

  await notify(
    c.env.DB,
    techId,
    'تذكرة جديدة مسندة إليك',
    `${t.ticket_number} — ${t.asset_name}`,
    'Ticket',
    `#/tickets/${id}`
  )
  await notify(
    c.env.DB,
    t.requester_user_id,
    'تحديث على تذكرتك',
    `${t.ticket_number} — تم تعيين الفني ${tech.full_name}`,
    'Ticket',
    `#/tickets/${id}`
  )
  await audit(c, 'Ticket', id, 'Update', { assigned_technician_id: techId })
  return c.json({ ok: true })
})

/* ==================== CHANGE STATUS ==================== */
tickets.post('/:id/status', async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const status = b.status
  if (!STATUSES.includes(status)) return c.json({ error: 'حالة غير صحيحة' }, 400)

  const t = await loadTicket(c, id!)
  if (!t) return c.json({ error: 'غير موجود' }, 404)

  // permissions per plan §9
  const isManager = u.role === 'Admin' || u.role === 'CompanyManager'
  const isTech = u.role === 'Technician' && t.assigned_technician_id === u.id
  if (status === 'Cancelled' && !isManager)
    return c.json({ error: 'إلغاء التذكرة يحتاج صلاحية إدارية' }, 403)
  if (status === 'Closed' && !isManager)
    return c.json({ error: 'إغلاق التذكرة يحتاج صلاحية إدارية' }, 403)
  if (['InProgress', 'WaitingParts', 'Resolved'].includes(status) && !isTech && !isManager)
    return c.json({ error: 'ليس لديك صلاحية لتغيير حالة هذه التذكرة' }, 403)
  if (['Closed', 'Cancelled'].includes(t.status))
    return c.json({ error: 'التذكرة مغلقة/ملغاة — لا يمكن تغيير حالتها' }, 400)

  const sets: string[] = ['status=?']
  const vals: any[] = [status]

  // first response time
  if (!t.first_response_at && ['InProgress', 'WaitingParts', 'Resolved'].includes(status)) {
    sets.push(`first_response_at=datetime('now')`)
  }
  if (status === 'Resolved') {
    sets.push(`resolved_at=datetime('now')`)
    if (b.resolution_report) {
      sets.push('resolution_report=?')
      vals.push(b.resolution_report)
    }
    const labor = toNum(b.labor_cost)
    if (labor) {
      sets.push('labor_cost=?')
      vals.push(labor)
    }
  }
  if (status === 'Closed') sets.push(`closed_at=datetime('now')`)

  // SLA breach evaluation
  const now = new Date()
  if (
    status === 'Resolved' &&
    t.sla_resolution_due_at &&
    now > new Date(t.sla_resolution_due_at.replace(' ', 'T') + 'Z')
  ) {
    sets.push('sla_breached=1')
  }

  vals.push(id)
  await c.env.DB.prepare(`UPDATE maintenance_tickets SET ${sets.join(', ')} WHERE id=?`)
    .bind(...vals)
    .run()

  // recompute total cost
  await c.env.DB.prepare(
    `UPDATE maintenance_tickets SET
        parts_cost=(SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?),
        total_cost=labor_cost + (SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?)
     WHERE id=?`
  )
    .bind(id, id, id)
    .run()

  await c.env.DB.prepare(
    `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, old_value, new_value, notes)
     VALUES (?,?,'StatusChanged',?,?,?)`
  )
    .bind(id, u.id, t.status, status, nn(b.notes) || nn(b.resolution_report))
    .run()

  // asset status side-effects
  if (status === 'Resolved' || status === 'Closed') {
    if (b.asset_unrepairable) {
      await c.env.DB.prepare(`UPDATE assets SET status='Damaged' WHERE id=?`).bind(t.asset_id).run()
    } else {
      const others = await c.env.DB.prepare(
        `SELECT COUNT(*) AS n FROM maintenance_tickets
          WHERE asset_id=? AND id<>? AND status NOT IN ('Closed','Cancelled','Resolved')`
      )
        .bind(t.asset_id, id)
        .first<{ n: number }>()
      if ((others?.n ?? 0) === 0)
        await c.env.DB.prepare(
          `UPDATE assets SET status='Active' WHERE id=? AND status='UnderMaintenance'`
        )
          .bind(t.asset_id)
          .run()
    }
  }
  if (status === 'InProgress')
    await c.env.DB.prepare(
      `UPDATE assets SET status='UnderMaintenance' WHERE id=? AND status='Active'`
    )
      .bind(t.asset_id)
      .run()

  const statusAr: Record<string, string> = {
    Open: 'مفتوحة',
    Assigned: 'مُسندة',
    InProgress: 'جاري العمل',
    WaitingParts: 'بانتظار قطع غيار',
    Resolved: 'تم الحل',
    Closed: 'مغلقة',
    Cancelled: 'ملغاة'
  }
  await notify(
    c.env.DB,
    t.requester_user_id,
    'تحديث حالة تذكرتك',
    `${t.ticket_number} — الحالة الآن: ${statusAr[status]}`,
    'Ticket',
    `#/tickets/${id}`
  )
  if (t.assigned_technician_id && t.assigned_technician_id !== u.id)
    await notify(
      c.env.DB,
      t.assigned_technician_id,
      'تحديث تذكرة',
      `${t.ticket_number} — الحالة: ${statusAr[status]}`,
      'Ticket',
      `#/tickets/${id}`
    )

  await audit(c, 'Ticket', id, 'Update', { status: { old: t.status, new: status } })
  return c.json({ ok: true })
})

/* ==================== COMMENTS ==================== */
tickets.post('/:id/comments', async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  if (!b.comment_text) return c.json({ error: 'نص التعليق مطلوب' }, 400)

  const t = await loadTicket(c, id!)
  if (!t) return c.json({ error: 'غير موجود' }, 404)

  const isInternal = !!b.is_internal
  if (isInternal && u.role === 'Employee')
    return c.json({ error: 'لا يمكنك إضافة ملاحظات داخلية' }, 403)

  const r = await c.env.DB.prepare(
    `INSERT INTO ticket_comments (ticket_id, user_id, comment_text, is_internal) VALUES (?,?,?,?)`
  )
    .bind(id, u.id, b.comment_text, isInternal ? 1 : 0)
    .run()

  if (!isInternal) {
    const targets = new Set<number>()
    if (t.requester_user_id) targets.add(t.requester_user_id)
    if (t.assigned_technician_id) targets.add(t.assigned_technician_id)
    targets.delete(u.id)
    for (const uid of targets)
      await notify(
        c.env.DB,
        uid,
        'تعليق جديد على تذكرة',
        `${t.ticket_number}: ${String(b.comment_text).slice(0, 60)}`,
        'Ticket',
        `#/tickets/${id}`
      )
  }
  return c.json({ ok: true, id: r.meta.last_row_id })
})

/* ==================== PARTS ==================== */
tickets.post('/:id/parts', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  if (!b.part_name) return c.json({ error: 'اسم قطعة الغيار مطلوب' }, 400)

  const t = await loadTicket(c, id!)
  if (!t) return c.json({ error: 'غير موجود' }, 404)
  if (u.role === 'Technician' && t.assigned_technician_id !== u.id)
    return c.json({ error: 'غير موجود' }, 404)

  const qty = toInt(b.quantity) ?? 1
  const unit = toNum(b.unit_cost)
  const total = qty * unit

  const r = await c.env.DB.prepare(
    `INSERT INTO ticket_parts (ticket_id, part_name, quantity, unit_cost, total_cost, supplier_name)
     VALUES (?,?,?,?,?,?)`
  )
    .bind(id, b.part_name, qty, unit, total, nn(b.supplier_name))
    .run()

  await c.env.DB.prepare(
    `UPDATE maintenance_tickets SET
        parts_cost=(SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?),
        total_cost=labor_cost + (SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?)
     WHERE id=?`
  )
    .bind(id, id, id)
    .run()

  await c.env.DB.prepare(
    `INSERT INTO ticket_logs (ticket_id, action_user_id, action_type, new_value, notes)
     VALUES (?,?,'CostAdded',?,?)`
  )
    .bind(id, u.id, String(total), `إضافة قطعة: ${b.part_name} × ${qty}`)
    .run()

  return c.json({ ok: true, id: r.meta.last_row_id })
})

tickets.delete('/:id/parts/:partId', requireRole('Admin', 'CompanyManager', 'Technician'), async (c) => {
  const id = toInt(c.req.param('id'))
  const partId = toInt(c.req.param('partId'))
  const t = await loadTicket(c, id!)
  if (!t) return c.json({ error: 'غير موجود' }, 404)
  await c.env.DB.prepare(`DELETE FROM ticket_parts WHERE id=? AND ticket_id=?`)
    .bind(partId, id)
    .run()
  await c.env.DB.prepare(
    `UPDATE maintenance_tickets SET
        parts_cost=(SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?),
        total_cost=labor_cost + (SELECT COALESCE(SUM(total_cost),0) FROM ticket_parts WHERE ticket_id=?)
     WHERE id=?`
  )
    .bind(id, id, id)
    .run()
  return c.json({ ok: true })
})

export default tickets
