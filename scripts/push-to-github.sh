#!/usr/bin/env bash
# Push WorkshopOS to GitHub using GITHUB_TOKEN from the environment.
#
#   export GITHUB_TOKEN=ghp_...   # or GH_TOKEN
#   ./scripts/push-to-github.sh
#   ./scripts/push-to-github.sh --tag v1.2.34
#
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

REPO_URL="${GITHUB_REPO_URL:-https://github.com/Ayden0726/repairos.git}"
TOKEN="${GITHUB_TOKEN:-${GH_TOKEN:-}}"
TAG="${GITHUB_TAG:-v1.2.34}"
SKIP_TAG=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag) TAG="$2"; shift 2 ;;
    --skip-tag) SKIP_TAG=1; shift ;;
    -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
    *) echo "[ERROR] Unknown option: $1"; exit 1 ;;
  esac
done

if [[ -z "$TOKEN" ]]; then
  echo "GITHUB_TOKEN is not set."
  echo "  export GITHUB_TOKEN=ghp_your_pat"
  echo "  Or use ./scripts/sync-to-github.sh with your normal git credentials."
  exit 1
fi

git remote remove github 2>/dev/null || true
git remote add github "https://x-access-token:${TOKEN}@github.com/Ayden0726/repairos.git"

echo "==> Pushing main"
git push -u github main

if [[ "$SKIP_TAG" -eq 0 && -n "$TAG" ]]; then
  echo "==> Pushing tag $TAG (triggers Release workflow when refs/tags/v*)"
  git tag -f "$TAG"
  git push -f github "$TAG" || git push -f github "refs/tags/$TAG"
fi

git remote set-url github "$REPO_URL"
echo "Done. Check https://github.com/Ayden0726/repairos/actions and Releases."
