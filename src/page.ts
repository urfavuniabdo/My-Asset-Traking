export const APP_VERSION = '1'

export const shellHtml = /* html */ `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>نظام إدارة وتتبع الأصول والدعم الفني</title>
  <link rel="icon" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text y='.9em' font-size='90'>📦</text></svg>">
  <script src="https://cdn.tailwindcss.com"></script>
  <link href="https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.4.0/css/all.min.css" rel="stylesheet">
  <link href="https://fonts.googleapis.com/css2?family=Cairo:wght@300;400;600;700;800&display=swap" rel="stylesheet">
  <link href="/static/style.css" rel="stylesheet">
  <script>
    tailwind.config = {
      theme: {
        extend: {
          fontFamily: { sans: ['Cairo', 'system-ui', 'sans-serif'] },
          colors: {
            brand: {
              50:'#eff6ff',100:'#dbeafe',200:'#bfdbfe',300:'#93c5fd',400:'#60a5fa',
              500:'#3b82f6',600:'#2563eb',700:'#1d4ed8',800:'#1e40af',900:'#1e3a8a'
            }
          }
        }
      }
    }
  </script>
</head>
<body class="bg-slate-100 font-sans text-slate-800 antialiased">
  <div id="root">
    <div class="min-h-screen flex items-center justify-center">
      <div class="text-center">
        <i class="fas fa-spinner fa-spin text-4xl text-brand-600"></i>
        <p class="mt-3 text-slate-500">جارٍ التحميل...</p>
      </div>
    </div>
  </div>
  <div id="toast-wrap" class="fixed top-4 left-1/2 -translate-x-1/2 z-[9999] space-y-2"></div>
  <div id="modal-root"></div>

  <script src="https://cdn.jsdelivr.net/npm/axios@1.6.0/dist/axios.min.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"></script>
  <script src="https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js"></script>
  <script src="/static/core.js?v=${APP_VERSION}"></script>
  <script src="/static/assets.js?v=${APP_VERSION}"></script>
  <script src="/static/custody.js?v=${APP_VERSION}"></script>
  <script src="/static/tickets.js?v=${APP_VERSION}"></script>
  <script src="/static/maint.js?v=${APP_VERSION}"></script>
  <script src="/static/reports.js?v=${APP_VERSION}"></script>
  <script src="/static/admin.js?v=${APP_VERSION}"></script>
  <script>A.boot()</script>
</body>
</html>`
