#!/usr/bin/env bash
# Install WorkshopOS server (API + PostgreSQL + worker) with Docker Compose.
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/<owner>/<repo>/main/scripts/install-server.sh | bash
#   or: ./scripts/install-server.sh [/opt/workshopos]

set -euo pipefail

INSTALL_DIR="${1:-${WORKSHOPOS_HOME:-/opt/workshopos}}"
REPO_URL="${WORKSHOPOS_REPO:-}"
API_PORT="${API_PORT:-5088}"

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing dependency: $1"; exit 1; }
}

need docker
docker compose version >/dev/null 2>&1 || { echo "Docker Compose v2 required (docker compose)."; exit 1; }

echo "==> Installing WorkshopOS server into $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
sudo chown "$(id -u):$(id -g)" "$INSTALL_DIR" 2>/dev/null || true

if [[ -f "$INSTALL_DIR/docker/docker-compose.yml" ]]; then
  echo "Found existing checkout at $INSTALL_DIR"
  cd "$INSTALL_DIR"
elif [[ -n "$REPO_URL" ]]; then
  need git
  git clone "$REPO_URL" "$INSTALL_DIR"
  cd "$INSTALL_DIR"
elif [[ -f "$(dirname "$0")/../docker/docker-compose.yml" ]]; then
  # Running from a local clone
  ROOT="$(cd "$(dirname "$0")/.." && pwd)"
  rsync -a --exclude '.git' --exclude '**/bin' --exclude '**/obj' "$ROOT/" "$INSTALL_DIR/" 2>/dev/null \
    || cp -a "$ROOT/." "$INSTALL_DIR/"
  cd "$INSTALL_DIR"
else
  echo "Set WORKSHOPOS_REPO to your GitHub clone URL, or run this script from a WorkshopOS checkout."
  exit 1
fi

cd docker
if [[ ! -f .env ]]; then
  cp .env.example .env
  # Generate secrets
  if command -v openssl >/dev/null 2>&1; then
    PW="$(openssl rand -base64 24 | tr -d '\n=/+' | cut -c1-28)"
    KEY="$(openssl rand -base64 48 | tr -d '\n')"
  else
    PW="change-me-$(date +%s)"
    KEY="change-me-signing-key-$(date +%s)-must-be-long"
  fi
  sed -i.bak "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${PW}/" .env 2>/dev/null \
    || sed -i '' "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${PW}/" .env
  sed -i.bak "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=${KEY}/" .env 2>/dev/null \
    || sed -i '' "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=${KEY}/" .env
  grep -q '^API_PORT=' .env || echo "API_PORT=${API_PORT}" >> .env
  echo "Wrote docker/.env with generated secrets — keep this file private."
fi

echo "==> Building and starting containers"
docker compose --env-file .env up -d --build

echo ""
echo "WorkshopOS server is starting."
echo "  API:     http://127.0.0.1:${API_PORT}"
echo "  Health:  http://127.0.0.1:${API_PORT}/api/health"
echo "  Swagger: http://127.0.0.1:${API_PORT}/swagger"
echo ""
echo "On first launch of the Windows client, enter that API URL, complete business setup,"
echo "then create staff accounts under Users / Settings."
echo ""
echo "Manage:  cd $INSTALL_DIR/docker && docker compose --env-file .env logs -f api"
echo "Stop:    cd $INSTALL_DIR/docker && docker compose --env-file .env down"
