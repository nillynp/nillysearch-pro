@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall_NillySearchPro.ps1"
if errorlevel 1 pause
