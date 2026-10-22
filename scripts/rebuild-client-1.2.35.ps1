#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root
$Client = Join-Path $Root "apps\windows-client\WorkshopOS.Client"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$Client\obj", "$Client\bin"
Write-Host "==> Building WorkshopOS client 1.2.35"
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root "packaging\build-client.ps1") -Configuration Release -Version 1.2.35
