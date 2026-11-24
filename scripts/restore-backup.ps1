param(
    [Parameter(Mandatory = $true)][string]$BackupName,
    [string]$DataDir = $(if ($env:MARKET_DATA_DIR) { $env:MARKET_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'MercadoInteligente' })
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$isZip = $BackupName -cmatch '^market-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.zip$'
$isDb = $BackupName -cmatch '^market-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.db$'
if (-not ($isZip -or $isDb)) { throw 'Nombre de backup inválido.' }
$database = Join-Path $DataDir 'data/market.db'
$backup = Join-Path $DataDir (Join-Path 'backups' $BackupName)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw 'Backup no encontrado.' }
if ((Get-Item -LiteralPath $backup).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se admiten enlaces.' }
# The API must not be serving requests or holding SQLite connections during replacement.
$apiProcesses = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'MercadoInteligente\.Api(?:\.dll|\.exe|/|\\)' })
if ($apiProcesses.Count -gt 0) { throw 'Detenga la aplicación/API antes de restaurar.' }
$listener = @(Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue)
if ($listener.Count -gt 0) { throw 'El puerto de la API sigue ocupado. Detenga la aplicación.' }
Write-Host "Backup: $backup"
Write-Host "Destino: $database"
if ($isZip) {
    Write-Warning 'La restauracion sustituye la base actual. Se guardara una copia preventiva en backups. RAW se restaura desde el archivo portable.'
} else {
    Write-Warning 'La restauracion sustituye la base actual. Se guardara una copia preventiva en backups. RAW no se restaura (requiere RAW local existente).'
}
$answer = Read-Host 'Escriba RESTAURAR para continuar'
if ($answer -cne 'RESTAURAR') { throw 'Restauración cancelada.' }
# Build a tiny caller outside the data/backup tree; backup contents are never executed.
$runner = Join-Path ([IO.Path]::GetTempPath()) ('market-restore-runner-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runner | Out-Null
try {
    $project = Join-Path $runner 'RestoreRunner.csproj'
    $reference = [System.Security.SecurityElement]::Escape((Join-Path $repo 'src/backend/MercadoInteligente.Infrastructure/MercadoInteligente.Infrastructure.csproj'))
    [IO.File]::WriteAllText($project, "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=`"$reference`" /></ItemGroup></Project>")
    [IO.File]::WriteAllText((Join-Path $runner 'Program.cs'), @'
using System;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;
var paths = new LocalPaths(args[0]);
var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite($"Data Source={paths.Database}").Options;
using var db = new MarketDb(options);
if (args[1].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
    await new BackupService(paths, db).RestoreArchiveAsync(args[1]);
else
    await new BackupService(paths, db).RestoreAsync(args[1]);
Console.WriteLine("Restauración completada. Reinicie la aplicación y compruebe su health endpoint.");
'@)
    dotnet run --project $project -- $DataDir $BackupName
    if ($LASTEXITCODE -ne 0) { throw 'La restauración falló; revise el error. La DB existente se conservó si falló la validación.' }
}
finally { Remove-Item -LiteralPath $runner -Recurse -Force -ErrorAction SilentlyContinue }
