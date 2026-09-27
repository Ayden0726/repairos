#!/usr/bin/env bash
# Restart WorkshopOS API + worker containers (safe ops restart).
# Usage:
#   ./scripts/restart-workshopos.sh
#   ./scripts/restart-workshopos.sh --update   # git pull + rebuild then up
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker/docker-compose.yml"
DO_UPDATE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --update|-u) DO_UPDATE=1; shift ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required." >&2
  exit 1
fi

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Compose file not found: $COMPOSE_FILE" >&2
  exit 1
fi

cd "$ROOT"

if [[ "$DO_UPDATE" -eq 1 ]]; then
  echo "==> Updating WorkshopOS from git and rebuilding containers"
  if [[ -d .git ]]; then
    git pull --ff-only origin main || git pull --ff-only
  fi
  docker compose -f "$COMPOSE_FILE" pull || true
  docker compose -f "$COMPOSE_FILE" up -d --build
else
  echo "==> Restarting WorkshopOS api + worker"
  docker compose -f "$COMPOSE_FILE" restart api worker
fi

echo "==> Health check"
sleep 2
API_PORT="${API_PORT:-5088}"
if command -v curl >/dev/null 2>&1; then
  curl -fsS "http://127.0.0.1:${API_PORT}/api/health" || true
  echo
fi
echo "Done. Open http://127.0.0.1:${API_PORT}/connect if you need the pairing code."
