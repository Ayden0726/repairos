#Requires -Version 5.1
# Rebuild Windows client matching ClientMinVersion 1.2.32 (API 1.2.33).
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Client = Join-Path $Root "apps\windows-client\WorkshopOS.Client"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$Client\obj", "$Client\bin"

Write-Host "==> Building WorkshopOS client 1.2.32"
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root "packaging\build-client.ps1") `
  -Configuration Release -Version 1.2.32

Write-Host ""
Write-Host "  Output under packaging\dist\"
Write-Host "  Upload Setup.exe / zip to GitHub Release v1.2.33 if not using Actions."
Write-Host ""
