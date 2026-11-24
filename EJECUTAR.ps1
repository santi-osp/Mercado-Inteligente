[CmdletBinding()]
param(
    [switch]$Dev,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $root) { $root = $PWD.Path }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         MERCADO INTELIGENTE - VERSION 1.0.0              " -ForegroundColor Green
Write-Host "   Comparador y Optimizador Local de Compras (D1 / Exito) " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$releaseExe = Join-Path $root "release\MercadoInteligente\MercadoInteligente.exe"
$url = "http://localhost:5000"
$healthUrl = "http://127.0.0.1:5000/api/v1/system/health"

# 1. Comprobar si ya esta en ejecucion
$alreadyRunning = $false
try {
    $res = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 1 -ErrorAction Stop
    if ($res.database -eq "ok") {
        $alreadyRunning = $true
    }
} catch { }

if ($alreadyRunning) {
    Write-Host "[OK] Mercado Inteligente ya esta en ejecucion en $url" -ForegroundColor Green
    if (-not $NoBrowser) {
        Start-Process $url
    }
    Write-Host "Acceda a la aplicacion en: $url" -ForegroundColor Cyan
    exit 0
}

# 2. Modo Desarrollo explicito
if ($Dev) {
    Write-Host "==> Iniciando en Modo Desarrollo..." -ForegroundColor Yellow
    & (Join-Path $root "scripts\run-app.ps1") -Dev
    exit 0
}

# 3. Lanzar ejecutable standalone si existe
if (Test-Path -LiteralPath $releaseExe) {
    $workDir = Split-Path -Parent $releaseExe

    # Sincronizar frontend actualizado si dist tiene una version mas reciente que release/wwwroot
    $frontendDist = Join-Path $root "src\frontend\dist\mercado-inteligente\browser"
    $frontendIndex = Join-Path $frontendDist "index.html"
    $releaseWwwroot = Join-Path $workDir "wwwroot"
    $releaseIndex = Join-Path $releaseWwwroot "index.html"

    if (Test-Path -LiteralPath $frontendIndex) {
        $needsSync = $false
        if (-not (Test-Path -LiteralPath $releaseIndex)) {
            $needsSync = $true
        } else {
            $distTime = (Get-Item -LiteralPath $frontendIndex).LastWriteTimeUtc
            $relTime = (Get-Item -LiteralPath $releaseIndex).LastWriteTimeUtc
            if ($distTime -gt $relTime) {
                $needsSync = $true
            }
        }
        if ($needsSync) {
            Write-Host "==> Sincronizando frontend actualizado en release\wwwroot..." -ForegroundColor Cyan
            if (Test-Path -LiteralPath $releaseWwwroot) {
                Remove-Item -LiteralPath $releaseWwwroot -Recurse -Force -ErrorAction SilentlyContinue
            }
            New-Item -ItemType Directory -Force -Path $releaseWwwroot | Out-Null
            Copy-Item (Join-Path $frontendDist '*') $releaseWwwroot -Recurse -Force
        }
    }
    Write-Host "==> Iniciando ejecutable standalone de Windows..." -ForegroundColor Cyan
    Write-Host "    Ubicacion: $releaseExe" -ForegroundColor Gray

    $proc = Start-Process -FilePath $releaseExe -WorkingDirectory $workDir -WindowStyle Hidden -PassThru
    $runtimeDir = Join-Path $env:LOCALAPPDATA "MercadoInteligente\runtime"
    if (-not (Test-Path -LiteralPath $runtimeDir)) { New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null }
    Set-Content -LiteralPath (Join-Path $runtimeDir "app.pid") -Value $proc.Id -Encoding ascii

    Write-Host "==> Verificando servicios locales..." -ForegroundColor Yellow
    $started = [DateTime]::UtcNow
    $ready = $false
    while (([DateTime]::UtcNow - $started).TotalSeconds -lt 15) {
        Start-Sleep -Milliseconds 500
        try {
            $h = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 1 -ErrorAction Stop
            if ($h.database -eq "ok") {
                $ready = $true
                break
            }
        } catch { }
        if ($proc.HasExited) { break }
    }

    if ($ready) {
        Write-Host "[OK] Servidor iniciado y listo en $url" -ForegroundColor Green
        if (-not $NoBrowser) {
            Write-Host "==> Abriendo navegador web..." -ForegroundColor Cyan
            Start-Process $url
        }
        Write-Host ""
        Write-Host "Para detener la aplicacion en cualquier momento, ejecute:" -ForegroundColor Gray
        Write-Host "    .\DETENER_MERCADO_INTELIGENTE.cmd" -ForegroundColor Yellow
        Write-Host "o en PowerShell:" -ForegroundColor Gray
        Write-Host "    .\scripts\stop-mercado-inteligente.ps1" -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }
}

# 4. Fallback si no hay release standalone: dotnet run directo
Write-Host "==> Iniciando via .NET runtime local..." -ForegroundColor Yellow
$builtUi = Join-Path $root 'src/frontend/dist/mercado-inteligente/browser/index.html'
if (-not (Test-Path -LiteralPath $builtUi)) {
    Write-Host "==> Compilando frontend Angular..." -ForegroundColor Yellow
    Push-Location (Join-Path $root 'src/frontend')
    try { npm run build } finally { Pop-Location }
}

Write-Host "==> Servidor escuchando en $url" -ForegroundColor Green
Write-Host "    Presione Ctrl+C para finalizar." -ForegroundColor DarkGray
if (-not $NoBrowser) {
    Start-Process $url
}
Push-Location (Join-Path $root 'src/backend/MercadoInteligente.Api')
try {
    dotnet run -c Release --urls "http://127.0.0.1:5000"
} finally {
    Pop-Location
}