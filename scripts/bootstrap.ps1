param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) { throw 'Este bootstrap requiere Windows.' }
$dotnet = dotnet --version
if ($LASTEXITCODE -ne 0 -or [int]($dotnet.Split('.')[0]) -lt 10) { throw '.NET SDK 10 o superior requerido.' }
dotnet restore (Join-Path $root 'MercadoInteligente.slnx')
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore falló.' }
if (-not $SkipFrontend) {
    $node = node --version
    if ($LASTEXITCODE -ne 0) { throw 'Node requerido para compilar la UI.' }
    $version = [version]$node.TrimStart('v')
    if (-not (($version.Major -eq 22 -and $version -ge [version]'22.22.3') -or
        ($version.Major -eq 24 -and $version -ge [version]'24.0.0') -or $version.Major -ge 26)) {
        throw 'Angular requiere Node 22.22.3+, 24.15.0+ o 26+.'
    }
    Push-Location (Join-Path $root 'src/frontend')
    try { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci falló.' }; npm run build; if ($LASTEXITCODE -ne 0) { throw 'Angular build falló.' } }
    finally { Pop-Location }
}
dotnet build (Join-Path $root 'MercadoInteligente.slnx') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Backend build falló.' }
$api = Join-Path $root 'src/backend/MercadoInteligente.Api/bin/Release/net10.0/MercadoInteligente.Api.dll'
dotnet $api --prepare-database
if ($LASTEXITCODE -ne 0) { throw 'Preparación SQLite falló; revise el backup previo y la integridad antes de iniciar.' }
Write-Host 'Bootstrap listo. SQLite migrada con backup previo e integridad verificada; iniciar con scripts/run-app.ps1.'
