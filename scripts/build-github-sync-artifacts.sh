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
cat > "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" <<'EOF'
WorkshopOS / repairos — sync this agent workspace to GitHub
===========================================================
Target: https://github.com/Ayden0726/repairos.git  (branch: main)

You need GitHub auth on YOUR machine (gh auth login, Git Credential Manager, or PAT).
This Cloud Agent VM cannot push to GitHub.

Download (from the agent HTTP serve, port 28765):
  http://127.0.0.1:28765/repairos-github-sync.zip
  http://127.0.0.1:28765/repairos-main.bundle
(Use the agent / ports UI URL if not on localhost.)

Also on disk: /opt/cursor/artifacts/repairos-github-sync.zip
              /opt/cursor/artifacts/repairos-main.bundle

────────────────────────────────────────────────────────────
OPTION A (recommended): zip overlay → commit → push
────────────────────────────────────────────────────────────

PowerShell (Windows), clone at C:\Users\ayden\src\repairos:

  $repo = 'C:\Users\ayden\src\repairos'
  $zip  = "$env:TEMP\repairos-github-sync.zip"
  # Download — replace URL with the agent download link if needed:
  Invoke-WebRequest -Uri 'http://127.0.0.1:28765/repairos-github-sync.zip' -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $repo -Force
  Set-Location $repo
  git add -A
  git status
  git commit -m "Sync agent workspace fixes to GitHub"
  git remote set-url origin https://github.com/Ayden0726/repairos.git
  git push -u origin main

WSL / bash (clone at ~/workshopos or ~/src/repairos):

  REPO="${HOME}/workshopos"   # or: /mnt/c/Users/ayden/src/repairos
  ZIP=/tmp/repairos-github-sync.zip
  curl -fsSL -o "$ZIP" 'http://127.0.0.1:28765/repairos-github-sync.zip'
  unzip -o "$ZIP" -d "$REPO"
  cd "$REPO"
  bash scripts/apply-agent-sync.sh --already-extracted
  # or manually:
  #   git add -A && git commit -m "Sync agent workspace fixes to GitHub"
  #   git push -u origin main

────────────────────────────────────────────────────────────
OPTION B: git bundle
────────────────────────────────────────────────────────────

  curl -fsSL -o /tmp/repairos-main.bundle 'http://127.0.0.1:28765/repairos-main.bundle'
  cd "$REPO"   # existing clone
  git fetch /tmp/repairos-main.bundle main:refs/remotes/agent/main
  git merge --ff-only refs/remotes/agent/main   # or: git reset --hard refs/remotes/agent/main
  git push origin main

  # Fresh clone from bundle then push:
  #   git clone /tmp/repairos-main.bundle repairos
  #   cd repairos && git remote add origin https://github.com/Ayden0726/repairos.git
  #   git push -u origin main

────────────────────────────────────────────────────────────
Key fixed files in this sync
────────────────────────────────────────────────────────────
  apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj  (EnableMsixTooling=true)
  packaging/build-client.ps1   (v4, UTF-8 BOM, ASCII-safe)
  packaging/build-client.cmd   (v4 launcher)
  scripts/get-workshopos.sh    (sed fix for JWT keys with '/')
  scripts/continue-workshopos-setup.sh
  scripts/apply-agent-sync.sh
  scripts/push-to-github.sh
  packaging/README.md, apps/windows-client/README.md, docs/INSTALL.md
EOF

cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$SERVE/"
ls -lh "$OUT/repairos-github-sync.zip" "$OUT/repairos-main.bundle" "$SERVE/repairos-github-sync.zip"
echo "OK"
