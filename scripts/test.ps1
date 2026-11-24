$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root 'MercadoInteligente.slnx')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Push-Location (Join-Path $root 'src/frontend')
try {
    $required = '^22.22.3 || ^24.15.0 || >=26.0.0'
    $version = node --version
    $major, $minor, $patch = $version.TrimStart('v').Split('.') | ForEach-Object { [int]$_ }
    if (-not (($major -eq 22 -and ($minor -gt 22 -or ($minor -eq 22 -and $patch -ge 3))) -or
               ($major -eq 24 -and $minor -ge 15) -or $major -ge 26)) {
        throw "Node $version no cumple $required. Instale la versión de .nvmrc antes de ejecutar tests."
    }
    npm test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }
