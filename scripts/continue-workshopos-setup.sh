#!/usr/bin/env bash
# Continue WorkshopOS setup after a failed get-workshopos.sh (post-clone).
set -euo pipefail
INSTALL_DIR="${1:-$HOME/workshopos}"
API_PORT="${API_PORT:-5088}"
cd "$INSTALL_DIR/docker"
if [[ ! -f .env ]]; then
  cp .env.example .env
  PW="$(openssl rand -base64 32 | tr -d '\n=/+' | cut -c1-28)"
  KEY="$(openssl rand -base64 64 | tr -d '\n=/+' | cut -c1-48)"
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
  echo "Using existing docker/.env"
fi
docker compose --env-file .env up -d --build
echo "Waiting for health..."
for i in $(seq 1 60); do
  curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null 2>&1 && break
  sleep 2
done
curl -fsS "http://127.0.0.1:${API_PORT}/api/health" || true
echo
echo "Open: http://127.0.0.1:${API_PORT}/connect"
