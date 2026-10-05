/**
 * D1-over-REST client.
 *
 * Vercel (and any host outside Cloudflare Workers) has no native D1 bindings,
 * so this class speaks to the SAME Cloudflare D1 database over the REST API.
 * It implements the small subset of the D1 interface the app uses:
 *   .prepare(sql).bind(...params).all() / .first() / .run()
 * (plus the same methods directly on .prepare() when there are no params).
 *
 * Required environment variables (set in Vercel dashboard):
 *   CF_ACCOUNT_ID      – Cloudflare account id
 *   CF_D1_DATABASE_ID  – id of the webapp-production D1 database
 *   CF_D1_API_TOKEN    – API token with D1 "Edit" permission
 */

export function d1Rest(): any {
  const ACCOUNT = process.env.CF_ACCOUNT_ID
  const DB_ID = process.env.CF_D1_DATABASE_ID
  const TOKEN = process.env.CF_D1_API_TOKEN

  if (!ACCOUNT || !DB_ID || !TOKEN) {
    throw new Error(
      'بيئة غير مكتملة: متغيرات CF_ACCOUNT_ID / CF_D1_DATABASE_ID / CF_D1_API_TOKEN غير مضبوطة'
    )
  }

  const url = `https://api.cloudflare.com/client/v4/accounts/${ACCOUNT}/d1/database/${DB_ID}/query`

  async function query(sql: string, params: any[]) {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${TOKEN}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ sql, params })
    })
    if (!res.ok) {
      throw new Error(`D1 REST HTTP ${res.status}: ${await res.text()}`)
    }
    const json: any = await res.json()
    if (!json.success) {
      throw new Error('D1 REST error: ' + JSON.stringify(json.errors || json))
    }
    return json.result[0] as { results: any[]; meta: any }
  }

  const makeResult = (sql: string, params: any[]) => ({
    async all() {
      const r = await query(sql, params)
      return { results: r.results ?? [], success: true as const, meta: r.meta }
    },
    async first<T = any>(): Promise<T | null> {
      const r = await query(sql, params)
      return (r.results ?? [])[0] ?? null
    },
    async run() {
      const r = await query(sql, params)
      return { success: true as const, meta: r.meta }
    }
  })

  return {
    prepare(sql: string) {
      return { bind: (...params: any[]) => makeResult(sql, params), ...makeResult(sql, []) }
    }
  }
}
