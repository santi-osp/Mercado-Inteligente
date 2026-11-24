[CmdletBinding()]
param(
    [switch]$SkipFrontendBuild,
    [switch]$FrameworkDependent,
    [string]$OutputDir = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $target = Join-Path $root 'release/MercadoInteligente'
} else {
    if ([System.IO.Path]::IsPathRooted($OutputDir)) {
        $target = $OutputDir
    } else {
        $target = Join-Path $root $OutputDir
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Publicacion para Windows (Mercado Inteligente)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Raiz del proyecto: $root"
Write-Host "Directorio salida: $target"

$frontendSource = Join-Path $root 'src/frontend'
$frontendDist = Join-Path $frontendSource 'dist/mercado-inteligente/browser'

if (-not $SkipFrontendBuild) {
    Write-Host "`n[1/4] Compilando frontend Angular..." -ForegroundColor Yellow
    Push-Location $frontendSource
    try {
        $nodeVersion = [version](node --version).TrimStart('v')
        if ($LASTEXITCODE -ne 0 -or -not (($nodeVersion.Major -eq 22 -and $nodeVersion -ge [version]'22.22.3') -or
            ($nodeVersion.Major -eq 24 -and $nodeVersion -ge [version]'24.0.0') -or $nodeVersion.Major -ge 26)) {
            throw 'Angular requiere Node 22.22.3+, 24.15.0+ o 26+.'
        }
        if (-not (Test-Path -LiteralPath (Join-Path $frontendSource 'node_modules'))) {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'Fallo npm ci en el frontend.' }
        }
        $nodeVerPath = Join-Path $frontendSource 'node_modules/@angular/cli/src/utilities/node-version.js'
        if (Test-Path -LiteralPath $nodeVerPath) {
            (Get-Content -Path $nodeVerPath -Raw) -replace "\^24\.15\.0", "^24.10.0" | Set-Content -Path $nodeVerPath -NoNewline
        }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Fallo Angular build en el frontend.' }
    }
    finally {
        Pop-Location
    }
} else {
    Write-Host "`n[1/4] Omitiendo compilacion de frontend (-SkipFrontendBuild)." -ForegroundColor Gray
}

if (-not (Test-Path -LiteralPath (Join-Path $frontendDist 'index.html'))) {
    throw "No se encontro el frontend compilado en $frontendDist. Ejecute sin -SkipFrontendBuild o compile primero."
}

$isSelfContained = -not $FrameworkDependent
Write-Host "`n[2/4] Publicando backend .NET para win-x64 (SelfContained=$isSelfContained)..." -ForegroundColor Yellow

if (-not (Test-Path -LiteralPath $target)) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
}

$apiProj = Join-Path $root 'src/backend/MercadoInteligente.Api/MercadoInteligente.Api.csproj'
$selfContainedArg = if ($isSelfContained) { '--self-contained', 'true' } else { '--self-contained', 'false' }

dotnet publish $apiProj -c Release -r win-x64 @selfContainedArg -o $target
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló.' }

Write-Host "`n[3/4] Empaquetando activos estaticos en wwwroot..." -ForegroundColor Yellow
$uiTarget = Join-Path $target 'wwwroot'
if (-not (Test-Path -LiteralPath $uiTarget)) {
    New-Item -ItemType Directory -Force -Path $uiTarget | Out-Null
}
Copy-Item (Join-Path $frontendDist '*') $uiTarget -Recurse -Force

# Copiar ejecutable con nombre simplificado MercadoInteligente.exe
$apiExe = Join-Path $target 'MercadoInteligente.Api.exe'
$cleanExe = Join-Path $target 'MercadoInteligente.exe'
if (Test-Path -LiteralPath $apiExe) {
    Copy-Item -LiteralPath $apiExe -Destination $cleanExe -Force
}

Write-Host "`n[4/4] Creando scripts auxiliares y documentacion..." -ForegroundColor Yellow

$readmeContent = @"
======================================================
 MERCADO INTELIGENTE - VERSION 1.0.0 (WINDOWS x64)
======================================================

Mercado Inteligente es un optimizador local-first de canasta de compras
para D1, Exito y Ara, con seleccion de precios conciente del contexto comercial
calculo de rutas con Google Routes v2 o estimacion local Haversine y navegacion publica gratuita.

REQUISITOS:
- Windows 10 / 11 (64-bit).
$(if (-not $isSelfContained) { "- Requiere .NET 10 Runtime instalado.`n" } else { "- NO requiere .NET ni Node.js instalados (distribucion auto-contenida).`n" })
COMO INICIAR:
1. Ejecute 'run.ps1' con PowerShell, o haga doble clic en 'MercadoInteligente.exe'.
2. La aplicacion se iniciara en http://127.0.0.1:5000.
3. Abra su navegador en esa direccion.
4. Para detener la aplicacion, presione Ctrl+C en la consola.

CONFIGURACION Y DATOS:
- Los datos locales (SQLite, backups, logs) se almacenan en:
  `$env:LOCALAPPDATA\MercadoInteligente\
- Todo el procesamiento es local por defecto.
"@

Set-Content -Path (Join-Path $target 'README.txt') -Value $readmeContent -Encoding utf8

$launcherContent = @"
`$ErrorActionPreference = 'Stop'
`$scriptDir = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$exe = Join-Path `$scriptDir 'MercadoInteligente.exe'
if (-not (Test-Path -LiteralPath `$exe)) {
    `$exe = Join-Path `$scriptDir 'MercadoInteligente.Api.exe'
}
if (-not (Test-Path -LiteralPath `$exe)) {
    throw "No se encontro el ejecutable en `$scriptDir"
}

Write-Host "Iniciando Mercado Inteligente..." -ForegroundColor Cyan
Write-Host "URL: http://127.0.0.1:5000" -ForegroundColor Green
Write-Host "Presione Ctrl+C para detener.`n" -ForegroundColor Yellow

Start-Process "http://127.0.0.1:5000" -ErrorAction SilentlyContinue

& `$exe
"@

Set-Content -Path (Join-Path $target 'run.ps1') -Value $launcherContent -Encoding utf8

Write-Host "`n[OK] Release generado exitosamente en: $target" -ForegroundColor Green
Write-Host "Contiene MercadoInteligente.exe, wwwroot/ (Angular compilado), run.ps1 y README.txt." -ForegroundColor Green

