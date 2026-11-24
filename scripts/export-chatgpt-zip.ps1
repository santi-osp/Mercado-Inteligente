[CmdletBinding()]
param(
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"

$startDir = if ($PSScriptRoot) { $PSScriptRoot } else { $PWD.Path }
$currentDir = $startDir

while ($currentDir -and (Test-Path -LiteralPath $currentDir)) {
    if (Test-Path -LiteralPath (Join-Path $currentDir "MercadoInteligente.slnx")) {
        break
    }
    $parent = Split-Path $currentDir -Parent
    if (-not $parent -or $parent -eq $currentDir) { break }
    $currentDir = $parent
}

$repoRoot = if (Test-Path -LiteralPath (Join-Path $currentDir "MercadoInteligente.slnx")) { $currentDir } else { $startDir }

if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $resolvedDest = Join-Path $repoRoot "Mercado_Inteligente_Source_v1.0.0.zip"
} else {
    if ([System.IO.Path]::IsPathRooted($OutputFile)) {
        $resolvedDest = $OutputFile
    } else {
        $resolvedDest = Join-Path $repoRoot $OutputFile
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Empaquetador de Mercado Inteligente (v1.0.0)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Raiz del repositorio: $repoRoot"
Write-Host "Destino del archivo : $resolvedDest"

if (Test-Path -LiteralPath $resolvedDest) {
    Remove-Item -LiteralPath $resolvedDest -Force
}

$excludeArgs = @(
    "--exclude=*.git*",
    "--exclude=*node_modules*",
    "--exclude=*/bin/*",
    "--exclude=*/bin",
    "--exclude=*/obj/*",
    "--exclude=*/obj",
    "--exclude=*.angular*",
    "--exclude=*artifacts*",
    "--exclude=*release/*",
    "--exclude=*release",
    "--exclude=*source_archives*",
    "--exclude=*.vs*",
    "--exclude=*.vscode*",
    "--exclude=*.zip",
    "--exclude=*.sqlite",
    "--exclude=*.db",
    "--exclude=*.log"
)

Push-Location $repoRoot
try {
    tar.exe -a -c -f "$resolvedDest" @excludeArgs *
} finally {
    Pop-Location
}

if (Test-Path -LiteralPath $resolvedDest) {
    $item = Get-Item -LiteralPath $resolvedDest
    $sizeMB = [math]::Round($item.Length / 1MB, 2)
    Write-Host "`n[OK] Archivo ZIP generado exitosamente:" -ForegroundColor Green
    Write-Host "Ubicacion: $($item.FullName)" -ForegroundColor Yellow
    Write-Host "Tamano   : $sizeMB MB" -ForegroundColor Yellow
} else {
    Write-Error "Error: No se encontro el archivo generado en $resolvedDest"
}
