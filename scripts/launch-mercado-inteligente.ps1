<#
.SYNOPSIS
    Lanzador local en segundo plano para Mercado Inteligente v1.0.0.
    Inicia el servicio local si no esta activo, verifica su salud,
    sincroniza/compila el frontend si hubo cambios y abre el navegador.
#>
[CmdletBinding()]
param(
    [switch]$NoBrowser,
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$url = "http://127.0.0.1:5000"
$healthUrl = "$url/api/v1/system/health"

$runtimeDir = Join-Path $env:LOCALAPPDATA "MercadoInteligente\runtime"
$logsDir = Join-Path $env:LOCALAPPDATA "MercadoInteligente\logs"
if (-not (Test-Path -LiteralPath $runtimeDir)) { New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null }
if (-not (Test-Path -LiteralPath $logsDir)) { New-Item -ItemType Directory -Force -Path $logsDir | Out-Null }

$pidFile = Join-Path $runtimeDir "app.pid"
$logFile = Join-Path $logsDir "launcher.log"

function Write-LauncherLog([string]$msg) {
    $ts = [DateTime]::UtcNow.ToString("o")
    Add-Content -LiteralPath $logFile -Value "[$ts] $msg" -Encoding utf8 -ErrorAction SilentlyContinue
}

function Test-FrontendSourceNewer {
    param(
        [string]$SrcDir,
        [string]$TargetFile
    )
    if (-not (Test-Path -LiteralPath $TargetFile)) { return $true }
    $targetTime = (Get-Item -LiteralPath $TargetFile).LastWriteTimeUtc

    $rootFrontend = Split-Path -Parent $SrcDir
    foreach ($cfg in @("package.json", "angular.json", "tsconfig.json")) {
        $cfgPath = Join-Path $rootFrontend $cfg
        if (Test-Path -LiteralPath $cfgPath) {
            if ((Get-Item -LiteralPath $cfgPath).LastWriteTimeUtc -gt $targetTime) {
                return $true
            }
        }
    }

    if (Test-Path -LiteralPath $SrcDir) {
        $latest = (Get-ChildItem -LiteralPath $SrcDir -Recurse -File -ErrorAction SilentlyContinue |
            Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum
        if ($latest -and ($latest -gt $targetTime)) {
            return $true
        }
    }
    return $false
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   MERCADO INTELIGENTE - v1.0.0 LOCAL" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Localizar el ejecutable binario
$candidates = @(
    (Join-Path $root "release\MercadoInteligente\MercadoInteligente.exe"),
    (Join-Path $root "release\MercadoInteligente\MercadoInteligente.Api.exe"),
    (Join-Path $root "src\backend\MercadoInteligente.Api\bin\Release\net10.0\win-x64\publish\MercadoInteligente.exe"),
    (Join-Path $root "src\backend\MercadoInteligente.Api\bin\Debug\net10.0\MercadoInteligente.Api.exe")
)

$targetExe = $null
foreach ($c in $candidates) {
    if (Test-Path -LiteralPath $c) {
        $targetExe = $c
        break
    }
}

if (-not $targetExe) {
    Write-Host "[INFO] No se encontro binario prepublicado. Publicando version local..." -ForegroundColor Yellow
    & (Join-Path $root "scripts\publish-windows.ps1") -FrameworkDependent
    $targetExe = Join-Path $root "release\MercadoInteligente\MercadoInteligente.exe"
    if (-not (Test-Path -LiteralPath $targetExe)) {
        $targetExe = Join-Path $root "release\MercadoInteligente\MercadoInteligente.Api.exe"
    }
    if (-not (Test-Path -LiteralPath $targetExe)) {
        throw "No se pudo localizar ni compilar el ejecutable de Mercado Inteligente."
    }
}

# 2. Comprobar si el frontend requiere compilacion o sincronizacion
$frontendDir = Join-Path $root "src\frontend"
$frontendSrc = Join-Path $frontendDir "src"
$frontendDist = Join-Path $frontendDir "dist\mercado-inteligente\browser"
$frontendDistIndex = Join-Path $frontendDist "index.html"
$exeWwwroot = Join-Path (Split-Path -Parent $targetExe) "wwwroot"
$exeIndex = Join-Path $exeWwwroot "index.html"

$needsBuild = $Rebuild -or (Test-FrontendSourceNewer -SrcDir $frontendSrc -TargetFile $frontendDistIndex)
$needsSync = $needsBuild -or (-not (Test-Path -LiteralPath $exeIndex))

if (-not $needsSync -and (Test-Path -LiteralPath $frontendDistIndex)) {
    $distTime = (Get-Item -LiteralPath $frontendDistIndex).LastWriteTimeUtc
    $targetTime = (Get-Item -LiteralPath $exeIndex).LastWriteTimeUtc
    if ($distTime -gt $targetTime) {
        $needsSync = $true
    }
}

# 3. Comprobar si el servicio ya esta corriendo
$alreadyRunning = $false
try {
    $res = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 2 -ErrorAction Stop
    if ($res.database -eq "ok") {
        $alreadyRunning = $true
    }
} catch {
    $alreadyRunning = $false
}

# Si el servicio ya corre pero se detectaron cambios en el frontend, reiniciarlo para servir el nuevo frontend
if ($alreadyRunning -and ($needsBuild -or $needsSync)) {
    Write-Host "[INFO] Se detectaron cambios en el frontend. Reiniciando servicio para aplicar cambios..." -ForegroundColor Yellow
    Write-LauncherLog "Frontend changes detected on running service. Restarting."
    & (Join-Path $root "scripts\stop-mercado-inteligente.ps1")
    $alreadyRunning = $false
    Start-Sleep -Seconds 1
}

# 4. Compilar y sincronizar frontend si es necesario
if ($needsBuild) {
    Write-Host "[INFO] Compilando frontend Angular (npm run build)..." -ForegroundColor Cyan
    Write-LauncherLog "Compiling Angular frontend."
    Push-Location $frontendDir
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "npm run build termino con codigo de salida $LASTEXITCODE."
        }
    } catch {
        Write-Warning "Error al ejecutar npm run build: $_"
    } finally {
        Pop-Location
    }
    $needsSync = $true
}

if ($needsSync -and (Test-Path -LiteralPath $frontendDistIndex)) {
    Write-Host "[INFO] Sincronizando frontend hacia wwwroot del ejecutable..." -ForegroundColor Cyan
    Write-LauncherLog "Syncing frontend dist to wwwroot: $exeWwwroot"
    if (Test-Path -LiteralPath $exeWwwroot) {
        Remove-Item -LiteralPath $exeWwwroot -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Force -Path $exeWwwroot | Out-Null
    Copy-Item (Join-Path $frontendDist '*') $exeWwwroot -Recurse -Force
}

# 5. Si ya esta corriendo y actualizado, simplemente abrir navegador y salir
if ($alreadyRunning) {
    Write-Host "[OK] Mercado Inteligente ya esta en ejecucion en $url" -ForegroundColor Green
    Write-LauncherLog "App already running and up-to-date. Opening browser if requested."
    if (-not $NoBrowser) {
        Start-Process $url
    }
    exit 0
}

# 6. Iniciar proceso en segundo plano
$workDir = Split-Path -Parent $targetExe
Write-Host "[1/3] Iniciando proceso en segundo plano..." -ForegroundColor Cyan
Write-Host "      Ejecutable: $targetExe" -ForegroundColor Gray
Write-Host "      Directorio: $workDir" -ForegroundColor Gray

$proc = Start-Process -FilePath $targetExe -WorkingDirectory $workDir -WindowStyle Hidden -PassThru
$procId = $proc.Id
Set-Content -LiteralPath $pidFile -Value $procId -Encoding ascii
Write-LauncherLog "Started process PID=$procId from $targetExe"

Write-Host "[2/3] Esperando inicializacion de servicios locales (PID: $procId)..." -ForegroundColor Cyan
$maxWaitSeconds = 15
$started = [DateTime]::UtcNow
$isHealthy = $false

while (([DateTime]::UtcNow - $started).TotalSeconds -lt $maxWaitSeconds) {
    Start-Sleep -Milliseconds 500
    try {
        $res = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 1 -ErrorAction Stop
        if ($res.database -eq "ok") {
            $isHealthy = $true
            break
        }
    } catch {
        # Sigue esperando
    }

    if ($proc.HasExited) {
        $exitCode = $proc.ExitCode
        Write-LauncherLog "Process PID=$procId exited prematurely with code $exitCode"
        throw "El proceso de Mercado Inteligente se detuvo inesperadamente con codigo $exitCode. Revisa los logs en $logsDir."
    }
}

if (-not $isHealthy) {
    Write-LauncherLog "Timeout waiting for health check at $healthUrl"
    throw "Tiempo de espera agotado esperando que Mercado Inteligente responda en $healthUrl."
}

Write-Host "[3/3] Aplicacion lista y saludable." -ForegroundColor Green
Write-Host "`n>> Abriendo navegador en: $url" -ForegroundColor Green
Write-LauncherLog "Application healthy. URL opened: $url"

if (-not $NoBrowser) {
    Start-Process $url
}

Write-Host "Para detener la aplicacion en cualquier momento, ejecute scripts\stop-mercado-inteligente.ps1 o DETENER_MERCADO_INTELIGENTE.cmd" -ForegroundColor Gray
