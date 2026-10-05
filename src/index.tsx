import { Hono } from 'hono'
import { cors } from 'hono/cors'
import { logger } from 'hono/logger'
import type { Env } from './lib'
import { authMiddleware } from './lib'
import { shellHtml } from './page'

import authRoutes from './routes/auth'
import orgRoutes from './routes/org'
import assetRoutes from './routes/assets'
import custodyRoutes from './routes/custody'
import ticketRoutes from './routes/tickets'
import miscRoutes from './routes/misc'

const app = new Hono<Env>()

app.use('/api/*', cors())
app.use('*', logger())

/* ---------- API ---------- */
const api = new Hono<Env>()

api.route('/auth', authRoutes)

// everything below requires a session
api.use('/assets/*', authMiddleware)
api.use('/custody/*', authMiddleware)
api.use('/tickets/*', authMiddleware)
api.use('/companies', authMiddleware)
api.use('/companies/*', authMiddleware)
api.use('/departments', authMiddleware)
api.use('/departments/*', authMiddleware)
api.use('/locations', authMiddleware)
api.use('/locations/*', authMiddleware)
api.use('/vendors', authMiddleware)
api.use('/vendors/*', authMiddleware)
api.use('/categories', authMiddleware)
api.use('/categories/*', authMiddleware)
api.use('/users', authMiddleware)
api.use('/users/*', authMiddleware)
api.use('/dashboard', authMiddleware)
api.use('/notifications', authMiddleware)
api.use('/notifications/*', authMiddleware)
api.use('/schedules', authMiddleware)
api.use('/schedules/*', authMiddleware)
api.use('/audits', authMiddleware)
api.use('/audits/*', authMiddleware)
api.use('/audit-logs', authMiddleware)
api.use('/reports/*', authMiddleware)
api.use('/jobs/*', authMiddleware)
api.use('/settings', authMiddleware)

api.route('/assets', assetRoutes)
api.route('/custody', custodyRoutes)
api.route('/tickets', ticketRoutes)
api.route('/', orgRoutes)
api.route('/', miscRoutes)

api.notFound((c) => c.json({ error: 'مسار غير موجود' }, 404))
api.onError((err, c) => {
  console.error('API error:', err)
  return c.json({ error: 'حدث خطأ في الخادم: ' + (err.message || 'غير معروف') }, 500)
})

app.route('/api', api)

/* ---------- QR short link: /a/AST-2026-00001 ---------- */
app.get('/a/:tag', (c) => c.redirect(`/#/scan-result/${encodeURIComponent(c.req.param('tag'))}`))

/* ---------- SPA shell (all non-API routes) ---------- */
app.get('*', (c) => c.html(shellHtml))

export default app
