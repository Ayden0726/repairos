#Requires -Version 5.1
<#
.SYNOPSIS
  One-shot WorkshopOS client 1.2.30 rebuild for a new Windows PC.
  Overlays agent sync zip, wipes obj/bin, publishes, extracts, launches.

.NOTES
  Edit $Repo if needed. Prefer "Developer PowerShell for VS 2022/18".
  Requires .NET 8 SDK (dotnet --list-sdks must show 8.x).
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.
#>
$ErrorActionPreference = 'Stop'

# ========== EDIT IF NEEDED ==========
$Repo   = 'C:\Users\ayden\src\repairos'
$NewRun = Join-Path $env:USERPROFILE 'Desktop\WorkshopOS-Client-1.2.30'
$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip'
$UseAgentOverlay = $true
$Version = '1.2.30'
# ====================================

Write-Host '=== WorkshopOS Client 1.2.30 - wipe / overlay / build / launch ===' -ForegroundColor Cyan

Get-Process -Name 'WorkshopOS.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

if (-not (Test-Path $Repo)) {
  New-Item -ItemType Directory -Force -Path $Repo | Out-Null
}

Set-Location $Repo

if ($UseAgentOverlay) {
  $zip = Join-Path $env:TEMP 'repairos-github-sync.zip'
  Write-Host "Downloading $AgentSyncUrl ..." -ForegroundColor Yellow
  Invoke-WebRequest -Uri $AgentSyncUrl -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $Repo -Force
} else {
  git fetch origin
  git checkout main
  git pull origin main
}

Write-Host 'Wiping obj/bin + known orphan controls...' -ForegroundColor Yellow
$views = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views'
Remove-Item -Force -ErrorAction SilentlyContinue `
  (Join-Path $views 'ServicePickerControl.xaml'), `
  (Join-Path $views 'ServicePickerControl.xaml.cs')
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  (Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\obj'), `
  (Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\bin'), `
  (Join-Path $Repo 'services\shared\WorkshopOS.Contracts\obj'), `
  (Join-Path $Repo 'services\shared\WorkshopOS.Contracts\bin')

$sdks = & dotnet --list-sdks
Write-Host 'Installed SDKs:'
$sdks
if (-not ($sdks | Where-Object { $_ -match '^\s*8\.' })) {
  throw 'Install .NET 8 SDK x64 from https://dotnet.microsoft.com/download/dotnet/8.0 then open a NEW shell.'
}

$gj = Join-Path $Repo 'global.json'
if (-not (Test-Path $gj)) { throw 'Missing global.json after overlay - sync zip is stale.' }
Write-Host ("dotnet --version (expect 8.x): {0}" -f (& dotnet --version))

$csp = Get-Content (Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj') -Raw
if ($csp -notmatch '<Version>1\.2\.30</Version>') {
  throw 'Client csproj is not 1.2.30 after overlay - wrong zip.'
}
if ($csp -notmatch 'GlobalPropertiesToRemove') {
  throw 'Client csproj missing GlobalPropertiesToRemove fix - sync zip is stale. Re-download.'
}
if (-not (Test-Path (Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\PosPage.xaml'))) {
  throw 'PosPage.xaml missing - sync zip is stale.'
}

Write-Host 'Building...' -ForegroundColor Cyan
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 `
  -Configuration Release -Version $Version -SkipInstaller

$built = Join-Path $Repo ("packaging\dist\WorkshopOS-Client-win-x64-v{0}.zip" -f $Version)
if (-not (Test-Path $built)) { throw ("Missing {0}" -f $built) }

if (Test-Path $NewRun) { Remove-Item -Recurse -Force $NewRun }
New-Item -ItemType Directory -Force -Path $NewRun | Out-Null
Expand-Archive -Path $built -DestinationPath $NewRun -Force
$exe = Join-Path $NewRun 'WorkshopOS.Client.exe'
if (-not (Test-Path $exe)) { throw ("WorkshopOS.Client.exe missing under {0}" -f $NewRun) }
Get-Item $exe | Format-List FullName, Length, LastWriteTime
Start-Process $exe

Write-Host ''
Write-Host ("OK - running {0}" -f $exe) -ForegroundColor Green
Write-Host 'Then: pair to API -> Settings -> POS -> Enable -> reload shell.'
Write-Host ("Zip also at: {0}" -f $built)
