# دليل النشر — GitHub + Vercel (+ قاعدة بيانات Cloudflare D1)

المشروع: نظام إدارة وتتبع الأصول (Hono + Cloudflare D1).
هذا المشروع يستخدم **Cloudflare D1** كقاعدة بيانات. و Vercel يستضيف التطبيق فقط
ويتصل بقاعدة D1 عبر الـ REST API — لا حاجة لأي تغيير في الكود عند النشر.

## 1) تجهيز قاعدة البيانات على Cloudflare (مرة واحدة)

```bash
npx wrangler login                                   # تسجيل الدخول لـ Cloudflare
npx wrangler d1 create webapp-production             # إنشاء القاعدة (احتفظ بـ database_id)
npx wrangler d1 migrations apply webapp-production --remote   # تطبيق الهيكل
npx wrangler d1 execute webapp-production --remote --file=./seed.sql  # بيانات أولية
```

> لو غيّرت `database_id` في `wrangler.jsonc` فلا مشكلة — ملفات Vercel لا تعتمد عليه
> بل على متغيرات البيئة بالأسفل. حسابات التجريبية تُنشأ بالـ seed وكلمة المرور `123456`.

## 2) بيانات الاتصال المطلوبة من Cloudflare

| المتغير | من أين تحصل عليه |
|---|---|
| `CF_ACCOUNT_ID` | لوحة Cloudflare → أي دومين → Overview (يمين الصفحة) أو من رابط اللوحة |
| `CF_D1_DATABASE_ID` | لوحة Cloudflare → Storage & Databases → D1 → webapp-production → Details |
| `CF_D1_API_TOKEN` | لوحة Cloudflare → My Profile → API Tokens → Create Token → صلاحية **D1 Edit** على الحساب |

## 3) رفع المشروع على GitHub

```bash
git add -A
git commit -m "جاهز للنشر"
git remote add origin https://github.com/<اسمك>/<اسم-المستودع>.git
git push -u origin main
```

## 4) الاستيراد على Vercel

1. vercel.com → **Add New Project** → اختر المستودع.
2. Framework Preset: **Other** — لا Build Command ولا Output Directory (المشروع جاهز).
3. في **Environment Variables** أضف المتغيرات الثلاثة من الخطوة 2.
4. اضغط **Deploy**.

## كيف يعمل التشغيل على Vercel؟

- `vercel.json` يعيد توجيه كل المسارات (عدا `/static/*` وهي ملفات واجهة ثابتة من مجلد `public/`) إلى الدالة `api/index.ts`.
- `api/index.ts` يشغّل نفس تطبيق Hono الموجود في `src/index.tsx` (نفس الكود الذي يعمل محليًا وعلى Cloudflare).
- `api/d1-rest.ts` ينفّذ استعلامات SQL على قاعدة D1 عبر `api.cloudflare.com` — نفس الواجهة (`prepare/bind/first/run/all`) بدون أي تغيير في الكود.

> ملاحظة: التشغيل المحلي والنشر على Cloudflare Pages لا يتأثران بشيء
> (`npm run build` ثم `wrangler pages dev dist` محليًا، أو `npm run deploy`).
