#!/usr/bin/env bash
set -euo pipefail
# Restore is intentionally explicit. Create a safety dump first, then apply the chosen backup SQL.
# Usage: ./scripts/restore.sh /path/to/workshopos-backup.tar

BACKUP_FILE="${1:?Provide a backup archive}"
SAFETY_DIR="${BACKUP_DIR:-./data/backups}/safety-$(date +%Y%m%d%H%M%S)"
mkdir -p "$SAFETY_DIR"
echo "Creating safety dump in $SAFETY_DIR"
pg_dump "$DATABASE_URL" --no-owner -f "$SAFETY_DIR/pre-restore.sql"
WORKDIR="$(mktemp -d)"
tar -xf "$BACKUP_FILE" -C "$WORKDIR"
echo "About to restore $WORKDIR/database.sql into $DATABASE_URL"
echo "This is destructive. Press Ctrl+C now to abort."
sleep 5
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$WORKDIR/database.sql"
echo "Restore finished. Safety dump: $SAFETY_DIR/pre-restore.sql"
