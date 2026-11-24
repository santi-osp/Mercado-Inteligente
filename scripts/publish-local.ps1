param([switch]$SkipFrontendBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $SkipFrontendBuild) {
    Push-Location (Join-Path $root 'src/frontend')
    try {
        $nodeVersion = [version](node --version).TrimStart('v')
        if ($LASTEXITCODE -ne 0 -or -not (($nodeVersion.Major -eq 22 -and $nodeVersion -ge [version]'22.22.3') -or
            ($nodeVersion.Major -eq 24 -and $nodeVersion -ge [version]'24.15.0') -or $nodeVersion.Major -ge 26)) {
            throw 'Angular requiere Node 22.22.3+, 24.15.0+ o 26+.'
        }
        npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci falló.' }
        npm run build; if ($LASTEXITCODE -ne 0) { throw 'Angular build falló.' }
    }
    finally { Pop-Location }
}
$frontend = Join-Path $root 'src/frontend/dist/mercado-inteligente/browser'
if (-not (Test-Path (Join-Path $frontend 'index.html'))) { throw 'Falta Angular compilado; ejecute npm run build con Node compatible.' }
$target = Join-Path $root 'artifacts/release/MercadoInteligente'
dotnet publish (Join-Path $root 'src/backend/MercadoInteligente.Api/MercadoInteligente.Api.csproj') -c Release -o $target
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló.' }
$uiTarget = Join-Path $target 'wwwroot'
New-Item -ItemType Directory -Force $uiTarget | Out-Null
Copy-Item (Join-Path $frontend '*') $uiTarget -Recurse -Force
Write-Host "Release local: $target. En el equipo final requiere .NET Runtime 10, no Node."
