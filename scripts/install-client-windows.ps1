#Requires -Version 5.1
<#
.SYNOPSIS
  One-shot WorkshopOS Windows client build (zip + optional Setup.exe).

.DESCRIPTION
  Pulls latest from GitHub (or overlays agent zip), wipes obj/bin, runs
  packaging\build-client.ps1, extracts the zip to Desktop, launches the app.

.EXAMPLE
  # Developer PowerShell for VS 2022 (recommended):
  powershell -ExecutionPolicy Bypass -File C:\Users\ayden\src\repairos\scripts\install-client-windows.ps1

  # Also build Inno Setup installer (needs Inno Setup 6):
  powershell -ExecutionPolicy Bypass -File .\scripts\install-client-windows.ps1 -WithInstaller

.NOTES
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.
  Needs .NET 8 SDK + WinUI / Windows App SDK workload.
#>
param(
  [string]$Repo = 'C:\Users\ayden\src\repairos',
  [string]$Version = '1.2.30',
  [string]$Branch = 'main',
  [switch]$WithInstaller,
  [switch]$UseAgentOverlay,
  [string]$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip',
  [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'
$ScriptVersion = 'v1'

Write-Host "WorkshopOS Windows client installer $ScriptVersion" -ForegroundColor Cyan
Write-Host ("  Repo={0}  Version={1}  Installer={2}" -f $Repo, $Version, [bool]$WithInstaller)

Get-Process -Name 'WorkshopOS.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

if (-not (Test-Path $Repo)) {
  New-Item -ItemType Directory -Force -Path $Repo | Out-Null
}

Set-Location $Repo

if ($UseAgentOverlay) {
  $zip = Join-Path $env:TEMP 'repairos-github-sync.zip'
  Write-Host ("Downloading {0} ..." -f $AgentSyncUrl) -ForegroundColor Yellow
  Invoke-WebRequest -Uri $AgentSyncUrl -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $Repo -Force
} else {
  if (-not (Test-Path (Join-Path $Repo '.git'))) {
    Write-Host 'Cloning https://github.com/Ayden0726/repairos.git ...' -ForegroundColor Yellow
    git clone https://github.com/Ayden0726/repairos.git $Repo
    Set-Location $Repo
  }
  git fetch origin
  git checkout $Branch
  git pull --ff-only origin $Branch
  if ($LASTEXITCODE -ne 0) {
    Write-Host 'ff-only pull failed; resetting to origin/' -NoNewline -ForegroundColor Yellow
    Write-Host $Branch -ForegroundColor Yellow
    git fetch origin
    git reset --hard ("origin/{0}" -f $Branch)
  }
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
  throw 'Install .NET 8 SDK x64 from https://dotnet.microsoft.com/download/dotnet/8.0 then open a NEW Developer PowerShell.'
}

$gj = Join-Path $Repo 'global.json'
if (-not (Test-Path $gj)) { throw 'Missing global.json - pull/clone failed.' }
Write-Host ("dotnet --version (expect 8.x): {0}" -f (& dotnet --version))

$cspPath = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
$csp = Get-Content $cspPath -Raw
if ($csp -notmatch ('<Version>{0}</Version>' -f [regex]::Escape($Version))) {
  throw ("Client csproj is not {0} after sync." -f $Version)
}
if ($csp -notmatch 'GlobalPropertiesToRemove') {
  throw 'Client csproj missing GlobalPropertiesToRemove fix - update from GitHub main.'
}
if (-not (Test-Path (Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\PosPage.xaml'))) {
  throw 'PosPage.xaml missing - update from GitHub main.'
}

Write-Host 'Building client...' -ForegroundColor Cyan
$buildArgs = @(
  '-NoProfile', '-ExecutionPolicy', 'Bypass',
  '-File', (Join-Path $Repo 'packaging\build-client.ps1'),
  '-Configuration', 'Release',
  '-Version', $Version
)
if (-not $WithInstaller) { $buildArgs += '-SkipInstaller' }

powershell @buildArgs
if ($LASTEXITCODE -ne 0) { throw ("build-client.ps1 failed (exit {0})" -f $LASTEXITCODE) }

$builtZip = Join-Path $Repo ("packaging\dist\WorkshopOS-Client-win-x64-v{0}.zip" -f $Version)
if (-not (Test-Path $builtZip)) { throw ("Missing {0}" -f $builtZip) }

$setupExe = Join-Path $Repo ("packaging\dist\WorkshopOS-Setup-{0}.exe" -f $Version)
if ($WithInstaller) {
  if (Test-Path $setupExe) {
    Write-Host ("Setup.exe: {0}" -f $setupExe) -ForegroundColor Green
  } else {
    Write-Host 'WARN: -WithInstaller set but Setup.exe missing (install Inno Setup 6 and re-run).' -ForegroundColor Yellow
  }
}

$newRun = Join-Path $env:USERPROFILE ("Desktop\WorkshopOS-Client-{0}" -f $Version)
if (Test-Path $newRun) { Remove-Item -Recurse -Force $newRun }
New-Item -ItemType Directory -Force -Path $newRun | Out-Null
Expand-Archive -Path $builtZip -DestinationPath $newRun -Force
$exe = Join-Path $newRun 'WorkshopOS.Client.exe'
if (-not (Test-Path $exe)) { throw ("WorkshopOS.Client.exe missing under {0}" -f $newRun) }

Get-Item $exe | Format-List FullName, Length, LastWriteTime
if (-not $SkipLaunch) { Start-Process $exe }

Write-Host ''
Write-Host ("OK - client zip: {0}" -f $builtZip) -ForegroundColor Green
Write-Host ("OK - extracted:  {0}" -f $exe) -ForegroundColor Green
if (Test-Path $setupExe) {
  Write-Host ("OK - Setup.exe:  {0}" -f $setupExe) -ForegroundColor Green
}
Write-Host 'Pair to http://127.0.0.1:5088/connect (WOS-XXXX), then enable POS under Settings if needed.'
