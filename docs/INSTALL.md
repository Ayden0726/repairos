# Install WorkshopOS

**Version 1.2.13+** — two pieces: a **server** (Docker, one command) and a **Windows client** (download `WorkshopOS-Setup-x.y.z.exe` from GitHub Releases).

```
  Client (Setup.exe)  <-->  API (:5088)  <-->  PostgreSQL
                              |
                           Worker
```

Do **not** compile the WinUI app for normal use. Use the Release installer.

---

## 1. What you need

| Piece | Requirement |
| --- | --- |
| Server host | Linux PC / NAS / VM / WSL2 / macOS with Docker |
| Git | Auto-installed by the setup script on common Linux distros |
| Docker + Compose v2 | Same — auto-installed when missing |
| Windows PCs | Windows 10/11 **x64**; download Setup.exe from Releases |
| Network | Shop PCs must reach the server on port **5088** (LAN) |

---

## 2. Install the server (one command)

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

The script:

1. Checks / installs **curl**, **Git**, **Docker** (+ Compose v2)
2. Clones (or updates) the repo into `~/workshopos` by default
3. Writes `docker/.env` with random secrets on first run
4. Runs `docker compose up -d --build`
5. Waits for **`/api/health`**, then prints the **connect URL** and **pairing code**

When it finishes, open:

- `http://127.0.0.1:5088/connect`
- or `http://<server-lan-ip>:5088/connect`

You’ll see a code like **`WOS-AB12`**.

### Options

```bash
# Custom directory / port
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash -s -- --dir ~/workshopos --port 5088

# Idempotent update (git pull + rebuild) — safe to re-run
~/workshopos/scripts/get-workshopos.sh --update
# or
~/workshopos/scripts/restart-workshopos.sh --update
```

### Manual / local clone

```bash
git clone https://github.com/Ayden0726/repairos.git
cd repairos
chmod +x scripts/*.sh
./scripts/get-workshopos.sh --dir "$(pwd)"
```

**chmod note:** if you see `Permission denied` on `.sh` files after clone or zip overlay:

```bash
chmod +x scripts/*.sh
```

### Continue after a partial failure

If clone succeeded but compose failed:

```bash
~/workshopos/scripts/continue-workshopos-setup.sh ~/workshopos
```

---

## 3. Install the Windows client (Setup.exe)

1. Open **[GitHub Releases](https://github.com/Ayden0726/repairos/releases)**
2. Download **`WorkshopOS-Setup-x.y.z.exe`** (example: `WorkshopOS-Setup-1.2.13.exe`)
3. Run it on each shop PC (Windows 10/11 x64)
4. Start the **server first**, then open the client

### Pairing

On the connect screen (appears within a few seconds — use **Enter server / pairing code** if needed):

- Enter the **pairing code** from `/connect`, **or**
- Tap **Find on this network** (same LAN), **or**
- Paste `http://<server-ip>:5088`

### First-run setup wizard

| PC | What happens |
| --- | --- |
| **First PC** | After connect, if setup is incomplete → **setup wizard** (business profile + owner account). This **does** exist in the product. |
| **Other PCs** | Sign in with staff accounts. Create staff under **Settings → Users & Roles** (no second install wizard). |

### Reset local client config

If the app keeps retrying a dead URL:

```powershell
Remove-Item -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS\client-settings.json"
```

Full wipe (settings + packaged data):

```powershell
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS"
Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'WorkshopOS' } |
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
```

Then relaunch and pair again.

### Windows Smart App Control / SmartScreen

Unsigned or newly published installers may be blocked:

- **SmartScreen**: More info → Run anyway (when you trust the Release from this repo)
- **Smart App Control** (Windows 11): may block unsigned apps in Enforcement mode — install while SAC is Off/Evaluation, or use an allow policy for your shop PCs
- Prefer the official **`WorkshopOS-Setup-*.exe`** from this repo’s Releases page only

---

## 4. Password reset

| Situation | How |
| --- | --- |
| Reset a staff user’s password | Owner/Admin → **Settings → Users & Roles** → select user → **Reset password…** |
| Change your own password | **Settings → App → Change password…** |
| Locked out of owner (have Docker/SSH) | See below — does **not** wipe the database |

```bash
cd ~/workshopos
chmod +x scripts/reset-owner-password.sh
./scripts/reset-owner-password.sh 'YourNewPassword1'
```

Details: [AUTH.md](AUTH.md).

---

## 5. Pricing & quotes (quick overview)

- Configure labour / markup / rounding / GST under **Settings → Pricing** and **Settings → Tax**
- Quote builder uses **server** pricing (`POST /api/pricing/preview`)
- Accepted quotes are frozen; settings changes do not rewrite history

Full doc: [PRICING_QUOTES.md](PRICING_QUOTES.md).

---

## 6. Clean server install (wipe and reinstall)

**WARNING:** This **deletes the database** and all shop data (repairs, customers, staff, volumes). Only for a brand-new shop or intentional wipe.

**WSL / Linux — wipe + reinstall:**

```bash
# Stop and remove containers + named volumes (DESTROYS DB)
cd ~/workshopos 2>/dev/null || cd ~/repairos 2>/dev/null || true
if [[ -f docker/docker-compose.yml ]]; then
  docker compose -f docker/docker-compose.yml down -v
fi

# Optional: remove the install directory entirely
# rm -rf ~/workshopos

# Fresh install
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash -s -- --dir ~/workshopos --port 5088

# Verify
curl -fsS http://127.0.0.1:5088/api/health
# Open http://127.0.0.1:5088/connect — then run Setup.exe on the first Windows PC
```

Wipe **only** Docker data but keep the git tree:

```bash
cd ~/workshopos
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d --build
```

After a clean server install, Windows PCs should clear saved client settings (section 3) and pair again.

---

## 7. Verify / health checks

```bash
curl -fsS http://127.0.0.1:5088/api/health
curl -fsS http://127.0.0.1:5088/api/discovery
```

Useful URLs:

| URL | Purpose |
| --- | --- |
| `/api/health` | Liveness + version |
| `/api/discovery` | Pairing code JSON |
| `/connect` | Human pairing page |
| `/swagger` | API docs (dev/ops) |

Logs:

```bash
cd ~/workshopos/docker
docker compose --env-file .env logs -f api
docker compose --env-file .env ps
```

---

## 8. Troubleshooting

### 404s on Pricing / Services / Users / Roles

The **Windows client is newer than the server**. Old Docker images return 404 for new API routes.

```bash
cd ~/workshopos
./scripts/restart-workshopos.sh --update
# or
./scripts/get-workshopos.sh --update
curl -fsS http://127.0.0.1:5088/api/health   # confirm version >= client
```

The client may show “Server outdated — update/restart WorkshopOS server”.

### Divergent git / pull won’t fast-forward

```bash
cd ~/workshopos
git status
git fetch origin
# WARNING: discards local commits on this clone
git reset --hard origin/main
./scripts/restart-workshopos.sh --update
```

Or install into a fresh directory: `get-workshopos.sh --dir ~/workshopos-new`.

### Permission denied on scripts

```bash
chmod +x scripts/*.sh
```

### Docker not usable / permission denied

```bash
sudo usermod -aG docker "$USER"
newgrp docker
# or re-run the installer with sudo
docker info
```

WSL: enable Docker Desktop **WSL integration**, or install Docker Engine inside the distro. Start the daemon before running the script.

### Port 5088 in use

```bash
ss -ltn | grep 5088
# Pick another port:
./scripts/get-workshopos.sh --port 5090
```

Then point the client at `http://<ip>:5090` or use the new `/connect` page.

### Containers won’t start

```bash
cd ~/workshopos/docker
docker compose --env-file .env logs api
docker compose --env-file .env logs postgres
df -h   # disk full?
```

### Continue helper

```bash
./scripts/continue-workshopos-setup.sh ~/workshopos
```

### Client can’t find server

- Server healthy? `curl http://<ip>:5088/api/health`
- Firewall allow **TCP 5088** on the server
- Same LAN / VPN as the shop PCs
- Clear `client-settings.json` and re-pair

---

## 9. Maintainer / advanced — build the Windows client

Shop installs should use **Setup.exe from Releases**. Building from source is only for maintainers publishing a new GitHub Release.

Prerequisites and commands: [apps/windows-client/README.md](../apps/windows-client/README.md) · [packaging/README.md](../packaging/README.md)

Publishing a Release tag (maintainers):

```bash
git tag v1.2.13
git push origin v1.2.13
```

Upload **`WorkshopOS-Setup-1.2.13.exe`** to the GitHub Release assets (or let CI attach build outputs when configured).
