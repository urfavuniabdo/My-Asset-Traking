/**
 * Vercel entry point.
 *
 * The same Hono app that runs on Cloudflare Pages (src/index.tsx) — here it is
 * served as a Vercel Node function. Vercel has no D1 bindings, so we inject a
 * REST-based DB client (api/d1-rest.ts) as env.DB. Static files come from the
 * repo's public/ directory (served by Vercel at /static/*), and vercel.json
 * rewrites every other path into this function so Hono's routing works as-is.
 */
import app from '../src/index'
import { d1Rest } from './d1-rest'

export default async function handler(req: Request): Promise<Response> {
  return app.fetch(req, { DB: d1Rest() } as any)
}
