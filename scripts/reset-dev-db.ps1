param([string]$DataDir = $(if ($env:MARKET_DATA_DIR) { $env:MARKET_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'MercadoInteligente' }))
$ErrorActionPreference = 'Stop'
$database = Join-Path $DataDir 'data/market.db'
Write-Host "Base de datos local: $database"
if (-not (Test-Path -LiteralPath $database -PathType Leaf)) { Write-Host 'No existe una DB para eliminar.'; exit 0 }
if ((Get-Item -LiteralPath $database).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se admiten enlaces de DB.' }
$listener = @(Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue)
if ($listener.Count -gt 0) { throw 'Detenga primero la API; el puerto 5000 está ocupado.' }
$apiProcesses = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'MercadoInteligente\.Api(?:\.dll|\.exe)' })
if ($apiProcesses.Count -gt 0) { throw 'Detenga primero todos los procesos de la API.' }
$answer = Read-Host 'Escriba ELIMINAR para borrar la DB local y sus sidecars (haga backup primero)'
if ($answer -cne 'ELIMINAR') { throw 'Cancelado.' }
foreach ($path in @($database, "$database-wal", "$database-shm")) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Write-Host 'DB de desarrollo eliminada. Los backups y RAW permanecen intactos.'
