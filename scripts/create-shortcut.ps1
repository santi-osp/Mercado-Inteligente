<#
.SYNOPSIS
    Genera el acceso directo .lnk y los comandos de inicio rapido en la raiz del proyecto.
#>
[CmdletBinding()]
param(
    [switch]$Desktop
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$launcherPs1 = Join-Path $root "scripts\launch-mercado-inteligente.ps1"
$stopPs1 = Join-Path $root "scripts\stop-mercado-inteligente.ps1"
$iconPath = Join-Path $root "src\frontend\public\favicon.ico"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   CREANDO ACCESOS DIRECTOS Y LANZADORES" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Crear comandos CMD en raiz
$startCmdContent = @"
@echo off
title Mercado Inteligente v1.0.0
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0scripts\launch-mercado-inteligente.ps1"
"@

$stopCmdContent = @"
@echo off
title Deteniendo Mercado Inteligente
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-mercado-inteligente.ps1"
pause
"@

$startCmdPath = Join-Path $root "INICIAR_MERCADO_INTELIGENTE.cmd"
$stopCmdPath = Join-Path $root "DETENER_MERCADO_INTELIGENTE.cmd"

Set-Content -LiteralPath $startCmdPath -Value $startCmdContent -Encoding ascii
Set-Content -LiteralPath $stopCmdPath -Value $stopCmdContent -Encoding ascii
Write-Host "[OK] Creado helper CMD: INICIAR_MERCADO_INTELIGENTE.cmd" -ForegroundColor Green
Write-Host "[OK] Creado helper CMD: DETENER_MERCADO_INTELIGENTE.cmd" -ForegroundColor Green

# 2. Crear acceso directo Windows .lnk usando WScript.Shell
$wsh = New-Object -ComObject WScript.Shell

$shortcutPath = Join-Path $root "Mercado Inteligente.lnk"
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "powershell.exe"
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$launcherPs1`""
$shortcut.WorkingDirectory = $root
$shortcut.Description = "Mercado Inteligente v1.0.0 - Optimizador de Canasta Local-First"
if (Test-Path -LiteralPath $iconPath) {
    $shortcut.IconLocation = "$iconPath,0"
}
$shortcut.Save()
Write-Host "[OK] Creado acceso directo: $shortcutPath" -ForegroundColor Green

if ($Desktop) {
    $desktopDir = [Environment]::GetFolderPath("Desktop")
    if (Test-Path -LiteralPath $desktopDir) {
        $desktopLnkPath = Join-Path $desktopDir "Mercado Inteligente.lnk"
        $desktopLnk = $wsh.CreateShortcut($desktopLnkPath)
        $desktopLnk.TargetPath = "powershell.exe"
        $desktopLnk.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$launcherPs1`""
        $desktopLnk.WorkingDirectory = $root
        $desktopLnk.Description = "Mercado Inteligente v1.0.0 - Optimizador de Canasta Local-First"
        if (Test-Path -LiteralPath $iconPath) {
            $desktopLnk.IconLocation = "$iconPath,0"
        }
        $desktopLnk.Save()
        Write-Host "[OK] Creado acceso directo en Escritorio: $desktopLnkPath" -ForegroundColor Green
    }
}
