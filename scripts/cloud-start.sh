#!/usr/bin/env bash
# Per-boot start: PostgreSQL + WorkshopOS API on :5088 (foreground).
set -euo pipefail
cd /workspace

export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"
export DOTNET_ROOT="${HOME}/.dotnet"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ConnectionStrings__Default="${ConnectionStrings__Default:-Host=127.0.0.1;Port=5432;Database=workshopos_net;Username=workshopos;Password=workshopos_dev}"
export Jwt__SigningKey="${Jwt__SigningKey:-dev-only-workshopos-jwt-signing-key-change-me-32+}"

if command -v pg_isready >/dev/null 2>&1; then
  if ! pg_isready -h 127.0.0.1 -p 5432 >/dev/null 2>&1; then
    sudo service postgresql start || sudo systemctl start postgresql || true
    for _ in $(seq 1 30); do
      pg_isready -h 127.0.0.1 -p 5432 >/dev/null 2>&1 && break
      sleep 1
    done
  fi
fi

# Free the port if a stale API is holding it
if command -v fuser >/dev/null 2>&1; then
  fuser -k 5088/tcp 2>/dev/null || true
fi

cd /workspace/services/api/WorkshopOS.Api
exec dotnet run --no-launch-profile --urls http://127.0.0.1:5088
