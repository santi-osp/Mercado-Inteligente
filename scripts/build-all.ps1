<#
.SYNOPSIS
    Compila la solución completa de Mercado Inteligente (backend .NET 10 y frontend Angular PWA).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Compilando Backend .NET 10..." -ForegroundColor Cyan
dotnet build (Join-Path $root 'MercadoInteligente.slnx') -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Compilando Frontend Angular PWA..." -ForegroundColor Cyan
Push-Location (Join-Path $root 'src/frontend')
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}

Write-Host "==> Compilación completa exitosa." -ForegroundColor Green
