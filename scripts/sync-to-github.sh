#!/usr/bin/env bash
# Commit this WorkshopOS tree and push to GitHub (Ayden0726/repairos).
#
#   ./scripts/sync-to-github.sh
#   ./scripts/sync-to-github.sh --tag v1.2.33
#   ./scripts/sync-to-github.sh --dry-run
#
# Run inside your local clone after overlaying the sync zip (docs/GITHUB_SYNC.md).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

REMOTE_URL="${GITHUB_REPO_URL:-https://github.com/Ayden0726/repairos.git}"
BRANCH="${GITHUB_BRANCH:-main}"
TAG="${GITHUB_TAG:-v1.2.33}"
MESSAGE="${GITHUB_COMMIT_MSG:-WorkshopOS 1.2.33: encrypted credentials, expenses, Raspberry Pi server}"
SKIP_TAG=0
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --remote) REMOTE_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    --tag) TAG="$2"; shift 2 ;;
    --message) MESSAGE="$2"; shift 2 ;;
    --skip-tag) SKIP_TAG=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help)
      sed -n '2,12p' "$0"
      exit 0
      ;;
    *) echo "[ERROR] Unknown option: $1"; exit 1 ;;
  esac
done

echo ""
echo "============================================================"
echo "  WorkshopOS -> GitHub sync"
echo "============================================================"
echo "  Root:   $ROOT"
echo "  Remote: $REMOTE_URL"
echo "  Branch: $BRANCH"
echo ""

[[ -d .git ]] || { echo "[ERROR] Not a git repo. Clone the GitHub repo first."; exit 1; }

rm -f \
  apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml \
  apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml.cs
rm -rf \
  apps/windows-client/WorkshopOS.Client/obj \
  apps/windows-client/WorkshopOS.Client/bin

git remote set-url origin "$REMOTE_URL"
git add -A

if git diff --cached --quiet && git diff --quiet; then
  echo "  Nothing new to commit (working tree clean)."
else
  git status --short
  if [[ "$DRY_RUN" -eq 1 ]]; then
    echo "  DryRun: skipping commit/push"
    exit 0
  fi
  git commit -m "$MESSAGE"
fi

[[ "$DRY_RUN" -eq 1 ]] && exit 0

echo "  Pushing $BRANCH..."
git push -u origin "$BRANCH"

if [[ "$SKIP_TAG" -eq 0 && -n "$TAG" ]]; then
  echo "  Tagging $TAG..."
  git tag -f "$TAG"
  git push -f origin "$TAG"
  echo "  Release workflow: https://github.com/Ayden0726/repairos/actions"
fi

echo ""
echo "  Done. Next:"
echo "    - Update server:  cd ~/workshopos && git pull && ./scripts/restart-workshopos.sh --update"
echo "    - Fresh server:   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash"
echo "    - Pi guide:       docs/RASPBERRY_PI.md"
echo "    - Full guide:     docs/GITHUB_SYNC.md"
echo ""
