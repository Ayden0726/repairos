#!/usr/bin/env bash
# Restart WorkshopOS API + worker containers (safe ops restart).
#
#   ./scripts/restart-workshopos.sh
#   ./scripts/restart-workshopos.sh --update   # git pull + rebuild then up
#
# ASCII-safe for Windows/WSL terminals. Does not build the Windows client.
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker/docker-compose.yml"
ENV_FILE="$ROOT/docker/.env"
DO_UPDATE=0
API_PORT="${API_PORT:-5088}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --update|-u) DO_UPDATE=1; shift ;;
    -h|--help)
      echo "Usage: $0 [--update|-u]"
      echo "  (default)  restart api + worker"
      echo "  --update   git pull + docker compose up -d --build"
      exit 0
      ;;
    *) echo "[ERROR] Unknown option: $1"; exit 1 ;;
  esac
done

echo ""
echo "============================================================"
echo "  WorkshopOS restart"
echo "============================================================"
echo ""

if [[ ! -x "$0" ]]; then
  echo "    Tip: chmod +x scripts/*.sh"
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERROR] Docker is required. Install: https://docs.docker.com/engine/install/" >&2
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "[ERROR] Docker Compose v2 is required (docker compose)." >&2
  exit 1
fi

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "[ERROR] Compose file not found: $COMPOSE_FILE" >&2
  echo "        Are you in a WorkshopOS install? Or run: scripts/get-workshopos.sh" >&2
  exit 1
fi

# Prefer .env port when present
if [[ -f "$ENV_FILE" ]] && grep -q '^API_PORT=' "$ENV_FILE" 2>/dev/null; then
  # shellcheck disable=SC1090
  API_PORT="$(grep '^API_PORT=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
  API_PORT="${API_PORT:-5088}"
fi

cd "$ROOT"

if [[ "$DO_UPDATE" -eq 1 ]]; then
  echo "==> [1/3] Updating from git and rebuilding containers"
  if [[ -d .git ]]; then
    if ! git pull --ff-only origin main 2>/dev/null && ! git pull --ff-only; then
      echo "    WARN: git pull --ff-only failed (divergent history?)."
      echo "      Inspect: git status && git log --oneline -5"
      echo "      Force to remote (DESTROYS local commits):"
      echo "        git fetch origin && git reset --hard origin/main"
      echo "      Continuing with current tree..."
    else
      echo "    OK: git up to date"
    fi
  else
    echo "    WARN: not a git checkout — skipping pull"
  fi
  docker compose -f "$COMPOSE_FILE" pull || true
  docker compose -f "$COMPOSE_FILE" up -d --build
else
  echo "==> [1/3] Restarting api + worker"
  docker compose -f "$COMPOSE_FILE" restart api worker
fi

echo "==> [2/3] Waiting for health on port ${API_PORT}"
sleep 2
ok=0
for i in $(seq 1 30); do
  if command -v curl >/dev/null 2>&1; then
    if BODY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/health" 2>/dev/null)"; then
      echo "    OK: $BODY"
      ok=1
      break
    fi
  fi
  sleep 2
done

if [[ "$ok" != "1" ]]; then
  echo "    WARN: health check did not succeed yet."
  echo "      curl -v http://127.0.0.1:${API_PORT}/api/health"
  echo "      docker compose -f $COMPOSE_FILE logs api"
fi

echo "==> [3/3] Done"
echo ""
echo "  Connect:  http://127.0.0.1:${API_PORT}/connect"
echo "  Health:   http://127.0.0.1:${API_PORT}/api/health"
echo ""
echo "  Windows client: download WorkshopOS-Setup-x.y.z.exe from"
echo "    https://github.com/Ayden0726/repairos/releases"
echo ""
