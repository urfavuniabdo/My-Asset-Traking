<#
    ═══════════════════════════════════════════════════════════════
    تشغيل نظام إدارة الأصول محلياً على Windows — بيئة التطوير
    ═══════════════════════════════════════════════════════════════

    يشغّل النظام بقاعدة SQLite محلية، فلا يحتاج SQL Server ولا IIS.
    قاعدة البيانات والبيانات التجريبية تُنشأ تلقائياً عند أول تشغيل.

    الاستخدام:
        .\run-local.ps1                # تشغيل عادي
        .\run-local.ps1 -Fresh         # حذف القاعدة وإعادة توليد البيانات
        .\run-local.ps1 -Clean         # تنظيف مخرجات البناء القديمة أولاً

    ملاحظة: -Clean يحذف bin/ و obj/ لحل مشكلة «أرى نسخة قديمة».
#>

[CmdletBinding()]
param(
    [switch]$Fresh,
    [switch]$Clean,
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$webProj = Join-Path $root 'src\AssetTracking.Web'

function Step($n, $msg) { Write-Host "`n[$n] $msg" -ForegroundColor Cyan }
function Ok($msg)        { Write-Host "    $msg" -ForegroundColor Green }
function Warn($msg)      { Write-Host "    $msg" -ForegroundColor Yellow }
function Fail($msg)      { Write-Host "`n!! $msg" -ForegroundColor Red }

Write-Host @"

  نظام إدارة وتتبع الأصول والدعم الفني
  تشغيل محلي (SQLite — بلا SQL Server)

"@ -ForegroundColor White

# ── 1) التحقق من .NET SDK ────────────────────────────────────
Step 1 'التحقق من .NET 8 SDK'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Fail 'الأمر dotnet غير موجود.'
    Write-Host @"
    ثبّت .NET 8 SDK من:
      https://dotnet.microsoft.com/download/dotnet/8.0

    أو عبر winget:
      winget install Microsoft.DotNet.SDK.8

    ثم أعد فتح PowerShell وشغّل هذا السكربت مرة أخرى.
"@ -ForegroundColor Yellow
    exit 1
}

$sdks = & dotnet --list-sdks 2>$null
$has8 = $sdks | Where-Object { $_ -match '^8\.' }
if (-not $has8) {
    Fail 'لم أجد .NET 8 SDK. الإصدارات المثبّتة:'
    $sdks | ForEach-Object { Write-Host "      $_" -ForegroundColor Yellow }
    Write-Host "    ثبّت الإصدار ٨:  winget install Microsoft.DotNet.SDK.8" -ForegroundColor Yellow
    exit 1
}
Ok ".NET 8 SDK موجود — $(($has8 | Select-Object -First 1).Trim())"

# ── 2) التحقق من أننا على أحدث كود ───────────────────────────
Step 2 'التحقق من إصدار الكود'
Push-Location $root
try {
    $sha = (& git rev-parse --short HEAD 2>$null)
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
    if ($sha) {
        Ok "الفرع: $branch   |   الكوميت: $sha"
        $msg = (& git log -1 --format='%s' 2>$null)
        if ($msg) { Write-Host "    ($msg)" -ForegroundColor DarkGray }
    }
} catch { Warn 'تعذّر قراءة معلومات git (غير مهم).' }
finally { Pop-Location }

# تحقق أن الثيم الأخضر موجود فعلاً في الكود
$css = Join-Path $webProj 'wwwroot\css\site.css'
if (Test-Path $css) {
    if ((Get-Content $css -Raw) -match '#0f766e') {
        Ok 'الثيم الأخضر (#0f766e) موجود في الكود.'
    } else {
        Warn 'لم أجد اللون #0f766e — قد تكون على كود قديم. نفّذ: git pull'
    }
}

# ── 3) تنظيف مخرجات البناء (اختياري) ────────────────────────
if ($Clean) {
    Step 3 'تنظيف مخرجات البناء القديمة'
    Get-ChildItem $root -Include bin, obj -Recurse -Directory -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    Ok 'حُذفت مجلدات bin و obj.'
} else {
    Step 3 'تخطّي التنظيف (استخدم -Clean لو ترى نسخة قديمة)'
}

# ── 4) إعادة توليد القاعدة (اختياري) ────────────────────────
$db = Join-Path $webProj 'assettracking_dev.db'
if ($Fresh) {
    Step 4 'حذف قاعدة البيانات لإعادة توليد البيانات التجريبية'
    'assettracking_dev.db', 'assettracking_dev.db-shm', 'assettracking_dev.db-wal' |
        ForEach-Object {
            $p = Join-Path $webProj $_
            if (Test-Path $p) { Remove-Item $p -Force; Ok "حُذف $_" }
        }
} else {
    Step 4 'الاحتفاظ بقاعدة البيانات الحالية'
    if (Test-Path $db) {
        Ok "القاعدة موجودة: $([math]::Round((Get-Item $db).Length/1KB)) ك.ب"
    } else {
        Warn 'لا توجد قاعدة — ستُنشأ تلقائياً مع البيانات التجريبية.'
    }
}

# ── 5) البناء ────────────────────────────────────────────────
Step 5 'بناء المشروع (قد يستغرق دقيقة في أول مرة)'
& dotnet build (Join-Path $webProj 'AssetTracking.Web.csproj') -c Debug --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    Fail 'فشل البناء — راجع الأخطاء أعلاه.'
    exit 1
}
Ok 'نجح البناء.'

# ── 6) التشغيل ───────────────────────────────────────────────
Step 6 'تشغيل التطبيق'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS        = "http://localhost:$Port"

Write-Host @"

  ─────────────────────────────────────────────────────────
   الرابط:   http://localhost:$Port
   البيئة:   Development  (SQLite — بلا SQL Server)

   الدخول — مدير مصنع المنصورة (سجل أعطال الماكينات):
     delta.manager@ats.eg    /  Admin@123

   حسابات أخرى (نفس كلمة المرور Admin@123):
     admin@ats.eg          مدير النظام (كل الشركات)
     nile.manager@ats.eg   مدير شركة النيل
     delta.tech1@ats.eg    فني صيانة ميكانيكية

   للإيقاف: Ctrl + C
  ─────────────────────────────────────────────────────────

"@ -ForegroundColor White

Start-Sleep -Seconds 1
try { Start-Process "http://localhost:$Port" } catch { }

& dotnet run --project (Join-Path $webProj 'AssetTracking.Web.csproj') `
             -c Debug --no-build --no-launch-profile
