#!/usr/bin/env bash
# Continue WorkshopOS setup after a failed/partial get-workshopos.sh (post-clone).
#
#   ./scripts/continue-workshopos-setup.sh [install-dir]
#
# ASCII-safe. Does not build the Windows client — use GitHub Releases Setup.exe.
#
set -euo pipefail

INSTALL_DIR="${1:-$HOME/workshopos}"
API_PORT="${API_PORT:-5088}"

echo ""
echo "============================================================"
echo "  WorkshopOS continue setup"
echo "============================================================"
echo ""
echo "  Dir:  $INSTALL_DIR"
echo "  Port: $API_PORT"
echo ""

if [[ ! -d "$INSTALL_DIR/docker" ]]; then
  echo "[ERROR] Missing $INSTALL_DIR/docker"
  echo "        Run the full installer first:"
  echo "          curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash"
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERROR] Docker is required." >&2
  exit 1
fi

cd "$INSTALL_DIR/docker"

if [[ ! -f .env ]]; then
  echo "==> Creating docker/.env from .env.example"
  cp .env.example .env
  if command -v openssl >/dev/null 2>&1; then
    PW="$(openssl rand -base64 32 | tr -d '\n=/+' | cut -c1-28)"
    KEY="$(openssl rand -base64 64 | tr -d '\n=/+' | cut -c1-48)"
  else
    PW="change-me-$(date +%s)"
    KEY="change-me-signing-key-$(date +%s)-must-be-long-enough"
  fi
  if command -v python3 >/dev/null 2>&1; then
    python3 - "$PW" "$KEY" "$API_PORT" <<'PY'
import sys
pw, key, port = sys.argv[1], sys.argv[2], sys.argv[3]
path = ".env"
lines = open(path).read().splitlines()
out, seen = [], set()
for line in lines:
    if line.startswith("POSTGRES_PASSWORD="):
        out.append(f"POSTGRES_PASSWORD={pw}"); seen.add("POSTGRES_PASSWORD")
    elif line.startswith("JWT_SIGNING_KEY="):
        out.append(f"JWT_SIGNING_KEY={key}"); seen.add("JWT_SIGNING_KEY")
    elif line.startswith("API_PORT="):
        out.append(f"API_PORT={port}"); seen.add("API_PORT")
    else:
        out.append(line)
if "POSTGRES_PASSWORD" not in seen: out.append(f"POSTGRES_PASSWORD={pw}")
if "JWT_SIGNING_KEY" not in seen: out.append(f"JWT_SIGNING_KEY={key}")
if "API_PORT" not in seen: out.append(f"API_PORT={port}")
open(path, "w").write("\n".join(out) + "\n")
print("Wrote docker/.env with random secrets")
PY
  else
    echo "POSTGRES_PASSWORD=$PW" >> .env
    echo "JWT_SIGNING_KEY=$KEY" >> .env
    echo "API_PORT=$API_PORT" >> .env
    echo "    OK: appended secrets to .env"
  fi
else
  echo "==> Using existing docker/.env"
fi

echo "==> Building & starting containers"
docker compose --env-file .env up -d --build

echo "==> Waiting for health..."
ok=0
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null 2>&1; then
    ok=1
    break
  fi
  sleep 2
done

if [[ "$ok" != "1" ]]; then
  echo "[ERROR] API did not become healthy."
  echo "  docker compose --env-file .env logs api"
  exit 1
fi

BODY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/health" || true)"
echo "    OK: $BODY"
echo ""
echo "  Connect: http://127.0.0.1:${API_PORT}/connect"
echo ""
echo "  Next: download WorkshopOS-Setup-x.y.z.exe from"
echo "        https://github.com/Ayden0726/repairos/releases"
echo ""
