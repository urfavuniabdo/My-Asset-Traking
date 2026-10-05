import { Hono } from 'hono'
import { type Env, requireRole, companyScope, audit, notify, toInt, nn, nowSql } from '../lib'

const custody = new Hono<Env>()

/* ==================== MY CUSTODY (employee/self) ==================== */
custody.get('/my', async (c) => {
  const u = c.get('user')

  // Assets currently in my custody (raw asset rows + joins)
  const current = await c.env.DB.prepare(
    `SELECT a.*, ct.name AS category_name, l.name AS location_name, co.name AS company_name
     FROM assets a
     LEFT JOIN categories ct ON ct.id=a.category_id
     LEFT JOIN locations l ON l.id=a.location_id
     LEFT JOIN companies co ON co.id=a.company_id
     WHERE a.current_custody_user_id=? AND a.is_deleted=0
     ORDER BY a.id DESC`
  )
    .bind(u.id)
    .all()

  // Pending acceptance handshakes for me
  const pending = await c.env.DB.prepare(
    `SELECT cl.*, a.asset_tag, a.name AS asset_name, ab.full_name AS assigned_by_name
     FROM custody_logs cl
     JOIN assets a ON a.id=cl.asset_id
     LEFT JOIN users ab ON ab.id=cl.assigned_by_user_id
     WHERE cl.new_user_id=? AND cl.acceptance_status='Pending' AND cl.action_type IN ('Assign','Transfer')
     ORDER BY cl.id DESC`
  )
    .bind(u.id)
    .all()

  // My full custody history
  const history = await c.env.DB.prepare(
    `SELECT cl.*, a.asset_tag, a.name AS asset_name,
            pu.full_name AS previous_user_name, nu.full_name AS new_user_name,
            ab.full_name AS assigned_by_name
     FROM custody_logs cl
     JOIN assets a ON a.id=cl.asset_id
     LEFT JOIN users pu ON pu.id=cl.previous_user_id
     LEFT JOIN users nu ON nu.id=cl.new_user_id
     LEFT JOIN users ab ON ab.id=cl.assigned_by_user_id
     WHERE cl.new_user_id=? OR cl.previous_user_id=?
     ORDER BY cl.id DESC LIMIT 100`
  )
    .bind(u.id, u.id)
    .all()

  return c.json({
    current: current.results || [],
    pending: pending.results || [],
    history: history.results || []
  })
})

/* ==================== ALL CUSTODY LOGS (managerial) ==================== */
custody.get('/logs', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const cid = companyScope(u)
  const assetId = toInt(c.req.query('asset_id'))
  const userId = toInt(c.req.query('user_id'))
  const params: any[] = []
  let where = 'WHERE 1=1'
  if (cid !== null) {
    where += ' AND a.company_id=?'
    params.push(cid)
  }
  if (assetId) {
    where += ' AND cl.asset_id=?'
    params.push(assetId)
  }
  if (userId) {
    where += ' AND (cl.new_user_id=? OR cl.previous_user_id=?)'
    params.push(userId, userId)
  }
  const { results } = await c.env.DB.prepare(
    `SELECT cl.*, a.asset_tag, a.name AS asset_name,
            pu.full_name AS previous_user_name, nu.full_name AS new_user_name,
            ab.full_name AS assigned_by_name
     FROM custody_logs cl
     JOIN assets a ON a.id=cl.asset_id
     LEFT JOIN users pu ON pu.id=cl.previous_user_id
     LEFT JOIN users nu ON nu.id=cl.new_user_id
     LEFT JOIN users ab ON ab.id=cl.assigned_by_user_id
     ${where} ORDER BY cl.id DESC LIMIT 200`
  )
    .bind(...params)
    .all()
  return c.json({ items: results || [] })
})

/* ==================== ASSIGN / TRANSFER CUSTODY ==================== */
custody.post('/assign', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const assetId = toInt(b.asset_id)
  const newUserId = toInt(b.new_user_id)
  if (!assetId || !newUserId) return c.json({ error: 'الأصل والمستخدم مطلوبان' }, 400)

  const cid = companyScope(u)
  const asset = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(assetId)
    .first<any>()
  if (!asset || (cid !== null && asset.company_id !== cid))
    return c.json({ error: 'غير موجود' }, 404)
  if (['Disposed', 'Lost'].includes(asset.status))
    return c.json({ error: 'لا يمكن تسليم أصل مكهّن أو مفقود' }, 400)

  const target = await c.env.DB.prepare(
    `SELECT * FROM users WHERE id=? AND is_deleted=0 AND is_active=1`
  )
    .bind(newUserId)
    .first<any>()
  if (!target) return c.json({ error: 'المستخدم غير موجود' }, 404)
  if (cid !== null && target.company_id !== cid)
    return c.json({ error: 'لا يمكن تسليم عهدة لموظف من شركة أخرى' }, 400)
  if (asset.current_custody_user_id === newUserId)
    return c.json({ error: 'الأصل بعهدة هذا الموظف بالفعل' }, 400)

  const actionType = asset.current_custody_user_id ? 'Transfer' : 'Assign'
  const r = await c.env.DB.prepare(
    `INSERT INTO custody_logs (asset_id, previous_user_id, new_user_id, action_type,
        acceptance_status, transfer_date, reason, assigned_by_user_id)
     VALUES (?,?,?,?,'Pending',?,?,?)`
  )
    .bind(
      assetId,
      asset.current_custody_user_id,
      newUserId,
      actionType,
      nowSql(),
      nn(b.reason) || 'إسناد عهدة',
      u.id
    )
    .run()

  await notify(
    c.env.DB,
    newUserId,
    'إقرار استلام عهدة',
    `لديك عهدة بانتظار الإقرار: ${asset.name} (${asset.asset_tag})`,
    'Custody',
    '#/custody'
  )
  await audit(c, 'CustodyLog', r.meta.last_row_id, 'Create', {
    asset_id: assetId,
    new_user_id: newUserId,
    action: actionType
  })
  return c.json({ ok: true, id: r.meta.last_row_id, message: 'تم الإرسال — بانتظار إقرار الموظف' })
})

/* ==================== EMPLOYEE RESPONDS (accept / reject) ==================== */
custody.post('/:id/respond', async (c) => {
  const u = c.get('user')
  const id = toInt(c.req.param('id'))
  const b = await c.req.json().catch(() => ({}))
  const accept = !!b.accept

  const log = await c.env.DB.prepare(
    `SELECT cl.*, a.name AS asset_name, a.asset_tag FROM custody_logs cl
      JOIN assets a ON a.id=cl.asset_id WHERE cl.id=?`
  )
    .bind(id)
    .first<any>()
  if (!log) return c.json({ error: 'غير موجود' }, 404)
  if (log.new_user_id !== u.id) return c.json({ error: 'غير موجود' }, 404)
  if (log.acceptance_status !== 'Pending') return c.json({ error: 'تم الرد على هذا الطلب مسبقاً' }, 400)

  if (accept) {
    await c.env.DB.prepare(
      `UPDATE custody_logs SET acceptance_status='Accepted', accepted_at=datetime('now') WHERE id=?`
    )
      .bind(id)
      .run()
    await c.env.DB.prepare(`UPDATE assets SET current_custody_user_id=? WHERE id=?`)
      .bind(u.id, log.asset_id)
      .run()
    await notify(
      c.env.DB,
      log.assigned_by_user_id,
      'تم إقرار استلام العهدة',
      `${u.full_name} أقرّ باستلام: ${log.asset_name} (${log.asset_tag})`,
      'Custody',
      `#/assets/${log.asset_id}`
    )
  } else {
    await c.env.DB.prepare(
      `UPDATE custody_logs SET acceptance_status='Rejected', rejection_reason=? WHERE id=?`
    )
      .bind(nn(b.note) || 'رفض بدون سبب محدد', id)
      .run()
    await notify(
      c.env.DB,
      log.assigned_by_user_id,
      'تم رفض استلام العهدة',
      `${u.full_name} رفض استلام: ${log.asset_name} — السبب: ${b.note || 'غير محدد'}`,
      'Custody',
      `#/assets/${log.asset_id}`
    )
  }

  await audit(c, 'CustodyLog', id, 'Update', { accept, note: b.note })
  return c.json({ ok: true })
})

/* ==================== RETURN CUSTODY ==================== */
custody.post('/return', async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const assetId = toInt(b.asset_id)
  if (!assetId) return c.json({ error: 'الأصل مطلوب' }, 400)

  const asset = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(assetId)
    .first<any>()
  if (!asset) return c.json({ error: 'غير موجود' }, 404)

  const isOwner = asset.current_custody_user_id === u.id
  const isManager = u.role === 'Admin' || u.role === 'CompanyManager'
  const cid = companyScope(u)
  if (!isOwner && !isManager) return c.json({ error: 'غير موجود' }, 404)
  if (isManager && cid !== null && asset.company_id !== cid)
    return c.json({ error: 'غير موجود' }, 404)
  if (!asset.current_custody_user_id) return c.json({ error: 'الأصل ليس بعهدة أحد' }, 400)

  const r = await c.env.DB.prepare(
    `INSERT INTO custody_logs (asset_id, previous_user_id, action_type, acceptance_status,
        transfer_date, reason, condition_note, assigned_by_user_id)
     VALUES (?,?,'Return','Accepted',?,?,?,?)`
  )
    .bind(
      assetId,
      asset.current_custody_user_id,
      nowSql(),
      nn(b.reason) || 'إرجاع عهدة',
      nn(b.condition),
      u.id
    )
    .run()

  await c.env.DB.prepare(`UPDATE assets SET current_custody_user_id=NULL WHERE id=?`)
    .bind(assetId)
    .run()

  if (isOwner) {
    // notify company managers + admin
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
        'إرجاع عهدة',
        `${u.full_name} أرجع الأصل: ${asset.name} (${asset.asset_tag})`,
        'Custody',
        `#/assets/${assetId}`
      )
  } else {
    await notify(
      c.env.DB,
      asset.current_custody_user_id,
      'تم إنهاء عهدتك',
      `تم إرجاع الأصل: ${asset.name} (${asset.asset_tag})`,
      'Custody',
      '#/custody'
    )
  }

  await audit(c, 'CustodyLog', r.meta.last_row_id, 'Create', { asset_id: assetId, action: 'Return' })
  return c.json({ ok: true })
})

/* ==================== TRANSFER LOCATION ==================== */
custody.post('/transfer-location', requireRole('Admin', 'CompanyManager'), async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const assetId = toInt(b.asset_id)
  const newLocationId = toInt(b.new_location_id)
  if (!assetId || !newLocationId) return c.json({ error: 'الأصل والموقع الجديد مطلوبان' }, 400)

  const cid = companyScope(u)
  const asset = await c.env.DB.prepare(`SELECT * FROM assets WHERE id=? AND is_deleted=0`)
    .bind(assetId)
    .first<any>()
  if (!asset || (cid !== null && asset.company_id !== cid))
    return c.json({ error: 'غير موجود' }, 404)
  if (asset.location_id === newLocationId)
    return c.json({ error: 'الأصل في هذا الموقع بالفعل' }, 400)

  const loc = await c.env.DB.prepare(`SELECT * FROM locations WHERE id=? AND is_deleted=0`)
    .bind(newLocationId)
    .first<any>()
  if (!loc) return c.json({ error: 'الموقع غير موجود' }, 404)
  if (loc.company_id !== asset.company_id)
    return c.json({ error: 'لا يمكن نقل الأصل لموقع تابع لشركة أخرى' }, 400)

  await c.env.DB.prepare(
    `INSERT INTO location_logs (asset_id, previous_location_id, new_location_id, transfer_date, reason, moved_by_user_id)
     VALUES (?,?,?,?,?,?)`
  )
    .bind(assetId, asset.location_id, newLocationId, nowSql(), nn(b.reason) || 'نقل موقع', u.id)
    .run()

  await c.env.DB.prepare(`UPDATE assets SET location_id=?, updated_at=datetime('now') WHERE id=?`)
    .bind(newLocationId, assetId)
    .run()

  if (asset.current_custody_user_id)
    await notify(
      c.env.DB,
      asset.current_custody_user_id,
      'نقل موقع أصل بعهدتك',
      `تم نقل ${asset.name} إلى: ${loc.name}`,
      'Custody',
      `#/assets/${assetId}`
    )

  await audit(c, 'LocationLog', assetId, 'Update', {
    from: asset.location_id,
    to: newLocationId,
    reason: b.reason
  })
  return c.json({ ok: true })
})

export default custody
