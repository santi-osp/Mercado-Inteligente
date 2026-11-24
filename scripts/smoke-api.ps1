$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root 'src/backend/MercadoInteligente.Api/bin/Debug/net10.0/MercadoInteligente.Api.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Compile el backend antes de ejecutar el smoke.' }
$listener = @(Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue)
if ($listener.Count -gt 0) { throw 'El puerto 5000 ya está ocupado; no se puede aislar el smoke.' }
$tempDir = Join-Path $env:TEMP ('mercado-smoke-' + [guid]::NewGuid().ToString('N'))
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = 'dotnet'
$start.Arguments = '"' + $dll + '"'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.EnvironmentVariables['MARKET_DATA_DIR'] = $tempDir
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $start
try {
    if (-not $process.Start()) { throw 'No se pudo iniciar la API.' }
    $result = $null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        if ($process.HasExited) { throw "La API terminó antes del health check (exit $($process.ExitCode))." }
        try {
            $result = Invoke-RestMethod 'http://127.0.0.1:5000/api/v1/system/health' -ErrorAction Stop
            if ($result.database -eq 'ok') { break }
        } catch { }
    }
    if (-not $result -or $result.database -ne 'ok' -or -not $result.foreignKeysValid) { throw 'Health or foreign-key check failed.' }
    $base = 'http://127.0.0.1:5000/api/v1'
    $settings = Invoke-RestMethod "$base/settings"
    if ($settings.maxStores -ne 2 -or $settings.aiEnabled) { throw 'Los ajustes iniciales no son los esperados.' }
    $settings.maxStores = 3
    $updated = Invoke-RestMethod "$base/settings" -Method Put -Body ($settings | ConvertTo-Json -Depth 5) -ContentType 'application/json'
    if ($updated.maxStores -ne 3 -or (Invoke-RestMethod "$base/settings").maxStores -ne 3) { throw 'Los ajustes no persistieron.' }
    $basket = Invoke-RestMethod "$base/baskets" -Method Post -Body '{}' -ContentType 'application/json'
    $item = Invoke-RestMethod "$base/baskets/$($basket.id)/items" -Method Post -Body '{"query":"Arroz","quantity":2}' -ContentType 'application/json'
    if (-not $item.id -or @((Invoke-RestMethod "$base/baskets/$($basket.id)").items).Count -ne 1) { throw 'La cesta no persistió.' }
    $backup = Invoke-RestMethod "$base/backups" -Method Post -Body '{}' -ContentType 'application/json'
    if (-not $backup.file -or -not (Test-Path -LiteralPath (Join-Path $tempDir (Join-Path 'backups' $backup.file)) -PathType Leaf)) { throw 'La copia de seguridad no se creó.' }
    # Invoke-WebRequest throws on 403 in Windows PowerShell 5.1; verify the status explicitly.
    foreach ($case in @(
        @{ Name = 'hostile Host GET'; Method = 'GET'; Path = '/api/v1/system/health'; Headers = @{ Host = 'evil.example' } },
        @{ Name = 'hostile Host POST'; Method = 'POST'; Path = '/api/v1/baskets'; Headers = @{ Host = 'evil.example' } },
        @{ Name = 'hostile Origin POST'; Method = 'POST'; Path = '/api/v1/baskets'; Headers = @{ Origin = 'https://evil.example' } }
    )) {
        $status = 0
        try {
            $response = Invoke-WebRequest -Uri ('http://127.0.0.1:5000' + $case.Path) -Method $case.Method -Headers $case.Headers -UseBasicParsing -ErrorAction Stop
            $status = [int]$response.StatusCode
        } catch {
            if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode } else { throw }
        }
        if ($status -ne 403) { throw "$($case.Name): esperaba HTTP 403, recibió $status." }
    }
    $result | ConvertTo-Json
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $process.Dispose()
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
