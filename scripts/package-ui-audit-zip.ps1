$ErrorActionPreference = "Stop"

$root = $PSScriptRoot ? (Split-Path $PSScriptRoot -Parent) : (Get-Location).Path
$auditDir = Join-Path $root "docs\ui-audit\current"
$zipPath = Join-Path $root "Mercado_Inteligente_UI_Audit.zip"

Write-Host "Empaquetando capturas de auditoría visual desde: $auditDir" -ForegroundColor Cyan

if (-not (Test-Path $auditDir)) {
    Write-Error "El directorio $auditDir no existe."
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$files = Get-ChildItem -Path $auditDir -File
Write-Host "Total de archivos a incluir en el ZIP: $($files.Count)" -ForegroundColor Green

Compress-Archive -Path "$auditDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

$zipItem = Get-Item $zipPath
Write-Host "ZIP generado con éxito: $zipPath ($([math]::Round($zipItem.Length / 1MB, 2)) MB)" -ForegroundColor Green
