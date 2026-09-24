#!/usr/bin/env bash
# WorkshopOS — one-command server install (SimplyPrint-style)
#
#   curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
#
# Installs Git + Docker (Compose v2) automatically on common Linux distros when missing.
# Or with options:
#   curl -fsSL ... | bash -s -- --dir /opt/workshopos --port 5088
#
set -euo pipefail

REPO_URL="${WORKSHOPOS_REPO:-https://github.com/Ayden0726/repairos.git}"
INSTALL_DIR="${WORKSHOPOS_HOME:-$HOME/workshopos}"
API_PORT="${API_PORT:-5088}"
BRANCH="${WORKSHOPOS_BRANCH:-main}"
SKIP_DEPS=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dir) INSTALL_DIR="$2"; shift 2 ;;
    --port) API_PORT="$2"; shift 2 ;;
    --repo) REPO_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    --skip-deps) SKIP_DEPS=1; shift ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

have() { command -v "$1" >/dev/null 2>&1; }

sudo_run() {
  if [[ "$(id -u)" -eq 0 ]]; then
    "$@"
  elif have sudo; then
    sudo "$@"
  else
    echo "Need root (or sudo) to install packages. Re-run as root, or install Docker + Git yourself."
    exit 1
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

install_curl_if_needed() {
  have curl && return 0
  echo "==> Installing curl"
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
      echo "Install curl (usually already present) or Homebrew: https://brew.sh"
      exit 1
      ;;
    *)
      echo "Cannot auto-install curl on this OS ($os). Install curl, then re-run."
      exit 1
      ;;
  esac
}

install_git_if_needed() {
  have git && return 0
  echo "==> Installing Git"
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
      echo "Cannot auto-install Git on this OS ($os). Install Git, then re-run."
      echo "  https://git-scm.com/downloads"
      exit 1
      ;;
  esac
}

install_docker_if_needed() {
  if have docker && docker compose version >/dev/null 2>&1; then
    return 0
  fi

  echo "==> Installing Docker Engine + Compose plugin"
  local os; os="$(detect_os)"

  case "$os" in
    ubuntu|debian|raspbian|linuxmint|pop)
      # Official convenience script covers current Debian/Ubuntu families.
      curl -fsSL https://get.docker.com | sudo_run sh
      ;;
    fedora|centos|rhel|rocky|almalinux)
      curl -fsSL https://get.docker.com | sudo_run sh
      ;;
    arch|manjaro)
      sudo_run pacman -Sy --noconfirm docker docker-compose
      ;;
    opensuse*|sles)
      curl -fsSL https://get.docker.com | sudo_run sh || {
        echo "OpenSUSE: install Docker from https://docs.docker.com/engine/install/"
        exit 1
      }
      ;;
    macos)
      if have brew; then
        if ! have docker; then
          echo "Installing Docker Desktop via Homebrew (may prompt for password)…"
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

  # Start service on Linux
  if [[ "$(uname -s)" == "Linux" ]]; then
    if have systemctl; then
      sudo_run systemctl enable --now docker 2>/dev/null || sudo_run service docker start 2>/dev/null || true
    fi
    # Allow current user to run docker without sudo (takes effect on next login; try now too)
    if [[ "$(id -u)" -ne 0 ]] && have sudo; then
      sudo_run usermod -aG docker "$USER" 2>/dev/null || true
      # Use sg/docker group for remaining commands when possible
      if ! docker info >/dev/null 2>&1; then
        echo "==> Docker installed. If 'permission denied', log out/in or run: newgrp docker"
        echo "    Or re-run this script with: sudo bash ..."
      fi
    fi
  fi

  if ! have docker; then
    echo "Docker install finished but 'docker' is not on PATH yet. Open a new terminal and re-run."
    exit 1
  fi

  if ! docker compose version >/dev/null 2>&1; then
    echo "Docker is installed but Compose v2 is missing."
    echo "Install docker-compose-plugin, then re-run. Docs: https://docs.docker.com/compose/install/"
    exit 1
  fi
}

ensure_docker_usable() {
  if docker info >/dev/null 2>&1; then
    DOCKER=(docker)
    return 0
  fi
  if have sudo && sudo docker info >/dev/null 2>&1; then
    echo "==> Using sudo for Docker (user not in docker group yet — log out/in later to fix)"
    DOCKER=(sudo docker)
    return 0
  fi
  echo "Docker is installed but not usable yet."
  echo "  • Linux: sudo usermod -aG docker \$USER && newgrp docker"
  echo "  • Or re-run: curl -fsSL ... | sudo bash"
  echo "  • macOS: start Docker Desktop and wait until it is running"
  exit 1
}

echo ""
echo "╔══════════════════════════════════════════╗"
echo "║         WorkshopOS server setup          ║"
echo "╚══════════════════════════════════════════╝"
echo ""

if [[ "$SKIP_DEPS" -eq 0 ]]; then
  install_curl_if_needed
  install_git_if_needed
  install_docker_if_needed
  ensure_docker_usable
else
  have curl || { echo "Missing curl"; exit 1; }
  have git || { echo "Missing git"; exit 1; }
  have docker || { echo "Missing docker"; exit 1; }
  docker compose version >/dev/null 2>&1 || { echo "Missing docker compose"; exit 1; }
fi

if [[ ! -d "$INSTALL_DIR/.git" ]]; then
  echo "==> Cloning $REPO_URL → $INSTALL_DIR"
  mkdir -p "$(dirname "$INSTALL_DIR")"
  if [[ -d "$INSTALL_DIR" ]] && [[ -z "$(ls -A "$INSTALL_DIR" 2>/dev/null || true)" ]]; then
    rmdir "$INSTALL_DIR" 2>/dev/null || true
  fi
  if [[ -d "$INSTALL_DIR" ]]; then
    echo "Directory exists — updating instead of cloning"
    git -C "$INSTALL_DIR" fetch --depth 1 origin "$BRANCH" || true
    git -C "$INSTALL_DIR" checkout "$BRANCH" || true
    git -C "$INSTALL_DIR" pull --ff-only origin "$BRANCH" || true
  else
    git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$INSTALL_DIR"
  fi
else
  echo "==> Updating existing install at $INSTALL_DIR"
  git -C "$INSTALL_DIR" fetch --depth 1 origin "$BRANCH"
  git -C "$INSTALL_DIR" checkout "$BRANCH"
  git -C "$INSTALL_DIR" pull --ff-only origin "$BRANCH" || true
fi

cd "$INSTALL_DIR/docker"
if [[ ! -f .env ]]; then
  cp .env.example .env
  if have openssl; then
    PW="$(openssl rand -base64 24 | tr -d '\n=/+' | cut -c1-28)"
    KEY="$(openssl rand -base64 48 | tr -d '\n')"
  else
    PW="change-me-$(date +%s)"
    KEY="change-me-signing-key-$(date +%s)-must-be-long-enough"
  fi
  tmp="$(mktemp)"
  sed "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${PW}/" .env | sed "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=${KEY}/" > "$tmp"
  mv "$tmp" .env
  grep -q '^API_PORT=' .env || echo "API_PORT=${API_PORT}" >> .env
  tmp="$(mktemp)"
  sed "s/^API_PORT=.*/API_PORT=${API_PORT}/" .env > "$tmp"
  mv "$tmp" .env
  echo "==> Wrote docker/.env with random secrets"
else
  echo "==> Using existing docker/.env"
fi

echo "==> Building & starting containers (first run can take a few minutes)"
docker compose --env-file .env up -d --build

echo "==> Waiting for API health…"
ok=0
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null 2>&1; then
    ok=1
    break
  fi
  sleep 2
done

if [[ "$ok" != "1" ]]; then
  echo "API did not become healthy in time. Check: docker compose -f $INSTALL_DIR/docker/docker-compose.yml --env-file $INSTALL_DIR/docker/.env logs api"
  exit 1
fi

DISCOVERY="$(curl -fsS "http://127.0.0.1:${API_PORT}/api/discovery" || true)"
CODE="$(python3 -c "import json,sys; print(json.load(sys.stdin).get('pairingCode',''))" <<<"$DISCOVERY" 2>/dev/null || true)"
if [[ -z "$CODE" ]]; then
  CODE="$(echo "$DISCOVERY" | sed -n 's/.*"pairingCode"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
fi

LAN_IPS="$(hostname -I 2>/dev/null || true)"
PRIMARY_IP="$(echo "$LAN_IPS" | awk '{print $1}')"

echo ""
echo "────────────────────────────────────────────"
echo "  WorkshopOS is running"
echo "────────────────────────────────────────────"
echo "  Open on this machine:"
echo "    http://127.0.0.1:${API_PORT}/connect"
if [[ -n "$PRIMARY_IP" ]]; then
  echo "  Open from a phone / PC on the same Wi‑Fi:"
  echo "    http://${PRIMARY_IP}:${API_PORT}/connect"
fi
echo ""
echo "  Pairing code:  ${CODE:-see /connect page}"
echo ""
echo "  Windows app:"
echo "    1. Install WorkshopOS Client"
echo "    2. Enter the pairing code  OR  tap Find on this network"
echo "    3. Complete setup on the first PC"
echo "────────────────────────────────────────────"
echo ""
echo "Logs:   cd $INSTALL_DIR/docker && docker compose --env-file .env logs -f api"
echo "Stop:   cd $INSTALL_DIR/docker && docker compose --env-file .env down"
echo ""
