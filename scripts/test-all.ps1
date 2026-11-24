<#
.SYNOPSIS
    Ejecuta la suite completa de pruebas automáticas (backend .NET y frontend Angular/Vitest).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Ejecutando pruebas unitarias, de integración y E2E de Backend..." -ForegroundColor Cyan
dotnet test (Join-Path $root 'MercadoInteligente.slnx') --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Ejecutando pruebas de Frontend Angular..." -ForegroundColor Cyan
Push-Location (Join-Path $root 'src/frontend')
try {
    npm test -- --watch=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}

Write-Host "==> Todas las pruebas pasaron satisfactoriamente (100% de éxito)." -ForegroundColor Green
