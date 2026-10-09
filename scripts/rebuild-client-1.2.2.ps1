#Requires -Version 5.1
<#
.SYNOPSIS
  Definitive WorkshopOS client 1.2.2 rebuild: kill process, wipe ALL local data,
  pull/overlay latest source, verify ServerConnect is initial page, build, extract, launch.

.NOTES
  Edit $Repo below. Prefer "Developer PowerShell for VS 2022".
  You MUST see Connect / pairing UI and version banner Client 1.2.2.
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.
#>
$ErrorActionPreference = 'Stop'

# ========== EDIT IF NEEDED ==========
$Repo   = 'C:\Users\ayden\src\repairos'
$NewRun = Join-Path $env:USERPROFILE 'Desktop\WorkshopOS-Client-1.2.2'
$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip'   # optional overlay if GitHub stale
$UseAgentOverlay = $false   # set $true to force zip overlay from agent :28765
# ====================================

Write-Host '=== WorkshopOS Client 1.2.2 - wipe / verify / build / launch ===' -ForegroundColor Cyan

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

# 4) Verify ServerConnect is ALWAYS the initial page + version 1.2.2 in source
$mw  = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\MainWindow.xaml.cs'
$scx = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\ServerConnectPage.xaml'
$csp = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
$bps = Join-Path $Repo 'packaging\build-client.ps1'
$main = Get-Content $mw -Raw
$xaml = Get-Content $scx -Raw
$proj = Get-Content $csp -Raw
$bld  = Get-Content $bps -Raw

$checks = [ordered]@{
  'MainWindow navigates ServerConnectPage' = ($main -match 'Navigate\(typeof\(Views\.ServerConnectPage\)\)')
  'MainWindow does NOT navigate Bootstrap first' = ($main -notmatch 'Navigate\(typeof\(Views\.BootstrapPage\)\)')
  'ServerConnect banner Client 1.2.2' = ($xaml -match 'Client 1\.2\.2')
  'csproj Version 1.2.2' = ($proj -match '<Version>1\.2\.2</Version>')
  'build-client.ps1 default 1.2.2' = ($bld -match 'Version\s*=\s*"1\.2\.2"')
  'build-client.ps1 script v5' = ($bld -match 'ScriptVersion\s*=\s*"v5"')
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

# 5) Build 1.2.2 (with or without installer - SkipInstaller by default; remove switch for Inno)
Set-Location $Repo
Write-Host 'Building client 1.2.2 (script v5)...' -ForegroundColor Cyan
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.2 -SkipInstaller
# For installer instead: omit -SkipInstaller (needs Inno Setup 6)
# powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.2

$built = Join-Path $Repo 'packaging\dist\WorkshopOS-Client-win-x64-v1.2.2.zip'
if (-not (Test-Path $built)) { throw "Missing $built - build failed or still produced an older version zip." }

# 6) Extract to Desktop\WorkshopOS-Client-1.2.2 and launch THAT exe only
if (Test-Path $NewRun) { Remove-Item -Recurse -Force $NewRun }
New-Item -ItemType Directory -Force -Path $NewRun | Out-Null
Expand-Archive -Path $built -DestinationPath $NewRun -Force
$exe = Join-Path $NewRun 'WorkshopOS.Client.exe'
if (-not (Test-Path $exe)) { throw "WorkshopOS.Client.exe missing under $NewRun" }
Get-Item $exe | Format-List FullName, Length, LastWriteTime
Start-Process $exe

Write-Host ''
Write-Host 'You must see Connect / pairing UI and version 1.2.2' -ForegroundColor Green
Write-Host '  - Green banner: Client 1.2.2'
Write-Host '  - Buttons: Connect with code / Find on this network / Connect'
Write-Host '  - NO "Connecting to server..." splash'
Write-Host "  - Running from: $exe"
Write-Host ''
