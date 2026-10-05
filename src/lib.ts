import type { Context, Next } from 'hono'
import { getCookie } from 'hono/cookie'

export type Bindings = { DB: D1Database }
export type Role = 'Admin' | 'CompanyManager' | 'Technician' | 'Employee'

export interface SessionUser {
  id: number
  company_id: number | null
  department_id: number | null
  full_name: string
  email: string
  role: Role
  job_title: string | null
}

export type Env = { Bindings: Bindings; Variables: { user: SessionUser } }

export const SALT = 'ats_salt_2026'
export const SESSION_COOKIE = 'ats_session'

/* ---------------- hashing (Web Crypto — Workers safe) ---------------- */
export async function sha256(text: string): Promise<string> {
  const data = new TextEncoder().encode(text)
  const buf = await crypto.subtle.digest('SHA-256', data)
  return [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, '0')).join('')
}
export const hashPassword = (pw: string) => sha256(pw + SALT)

/* ---------------- date helpers ---------------- */
export const nowSql = () => new Date().toISOString().slice(0, 19).replace('T', ' ')
export const addHoursSql = (h: number) =>
  new Date(Date.now() + h * 3600_000).toISOString().slice(0, 19).replace('T', ' ')
export const addDaysSql = (d: number) =>
  new Date(Date.now() + d * 86400_000).toISOString().slice(0, 10)

/* ---------------- auth middleware ---------------- */
export async function authMiddleware(c: Context<Env>, next: Next) {
  const token = getCookie(c, SESSION_COOKIE)
  if (!token) return c.json({ error: 'غير مصرح — يجب تسجيل الدخول' }, 401)

  const row = await c.env.DB.prepare(
    `SELECT u.id, u.company_id, u.department_id, u.full_name, u.email, u.role, u.job_title
       FROM sessions s JOIN users u ON u.id = s.user_id
      WHERE s.token = ? AND s.expires_at > datetime('now')
        AND u.is_active = 1 AND u.is_deleted = 0`
  )
    .bind(token)
    .first<SessionUser>()

  if (!row) return c.json({ error: 'انتهت الجلسة — يرجى تسجيل الدخول مرة أخرى' }, 401)
  c.set('user', row)
  await next()
}

/* ---------------- RBAC ---------------- */
export const requireRole = (...roles: Role[]) => async (c: Context<Env>, next: Next) => {
  const u = c.get('user')
  if (!u || !roles.includes(u.role))
    return c.json({ error: 'ليس لديك صلاحية للقيام بهذا الإجراء' }, 403)
  await next()
}

/** Admin sees everything; others are limited to their own company. */
export const companyScope = (u: SessionUser) => (u.role === 'Admin' ? null : u.company_id)

/**
 * Builds a SQL fragment + params enforcing company isolation.
 * Returns e.g. { sql: ' AND a.company_id = ?', params: [3] }
 */
export function scopeClause(u: SessionUser, col = 'company_id') {
  const cid = companyScope(u)
  return cid === null ? { sql: '', params: [] as any[] } : { sql: ` AND ${col} = ?`, params: [cid] }
}

/* ---------------- audit log ---------------- */
export async function audit(
  c: Context<Env>,
  entity: string,
  entityId: string | number | null,
  action: string,
  changes?: any
) {
  const u = c.get('user')
  const ip =
    c.req.header('cf-connecting-ip') || c.req.header('x-forwarded-for') || '127.0.0.1'
  try {
    await c.env.DB.prepare(
      `INSERT INTO audit_logs (user_id, entity_name, entity_id, action, changes_json, ip_address)
       VALUES (?, ?, ?, ?, ?, ?)`
    )
      .bind(
        u?.id ?? null,
        entity,
        entityId === null ? null : String(entityId),
        action,
        changes ? JSON.stringify(changes) : null,
        ip
      )
      .run()
  } catch (_) {
    /* audit must never break the request */
  }
}

/* ---------------- notifications ---------------- */
export async function notify(
  db: D1Database,
  userId: number | null | undefined,
  title: string,
  message: string,
  type = 'System',
  targetUrl: string | null = null
) {
  if (!userId) return
  try {
    await db
      .prepare(
        `INSERT INTO notifications (user_id, title, message, type, target_url) VALUES (?,?,?,?,?)`
      )
      .bind(userId, title, message, type, targetUrl)
      .run()
  } catch (_) {}
}

/* ---------------- sequence generators ---------------- */
export async function nextAssetTag(db: D1Database): Promise<string> {
  const year = new Date().getFullYear()
  const row = await db
    .prepare(`SELECT asset_tag FROM assets WHERE asset_tag LIKE ? ORDER BY id DESC LIMIT 1`)
    .bind(`AST-${year}-%`)
    .first<{ asset_tag: string }>()
  let n = 1
  if (row?.asset_tag) n = parseInt(row.asset_tag.split('-')[2], 10) + 1
  // guard against gaps / collisions
  for (let i = 0; i < 50; i++) {
    const tag = `AST-${year}-${String(n).padStart(5, '0')}`
    const dup = await db.prepare(`SELECT 1 FROM assets WHERE asset_tag = ?`).bind(tag).first()
    if (!dup) return tag
    n++
  }
  return `AST-${year}-${Date.now().toString().slice(-5)}`
}

export async function nextTicketNumber(db: D1Database): Promise<string> {
  const year = new Date().getFullYear()
  const row = await db
    .prepare(
      `SELECT ticket_number FROM maintenance_tickets WHERE ticket_number LIKE ? ORDER BY id DESC LIMIT 1`
    )
    .bind(`TKT-${year}-%`)
    .first<{ ticket_number: string }>()
  let n = 1
  if (row?.ticket_number) n = parseInt(row.ticket_number.split('-')[2], 10) + 1
  for (let i = 0; i < 50; i++) {
    const num = `TKT-${year}-${String(n).padStart(5, '0')}`
    const dup = await db
      .prepare(`SELECT 1 FROM maintenance_tickets WHERE ticket_number = ?`)
      .bind(num)
      .first()
    if (!dup) return num
    n++
  }
  return `TKT-${year}-${Date.now().toString().slice(-5)}`
}

/* ---------------- depreciation ---------------- */
/** Straight-line monthly depreciation amount. */
export function monthlyDepreciation(cost: number, salvage: number, years: number) {
  if (!cost || !years || years <= 0) return 0
  return (cost - (salvage || 0)) / (years * 12)
}

/* ---------------- misc ---------------- */
export const toInt = (v: any): number | null => {
  if (v === undefined || v === null || v === '') return null
  const n = parseInt(String(v), 10)
  return Number.isNaN(n) ? null : n
}
export const toNum = (v: any): number => {
  if (v === undefined || v === null || v === '') return 0
  const n = Number(v)
  return Number.isNaN(n) ? 0 : n
}
export const nn = (v: any) => (v === undefined || v === '' ? null : v)
