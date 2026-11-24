$root = Split-Path -Parent $PSScriptRoot
Start-Process dotnet -ArgumentList @('run', '--project', (Join-Path $root 'src/backend/MercadoInteligente.Api'))
Set-Location (Join-Path $root 'src/frontend')
npm start -- --proxy-config proxy.conf.json
