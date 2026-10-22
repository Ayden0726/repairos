#!/usr/bin/env bash
# Build sync artifacts for updating https://github.com/Ayden0726/repairos from this workspace.
# Outputs:
#   /opt/cursor/artifacts/repairos-github-sync.zip
#   /opt/cursor/artifacts/repairos-main.bundle
#   /opt/cursor/artifacts/GITHUB_SYNC_INSTRUCTIONS.txt
#   copies under HTTP serve dir if WORKSHOPOS_SERVE_DIR is set (default /tmp/workshopos-serve)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ARTIFACTS_DIR:-/opt/cursor/artifacts}"
SERVE="${WORKSHOPOS_SERVE_DIR:-/tmp/workshopos-serve}"
mkdir -p "$OUT" "$SERVE"

STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

VERSION="1.2.34"
TIP="$(git -C "$ROOT" rev-parse --short HEAD)"

echo "==> Staging clean tree (no .git / bin / obj / node_modules / large zips)"
mkdir -p "$STAGING/repairos"
tar -C "$ROOT" \
  --exclude='.git' \
  --exclude='bin' \
  --exclude='obj' \
  --exclude='node_modules' \
  --exclude='packaging/dist' \
  --exclude='dist' \
  --exclude='workshopos-build-client-*.zip' \
  --exclude='repairos-github-sync.zip' \
  --exclude='repairos-main.bundle' \
  --exclude='WorkshopOS-*-github-sync.zip' \
  --exclude='.cursor' \
  -cf - . | tar -C "$STAGING/repairos" -xf -

# Ensure shell scripts are executable inside the zip tree
chmod +x "$STAGING"/repairos/scripts/*.sh 2>/dev/null || true

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
# Product-named flat zip (used by README / one-shot script)
NAMED="$OUT/WorkshopOS-${VERSION}-github-sync.zip"
cp -f "$FLAT" "$NAMED"
cp -f "$FLAT" "$SERVE/WorkshopOS-${VERSION}-github-sync.zip"
cp -f "$FLAT" "$ROOT/dist/WorkshopOS-${VERSION}-github-sync.zip" 2>/dev/null || {
  mkdir -p "$ROOT/dist"
  cp -f "$FLAT" "$ROOT/dist/WorkshopOS-${VERSION}-github-sync.zip"
}

cat > "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" <<EOF
WorkshopOS / repairos — sync this agent workspace to GitHub
===========================================================
Target: https://github.com/Ayden0726/repairos.git  (branch: main)
Agent tip: $TIP   Product: $VERSION
  - Encrypted API credentials + settings persistence fixes
  - Business expenses (GST / AU FY)
  - Raspberry Pi 3B+ Docker server support

You need GitHub auth on YOUR machine (gh auth login, Git Credential Manager, or PAT).
This Cloud Agent VM cannot push to your personal GitHub without your token.

Download (agent HTTP serve, port 18765 — use Ports UI URL if not localhost):
  http://127.0.0.1:18765/WorkshopOS-1.2.34-github-sync.zip     (FLAT tree overlay)
  http://127.0.0.1:18765/repairos-github-sync.zip
  http://127.0.0.1:18765/repairos-main.bundle
  http://127.0.0.1:18765/GITHUB_SYNC_INSTRUCTIONS.txt

Also on disk:
  /opt/cursor/artifacts/WorkshopOS-1.2.34-github-sync.zip
  /opt/cursor/artifacts/repairos-github-sync.zip
  /opt/cursor/artifacts/repairos-main.bundle
  /opt/cursor/artifacts/GITHUB_SYNC_INSTRUCTIONS.txt

Full guide in the repo after overlay: docs/GITHUB_SYNC.md
Push + Setup.exe one-shot: scripts/update-github-and-build-client.ps1 (see README.md)

────────────────────────────────────────────────────────────
OPTION A (recommended): PowerShell overlay → push + Setup.exe
────────────────────────────────────────────────────────────

  \$r = 'C:\\Users\\ayden\\src\\repairos'
  \$z = "\$env:TEMP\\WorkshopOS-1.2.34-github-sync.zip"
  New-Item -ItemType Directory -Force -Path \$r | Out-Null
  Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.34-github-sync.zip' -OutFile \$z
  Expand-Archive -Path \$z -DestinationPath \$r -Force
  powershell -ExecutionPolicy Bypass -File "\$r\\scripts\\update-github-and-build-client.ps1"

────────────────────────────────────────────────────────────
OPTION B: WSL / bash (push only)
────────────────────────────────────────────────────────────

  REPO="\${HOME}/src/repairos"
  ZIP=/tmp/WorkshopOS-1.2.34-github-sync.zip
  [[ -d "\$REPO/.git" ]] || git clone https://github.com/Ayden0726/repairos.git "\$REPO"
  curl -fsSL -o "\$ZIP" 'http://127.0.0.1:18765/WorkshopOS-1.2.34-github-sync.zip'
  unzip -o "\$ZIP" -d "\$REPO"
  cd "\$REPO" && chmod +x scripts/*.sh && ./scripts/sync-to-github.sh

────────────────────────────────────────────────────────────
After GitHub push — deploy servers
────────────────────────────────────────────────────────────

  # Fresh (auto-detects Raspberry Pi)
  curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash

  # Existing install
  cd ~/workshopos && git pull origin main && ./scripts/restart-workshopos.sh --update

  # Pi docs: docs/RASPBERRY_PI.md
  # Secrets:  Encryption__Key in docker/.env (see docs/SETTINGS_AND_SECURITY.md)

────────────────────────────────────────────────────────────
Key changes in this sync ($VERSION)
────────────────────────────────────────────────────────────
  Encrypted integration_credentials (AES-GCM); Woo/POS/webhook secrets migrated
  Settings no longer overwritten by pricing defaults
  Finance → Expenses (AU GST, FY Jul–Jun, recurring, CSV)
  Raspberry Pi 3B+ compose overlay + multi-arch Dockerfiles
  Redis optional (unused); Encryption__Key written by installer
EOF
cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$SERVE/"

# Also drop a copy inside packaging for people who only take the zip
cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$STAGING/repairos/GITHUB_SYNC_INSTRUCTIONS.txt" 2>/dev/null || true

ls -lh "$OUT/repairos-github-sync.zip" "$OUT/repairos-main.bundle" "$SERVE/repairos-github-sync.zip"
echo "OK — see $OUT/GITHUB_SYNC_INSTRUCTIONS.txt"
