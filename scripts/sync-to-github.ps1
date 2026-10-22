#Requires -Version 5.1
<#
.SYNOPSIS
  Commit the current WorkshopOS tree and push to GitHub (Ayden0726/repairos).

.DESCRIPTION
  Run from inside your local clone AFTER overlaying the sync zip (see docs/GITHUB_SYNC.md).
  Requires git + GitHub credentials (gh auth login, GCM, or PAT).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\sync-to-github.ps1
  powershell -ExecutionPolicy Bypass -File .\scripts\sync-to-github.ps1 -Tag v1.2.34
#>
param(
  [string]$RemoteUrl = "https://github.com/Ayden0726/repairos.git",
  [string]$Branch = "main",
  [string]$Tag = "v1.2.34",
  [string]$Message = "WorkshopOS 1.2.34: encrypted credentials, expenses, Raspberry Pi server",
  [switch]$SkipTag,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

Write-Host ""
Write-Host "============================================================"
Write-Host "  WorkshopOS -> GitHub sync"
Write-Host "============================================================"
Write-Host "  Root:   $Root"
Write-Host "  Remote: $RemoteUrl"
Write-Host "  Branch: $Branch"
Write-Host ""

if (-not (Test-Path (Join-Path $Root ".git"))) {
  throw "Not a git repo. Clone https://github.com/Ayden0726/repairos.git first, then overlay the sync zip."
}

# Orphans that Expand-Archive never deletes
@(
  "apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml",
  "apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml.cs"
) | ForEach-Object {
  $p = Join-Path $Root $_
  if (Test-Path $p) {
    Remove-Item -Force $p
    Write-Host "  Removed orphan: $_"
  }
}
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  (Join-Path $Root "apps\windows-client\WorkshopOS.Client\obj"), `
  (Join-Path $Root "apps\windows-client\WorkshopOS.Client\bin")

git remote set-url origin $RemoteUrl
git add -A
$status = git status --porcelain
if (-not $status) {
  Write-Host "  Nothing new to commit (working tree clean)."
} else {
  Write-Host "  Staging changes..."
  git status --short
  if ($DryRun) {
    Write-Host "  DryRun: skipping commit/push"
    exit 0
  }
  git commit -m $Message
}

if ($DryRun) { exit 0 }

Write-Host "  Pushing $Branch..."
git push -u origin $Branch

if (-not $SkipTag -and $Tag) {
  Write-Host "  Tagging $Tag..."
  git tag -f $Tag
  git push -f origin $Tag
  Write-Host "  Release workflow: https://github.com/Ayden0726/repairos/actions"
}

Write-Host ""
Write-Host "  Done. Next:"
Write-Host "    - Rebuild client: packaging\build-client.ps1 -Version 1.2.34"
Write-Host "    - Update servers:  git pull && ./scripts/restart-workshopos.sh --update"
Write-Host "    - Pi / security:   docs\RASPBERRY_PI.md  docs\SETTINGS_AND_SECURITY.md"
Write-Host "    - Full guide:      docs\GITHUB_SYNC.md"
Write-Host ""
