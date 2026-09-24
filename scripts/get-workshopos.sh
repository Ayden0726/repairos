#!/usr/bin/env bash
# WorkshopOS — one-command server install (SimplyPrint-style)
#
#   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
#
# Or with options:
#   curl -fsSL ... | bash -s -- --dir /opt/workshopos --port 5088
#
set -euo pipefail

REPO_URL="${WORKSHOPOS_REPO:-https://github.com/Ayden0726/repairos.git}"
INSTALL_DIR="${WORKSHOPOS_HOME:-$HOME/workshopos}"
API_PORT="${API_PORT:-5088}"
BRANCH="${WORKSHOPOS_BRANCH:-main}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dir) INSTALL_DIR="$2"; shift 2 ;;
    --port) API_PORT="$2"; shift 2 ;;
    --repo) REPO_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing dependency: $1"; exit 1; }; }

echo ""
echo "╔══════════════════════════════════════════╗"
echo "║         WorkshopOS server setup          ║"
echo "╚══════════════════════════════════════════╝"
echo ""

need docker
docker compose version >/dev/null 2>&1 || { echo "Docker Compose v2 required (docker compose)."; exit 1; }
need git
need curl

if [[ ! -d "$INSTALL_DIR/.git" ]]; then
  echo "==> Cloning $REPO_URL → $INSTALL_DIR"
  mkdir -p "$(dirname "$INSTALL_DIR")"
  if [[ -d "$INSTALL_DIR" ]] && [[ -z "$(ls -A "$INSTALL_DIR" 2>/dev/null || true)" ]]; then
    rmdir "$INSTALL_DIR" 2>/dev/null || true
  fi
  if [[ -d "$INSTALL_DIR" ]]; then
    echo "Directory exists — updating instead of cloning"
    git -C "$INSTALL_DIR" fetch --depth 1 origin "$BRANCH" || true
    git -C "$INSTALL_DIR" checkout "$BRANCH" || true
    git -C "$INSTALL_DIR" pull --ff-only origin "$BRANCH" || true
  else
    git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$INSTALL_DIR"
  fi
else
  echo "==> Updating existing install at $INSTALL_DIR"
  git -C "$INSTALL_DIR" fetch --depth 1 origin "$BRANCH"
  git -C "$INSTALL_DIR" checkout "$BRANCH"
  git -C "$INSTALL_DIR" pull --ff-only origin "$BRANCH" || true
fi

cd "$INSTALL_DIR/docker"
if [[ ! -f .env ]]; then
  cp .env.example .env
  if command -v openssl >/dev/null 2>&1; then
    PW="$(openssl rand -base64 24 | tr -d '\n=/+' | cut -c1-28)"
    KEY="$(openssl rand -base64 48 | tr -d '\n')"
  else
    PW="change-me-$(date +%s)"
    KEY="change-me-signing-key-$(date +%s)-must-be-long-enough"
  fi
  # portable in-place edit
  tmp="$(mktemp)"
  sed "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${PW}/" .env | sed "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=${KEY}/" > "$tmp"
  mv "$tmp" .env
  grep -q '^API_PORT=' .env || echo "API_PORT=${API_PORT}" >> .env
  # force port
  tmp="$(mktemp)"
  sed "s/^API_PORT=.*/API_PORT=${API_PORT}/" .env > "$tmp"
  mv "$tmp" .env
  echo "==> Wrote docker/.env with random secrets"
else
  echo "==> Using existing docker/.env"
fi

echo "==> Building & starting containers (first run can take a few minutes)"
docker compose --env-file .env up -d --build

echo "==> Waiting for API health…"
ok=0
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null 2>&1; then
    ok=1
    break
  fi
  sleep 2
done

if [[ "$ok" != "1" ]]; then
  echo "API did not become healthy in time. Check: docker compose -f $INSTALL_DIR/docker/docker-compose.yml --env-file $INSTALL_DIR/docker/.env logs api"
  exit 1
fi

DISCOVERY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/discovery" || true)"
CODE="$(python3 -c "import json,sys; print(json.load(sys.stdin).get('pairingCode',''))" <<<"$DISCOVERY" 2>/dev/null || true)"
if [[ -z "$CODE" ]]; then
  CODE="$(echo "$DISCOVERY" | sed -n 's/.*"pairingCode"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
fi

LAN_IPS="$(hostname -I 2>/dev/null || true)"
PRIMARY_IP="$(echo "$LAN_IPS" | awk '{print $1}')"

echo ""
echo "────────────────────────────────────────────"
echo "  WorkshopOS is running"
echo "────────────────────────────────────────────"
echo "  Open on this machine:"
echo "    http://127.0.0.1:${API_PORT}/connect"
if [[ -n "$PRIMARY_IP" ]]; then
  echo "  Open from a phone / PC on the same Wi‑Fi:"
  echo "    http://${PRIMARY_IP}:${API_PORT}/connect"
fi
echo ""
echo "  Pairing code:  ${CODE:-see /connect page}"
echo ""
echo "  Windows app:"
echo "    1. Install WorkshopOS Client"
echo "    2. Enter the pairing code  OR  tap Find on this network"
echo "    3. Complete setup on the first PC"
echo "────────────────────────────────────────────"
echo ""
echo "Logs:   cd $INSTALL_DIR/docker && docker compose --env-file .env logs -f api"
echo "Stop:   cd $INSTALL_DIR/docker && docker compose --env-file .env down"
echo ""
