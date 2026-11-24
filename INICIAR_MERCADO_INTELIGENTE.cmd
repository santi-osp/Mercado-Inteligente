@echo off
title Mercado Inteligente v1.0.0
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0scripts\launch-mercado-inteligente.ps1" %*
