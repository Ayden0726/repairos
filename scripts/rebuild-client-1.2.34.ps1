#Requires -Version 5.1
# Rebuild Windows client matching ClientMinVersion 1.2.34 (API 1.2.34).
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$Client = Join-Path $Root "apps\windows-client\WorkshopOS.Client"
$ConnectXaml = Join-Path $Client "Views\ServerConnectPage.xaml"
$Csproj = Join-Path $Client "WorkshopOS.Client.csproj"
$Contracts = Join-Path $Root "services\shared\WorkshopOS.Contracts\Common\CommonDtos.cs"
$xaml = Get-Content $ConnectXaml -Raw
$proj = Get-Content $Csproj -Raw
$dto = Get-Content $Contracts -Raw
if ($xaml -notmatch 'Client 1\.2\.34') { throw "ServerConnectPage.xaml banner is not Client 1.2.34" }
if ($proj -notmatch '<Version>1\.2\.34</Version>') { throw "csproj Version is not 1.2.34" }
if ($dto -notmatch 'Api = "1\.2\.34"') { throw "ProductVersions.Api is not 1.2.34" }
if ($dto -notmatch 'ClientMinimum = "1\.2\.34"') { throw "ProductVersions.ClientMinimum is not 1.2.34" }

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$Client\obj", "$Client\bin"

Write-Host "==> Building WorkshopOS client 1.2.34"
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root "packaging\build-client.ps1") `
  -Configuration Release -Version 1.2.34

Write-Host ""
Write-Host "  Output under packaging\dist\"
Write-Host "  Connect banner / Settings must show: Client 1.2.34"
Write-Host "  Upload Setup.exe / zip to GitHub Release v1.2.34 if not using Actions."
Write-Host ""
