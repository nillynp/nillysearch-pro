# ============================================================================
# NillySearch Pro — Clean Uninstaller (Zero Admin · HKCU only)
# created by Nilly graphics (Sotiris Christos) — https://nillygraphics.com/
# ============================================================================
param([switch]$Silent)

$ErrorActionPreference = 'SilentlyContinue'
if (Test-Path 'HKCU:\Software\Classes\Applications\NillySearchPro.exe') {
    Remove-Item -Path 'HKCU:\Software\Classes\Applications\NillySearchPro.exe' -Recurse -Force
}
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'NillySearch Pro.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Nilly graphics\NillySearch Pro.lnk') -Force
Remove-Item (Join-Path $env:LOCALAPPDATA 'NillySuite\.nillysearch_index.bin') -Force
if (-not $Silent) { Write-Host '  Το NillySearch Pro απεγκαταστάθηκε καθαρά.' -ForegroundColor Green }
