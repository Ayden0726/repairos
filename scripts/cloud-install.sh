#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for WorkshopOS API development.
set -euo pipefail
cd /workspace

echo "==> Ensuring PostgreSQL is running"
if command -v pg_isready >/dev/null 2>&1; then
  if ! pg_isready -h 127.0.0.1 -p 5432 >/dev/null 2>&1; then
    sudo service postgresql start || sudo systemctl start postgresql || true
  fi
fi

echo "==> Ensuring workshopos role and databases exist"
sudo -u postgres psql -v ON_ERROR_STOP=1 <<'SQL' || true
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'workshopos') THEN
    CREATE ROLE workshopos LOGIN PASSWORD 'workshopos_dev' SUPERUSER;
  END IF;
END
$$;
SQL

for db in workshopos_net workshopos_api_tests workshopos_test workshopos; do
  if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='${db}'" | grep -q 1; then
    sudo -u postgres createdb -O workshopos "$db"
  fi
done

export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"
export DOTNET_ROOT="${HOME}/.dotnet"

echo "==> Restoring and building WorkshopOS.sln"
dotnet restore WorkshopOS.sln
dotnet build WorkshopOS.sln -c Debug --no-restore

echo "==> cloud-install complete"
