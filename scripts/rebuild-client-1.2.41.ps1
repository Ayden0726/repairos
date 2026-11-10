$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Write-Host "==> Building WorkshopOS client 1.2.41"
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root "packaging\build-client.ps1") -Configuration Release -Version 1.2.41
