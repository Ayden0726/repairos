#!/usr/bin/env bash
# Emergency reset of the shop OWNER password when you have server/Docker access
# (e.g. locked out and no other admin can reset from Settings → Users & Roles).
#
# Usage:
#   ./scripts/reset-owner-password.sh 'NewPasswordHere'
#   ./scripts/reset-owner-password.sh   # prompts (hidden)
#
# Password rules: min 10 chars, at least one upper, one lower, one digit.
#
# Alternative (env, one-shot restart):
#   export WORKSHOPOS_OWNER_PASSWORD_RESET='NewPasswordHere'
#   docker compose -f docker/docker-compose.yml up -d --force-recreate api
#   unset WORKSHOPOS_OWNER_PASSWORD_RESET
#   docker compose -f docker/docker-compose.yml up -d --force-recreate api
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker/docker-compose.yml"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Compose file not found: $COMPOSE_FILE" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required." >&2
  exit 1
fi

NEW_PASSWORD="${1:-}"
if [[ -z "$NEW_PASSWORD" ]]; then
  read -r -s -p "New owner password: " NEW_PASSWORD
  echo
  read -r -s -p "Confirm password: " CONFIRM
  echo
  if [[ "$NEW_PASSWORD" != "$CONFIRM" ]]; then
    echo "Passwords do not match." >&2
    exit 1
  fi
fi

if [[ ${#NEW_PASSWORD} -lt 10 ]]; then
  echo "Password must be at least 10 characters." >&2
  exit 1
fi
if ! [[ "$NEW_PASSWORD" =~ [A-Z] && "$NEW_PASSWORD" =~ [a-z] && "$NEW_PASSWORD" =~ [0-9] ]]; then
  echo "Password must include upper, lower and a number." >&2
  exit 1
fi

cd "$ROOT"

echo "==> Ensuring Postgres is up"
docker compose -f "$COMPOSE_FILE" up -d postgres
# Wait briefly for health
for _ in $(seq 1 30); do
  if docker compose -f "$COMPOSE_FILE" exec -T postgres pg_isready -U workshopos >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

echo "==> Resetting owner password via API image"
# Use the built api image; override entrypoint to run one-shot CLI mode (does not start HTTP).
docker compose -f "$COMPOSE_FILE" run --rm --no-deps \
  --entrypoint dotnet \
  api WorkshopOS.Api.dll --reset-owner-password "$NEW_PASSWORD"

echo
echo "Done. Sign in as the owner with the new password."
echo "If the API image is outdated, rebuild first:"
echo "  docker compose -f docker/docker-compose.yml up -d --build"
