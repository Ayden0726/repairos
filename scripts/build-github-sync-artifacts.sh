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
cat > "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" <<EOF
WorkshopOS / repairos — sync this agent workspace to GitHub
===========================================================
Target: https://github.com/Ayden0726/repairos.git  (branch: main)
Agent main tip: $(git -C "$ROOT" rev-parse HEAD)  (docs/setup 1.2.13+)

You need GitHub auth on YOUR machine (gh auth login, Git Credential Manager, or PAT).
This Cloud Agent VM cannot push to GitHub.

Download (agent HTTP serve, port 28765 — use Ports UI URL if not localhost):
  http://127.0.0.1:28765/repairos-github-sync.zip     (FLAT tree overlay)
  http://127.0.0.1:28765/repairos-main.bundle
  http://127.0.0.1:28765/GITHUB_SYNC_INSTRUCTIONS.txt

Also on disk: /opt/cursor/artifacts/repairos-github-sync.zip
              /opt/cursor/artifacts/repairos-main.bundle

────────────────────────────────────────────────────────────
OPTION A (recommended): zip overlay → commit → push
────────────────────────────────────────────────────────────

PowerShell (ASCII-safe):

  \$repo = 'C:\\Users\\ayden\\src\\repairos'
  \$zip  = "\$env:TEMP\\repairos-github-sync.zip"
  Invoke-WebRequest -Uri 'http://127.0.0.1:28765/repairos-github-sync.zip' -OutFile \$zip
  Expand-Archive -Path \$zip -DestinationPath \$repo -Force
  Set-Location \$repo
  git add -A
  git status
  git commit -m "Docs + setup: Release Setup.exe install path, improved get-workshopos.sh"
  git remote set-url origin https://github.com/Ayden0726/repairos.git
  git push -u origin main

Then update SERVER (WSL):

  cd ~/workshopos
  git pull origin main
  chmod +x scripts/*.sh
  ./scripts/restart-workshopos.sh --update

Windows client: download WorkshopOS-Setup-x.y.z.exe from
  https://github.com/Ayden0726/repairos/releases
(Do not run packaging\\build-client.ps1 for normal use.)

WSL / bash:

  REPO="\${HOME}/workshopos"
  ZIP=/tmp/repairos-github-sync.zip
  curl -fsSL -o "\$ZIP" 'http://127.0.0.1:28765/repairos-github-sync.zip'
  unzip -o "\$ZIP" -d "\$REPO"
  cd "\$REPO"
  git add -A && git commit -m "Docs + setup: Release Setup.exe install path, improved get-workshopos.sh"
  git push -u origin main
  chmod +x scripts/*.sh
  ./scripts/restart-workshopos.sh --update

────────────────────────────────────────────────────────────
OPTION B: git bundle
────────────────────────────────────────────────────────────

  curl -fsSL -o /tmp/repairos-main.bundle 'http://127.0.0.1:28765/repairos-main.bundle'
  cd "\$REPO"
  git fetch /tmp/repairos-main.bundle main:refs/remotes/agent/main
  git merge --ff-only refs/remotes/agent/main
  git push origin main

────────────────────────────────────────────────────────────
Key changes in this sync
────────────────────────────────────────────────────────────
  Improved scripts/get-workshopos.sh (--update, health, pairing, Releases next steps)
  restart-workshopos.sh / continue-workshopos-setup.sh clearer banners
  README + docs/INSTALL.md: client via GitHub Release Setup.exe
  Troubleshooting: 404=old server, Smart App Control, divergent git, chmod, Docker, ports
EOF
cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$SERVE/"
ls -lh "$OUT/repairos-github-sync.zip" "$OUT/repairos-main.bundle" "$SERVE/repairos-github-sync.zip"
echo "OK"
