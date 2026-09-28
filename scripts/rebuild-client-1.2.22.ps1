#Requires -Version 5.1
<#
.SYNOPSIS
  Definitive WorkshopOS client 1.2.22 rebuild: kill process, wipe ALL local data,
  pull/overlay latest source, FORCE-DELETE orphan ServicePickerControl files,
  wipe obj/, verify single CatalogueServiceRow, build, extract, launch.

.NOTES
  Edit $Repo below. Prefer "Developer PowerShell for VS 2022".
  You MUST see Connect / pairing UI and version banner Client 1.2.22.
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.

  Root cause of CS0101/CS0111 on 1.2.21: Expand-Archive overlay does NOT delete
  files removed from git. ServicePickerControl.xaml(.cs) stayed on disk beside
  ServiceCataloguePickerLogic.cs → duplicate CatalogueServiceRow.

  If publish fails with XamlCompiler / exit 1:
    powershell -ExecutionPolicy Bypass -File .\packaging\dump-xaml-errors.ps1
#>
$ErrorActionPreference = 'Stop'

# ========== EDIT IF NEEDED ==========
$Repo   = 'C:\Users\ayden\src\repairos'
$NewRun = Join-Path $env:USERPROFILE 'Desktop\WorkshopOS-Client-1.2.22'
$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip'
$UseAgentOverlay = $true
# ====================================

Write-Host '=== WorkshopOS Client 1.2.22 - wipe / verify / build / launch ===' -ForegroundColor Cyan

# 1) Kill any running client
Get-Process -Name 'WorkshopOS.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 2) Clear ALL local persistence that can force auto-connect / bootstrap
Write-Host 'Clearing local client data...' -ForegroundColor Yellow
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\Packages\*WorkshopOS*"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\Packages\*workshopos*"
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

# 3b) CRITICAL: zip overlay never deletes orphans. Remove leftover UserControl + obj.
$views = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views'
$pickerXaml = Join-Path $views 'ServicePickerControl.xaml'
$pickerCs   = Join-Path $views 'ServicePickerControl.xaml.cs'
$objDir     = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\obj'
$binDir     = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\bin'

Write-Host 'Force-deleting ServicePickerControl leftovers + obj/bin...' -ForegroundColor Yellow
Remove-Item -Force -ErrorAction SilentlyContinue $pickerXaml, $pickerCs
Get-ChildItem -Path $views -Filter 'ServicePickerControl*' -ErrorAction SilentlyContinue |
  Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $objDir, $binDir

# 4) Verify version + ServicePickerControl GONE + single CatalogueServiceRow
$mw  = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\MainWindow.xaml.cs'
$scx = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\ServerConnectPage.xaml'
$csp = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
$bps = Join-Path $Repo 'packaging\build-client.ps1'
$nrp = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\NewRepairPage.xaml'
$qbp = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\Views\QuoteBuilderPage.xaml'
$logic = Join-Path $views 'ServiceCataloguePickerLogic.cs'
$main = Get-Content $mw -Raw
$xaml = Get-Content $scx -Raw
$proj = Get-Content $csp -Raw
$bld  = Get-Content $bps -Raw
$nr   = Get-Content $nrp -Raw
$qb   = Get-Content $qbp -Raw
$logicText = Get-Content $logic -Raw

$dupRowHits = @(Get-ChildItem -Path (Join-Path $Repo 'apps\windows-client') -Recurse -Include *.cs,*.xaml |
  Select-String -Pattern 'class\s+CatalogueServiceRow' -SimpleMatch:$false)
$svcPickerHits = @(Get-ChildItem -Path (Join-Path $Repo 'apps\windows-client') -Recurse -Include *.cs,*.xaml |
  Select-String -Pattern 'ServicePickerControl' -SimpleMatch)

$checks = [ordered]@{
  'MainWindow navigates ServerConnectPage' = ($main -match 'Navigate\(typeof\(Views\.ServerConnectPage\)\)')
  'MainWindow does NOT navigate Bootstrap first' = ($main -notmatch 'Navigate\(typeof\(Views\.BootstrapPage\)\)')
  'ServerConnect banner Client 1.2.22' = ($xaml -match 'Client 1\.2\.22')
  'csproj Version 1.2.22' = ($proj -match '<Version>1\.2\.22</Version>')
  'build-client.ps1 default 1.2.22' = ($bld -match 'Version\s*=\s*"1\.2\.22"')
  'build-client.ps1 script v6' = ($bld -match 'ScriptVersion\s*=\s*"v6"')
  'ServicePickerControl.xaml DELETED' = (-not (Test-Path $pickerXaml))
  'ServicePickerControl.xaml.cs DELETED' = (-not (Test-Path $pickerCs))
  'Zero ServicePickerControl string leftovers' = ($svcPickerHits.Count -eq 0)
  'CatalogueServiceRow only in ServiceCataloguePickerLogic.cs' = (
    $dupRowHits.Count -eq 1 -and ($dupRowHits[0].Path -like '*ServiceCataloguePickerLogic.cs')
  )
  'ServiceCataloguePickerLogic defines CatalogueServiceRow' = ($logicText -match 'class\s+CatalogueServiceRow')
  'NewRepairPage has no ServicePickerControl' = ($nr -notmatch 'ServicePickerControl')
  'NewRepairPage has no NumberBox' = ($nr -notmatch '<NumberBox')
  'QuoteBuilder has no ServicePickerControl' = ($qb -notmatch 'ServicePickerControl')
  'QuoteBuilder has no NumberBox' = ($qb -notmatch '<NumberBox')
  'QuoteBuilder uses JobLabourFeeText' = ($qb -match 'JobLabourFeeText')
  'obj wiped' = (-not (Test-Path $objDir))
}

$allOk = $true
foreach ($k in $checks.Keys) {
  $ok = [bool]$checks[$k]
  "{0}: {1}" -f $k, $ok
  if (-not $ok) { $allOk = $false }
}
if (-not $allOk) {
  if ($svcPickerHits.Count -gt 0) {
    Write-Host 'ServicePickerControl leftovers:' -ForegroundColor Red
    $svcPickerHits | ForEach-Object { Write-Host ("  {0}:{1}" -f $_.Path, $_.LineNumber) }
  }
  if ($dupRowHits.Count -ne 1) {
    Write-Host 'CatalogueServiceRow definitions:' -ForegroundColor Red
    $dupRowHits | ForEach-Object { Write-Host ("  {0}:{1}" -f $_.Path, $_.LineNumber) }
  }
  throw 'Source is OLD or wrong - pull/overlay failed. Do not rebuild. Set $UseAgentOverlay=$true and retry.'
}

# 5) Build 1.2.22
Set-Location $Repo
Write-Host 'Building client 1.2.22 (script v6)...' -ForegroundColor Cyan
try {
  powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.22 -SkipInstaller
} catch {
  Write-Host ''
  Write-Host 'PUBLISH FAILED. Dumping XamlCompiler diagnostics...' -ForegroundColor Red
  $dump = Join-Path $Repo 'packaging\dump-xaml-errors.ps1'
  if (Test-Path $dump) {
    powershell -NoProfile -ExecutionPolicy Bypass -File $dump
  } else {
    $log = Join-Path $Repo 'packaging\out\logs\publish-last.log'
    if (Test-Path $log) { Get-Content $log -Tail 80 }
  }
  throw
}

$built = Join-Path $Repo 'packaging\dist\WorkshopOS-Client-win-x64-v1.2.22.zip'
if (-not (Test-Path $built)) { throw "Missing $built - build failed or still produced an older version zip." }

# 6) Extract + launch
if (Test-Path $NewRun) { Remove-Item -Recurse -Force $NewRun }
New-Item -ItemType Directory -Force -Path $NewRun | Out-Null
Expand-Archive -Path $built -DestinationPath $NewRun -Force
$exe = Join-Path $NewRun 'WorkshopOS.Client.exe'
if (-not (Test-Path $exe)) { throw "WorkshopOS.Client.exe missing under $NewRun" }
Get-Item $exe | Format-List FullName, Length, LastWriteTime
Start-Process $exe

Write-Host ''
Write-Host 'You must see Connect / pairing UI and version 1.2.22' -ForegroundColor Green
Write-Host '  - Green banner: Client 1.2.22'
Write-Host '  - Buttons: Connect with code / Find on this network / Connect'
Write-Host '  - NO "Connecting to server..." splash'
Write-Host "  - Running from: $exe"
Write-Host ''
