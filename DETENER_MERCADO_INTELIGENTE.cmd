@echo off
title Deteniendo Mercado Inteligente
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-mercado-inteligente.ps1"
pause
