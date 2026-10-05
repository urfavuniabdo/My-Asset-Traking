import { Hono } from 'hono'
import { setCookie, deleteCookie, getCookie } from 'hono/cookie'
import { type Env, hashPassword, SESSION_COOKIE, authMiddleware, audit } from '../lib'

const auth = new Hono<Env>()

/* POST /api/auth/login */
auth.post('/login', async (c) => {
  const body = await c.req.json().catch(() => ({}))
  const email = String(body.email || '').trim().toLowerCase()
  const password = String(body.password || '')
  if (!email || !password) return c.json({ error: 'البريد الإلكتروني وكلمة المرور مطلوبان' }, 400)

  const hash = await hashPassword(password)
  const user = await c.env.DB.prepare(
    `SELECT id, company_id, department_id, full_name, email, role, job_title
       FROM users
      WHERE lower(email) = ? AND password_hash = ? AND is_active = 1 AND is_deleted = 0`
  )
    .bind(email, hash)
    .first<any>()

  if (!user) return c.json({ error: 'بيانات الدخول غير صحيحة' }, 401)

  const token = crypto.randomUUID() + '-' + crypto.randomUUID()
  const expires = new Date(Date.now() + 7 * 86400_000)
  await c.env.DB.prepare(
    `INSERT INTO sessions (token, user_id, expires_at) VALUES (?, ?, ?)`
  )
    .bind(token, user.id, expires.toISOString().slice(0, 19).replace('T', ' '))
    .run()

  await c.env.DB.prepare(`UPDATE users SET last_login_at = datetime('now') WHERE id = ?`)
    .bind(user.id)
    .run()

  await c.env.DB.prepare(
    `INSERT INTO audit_logs (user_id, entity_name, entity_id, action, ip_address)
     VALUES (?, 'Login', ?, 'Login', ?)`
  )
    .bind(user.id, String(user.id), c.req.header('cf-connecting-ip') || '127.0.0.1')
    .run()

  setCookie(c, SESSION_COOKIE, token, {
    path: '/',
    httpOnly: true,
    sameSite: 'Lax',
    maxAge: 7 * 86400
  })

  return c.json({ ok: true, user })
})

/* POST /api/auth/logout */
auth.post('/logout', async (c) => {
  const token = getCookie(c, SESSION_COOKIE)
  if (token) await c.env.DB.prepare(`DELETE FROM sessions WHERE token = ?`).bind(token).run()
  deleteCookie(c, SESSION_COOKIE, { path: '/' })
  return c.json({ ok: true })
})

/* GET /api/auth/me */
auth.get('/me', authMiddleware, async (c) => {
  const u = c.get('user')
  let companyName: string | null = null
  if (u.company_id) {
    const row = await c.env.DB.prepare(`SELECT name FROM companies WHERE id = ?`)
      .bind(u.company_id)
      .first<{ name: string }>()
    companyName = row?.name ?? null
  }
  const unread = await c.env.DB.prepare(
    `SELECT COUNT(*) AS n FROM notifications WHERE user_id = ? AND is_read = 0`
  )
    .bind(u.id)
    .first<{ n: number }>()
  return c.json({ user: { ...u, company_name: companyName }, unread: unread?.n ?? 0 })
})

/* POST /api/auth/change-password */
auth.post('/change-password', authMiddleware, async (c) => {
  const u = c.get('user')
  const b = await c.req.json().catch(() => ({}))
  const oldPw = String(b.old_password || '')
  const newPw = String(b.new_password || '')
  if (newPw.length < 6) return c.json({ error: 'كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل' }, 400)

  const oldHash = await hashPassword(oldPw)
  const ok = await c.env.DB.prepare(`SELECT 1 FROM users WHERE id = ? AND password_hash = ?`)
    .bind(u.id, oldHash)
    .first()
  if (!ok) return c.json({ error: 'كلمة المرور الحالية غير صحيحة' }, 400)

  await c.env.DB.prepare(`UPDATE users SET password_hash = ? WHERE id = ?`)
    .bind(await hashPassword(newPw), u.id)
    .run()
  await audit(c, 'User', u.id, 'Update', { password: 'changed' })
  return c.json({ ok: true })
})

export default auth
