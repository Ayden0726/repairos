#Requires -Version 5.1
<#
.SYNOPSIS
  One-shot: overlay agent sync -> push GitHub -> build client zip + Setup.exe.

.DESCRIPTION
  1) Downloads the flat agent sync zip (port 28765)
  2) Overlays it onto your local clone
  3) Commits and pushes to https://github.com/Ayden0726/repairos (main)
  4) Builds WorkshopOS.Client (Release) zip AND Setup.exe (Inno Setup 6)

.EXAMPLE
  # Developer PowerShell for VS 2022 (recommended):
  powershell -ExecutionPolicy Bypass -File C:\Users\ayden\src\repairos\scripts\update-github-and-build-client.ps1

  # First run when the script is not on disk yet (download then run):
  $r='C:\Users\ayden\src\repairos'; $z="$env:TEMP\repairos-github-sync.zip"
  New-Item -ItemType Directory -Force -Path $r | Out-Null
  Invoke-WebRequest -Uri 'http://127.0.0.1:28765/repairos-github-sync.zip' -OutFile $z
  Expand-Archive -Path $z -DestinationPath $r -Force
  powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1"

.NOTES
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.
  Needs: Git (auth to GitHub), .NET 8 SDK, WinUI workload, Inno Setup 6 for Setup.exe.
#>
param(
  [string]$Repo = 'C:\Users\ayden\src\repairos',
  [string]$Version = '1.2.33',
  [string]$Branch = 'main',
  [string]$GitHubUrl = 'https://github.com/Ayden0726/repairos.git',
  [string]$AgentSyncUrl = 'http://127.0.0.1:28765/repairos-github-sync.zip',
  [string]$LocalZip = '',
  [switch]$SkipAgentSync,
  [switch]$SkipGitHubPush,
  [switch]$SkipInstaller,
  [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'

Write-Host '=== WorkshopOS: sync -> GitHub -> client zip + Setup.exe ===' -ForegroundColor Cyan
Write-Host ("  Repo={0}" -f $Repo)
Write-Host ("  Version={0}  Branch={1}" -f $Version, $Branch)
Write-Host ("  Setup.exe={0}" -f (-not $SkipInstaller))

Get-Process -Name 'WorkshopOS.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

if (-not (Test-Path $Repo)) {
  New-Item -ItemType Directory -Force -Path $Repo | Out-Null
}

# --- 1) Overlay agent sync zip (optional) ---
Set-Location $Repo
if ($SkipAgentSync) {
  Write-Host 'Skipping agent sync download (-SkipAgentSync). Using existing repo overlay.' -ForegroundColor DarkYellow
} else {
  $zip = Join-Path $env:TEMP 'repairos-github-sync.zip'
  if (-not [string]::IsNullOrWhiteSpace($LocalZip)) {
    if (-not (Test-Path $LocalZip)) { throw ("LocalZip not found: {0}" -f $LocalZip) }
    Write-Host ("Using local zip {0} ..." -f $LocalZip) -ForegroundColor Yellow
    Copy-Item -Force $LocalZip $zip
  } else {
    Write-Host ("Downloading {0} ..." -f $AgentSyncUrl) -ForegroundColor Yellow
    $ok = $false
    for ($i = 1; $i -le 4; $i++) {
      try {
        Invoke-WebRequest -Uri $AgentSyncUrl -OutFile $zip -TimeoutSec 120
        $ok = $true
        break
      } catch {
        Write-Host ("Download attempt {0}/4 failed: {1}" -f $i, $_.Exception.Message) -ForegroundColor Yellow
        if ($i -lt 4) { Start-Sleep -Seconds (2 * $i) }
      }
    }
    if (-not $ok) {
      if (Test-Path $zip) {
        Write-Host ("Download failed; reusing existing {0}" -f $zip) -ForegroundColor DarkYellow
      } else {
        throw @"
Could not download agent sync zip from $AgentSyncUrl
Fix: open Cursor Ports and forward 28765, or download once manually then re-run with:
  -LocalZip `$env:TEMP\repairos-github-sync.zip
Or if you already expanded the zip into `$Repo:
  -SkipAgentSync
"@
      }
    }
  }
  Expand-Archive -Path $zip -DestinationPath $Repo -Force
  Set-Location $Repo
}

$scriptPath = Join-Path $Repo 'scripts\update-github-and-build-client.ps1'
if (-not (Test-Path $scriptPath)) {
  throw 'Overlay missing scripts\update-github-and-build-client.ps1 - sync zip is stale.'
}

# --- 2) Push to GitHub ---
if (-not $SkipGitHubPush) {
  Write-Host 'Pushing overlay to GitHub...' -ForegroundColor Yellow
  if (-not (Test-Path (Join-Path $Repo '.git'))) {
    git init
    git checkout -B $Branch
  }

  git remote get-url origin 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) {
    git remote add origin $GitHubUrl
  } else {
    git remote set-url origin $GitHubUrl
  }

  git add -A
  git diff --cached --quiet
  if ($LASTEXITCODE -ne 0) {
    git commit -m @"
Sync agent workspace to GitHub (client $Version)

Overlay from agent sync zip; Windows client build artifacts follow.
"@
  } else {
    Write-Host 'No new file changes to commit (tree already matches sync).' -ForegroundColor DarkYellow
  }

  git push -u origin $Branch
  if ($LASTEXITCODE -ne 0) {
    throw 'git push failed. Run: gh auth login   (or set a PAT) then re-run this script.'
  }
  Write-Host ("OK - pushed {0}" -f $GitHubUrl) -ForegroundColor Green
} else {
  Write-Host 'Skipping GitHub push (-SkipGitHubPush).' -ForegroundColor DarkYellow
}

# --- 3) Build client zip (+ Setup.exe) ---
Write-Host 'Wiping obj/bin...' -ForegroundColor Yellow
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
if (-not (Test-Path $gj)) { throw 'Missing global.json after overlay.' }
Write-Host ("dotnet --version (expect 8.x): {0}" -f (& dotnet --version))

$cspPath = Join-Path $Repo 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
$csp = Get-Content $cspPath -Raw
if ($csp -notmatch ('<Version>{0}</Version>' -f [regex]::Escape($Version))) {
  throw ("Client csproj is not {0} after overlay." -f $Version)
}

Write-Host 'Building client (Release)...' -ForegroundColor Cyan
$buildArgs = @(
  '-NoProfile', '-ExecutionPolicy', 'Bypass',
  '-File', (Join-Path $Repo 'packaging\build-client.ps1'),
  '-Configuration', 'Release',
  '-Version', $Version
)
if ($SkipInstaller) { $buildArgs += '-SkipInstaller' }

powershell @buildArgs
if ($LASTEXITCODE -ne 0) { throw ("build-client.ps1 failed (exit {0})" -f $LASTEXITCODE) }

$builtZip = Join-Path $Repo ("packaging\dist\WorkshopOS-Client-win-x64-v{0}.zip" -f $Version)
$setupExe = Join-Path $Repo ("packaging\dist\WorkshopOS-Setup-{0}.exe" -f $Version)
if (-not (Test-Path $builtZip)) { throw ("Missing {0}" -f $builtZip) }

if (-not $SkipInstaller) {
  if (Test-Path $setupExe) {
    Write-Host ("OK - Setup.exe: {0}" -f $setupExe) -ForegroundColor Green
  } else {
    Write-Host 'WARN: Setup.exe missing. Install Inno Setup 6 from https://jrsoftware.org/isdl.php then re-run.' -ForegroundColor Yellow
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
Write-Host ("OK - GitHub:   {0} ({1})" -f $GitHubUrl, $Branch) -ForegroundColor Green
Write-Host ("OK - Client zip: {0}" -f $builtZip) -ForegroundColor Green
Write-Host ("OK - Extracted:  {0}" -f $exe) -ForegroundColor Green
if (Test-Path $setupExe) {
  Write-Host ("OK - Setup.exe:  {0}" -f $setupExe) -ForegroundColor Green
}
Write-Host 'Pair to http://127.0.0.1:5088/connect (WOS-XXXX), then Settings -> POS if needed.'
