#!/usr/bin/env bash
# Install WorkshopOS server (API + PostgreSQL + worker).
#
# Preferred one-liner (SimplyPrint-style):
#   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
#
# This script wraps the same flow for local checkouts / release tarballs:
#   ./scripts/install-server.sh [/opt/workshopos]
#   WORKSHOPOS_REPO=https://github.com/Ayden0726/repairos.git ./scripts/install-server.sh
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INSTALL_DIR="${1:-${WORKSHOPOS_HOME:-$HOME/workshopos}}"
API_PORT="${API_PORT:-5088}"
REPO_URL="${WORKSHOPOS_REPO:-https://github.com/Ayden0726/repairos.git}"
BRANCH="${WORKSHOPOS_BRANCH:-main}"

export WORKSHOPOS_HOME="$INSTALL_DIR"
export WORKSHOPOS_REPO="$REPO_URL"
export WORKSHOPOS_BRANCH="$BRANCH"
export API_PORT

# If we already have get-workshopos.sh next to us (git clone / tarball), use it.
if [[ -x "$SCRIPT_DIR/get-workshopos.sh" ]] || [[ -f "$SCRIPT_DIR/get-workshopos.sh" ]]; then
  echo "==> Delegating to get-workshopos.sh (one-command installer)"
  exec bash "$SCRIPT_DIR/get-workshopos.sh" --dir "$INSTALL_DIR" --port "$API_PORT" --repo "$REPO_URL" --branch "$BRANCH"
fi

# Fallback: fetch the installer from the repo (e.g. copied alone)
echo "==> Fetching get-workshopos.sh"
tmp="$(mktemp)"
curl -fsSL "https://raw.githubusercontent.com/Ayden0726/repairos/${BRANCH}/scripts/get-workshopos.sh" -o "$tmp"
chmod +x "$tmp"
exec bash "$tmp" --dir "$INSTALL_DIR" --port "$API_PORT" --repo "$REPO_URL" --branch "$BRANCH"
