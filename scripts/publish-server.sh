#!/usr/bin/env bash
# Pack a portable server distribution (compose + Dockerfiles + install script).
# Does not require a successful image build — operators build on the target host.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-1.2.0}"
OUT="$ROOT/packaging/dist"
mkdir -p "$OUT"

BUNDLE="$OUT/WorkshopOS-Server-v${VERSION}"
rm -rf "$BUNDLE"
mkdir -p "$BUNDLE"
cp -a "$ROOT/docker" "$BUNDLE/"
cp -a "$ROOT/scripts/install-server.sh" "$BUNDLE/"
chmod +x "$BUNDLE/install-server.sh"
cp "$ROOT/README.md" "$BUNDLE/"
[[ -f "$ROOT/docs/INSTALL.md" ]] && cp "$ROOT/docs/INSTALL.md" "$BUNDLE/"
[[ -f "$ROOT/docs/FEATURES.md" ]] && cp "$ROOT/docs/FEATURES.md" "$BUNDLE/"

# Include source needed to build Docker images
mkdir -p "$BUNDLE/source"
cp -a "$ROOT/services" "$BUNDLE/source/"
cp -a "$ROOT/WorkshopOS.sln" "$BUNDLE/source/" 2>/dev/null || true
# Dockerfiles expect context = repo root
# Rewrite install helper for extracted layout
cat > "$BUNDLE/README-SERVER.txt" <<EOF
WorkshopOS Server v${VERSION}

1. Copy this folder to the host (or use scripts/install-server.sh from the git repo).
2. cd docker && cp .env.example .env  # set POSTGRES_PASSWORD and JWT_SIGNING_KEY
3. From the *repository root* (parent of docker/), run:
     docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build
   Or set WORKSHOPOS_REPO and run ./install-server.sh

API listens on port 5088 by default.
EOF

# Bundle expects docker build context at parent — keep a thin wrapper tree
mkdir -p "$OUT/staging"
rm -rf "$OUT/staging/WorkshopOS-Server-v${VERSION}"
mkdir -p "$OUT/staging/WorkshopOS-Server-v${VERSION}"
# Full tree for docker context = this folder
rsync -a --exclude '**/bin' --exclude '**/obj' --exclude '.git' \
  "$ROOT/docker" "$ROOT/services" "$ROOT/scripts" "$ROOT/docs" \
  "$ROOT/WorkshopOS.sln" "$ROOT/README.md" \
  "$OUT/staging/WorkshopOS-Server-v${VERSION}/" 2>/dev/null || {
  cp -a "$ROOT/docker" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
  cp -a "$ROOT/services" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
  cp -a "$ROOT/scripts" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
  cp -a "$ROOT/docs" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
  cp "$ROOT/WorkshopOS.sln" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
  cp "$ROOT/README.md" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
}
cp "$BUNDLE/README-SERVER.txt" "$OUT/staging/WorkshopOS-Server-v${VERSION}/"
cp "$BUNDLE/install-server.sh" "$OUT/staging/WorkshopOS-Server-v${VERSION}/" 2>/dev/null || true
chmod +x "$OUT/staging/WorkshopOS-Server-v${VERSION}/scripts/install-server.sh" 2>/dev/null || true

( cd "$OUT/staging" && tar -czf "$OUT/WorkshopOS-Server-v${VERSION}.tar.gz" "WorkshopOS-Server-v${VERSION}" )
rm -rf "$BUNDLE" "$OUT/staging"
echo "Server bundle: $OUT/WorkshopOS-Server-v${VERSION}.tar.gz"
