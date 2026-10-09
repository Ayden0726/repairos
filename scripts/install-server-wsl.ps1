#Requires -Version 5.1
<#
.SYNOPSIS
  From Windows: enable WSL2, ensure Ubuntu, install WorkshopOS server inside WSL.

.DESCRIPTION
  Run in an elevated PowerShell (Run as administrator) on Windows 10/11.
  Installs/uses Ubuntu in WSL2, then runs get-workshopos.sh (Git + Docker + compose).

.EXAMPLE
  # Elevated PowerShell:
  powershell -ExecutionPolicy Bypass -File .\scripts\install-server-wsl.ps1

  # One-liner (agent sync serve / after overlay):
  irm http://127.0.0.1:28765/install-server-wsl.ps1 | iex

.NOTES
  ASCII-only + UTF-8 BOM for Windows PowerShell 5.1.
#>
param(
  [string]$Distro = 'Ubuntu',
  [int]$Port = 5088,
  [string]$InstallDir = '~/workshopos',
  [string]$RepoUrl = 'https://github.com/Ayden0726/repairos.git',
  [string]$Branch = 'main',
  [switch]$SkipFeatureEnable,
  [switch]$UpdateOnly
)

$ErrorActionPreference = 'Stop'
$ScriptVersion = 'v1'

function Write-Step([string]$msg) {
  Write-Host ""
  Write-Host "==> $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
  Write-Host "    OK: $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
  Write-Host "    WARN: $msg" -ForegroundColor Yellow
}

function Test-IsAdmin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $p = New-Object Security.Principal.WindowsPrincipal($id)
  return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-WslDistros {
  # wsl -l -q emits UTF-16LE on many builds; force unicode read.
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  $raw = & wsl.exe -l -q 2>$null
  $ErrorActionPreference = $prev
  if (-not $raw) { return @() }
  $text = if ($raw -is [string]) { $raw } else { ($raw | Out-String) }
  # Strip nulls from UTF-16 mistaken as ANSI
  $text = $text -replace "`0", ''
  return @(
    $text -split "`r?`n" |
      ForEach-Object { $_.Trim() } |
      Where-Object { $_ -and $_ -notmatch '^(Windows Subsystem|Copyright|Usage)' }
  )
}

function Test-WslDistro([string]$name) {
  $list = Get-WslDistros
  foreach ($d in $list) {
    if ($d -eq $name) { return $true }
    # Match "Ubuntu-22.04" when user asked for Ubuntu, etc.
    if ($d -like "$name*") { return $true }
  }
  return $false
}

function Resolve-WslDistro([string]$preferred) {
  if (Test-WslDistro $preferred) {
    $list = Get-WslDistros
    foreach ($d in $list) {
      if ($d -eq $preferred) { return $d }
    }
    foreach ($d in $list) {
      if ($d -like "$preferred*") { return $d }
    }
  }
  return $null
}

Write-Host "WorkshopOS WSL server installer $ScriptVersion" -ForegroundColor Cyan
Write-Host "  Distro=$Distro  Port=$Port  Dir=$InstallDir"

if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
  throw "wsl.exe not found. Install 'Windows Subsystem for Linux' from Optional Features or Microsoft Store, then reboot."
}

# --- Enable WSL features (needs admin; reboot may be required) ---
if (-not $SkipFeatureEnable) {
  Write-Step "Checking WSL / Virtual Machine Platform features"
  if (-not (Test-IsAdmin)) {
    Write-Warn "Not running as Administrator. Feature enable may fail; re-run elevated if WSL is missing."
  } else {
    try {
      & wsl.exe --install --no-distribution 2>$null | Out-Null
    } catch { }
    try {
      dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart | Out-Null
      dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart | Out-Null
    } catch {
      Write-Warn "DISM feature enable skipped: $($_.Exception.Message)"
    }
    try { & wsl.exe --set-default-version 2 | Out-Null } catch { }
    Write-Ok "WSL features requested (reboot Windows if this is the first enable)"
  }
}

# --- Ensure distro exists ---
$resolved = Resolve-WslDistro $Distro
if (-not $resolved) {
  Write-Step "Installing WSL distro: $Distro"
  if (-not (Test-IsAdmin)) {
    throw "No '$Distro' distro yet. Re-run this script in elevated PowerShell (Run as administrator) so it can install WSL/Ubuntu. After install, finish the first-time Ubuntu username prompt if shown, then re-run."
  }
  Write-Host "    This can take several minutes (Microsoft Store / web download)..."
  $installOut = & wsl.exe --install -d $Distro --web-download 2>&1
  Write-Host ($installOut | Out-String)
  $resolved = Resolve-WslDistro $Distro
  if (-not $resolved) {
    Write-Host ""
    Write-Host "WSL distro install was started or needs a reboot." -ForegroundColor Yellow
    Write-Host "1) Reboot Windows if prompted"
    Write-Host "2) Open '$Distro' once from the Start menu and create a UNIX username/password"
    Write-Host "3) Re-run this same script"
    exit 2
  }
}

$Distro = $resolved
Write-Ok "Using WSL distro: $Distro"

# Probe that the distro can run commands (first-boot user setup may block)
Write-Step "Probing WSL distro"
$probe = & wsl.exe -d $Distro -u root -- bash -lc "echo WSL_OK; uname -a" 2>&1
if (($probe | Out-String) -notmatch 'WSL_OK') {
  Write-Host ($probe | Out-String)
  throw "WSL distro '$Distro' is not ready. Open it from Start, finish username setup, then re-run."
}
Write-Ok (($probe | Select-Object -First 2) -join ' | ')

# Enable systemd when possible (helps Docker)
Write-Step "Enabling systemd in WSL (best-effort)"
$wslConf = @'
[boot]
systemd=true
'@
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($wslConf))
& wsl.exe -d $Distro -u root -- bash -lc "echo $b64 | base64 -d > /etc/wsl.conf" | Out-Null
Write-Ok "/etc/wsl.conf updated (systemd=true). Soft-restarting distro..."
try { & wsl.exe -t $Distro | Out-Null } catch { }
Start-Sleep -Seconds 2

# --- Install WorkshopOS server inside WSL ---
Write-Step "Installing WorkshopOS server inside WSL (Git + Docker + compose)"

$updateFlag = ''
if ($UpdateOnly) { $updateFlag = '--update' }

# Expand ~ for the Linux side; keep quoted for bash.
$linuxDir = $InstallDir
if ($linuxDir -eq '~/workshopos') { $linuxDir = '$HOME/workshopos' }

$bash = @"
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
# Noninteractive apt / sudo
if command -v apt-get >/dev/null 2>&1; then
  apt-get update -y
  apt-get install -y curl ca-certificates
fi
# Installer (root is fine for shop server)
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh -o /tmp/get-workshopos.sh
chmod +x /tmp/get-workshopos.sh
bash /tmp/get-workshopos.sh --dir $linuxDir --port $Port --repo '$RepoUrl' --branch '$Branch' $updateFlag
# Print connect hints
echo ''
echo '============================================================'
echo '  WorkshopOS server install finished (WSL)'
echo '============================================================'
IP=`$(hostname -I 2>/dev/null | awk '{print `$1}')
echo "  Connect page (from Windows):  http://127.0.0.1:$Port/connect"
if [[ -n "`$IP" ]]; then
  echo "  Connect page (WSL IP):        http://`$IP:$Port/connect"
fi
echo "  Health:                       curl -fsS http://127.0.0.1:$Port/api/health"
echo "  Update later:  wsl -d $Distro -u root -- bash -lc '$linuxDir/scripts/get-workshopos.sh --update'"
echo ''
"@

# Pass bash script via stdin to avoid Windows quoting hell
$bashBytes = [Text.Encoding]::UTF8.GetBytes($bash)
$bashB64 = [Convert]::ToBase64String($bashBytes)

& wsl.exe -d $Distro -u root -- bash -lc "echo $bashB64 | base64 -d | bash"
$exit = $LASTEXITCODE
if ($exit -ne 0) {
  throw "WorkshopOS install inside WSL failed (exit $exit). Scroll up for errors. Tip: sudo service docker start inside WSL, then re-run with -UpdateOnly."
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "Open from Windows:  http://127.0.0.1:$Port/connect" -ForegroundColor Green
Write-Host "Pair the WorkshopOS Windows client with the WOS-XXXX code on that page."
Write-Host ""
Write-Host "Useful:"
Write-Host "  wsl -d $Distro -u root -- bash -lc 'curl -fsS http://127.0.0.1:$Port/api/health'"
Write-Host "  wsl -d $Distro -u root -- bash -lc 'cd $linuxDir/docker && docker compose --env-file .env ps'"
Write-Host "  Re-run update:  powershell -ExecutionPolicy Bypass -File .\scripts\install-server-wsl.ps1 -UpdateOnly"
