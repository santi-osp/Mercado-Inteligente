<#
.SYNOPSIS
    Inicia la aplicación local Mercado Inteligente 1.0.0.
    Inicia el backend ASP.NET Core que sirve automáticamente la UI de Angular PWA compilada.
.PARAMETER Dev
    Si se especifica, inicia el backend y además lanza ng serve para desarrollo en caliente.
#>
param(
    [switch]$Dev
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  MERCADO INTELIGENTE — Versión Local 1.0.0" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if ($Dev) {
    Write-Host "==> Modo Desarrollo: iniciando backend y frontend en paralelo..." -ForegroundColor Yellow
    $backendProcess = Start-Process dotnet -ArgumentList @('run', '--project', (Join-Path $root 'src/backend/MercadoInteligente.Api')) -WindowStyle Hidden -PassThru
    Push-Location (Join-Path $root 'src/frontend')
    try {
        npm start -- --proxy-config proxy.conf.json
    } finally {
        Pop-Location
        if ($backendProcess -and -not $backendProcess.HasExited) {
            Stop-Process -Id $backendProcess.Id -Force
        }
    }
} else {
    # Verificar si el frontend ya está compilado; si no, compilarlo
    $builtUi = Join-Path $root 'src/frontend/dist/mercado-inteligente/browser/index.html'
    if (-not (Test-Path $builtUi)) {
        Write-Host "==> Compilando frontend por primera vez..." -ForegroundColor Yellow
        Push-Location (Join-Path $root 'src/frontend')
        try {
            npm run build
        } finally {
            Pop-Location
        }
    }

    Write-Host "==> Iniciando servidor local en http://localhost:5000..." -ForegroundColor Green
    Write-Host "    Abra su navegador en http://localhost:5000" -ForegroundColor Cyan
    Write-Host "    Presione Ctrl+C para detener la aplicación." -ForegroundColor DarkGray

    # Iniciar backend directamente en la consola actual
    Set-Location (Join-Path $root 'src/backend/MercadoInteligente.Api')
    dotnet run -c Release --urls "http://localhost:5000"
}
