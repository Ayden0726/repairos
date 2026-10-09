#Requires -Version 5.1
<#
.SYNOPSIS
  Definitive WorkshopOS client 1.2.20 rebuild: kill process, wipe ALL local data,
  pull/overlay latest source, verify ServerConnect is initial page, build, extract, launch.

.NOTES
  Edit $Repo below. Prefer "Developer PowerShell for VS 2022".
  You MUST see Connect / pairing UI and version banner Client 1.2.20.
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.

  If publish fails with XamlCompiler / exit 1:
    Get-Content packaging\out\logs\publish-last.log -Tail 80
    Look for lines with error / XamlCompiler / WMC / CS.
#>
$ErrorActionPreference = 'Stop'

# ========== EDIT IF NEEDED ==========
$Repo   = 'C:\Users\ayden\src\repairos'
$NewRun = Join-Path $env:USERPROFILE 'Desktop\WorkshopOS-Client-1.2.20'
$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip'   # ServicePicker XamlCompiler-safe rewrite overlay
$UseAgentOverlay = $true   # pull agent zip (no DataTemplate/x:Bind/NumberBox picker) before build
# ====================================

Write-Host '=== WorkshopOS Client 1.2.20 - wipe / verify / build / launch ===' -ForegroundColor Cyan

# 1) Kill any running client
Get-Process -Name 'WorkshopOS.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 2) Clear ALL local persistence that can force auto-connect / bootstrap
Write-Host 'Clearing local client data...' -ForegroundColor Yellow
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\Packages\*WorkshopOS*"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\Packages\*workshopos*"
# Credential Manager / PasswordVault leftovers (best-effort)
cmdkey /list 2>$null | Select-String -Pattern 'WorkshopOS' | ForEach-Object {
  if ($_ -match 'Target:\s*(.+)$') {
    cmdkey /delete:$($Matches[1].Trim()) 2>$null | Out-Null
  }
}

# 3) Sync source
if (-not (Test-Path $Repo)) { throw "Repo not found: $Repo - edit `$Repo at top of script." }
Set-Location $Repo

if ($UseAgentOverlay) {
  $zip = Join-Path $env:TEMP 'repairos-github-sync.zip'
  Write-Host "Overlay from $AgentSyncUrl ..." -ForegroundColor Yellow
  Invoke-WebRequest -Uri $AgentSyncUrl -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $Repo -Force
} else {
  git fetch origin
  git checkout main
  git pull origin main
}

# 4) Verify ServerConnect is ALWAYS the initial page + version 1.2.20 in source
$mw  = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\MainWindow.xaml.cs'
$scx = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\ServerConnectPage.xaml'
$csp = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
$bps = Join-Path $Repo 'packaging\build-client.ps1'
$picker = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml'
$main = Get-Content $mw -Raw
$xaml = Get-Content $scx -Raw
$proj = Get-Content $csp -Raw
$bld  = Get-Content $bps -Raw
$pick = Get-Content $picker -Raw

$checks = [ordered]@{
  'MainWindow navigates ServerConnectPage' = ($main -match 'Navigate\(typeof\(Views\.ServerConnectPage\)\)')
  'MainWindow does NOT navigate Bootstrap first' = ($main -notmatch 'Navigate\(typeof\(Views\.BootstrapPage\)\)')
  'ServerConnect banner Client 1.2.20' = ($xaml -match 'Client 1\.2\.20')
  'csproj Version 1.2.20' = ($proj -match '<Version>1\.2\.20</Version>')
  'build-client.ps1 default 1.2.20' = ($bld -match 'Version\s*=\s*"1\.2\.20"')
  'build-client.ps1 script v6' = ($bld -match 'ScriptVersion\s*=\s*"v6"')
  'ServicePicker has no DataTemplate element' = ($pick -notmatch '<DataTemplate')
  'ServicePicker has no compiled bind markup' = ($pick -notmatch '\{x:Bind')
  'ServicePicker has no NumberBox element' = ($pick -notmatch '<NumberBox')
}

$allOk = $true
foreach ($k in $checks.Keys) {
  $ok = [bool]$checks[$k]
  "{0}: {1}" -f $k, $ok
  if (-not $ok) { $allOk = $false }
}
if (-not $allOk) {
  throw 'Source is OLD or wrong - pull/overlay failed. Do not rebuild. Set $UseAgentOverlay=$true and retry.'
}

# 5) Build 1.2.20 (with or without installer - SkipInstaller by default; remove switch for Inno)
Set-Location $Repo
Write-Host 'Building client 1.2.20 (script v6)...' -ForegroundColor Cyan
try {
  powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.20 -SkipInstaller
} catch {
  Write-Host ''
  Write-Host 'PUBLISH FAILED. Dump last 80 lines of publish log:' -ForegroundColor Red
  $log = Join-Path $Repo 'packaging\out\logs\publish-last.log'
  if (Test-Path $log) {
    Get-Content $log -Tail 80
    Write-Host ''
    Write-Host 'Tip: Get-Content packaging\out\logs\publish-last.log -Tail 80' -ForegroundColor Yellow
    Write-Host 'Look for error / XamlCompiler / WMC / CS lines.' -ForegroundColor Yellow
  } else {
    Write-Host "(no log at $log)" -ForegroundColor DarkYellow
  }
  throw
}
# For installer instead: omit -SkipInstaller (needs Inno Setup 6)
# powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.20

$built = Join-Path $Repo 'packaging\dist\WorkshopOS-Client-win-x64-v1.2.20.zip'
if (-not (Test-Path $built)) { throw "Missing $built - build failed or still produced an older version zip." }

# 6) Extract to Desktop\WorkshopOS-Client-1.2.20 and launch THAT exe only
if (Test-Path $NewRun) { Remove-Item -Recurse -Force $NewRun }
New-Item -ItemType Directory -Force -Path $NewRun | Out-Null
Expand-Archive -Path $built -DestinationPath $NewRun -Force
$exe = Join-Path $NewRun 'WorkshopOS.Client.exe'
if (-not (Test-Path $exe)) { throw "WorkshopOS.Client.exe missing under $NewRun" }
Get-Item $exe | Format-List FullName, Length, LastWriteTime
Start-Process $exe

Write-Host ''
Write-Host 'You must see Connect / pairing UI and version 1.2.20' -ForegroundColor Green
Write-Host '  - Green banner: Client 1.2.20'
Write-Host '  - Buttons: Connect with code / Find on this network / Connect'
Write-Host '  - NO "Connecting to server..." splash'
Write-Host "  - Running from: $exe"
Write-Host ''
