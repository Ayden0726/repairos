#!/usr/bin/env bash
# Overlay an agent sync zip onto a local clone, then commit + push to GitHub.
#
# Usage (WSL / Linux / macOS), from your clone of Ayden0726/repairos:
#   curl -fsSL -o /tmp/repairos-github-sync.zip 'DOWNLOAD_URL'
#   bash scripts/apply-agent-sync.sh /tmp/repairos-github-sync.zip
#   # or, if this script is not in the clone yet:
#   unzip -o /tmp/repairos-github-sync.zip -d /path/to/clone
#   cd /path/to/clone && bash scripts/apply-agent-sync.sh --already-extracted
#
# Requires: git auth to https://github.com/Ayden0726/repairos.git (gh auth / credential manager / PAT)
set -euo pipefail

REPO_URL="${GITHUB_REPO_URL:-https://github.com/Ayden0726/repairos.git}"
BRANCH="${GITHUB_BRANCH:-main}"
ZIP=""
ALREADY=0

usage() {
  sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --already-extracted) ALREADY=1; shift ;;
    -h|--help) usage 0 ;;
    *) ZIP="$1"; shift ;;
  esac
done

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ "$ALREADY" -eq 0 ]]; then
  if [[ -z "$ZIP" || ! -f "$ZIP" ]]; then
    echo "Provide path to repairos-github-sync.zip, or use --already-extracted after unzip."
    usage 1
  fi
  echo "==> Extracting $ZIP over $ROOT"
  unzip -o "$ZIP" -d "$ROOT"
fi

echo "==> Staging all changes"
git add -A
if git diff --cached --quiet; then
  echo "No file changes to commit (tree already matches sync)."
else
  git commit -m "$(cat <<'EOF'
Sync agent workspace fixes to GitHub

EnableMsixTooling, build-client v4, get-workshopos sed fix, continue helper, docs.
EOF
)"
fi

echo "==> Pushing $BRANCH to $REPO_URL"
git remote get-url origin >/dev/null 2>&1 || git remote add origin "$REPO_URL"
git remote set-url origin "$REPO_URL"
git push -u origin "$BRANCH"

echo "Done. Verify: https://github.com/Ayden0726/repairos"
echo "Optional release tag: git tag v1.2.0 && git push origin v1.2.0"
