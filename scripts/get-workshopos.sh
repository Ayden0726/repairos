#!/usr/bin/env bash
# WorkshopOS — one-command server install
#
#   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
#
# Installs Git + Docker (Compose v2) on common Linux distros when missing.
# Options:
#   bash get-workshopos.sh --dir /opt/workshopos --port 5088
#   bash get-workshopos.sh --update              # pull + rebuild (idempotent re-run)
#   bash get-workshopos.sh --skip-deps
#
# Windows client is NOT built here. Download WorkshopOS-Setup-x.y.z.exe from GitHub Releases.
#
set -euo pipefail

trap 'echo ""; echo "[ERROR] WorkshopOS setup failed at line $LINENO (exit $?). See messages above."; echo "        Help: https://github.com/Ayden0726/repairos/blob/main/docs/INSTALL.md"; echo ""' ERR

REPO_URL="${WORKSHOPOS_REPO:-https://github.com/Ayden0726/repairos.git}"
INSTALL_DIR="${WORKSHOPOS_HOME:-$HOME/workshopos}"
API_PORT="${API_PORT:-5088}"
BRANCH="${WORKSHOPOS_BRANCH:-main}"
SKIP_DEPS=0
DO_UPDATE=0
PRODUCT_VERSION="1.2.14+"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dir) INSTALL_DIR="$2"; shift 2 ;;
    --port) API_PORT="$2"; shift 2 ;;
    --repo) REPO_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    --skip-deps) SKIP_DEPS=1; shift ;;
    --update|-u) DO_UPDATE=1; shift ;;
    -h|--help)
      cat <<'HELP'
WorkshopOS server setup

Usage:
  curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
  bash get-workshopos.sh [options]

Options:
  --dir PATH       Install directory (default: ~/workshopos)
  --port N         API / connect port (default: 5088)
  --repo URL       Git remote (default: Ayden0726/repairos)
  --branch NAME    Branch to use (default: main)
  --update, -u     Pull latest + rebuild containers (safe re-run)
  --skip-deps      Do not auto-install Git/Docker
  -h, --help       Show this help

After the server is up, download the Windows installer from:
  https://github.com/Ayden0726/repairos/releases
  (WorkshopOS-Setup-x.y.z.exe — do not compile the WinUI client yourself)
HELP
      exit 0
      ;;
    *)
      echo "[ERROR] Unknown option: $1"
      echo "        Run with --help for usage."
      exit 1
      ;;
  esac
done

have() { command -v "$1" >/dev/null 2>&1; }

banner() {
  echo ""
  echo "============================================================"
  echo "  $1"
  echo "============================================================"
  echo ""
}

step() {
  echo ""
  echo "==> [$1] $2"
}

ok() { echo "    OK: $1"; }
warn() { echo "    WARN: $1"; }
fail() { echo "[ERROR] $1" >&2; exit 1; }

sudo_run() {
  if [[ "$(id -u)" -eq 0 ]]; then
    "$@"
  elif have sudo; then
    sudo "$@"
  else
    fail "Need root (or sudo) to install packages. Install Docker + Git yourself, or re-run as root."
  fi
}

detect_os() {
  if [[ "$(uname -s)" == "Darwin" ]]; then
    echo "macos"
    return
  fi
  if [[ -f /etc/os-release ]]; then
    # shellcheck disable=SC1091
    . /etc/os-release
    echo "${ID:-linux}"
    return
  fi
  echo "unknown"
}

note_chmod() {
  # Scripts must be executable when run from a local clone (curl|bash does not need this).
  if [[ -f "$0" ]] && [[ ! -x "$0" ]]; then
    warn "This script is not executable. Fix with:"
    echo "      chmod +x scripts/*.sh"
  fi
}

install_curl_if_needed() {
  have curl && { ok "curl present ($(curl --version | head -1 | cut -d' ' -f1-2))"; return 0; }
  step "deps" "Installing curl"
  local os; os="$(detect_os)"
  case "$os" in
    ubuntu|debian|raspbian|linuxmint|pop)
      sudo_run apt-get update -y
      sudo_run apt-get install -y curl ca-certificates
      ;;
    fedora|rhel|centos|rocky|almalinux)
      if have dnf; then sudo_run dnf install -y curl ca-certificates
      else sudo_run yum install -y curl ca-certificates
      fi
      ;;
    arch|manjaro)
      sudo_run pacman -Sy --noconfirm curl ca-certificates
      ;;
    opensuse*|sles)
      sudo_run zypper install -y curl ca-certificates
      ;;
    macos)
      fail "Install curl (usually present) or Homebrew: https://brew.sh"
      ;;
    *)
      fail "Cannot auto-install curl on this OS ($os). Install curl, then re-run."
      ;;
  esac
  ok "curl installed"
}

install_git_if_needed() {
  if have git; then
    ok "git present ($(git --version))"
    return 0
  fi
  step "deps" "Installing Git"
  local os; os="$(detect_os)"
  case "$os" in
    ubuntu|debian|raspbian|linuxmint|pop)
      sudo_run apt-get update -y
      sudo_run apt-get install -y git
      ;;
    fedora|rhel|centos|rocky|almalinux)
      if have dnf; then sudo_run dnf install -y git
      else sudo_run yum install -y git
      fi
      ;;
    arch|manjaro)
      sudo_run pacman -Sy --noconfirm git
      ;;
    opensuse*|sles)
      sudo_run zypper install -y git
      ;;
    macos)
      if have brew; then
        brew install git
      else
        echo "Install Git: xcode-select --install   or   https://git-scm.com/download/mac"
        echo "Or install Homebrew: https://brew.sh"
        exit 1
      fi
      ;;
    *)
      echo "Cannot auto-install Git on this OS ($os)."
      echo "  https://git-scm.com/downloads"
      exit 1
      ;;
  esac
  ok "git installed"
}

install_docker_if_needed() {
  if have docker && docker compose version >/dev/null 2>&1; then
    ok "docker present ($(docker --version | head -1))"
    ok "compose present ($(docker compose version 2>/dev/null | head -1))"
    return 0
  fi

  step "deps" "Installing Docker Engine + Compose plugin"
  local os; os="$(detect_os)"

  case "$os" in
    ubuntu|debian|raspbian|linuxmint|pop|fedora|centos|rhel|rocky|almalinux)
      curl -fsSL https://get.docker.com | sudo_run sh
      ;;
    arch|manjaro)
      sudo_run pacman -Sy --noconfirm docker docker-compose
      ;;
    opensuse*|sles)
      curl -fsSL https://get.docker.com | sudo_run sh || {
        fail "OpenSUSE: install Docker from https://docs.docker.com/engine/install/"
      }
      ;;
    macos)
      if have brew; then
        if ! have docker; then
          echo "Installing Docker Desktop via Homebrew (may prompt for password)..."
          brew install --cask docker
          echo "Open Docker Desktop once from Applications, wait until it says Running, then re-run this script."
          open -a Docker 2>/dev/null || true
          exit 1
        fi
      else
        echo "Install Docker Desktop for Mac: https://docs.docker.com/desktop/setup/install/mac-install/"
        echo "Or install Homebrew first: https://brew.sh"
        exit 1
      fi
      ;;
    *)
      echo "Cannot auto-install Docker on this OS ($os)."
      echo "Install Docker Engine + Compose: https://docs.docker.com/engine/install/"
      exit 1
      ;;
  esac

  if [[ "$(uname -s)" == "Linux" ]]; then
    if have systemctl; then
      sudo_run systemctl enable --now docker 2>/dev/null || sudo_run service docker start 2>/dev/null || true
    fi
    if [[ "$(id -u)" -ne 0 ]] && have sudo; then
      sudo_run usermod -aG docker "$USER" 2>/dev/null || true
      if ! docker info >/dev/null 2>&1; then
        warn "Docker installed. If you see 'permission denied', log out/in or run: newgrp docker"
        echo "      Or re-run this script with: sudo bash ..."
      fi
    fi
  fi

  have docker || fail "Docker install finished but 'docker' is not on PATH yet. Open a new terminal and re-run."

  if ! docker compose version >/dev/null 2>&1; then
    fail "Docker is installed but Compose v2 is missing. Install docker-compose-plugin: https://docs.docker.com/compose/install/"
  fi
  ok "docker + compose ready"
}

ensure_docker_usable() {
  if docker info >/dev/null 2>&1; then
    DOCKER=(docker)
    ok "Docker daemon reachable"
    return 0
  fi
  if have sudo && sudo docker info >/dev/null 2>&1; then
    warn "Using sudo for Docker (user not in docker group yet — log out/in later)"
    DOCKER=(sudo docker)
    return 0
  fi
  echo "[ERROR] Docker is installed but not usable yet."
  echo "  - Linux: sudo usermod -aG docker \$USER && newgrp docker"
  echo "  - Or re-run: curl -fsSL ... | sudo bash"
  echo "  - macOS: start Docker Desktop and wait until it is running"
  echo "  - WSL: ensure Docker Desktop WSL integration is on, or install Docker Engine in the distro"
  exit 1
}

check_port_hint() {
  local port="$1"
  if have ss; then
    if ss -ltn 2>/dev/null | grep -q ":${port} "; then
      # Port may already be our own API — that is fine for --update
      return 0
    fi
  fi
}

# Rewrite KEY=VALUE lines without sed/awk delimiters (secrets/paths may contain / + & etc.).
set_env_kv() {
  local file="$1" key="$2" value="$3" tmp line found=0
  tmp="$(mktemp)" || fail "could not create temp file while writing ${key} into ${file}"
  if [[ -f "$file" ]]; then
    while IFS= read -r line || [[ -n "$line" ]]; do
      if [[ "$line" == "${key}="* ]]; then
        printf '%s=%s\n' "$key" "$value"
        found=1
      else
        printf '%s\n' "$line"
      fi
    done < "$file" > "$tmp" || {
      rm -f "$tmp"
      fail "failed rewriting ${file} (${key})"
    }
  fi
  if [[ "$found" -eq 0 ]]; then
    printf '%s=%s\n' "$key" "$value" >> "$tmp" || {
      rm -f "$tmp"
      fail "failed appending ${key} to ${file}"
    }
  fi
  mv "$tmp" "$file" || {
    rm -f "$tmp"
    fail "failed to update ${file} with ${key}"
  }
}

pull_ff_or_warn() {
  git -C "$INSTALL_DIR" fetch --depth 1 origin "$BRANCH" || warn "git fetch failed — using local tree"
  git -C "$INSTALL_DIR" checkout "$BRANCH" 2>/dev/null || true
  if git -C "$INSTALL_DIR" pull --ff-only origin "$BRANCH"; then
    ok "pulled latest $BRANCH"
  else
    warn "git pull --ff-only failed (divergent local commits or dirty tree)."
    echo "      Inspect:  cd \"$INSTALL_DIR\" && git status && git log --oneline -5"
    echo "      Force to remote (DESTROYS local commits):"
    echo "        git fetch origin && git reset --hard origin/$BRANCH"
    echo "      Continuing with current tree..."
  fi
}

sync_repo() {
  mkdir -p "$(dirname "$INSTALL_DIR")"

  if [[ -d "$INSTALL_DIR/.git" ]]; then
    step "git" "Updating existing install at $INSTALL_DIR"
    pull_ff_or_warn
    return 0
  fi

  if [[ -d "$INSTALL_DIR" ]] && [[ -z "$(ls -A "$INSTALL_DIR" 2>/dev/null || true)" ]]; then
    rmdir "$INSTALL_DIR" 2>/dev/null || true
  fi

  if [[ ! -d "$INSTALL_DIR" ]]; then
    step "git" "Cloning $REPO_URL -> $INSTALL_DIR"
    git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$INSTALL_DIR"
    ok "cloned"
    return 0
  fi

  # Directory exists without .git
  if [[ -d "$INSTALL_DIR/docker" ]]; then
    warn "Directory exists without .git but has docker/ — continuing with local tree"
    echo "      Tip: for updates, use a proper clone or: git -C \"$INSTALL_DIR\" init && git remote add origin $REPO_URL"
    return 0
  fi

  fail "Directory $INSTALL_DIR exists but is not a WorkshopOS install. Remove it or pass --dir elsewhere."
}

print_success() {
  local code="$1"
  local primary_ip="$2"
  local health_json="${3:-}"

  banner "WorkshopOS is running (${PRODUCT_VERSION})"
  echo "  Connect portal (pairing code):"
  echo "    http://127.0.0.1:${API_PORT}/connect"
  if [[ -n "$primary_ip" ]]; then
    echo "    http://${primary_ip}:${API_PORT}/connect"
  fi
  echo ""
  echo "  Health:"
  echo "    http://127.0.0.1:${API_PORT}/api/health"
  if [[ -n "$health_json" ]]; then
    echo "    $health_json"
  fi
  echo ""
  echo "  Pairing code:  ${code:-see /connect page}"
  echo ""
  echo "  NEXT STEPS — Windows PCs"
  echo "  ----------------------------------------------------------"
  echo "  1. Download the installer from GitHub Releases:"
  echo "       https://github.com/Ayden0726/repairos/releases"
  echo "       File: WorkshopOS-Setup-x.y.z.exe"
  echo "  2. Run Setup.exe on each shop PC (Windows 10/11 x64)."
  echo "  3. Enter the pairing code above, or tap Find on this network."
  echo "  4. First PC: complete the shop setup wizard (business + owner)."
  echo "  5. Other PCs: sign in; add staff under Settings -> Users & Roles."
  echo ""
  echo "  Do NOT compile the WinUI client. Use the Release Setup.exe."
  echo "  ----------------------------------------------------------"
  echo ""
  echo "  Install dir:  $INSTALL_DIR"
  echo "  Update later: cd $INSTALL_DIR && ./scripts/get-workshopos.sh --update"
  echo "                (or: ./scripts/restart-workshopos.sh --update)"
  echo "  Logs:         cd $INSTALL_DIR/docker && docker compose --env-file .env logs -f api"
  echo "  Stop:         cd $INSTALL_DIR/docker && docker compose --env-file .env down"
  echo "  Docs:         $INSTALL_DIR/docs/INSTALL.md"
  echo ""
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

banner "WorkshopOS server setup (${PRODUCT_VERSION})"
echo "  Repo:    $REPO_URL ($BRANCH)"
echo "  Dir:     $INSTALL_DIR"
echo "  Port:    $API_PORT"
if [[ "$DO_UPDATE" -eq 1 ]]; then
  echo "  Mode:    --update (pull + rebuild)"
fi
echo ""
echo "  Tip: if you cloned the repo and scripts fail with Permission denied:"
echo "       chmod +x scripts/*.sh"
note_chmod

if [[ "$SKIP_DEPS" -eq 0 ]]; then
  step "1/5" "Checking / installing dependencies"
  install_curl_if_needed
  install_git_if_needed
  install_docker_if_needed
  ensure_docker_usable
else
  step "1/5" "Checking dependencies (--skip-deps)"
  have curl || fail "Missing curl"
  have git || fail "Missing git"
  have docker || fail "Missing docker"
  docker compose version >/dev/null 2>&1 || fail "Missing docker compose"
  DOCKER=(docker)
  ok "deps present"
fi

compose() { "${DOCKER[@]}" compose "$@"; }

step "2/5" "Syncing source"
sync_repo

# Idempotent: --update is the same path as a re-run on an existing clone
if [[ "$DO_UPDATE" -eq 1 ]]; then
  ok "--update: will rebuild containers after sync"
fi

cd "$INSTALL_DIR/docker" || fail "missing docker/ under $INSTALL_DIR — clone may be incomplete."

step "3/5" "Preparing docker/.env"
if [[ ! -f .env ]]; then
  cp .env.example .env || fail "could not copy docker/.env.example -> docker/.env"
  if have openssl; then
    PW="$(openssl rand -base64 32 | tr -d '\n=/+' | cut -c1-28)"
    KEY="$(openssl rand -base64 64 | tr -d '\n=/+' | cut -c1-48)"
  else
    PW="change-me-$(date +%s)"
    KEY="change-me-signing-key-$(date +%s)-must-be-long-enough"
  fi
  set_env_kv .env POSTGRES_PASSWORD "$PW"
  set_env_kv .env JWT_SIGNING_KEY "$KEY"
  set_env_kv .env API_PORT "$API_PORT"
  ok "Wrote docker/.env with random secrets"
else
  # Keep existing secrets; ensure port matches requested API_PORT when explicitly set
  set_env_kv .env API_PORT "$API_PORT"
  ok "Using existing docker/.env (API_PORT=$API_PORT)"
fi

check_port_hint "$API_PORT"

step "4/5" "Building & starting containers"
echo "    (first run can take several minutes)"
if ! compose --env-file .env up -d --build; then
  echo "[ERROR] docker compose up failed."
  echo "  Check: cd $INSTALL_DIR/docker && ${DOCKER[*]} compose --env-file .env logs"
  echo "  Common: Docker daemon not running; port $API_PORT in use; disk full"
  exit 1
fi
ok "containers started"

step "5/5" "Waiting for API health (up to ~2 min)"
ok_health=0
HEALTH_BODY=""
for i in $(seq 1 60); do
  if HEALTH_BODY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/health" 2>/dev/null)"; then
    ok_health=1
    break
  fi
  sleep 2
  if [[ $((i % 10)) -eq 0 ]]; then
    echo "    still waiting... (${i}/60)"
  fi
done

if [[ "$ok_health" != "1" ]]; then
  echo "[ERROR] API did not become healthy in time on port ${API_PORT}."
  echo "  Check: cd $INSTALL_DIR/docker && ${DOCKER[*]} compose --env-file .env logs api"
  echo "  Port:  ss -ltn | grep ${API_PORT}   or   curl -v http://127.0.0.1:${API_PORT}/api/health"
  echo "  Docs:  $INSTALL_DIR/docs/INSTALL.md  (Troubleshooting)"
  exit 1
fi
ok "API healthy"

DISCOVERY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/discovery" || true)"
CODE="$(python3 -c "import json,sys; print(json.load(sys.stdin).get('pairingCode',''))" <<<"$DISCOVERY" 2>/dev/null || true)"
if [[ -z "$CODE" ]]; then
  CODE="$(echo "$DISCOVERY" | sed -n 's|.*"pairingCode"[[:space:]]*:[[:space:]]*"\([^"]*\)".*|\1|p' | head -1)"
fi

LAN_IPS="$(hostname -I 2>/dev/null || true)"
PRIMARY_IP="$(echo "$LAN_IPS" | awk '{print $1}')"

print_success "$CODE" "$PRIMARY_IP" "$HEALTH_BODY"
