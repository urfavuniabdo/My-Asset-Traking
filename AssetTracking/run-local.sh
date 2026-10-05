#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════
#  تشغيل نظام إدارة الأصول محلياً — Linux / macOS
# ═══════════════════════════════════════════════════════════════
#  يشغّل النظام بقاعدة SQLite محلية، بلا SQL Server ولا IIS.
#
#  الاستخدام:
#     ./run-local.sh              تشغيل عادي
#     ./run-local.sh --fresh      حذف القاعدة وإعادة توليد البيانات
#     ./run-local.sh --clean      تنظيف مخرجات البناء القديمة
# ═══════════════════════════════════════════════════════════════
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEB="$ROOT/src/AssetTracking.Web"
PORT="${PORT:-5000}"
FRESH=0; CLEAN=0

for a in "$@"; do
  case "$a" in
    --fresh) FRESH=1 ;;
    --clean) CLEAN=1 ;;
    --port=*) PORT="${a#*=}" ;;
    *) echo "وسيط غير معروف: $a"; exit 1 ;;
  esac
done

C_CYAN='\033[36m'; C_GREEN='\033[32m'; C_YEL='\033[33m'; C_RED='\033[31m'; C_OFF='\033[0m'
step() { echo -e "\n${C_CYAN}[$1] $2${C_OFF}"; }
ok()   { echo -e "    ${C_GREEN}$1${C_OFF}"; }
warn() { echo -e "    ${C_YEL}$1${C_OFF}"; }
fail() { echo -e "\n${C_RED}!! $1${C_OFF}"; }

echo
echo "  نظام إدارة وتتبع الأصول والدعم الفني"
echo "  تشغيل محلي (SQLite — بلا SQL Server)"

# ── 1) .NET SDK ───────────────────────────────────────────────
step 1 'التحقق من .NET 8 SDK'
if ! command -v dotnet >/dev/null 2>&1; then
  fail 'الأمر dotnet غير موجود.'
  echo "    ثبّت .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
  exit 1
fi
if ! dotnet --list-sdks | grep -q '^8\.'; then
  fail 'لم أجد .NET 8 SDK. المثبّت حالياً:'
  dotnet --list-sdks | sed 's/^/      /'
  exit 1
fi
ok ".NET 8 SDK موجود — $(dotnet --list-sdks | grep '^8\.' | head -1)"

# ── 2) إصدار الكود ────────────────────────────────────────────
step 2 'التحقق من إصدار الكود'
if git -C "$ROOT" rev-parse --short HEAD >/dev/null 2>&1; then
  ok "الفرع: $(git -C "$ROOT" rev-parse --abbrev-ref HEAD)   |   الكوميت: $(git -C "$ROOT" rev-parse --short HEAD)"
fi
if [ -f "$WEB/wwwroot/css/site.css" ]; then
  if grep -q '#0f766e' "$WEB/wwwroot/css/site.css"; then
    ok 'الثيم الأخضر (#0f766e) موجود في الكود.'
  else
    warn 'لم أجد #0f766e — قد تكون على كود قديم. نفّذ: git pull'
  fi
fi

# ── 3) تنظيف البناء ───────────────────────────────────────────
if [ "$CLEAN" -eq 1 ]; then
  step 3 'تنظيف مخرجات البناء القديمة'
  find "$ROOT" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
  ok 'حُذفت مجلدات bin و obj.'
else
  step 3 'تخطّي التنظيف (استخدم --clean لو ترى نسخة قديمة)'
fi

# ── 4) قاعدة البيانات ─────────────────────────────────────────
if [ "$FRESH" -eq 1 ]; then
  step 4 'حذف قاعدة البيانات لإعادة توليد البيانات التجريبية'
  rm -f "$WEB"/assettracking_dev.db*
  ok 'حُذفت القاعدة — ستُنشأ من جديد مع البيانات التجريبية.'
else
  step 4 'الاحتفاظ بقاعدة البيانات الحالية'
  [ -f "$WEB/assettracking_dev.db" ] \
    && ok "القاعدة موجودة: $(du -h "$WEB/assettracking_dev.db" | cut -f1)" \
    || warn 'لا توجد قاعدة — ستُنشأ تلقائياً.'
fi

# ── 5) البناء ─────────────────────────────────────────────────
step 5 'بناء المشروع (قد يستغرق دقيقة في أول مرة)'
dotnet build "$WEB/AssetTracking.Web.csproj" -c Debug --nologo -v minimal
ok 'نجح البناء.'

# ── 6) التشغيل ────────────────────────────────────────────────
step 6 'تشغيل التطبيق'
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://localhost:$PORT"

cat <<EOF

  ─────────────────────────────────────────────────────────
   الرابط:   http://localhost:$PORT
   البيئة:   Development  (SQLite — بلا SQL Server)

   الدخول — مدير مصنع المنصورة (سجل أعطال الماكينات):
     delta.manager@ats.eg    /  Admin@123

   حسابات أخرى (نفس كلمة المرور Admin@123):
     admin@ats.eg          مدير النظام (كل الشركات)
     nile.manager@ats.eg   مدير شركة النيل
     delta.tech1@ats.eg    فني صيانة ميكانيكية

   للإيقاف: Ctrl + C
  ─────────────────────────────────────────────────────────

EOF

exec dotnet run --project "$WEB/AssetTracking.Web.csproj" \
                -c Debug --no-build --no-launch-profile
