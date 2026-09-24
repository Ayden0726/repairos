#!/usr/bin/env bash
# Push WorkshopOS to GitHub using GITHUB_TOKEN from the environment.
# Usage: ./scripts/push-to-github.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

REPO_URL="${GITHUB_REPO_URL:-https://github.com/Ayden0726/repairos.git}"
TOKEN="${GITHUB_TOKEN:-${GH_TOKEN:-}}"

if [[ -z "$TOKEN" ]]; then
  echo "GITHUB_TOKEN is not set in this environment."
  exit 1
fi

git remote remove github 2>/dev/null || true
# Prefer x-access-token form for PATs / fine-grained tokens
git remote add github "https://x-access-token:${TOKEN}@github.com/Ayden0726/repairos.git"

echo "==> Pushing main"
git push -u github main

if ! git rev-parse v1.2.0 >/dev/null 2>&1; then
  git tag v1.2.0
fi
echo "==> Pushing tag v1.2.0 (triggers Windows client build on Actions)"
git push github v1.2.0 || git push github refs/tags/v1.2.0

# Scrub token from remote URL after push
git remote set-url github "$REPO_URL"
echo "Done. Check https://github.com/Ayden0726/repairos/actions and Releases."
