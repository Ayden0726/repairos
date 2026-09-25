#!/usr/bin/env bash
# Build sync artifacts for updating https://github.com/Ayden0726/repairos from this workspace.
# Outputs:
#   /opt/cursor/artifacts/repairos-github-sync.zip
#   /opt/cursor/artifacts/repairos-main.bundle
#   copies under HTTP serve dir if WORKSHOPOS_SERVE_DIR is set (default /tmp/workshopos-serve)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ARTIFACTS_DIR:-/opt/cursor/artifacts}"
SERVE="${WORKSHOPOS_SERVE_DIR:-/tmp/workshopos-serve}"
mkdir -p "$OUT" "$SERVE"

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

echo "==> Staging clean tree (no .git / bin / obj / node_modules / large zips)"
mkdir -p "$STAGING/repairos"
tar -C "$ROOT" \
  --exclude='.git' \
  --exclude='bin' \
  --exclude='obj' \
  --exclude='node_modules' \
  --exclude='packaging/dist' \
  --exclude='workshopos-build-client-v3.zip' \
  --exclude='workshopos-build-client-v4.zip' \
  --exclude='repairos-github-sync.zip' \
  --exclude='repairos-main.bundle' \
  --exclude='.cursor' \
  -cf - . | tar -C "$STAGING/repairos" -xf -

ZIP="$OUT/repairos-github-sync.zip"
rm -f "$ZIP"
( cd "$STAGING" && zip -qr "$ZIP" repairos )
# Flat overlay zip (contents at archive root) for extract-over-clone
FLAT="$OUT/repairos-github-sync-flat.zip"
rm -f "$FLAT"
( cd "$STAGING/repairos" && zip -qr "$FLAT" . )

BUNDLE="$OUT/repairos-main.bundle"
rm -f "$BUNDLE"
git -C "$ROOT" bundle create "$BUNDLE" main

cp -f "$ZIP" "$FLAT" "$BUNDLE" "$SERVE/"
# Prefer flat zip as the canonical download name on the HTTP server
cp -f "$FLAT" "$SERVE/repairos-github-sync.zip"
cp -f "$FLAT" "$OUT/repairos-github-sync.zip"

# Instructions next to artifacts
MAIN_FULL="$(git -C "$ROOT" rev-parse main)"
MAIN_SHORT="$(git -C "$ROOT" rev-parse --short main)"
MAIN_SUBJ="$(git -C "$ROOT" log -1 --pretty=%s main)"
HEAD_FULL="$(git -C "$ROOT" rev-parse HEAD)"
HEAD_SHORT="$(git -C "$ROOT" rev-parse --short HEAD)"
cat > "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" <<EOF
WorkshopOS / repairos — sync this agent workspace to GitHub
===========================================================
Target: https://github.com/Ayden0726/repairos.git  (branch: main)
Client 1.2.4 on main: ${MAIN_FULL} (${MAIN_SHORT}) — ${MAIN_SUBJ}
Workspace tree in this zip: ${HEAD_FULL} (${HEAD_SHORT})
Client: 1.2.4 (nav redesign: Dashboard calendar / Work / Inventory / Customers / Reports + Settings hub)

You need GitHub auth on YOUR machine (Git Credential Manager, or PAT).
This Cloud Agent VM cannot push to GitHub. Do NOT open a PR.

Download (agent HTTP serve, port 28765 — use Ports UI URL if not localhost):
  http://127.0.0.1:28765/repairos-github-sync.zip     (FLAT tree overlay)
  http://127.0.0.1:28765/repairos-main.bundle
  http://127.0.0.1:28765/GITHUB_SYNC_INSTRUCTIONS.txt

Also on disk: /opt/cursor/artifacts/repairos-github-sync.zip
              /opt/cursor/artifacts/repairos-main.bundle

────────────────────────────────────────────────────────────
ONE PowerShell block: download → overlay → commit → push → build 1.2.4 → Desktop launch
────────────────────────────────────────────────────────────

\$ErrorActionPreference = 'Stop'
\$repo = 'C:\\Users\\ayden\\src\\repairos'
\$zip  = "\$env:TEMP\\repairos-github-sync.zip"
\$desk = Join-Path \$env:USERPROFILE 'Desktop\\WorkshopOS-Client-1.2.4'
Invoke-WebRequest -Uri 'http://127.0.0.1:28765/repairos-github-sync.zip' -OutFile \$zip
if (-not (Test-Path \$repo)) { New-Item -ItemType Directory -Force -Path \$repo | Out-Null }
Expand-Archive -Path \$zip -DestinationPath \$repo -Force
Set-Location \$repo
git remote set-url origin https://github.com/Ayden0726/repairos.git
git checkout main 2>\$null; if (\$LASTEXITCODE -ne 0) { git checkout -b main }
git add -A
git status
git commit -m "Client 1.2.4: dashboard calendar, repairs polish, slim nav"
git push -u origin main
powershell -NoProfile -ExecutionPolicy Bypass -File .\\packaging\\build-client.ps1 -Configuration Release -Version 1.2.4 -SkipInstaller
\$built = Join-Path \$repo 'packaging\\dist\\WorkshopOS-Client-win-x64-v1.2.4.zip'
if (-not (Test-Path \$built)) { throw "Missing \$built" }
if (Test-Path \$desk) { Remove-Item -Recurse -Force \$desk }
New-Item -ItemType Directory -Force -Path \$desk | Out-Null
Expand-Archive -Path \$built -DestinationPath \$desk -Force
\$exe = Join-Path \$desk 'WorkshopOS.Client.exe'
Start-Process \$exe
Write-Host "Launched \$exe - expect Connect UI and banner Client 1.2.4" -ForegroundColor Green

────────────────────────────────────────────────────────────
ONE Git CMD (bat) variant
────────────────────────────────────────────────────────────

@echo off
setlocal EnableExtensions
set REPO=C:\\Users\\ayden\\src\\repairos
set ZIP=%TEMP%\\repairos-github-sync.zip
set DESK=%USERPROFILE%\\Desktop\\WorkshopOS-Client-1.2.4
curl -fsSL -o "%ZIP%" http://127.0.0.1:28765/repairos-github-sync.zip
if not exist "%REPO%" mkdir "%REPO%"
powershell -NoProfile -Command "Expand-Archive -Path '%ZIP%' -DestinationPath '%REPO%' -Force"
cd /d "%REPO%"
git remote set-url origin https://github.com/Ayden0726/repairos.git
git checkout main 2>nul || git checkout -b main
git add -A
git status
git commit -m "Client 1.2.4: dashboard calendar, repairs polish, slim nav"
git push -u origin main
powershell -NoProfile -ExecutionPolicy Bypass -File .\\packaging\\build-client.ps1 -Configuration Release -Version 1.2.4 -SkipInstaller
if not exist "packaging\\dist\\WorkshopOS-Client-win-x64-v1.2.4.zip" exit /b 1
if exist "%DESK%" rmdir /s /q "%DESK%"
mkdir "%DESK%"
powershell -NoProfile -Command "Expand-Archive -Path '%REPO%\\packaging\\dist\\WorkshopOS-Client-win-x64-v1.2.4.zip' -DestinationPath '%DESK%' -Force"
start "" "%DESK%\\WorkshopOS.Client.exe"
echo Launched %DESK%\\WorkshopOS.Client.exe - expect Connect UI and banner Client 1.2.4

────────────────────────────────────────────────────────────
Key files in this sync (client 1.2.4)
────────────────────────────────────────────────────────────
  apps/windows-client/.../MainWindow.xaml.cs  (ALWAYS ServerConnect; slim nav)
  apps/windows-client/.../Views/ServerConnectPage.xaml  (Client 1.2.4 banner)
  apps/windows-client/.../Views/Dashboard* / Settings* / nav shell
  apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj  (Version 1.2.4)
  packaging/build-client.ps1   (v5, default Version 1.2.4, UTF-8 BOM + ASCII)
  packaging/rebuild-client.ps1 (wipe + verify + build 1.2.4 + Desktop launch)
  WINDOWS_CLIENT_1.2.4_REBUILD.txt
EOF

cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$SERVE/"
ls -lh "$OUT/repairos-github-sync.zip" "$OUT/repairos-main.bundle" "$SERVE/repairos-github-sync.zip"
echo "OK"
