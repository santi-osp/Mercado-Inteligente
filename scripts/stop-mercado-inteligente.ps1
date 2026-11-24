<#
.SYNOPSIS
    Detiene de forma limpia el servicio local de Mercado Inteligente v1.0.0.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'SilentlyContinue'
$runtimeDir = Join-Path $env:LOCALAPPDATA "MercadoInteligente\runtime"
$logsDir = Join-Path $env:LOCALAPPDATA "MercadoInteligente\logs"
$pidFile = Join-Path $runtimeDir "app.pid"
$logFile = Join-Path $logsDir "launcher.log"

function Write-LauncherLog([string]$msg) {
    $ts = [DateTime]::UtcNow.ToString("o")
    if (Test-Path -LiteralPath $logsDir) {
        Add-Content -LiteralPath $logFile -Value "[$ts] $msg" -Encoding utf8 -ErrorAction SilentlyContinue
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   DETENIENDO MERCADO INTELIGENTE v1.0.0" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$stopped = $false

# 1. Detener por archivo PID si existe
if (Test-Path -LiteralPath $pidFile) {
    try {
        $savedPid = Get-Content -LiteralPath $pidFile -Raw -ErrorAction Stop
        if ($savedPid -match '^\d+$') {
            $procId = [int]$savedPid
            $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($p) {
                Write-Host "Deteniendo proceso PID $procId ($($p.ProcessName))..." -ForegroundColor Yellow
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                $stopped = $true
                Write-LauncherLog "Process PID=$procId stopped by stop script."
            }
        }
    } catch {
        # Ignorar error de lectura
    }
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
}

# 2. Verificar si algun proceso MercadoInteligente sigue activo
$namedProcs = Get-Process -Name "MercadoInteligente", "MercadoInteligente.Api" -ErrorAction SilentlyContinue
foreach ($np in $namedProcs) {
    Write-Host "Deteniendo proceso residual PID $($np.Id) ($($np.ProcessName))..." -ForegroundColor Yellow
    Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue
    $stopped = $true
    Write-LauncherLog "Residual process PID=$($np.Id) stopped."
}

# 3. Comprobar si el puerto 5000 aun esta ocupado
try {
    $connections = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        $portPid = $conn.OwningProcess
        if ($portPid -gt 0) {
            $portProc = Get-Process -Id $portPid -ErrorAction SilentlyContinue
            if ($portProc -and ($portProc.ProcessName -like "*MercadoInteligente*" -or $portProc.ProcessName -like "*dotnet*")) {
                Write-Host "Liberando puerto 5000 ocupado por PID $portPid ($($portProc.ProcessName))..." -ForegroundColor Yellow
                Stop-Process -Id $portPid -Force -ErrorAction SilentlyContinue
                $stopped = $true
                Write-LauncherLog "Port 5000 listener PID=$portPid stopped."
            }
        }
    }
} catch {
    # Ignorar si no soporta Get-NetTCPConnection
}

if ($stopped) {
    Write-Host "[OK] Mercado Inteligente detenido exitosamente." -ForegroundColor Green
} else {
    Write-Host "[INFO] Mercado Inteligente no estaba en ejecucion." -ForegroundColor Gray
}
