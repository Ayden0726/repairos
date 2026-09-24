#!/usr/bin/env bash
# Thin wrapper — prefer the SimplyPrint-style one-liner:
#   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
exec bash "$ROOT/get-workshopos.sh" --dir "${1:-${WORKSHOPOS_HOME:-$HOME/workshopos}}"
