#Requires -Version 5.1
<#
.SYNOPSIS
  Build TechBench NQ / WorkshopOS Windows client 1.2.42 and update GitHub.

.DESCRIPTION
  1) Commits any pending local changes (optional)
  2) Pushes main to GitHub (Ayden0726/repairos)
  3) Creates/updates tag v1.2.42 (triggers Actions release workflow)
  4) Builds client zip + Setup.exe
  5) If gh CLI is available, creates/updates the GitHub Release notes

.EXAMPLE
  # Developer PowerShell for VS 2022:
  powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1

  # Build only (no GitHub):
  powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1 -SkipGitHub
#>
param(
  [string]$Version = '1.2.42',
  [string]$Branch = 'main',
  [string]$GitHubUrl = 'https://github.com/Ayden0726/repairos.git',
  [string]$ReleaseTag = 'v1.2.42',
  [string]$CommitMessage = 'WorkshopOS 1.2.42: TechBench NQ PIN login + native client',
  [switch]$SkipGitHub,
  [switch]$SkipInstaller,
  [switch]$SkipCommit
)

$ErrorActionPreference = 'Stop'
$Root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $Root

Write-Host ''
Write-Host '=== TechBench NQ / WorkshopOS 1.2.42: GitHub + Windows client ===' -ForegroundColor Cyan
Write-Host ("  Root={0}" -f $Root)
Write-Host ("  Version={0}  Tag={1}" -f $Version, $ReleaseTag)

# --- Ensure client version stamp ---
$cspPath = Join-Path $Root 'apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj'
if (-not (Test-Path $cspPath)) { throw "Missing $cspPath" }
$csp = Get-Content $cspPath -Raw
if ($csp -notmatch ('<Version>{0}</Version>' -f [regex]::Escape($Version))) {
  Write-Host ("Updating csproj Version to {0}..." -f $Version) -ForegroundColor Yellow
  $csp = $csp -replace '<Version>[^<]+</Version>', ("<Version>{0}</Version>" -f $Version)
  $csp = $csp -replace '<InformationalVersion>[^<]+</InformationalVersion>', ("<InformationalVersion>{0}</InformationalVersion>" -f $Version)
  Set-Content -Path $cspPath -Value $csp -Encoding UTF8
}

# --- GitHub: commit, push, tag ---
if (-not $SkipGitHub) {
  if (-not (Test-Path (Join-Path $Root '.git'))) {
    throw 'Not a git repo. Clone https://github.com/Ayden0726/repairos.git first.'
  }

  git remote get-url origin 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) {
    git remote add origin $GitHubUrl
  } else {
    git remote set-url origin $GitHubUrl
  }

  if (-not $SkipCommit) {
    # Orphans Expand-Archive never deletes
    Remove-Item -Force -ErrorAction SilentlyContinue `
      (Join-Path $Root 'apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml'), `
      (Join-Path $Root 'apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml.cs')

    git add -A
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
      Write-Host 'Committing changes for GitHub...' -ForegroundColor Yellow
      git commit -m $CommitMessage
      if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }
    } else {
      Write-Host 'Working tree clean — nothing new to commit.' -ForegroundColor DarkYellow
    }
  }

  Write-Host ("Pushing {0} -> {1} ..." -f $Branch, $GitHubUrl) -ForegroundColor Yellow
  git push -u origin $Branch
  if ($LASTEXITCODE -ne 0) {
    throw 'git push failed. Run: gh auth login  (or configure Git Credential Manager) then re-run.'
  }
  Write-Host ("OK — pushed {0}" -f $Branch) -ForegroundColor Green

  Write-Host ("Tagging {0} ..." -f $ReleaseTag) -ForegroundColor Yellow
  git tag -f $ReleaseTag -m @"
WorkshopOS / TechBench NQ $Version

- Native WinUI client with PIN keypad + password sign-in
- Optional owner PIN at first-run setup
- API: POST /api/auth/login/pin, PUT /api/auth/pin
- Home workshop profile from 1.2.41 retained
- Install: WorkshopOS-Setup-$Version.exe (Windows) + Docker server :5088
"@
  git push -f origin $ReleaseTag
  if ($LASTEXITCODE -ne 0) {
    Write-Host 'WARN: tag push failed (branch push succeeded).' -ForegroundColor Yellow
  } else {
    Write-Host ("OK — tag {0} (GitHub Actions release may run)" -f $ReleaseTag) -ForegroundColor Green
    Write-Host '     https://github.com/Ayden0726/repairos/actions' -ForegroundColor DarkCyan
  }
} else {
  Write-Host 'Skipping GitHub (-SkipGitHub).' -ForegroundColor DarkYellow
}

# --- Build Windows client ---
Write-Host ''
Write-Host ("Building Windows client {0}..." -f $Version) -ForegroundColor Cyan
$buildArgs = @(
  '-NoProfile', '-ExecutionPolicy', 'Bypass',
  '-File', (Join-Path $Root 'packaging\build-client.ps1'),
  '-Configuration', 'Release',
  '-Version', $Version
)
if ($SkipInstaller) { $buildArgs += '-SkipInstaller' }

& powershell @buildArgs
if ($LASTEXITCODE -ne 0) { throw ("build-client.ps1 failed (exit {0})" -f $LASTEXITCODE) }

$builtZip = Join-Path $Root ("packaging\dist\WorkshopOS-Client-win-x64-v{0}.zip" -f $Version)
$setupExe = Join-Path $Root ("packaging\dist\WorkshopOS-Setup-{0}.exe" -f $Version)
if (-not (Test-Path $builtZip)) { throw ("Missing {0}" -f $builtZip) }

# --- Publish / refresh GitHub Release notes + attach artifacts when gh is available ---
if (-not $SkipGitHub) {
  $gh = Get-Command gh -ErrorAction SilentlyContinue
  if ($gh) {
    Write-Host 'Updating GitHub Release via gh...' -ForegroundColor Yellow
    $notes = @"
## TechBench NQ / WorkshopOS $Version

Native **Windows desktop** client for TechBench NQ (home workshop profile).

### What's in this release
- TechBench NQ branded login with **PIN keypad** or email/password
- Optional owner PIN during first-run setup
- Server API PIN endpoints: ``POST /api/auth/login/pin``, ``PUT /api/auth/pin``
- Keeps 1.2.41 home / side-hustle workshop profile

### Install
1. Run the Docker server (``scripts/get-workshopos.sh``) and open ``http://<server>:5088/connect``
2. Install **WorkshopOS-Setup-$Version.exe** (or unzip the portable client)
3. Pair with the code, complete setup, sign in with PIN or password

### Docs
- [BUILD_NATIVE.md](https://github.com/Ayden0726/repairos/blob/main/BUILD_NATIVE.md)
- [WINDOWS_CLIENT_1.2.42_REBUILD.txt](https://github.com/Ayden0726/repairos/blob/main/WINDOWS_CLIENT_1.2.42_REBUILD.txt)
- [docs/INSTALL.md](https://github.com/Ayden0726/repairos/blob/main/docs/INSTALL.md)
"@
    $notesFile = Join-Path $env:TEMP ("workshopos-{0}-release-notes.md" -f $Version)
    Set-Content -Path $notesFile -Value $notes -Encoding UTF8

    # Prefer updating an existing tag release; create if missing
    gh release view $ReleaseTag --repo Ayden0726/repairos 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
      gh release edit $ReleaseTag --repo Ayden0726/repairos --title ("WorkshopOS {0}" -f $Version) --notes-file $notesFile
    } else {
      gh release create $ReleaseTag --repo Ayden0726/repairos --title ("WorkshopOS {0}" -f $Version) --notes-file $notesFile --latest
    }

    $assets = @($builtZip)
    if (Test-Path $setupExe) { $assets += $setupExe }
    foreach ($a in $assets) {
      Write-Host ("  Uploading {0} ..." -f (Split-Path $a -Leaf)) -ForegroundColor DarkCyan
      gh release upload $ReleaseTag $a --repo Ayden0726/repairos --clobber
    }
    Write-Host ("OK — GitHub Release: https://github.com/Ayden0726/repairos/releases/tag/{0}" -f $ReleaseTag) -ForegroundColor Green
  } else {
    Write-Host 'gh CLI not found — tag push still updates GitHub; Actions may attach CI artifacts.' -ForegroundColor DarkYellow
    Write-Host 'Install: https://cli.github.com/  then re-run to attach Setup.exe to the Release.' -ForegroundColor DarkYellow
  }
}

Write-Host ''
Write-Host ("OK — Client zip: {0}" -f $builtZip) -ForegroundColor Green
if (Test-Path $setupExe) {
  Write-Host ("OK — Setup.exe:  {0}" -f $setupExe) -ForegroundColor Green
}
Write-Host ("OK — GitHub:     {0}" -f $GitHubUrl) -ForegroundColor Green
Write-Host ("OK — Tag:        {0}" -f $ReleaseTag) -ForegroundColor Green
