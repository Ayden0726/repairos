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
Agent main tip: $(git -C "$ROOT" rev-parse HEAD)  (client 1.2.22 — delete ServicePickerControl)

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
  # CRITICAL: Expand-Archive never deletes orphans. Remove leftover UserControl:
  Remove-Item -Force -ErrorAction SilentlyContinue \`
    "\$repo\\apps\\windows-client\\WorkshopOS.Client\\Views\\ServicePickerControl.xaml", \`
    "\$repo\\apps\\windows-client\\WorkshopOS.Client\\Views\\ServicePickerControl.xaml.cs"
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue \`
    "\$repo\\apps\\windows-client\\WorkshopOS.Client\\obj", \`
    "\$repo\\apps\\windows-client\\WorkshopOS.Client\\bin"
  git add -A
  git status
  # Expect: deleted ServicePickerControl.*, Version 1.2.22
  git commit -m "Client 1.2.22: delete ServicePickerControl (fix CS0101 CatalogueServiceRow)"
  git remote set-url origin https://github.com/Ayden0726/repairos.git
  git push -u origin main

Windows client rebuild (after overlay + delete above):

  powershell -ExecutionPolicy Bypass -File .\\scripts\\rebuild-client-1.2.22.ps1

  # or manually:
  # powershell -ExecutionPolicy Bypass -File .\\packaging\\build-client.ps1 -Configuration Release -Version 1.2.22

Then update SERVER (WSL):

  cd ~/workshopos
  git pull origin main
  chmod +x scripts/*.sh
  ./scripts/restart-workshopos.sh --update

WSL / bash:

  REPO="\${HOME}/workshopos"
  ZIP=/tmp/repairos-github-sync.zip
  curl -fsSL -o "\$ZIP" 'http://127.0.0.1:28765/repairos-github-sync.zip'
  unzip -o "\$ZIP" -d "\$REPO"
  cd "\$REPO"
  rm -f apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml \\
        apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml.cs
  rm -rf apps/windows-client/WorkshopOS.Client/obj apps/windows-client/WorkshopOS.Client/bin
  git add -A && git commit -m "Client 1.2.22: delete ServicePickerControl (fix CS0101 CatalogueServiceRow)"
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
  # Still force-delete orphans if merge did not remove them:
  rm -f apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml \\
        apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml.cs
  git push origin main

────────────────────────────────────────────────────────────
Key changes in this sync (1.2.22)
────────────────────────────────────────────────────────────
  FIX CS0101: CatalogueServiceRow defined twice — ServicePickerControl.xaml.cs
  still on disk after 1.2.21 zip overlay. Delete those files + wipe obj.
  CatalogueServiceRow lives only in ServiceCataloguePickerLogic.cs.
  Client / API / ClientMinVersion = 1.2.22
  scripts/rebuild-client-1.2.22.ps1 force-deletes orphans before publish
EOF
cp -f "$OUT/GITHUB_SYNC_INSTRUCTIONS.txt" "$SERVE/"
ls -lh "$OUT/repairos-github-sync.zip" "$OUT/repairos-main.bundle" "$SERVE/repairos-github-sync.zip"
echo "OK"
