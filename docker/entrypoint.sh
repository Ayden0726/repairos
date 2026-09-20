#!/usr/bin/env bash
set -euo pipefail
npx prisma migrate deploy
if [ "${SEED_DEMO:-false}" = "true" ]; then
  npx tsx prisma/seed.ts || true
fi
if [ "${1:-}" = "node" ]; then
  exec "$@"
fi
exec npx next start -H 0.0.0.0 -p 3000
