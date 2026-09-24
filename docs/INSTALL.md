# Install WorkshopOS

Two pieces: **server** (API + PostgreSQL, any Linux/Windows host with Docker) and **Windows client** (WinUI 3 app on each shop PC).

## 1. Install the server

### Option A — one-command install (recommended)

On a host with **Docker** + **git** + **curl**:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Options:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash -s -- --dir /opt/workshopos --port 5088
```

The script clones the repo, writes `docker/.env` with random secrets, starts containers, waits for health, then prints:

- Pairing code (e.g. `WOS-AB12`)
- Connect portal: `http://127.0.0.1:5088/connect` (and the LAN IP URL)

Open `/connect` on a phone or PC on the same Wi‑Fi to show the code to staff.

### Option B — from a git clone

```bash
git clone https://github.com/Ayden0726/repairos.git WorkshopOS
cd WorkshopOS
chmod +x scripts/install-server.sh scripts/get-workshopos.sh
./scripts/install-server.sh
```

`install-server.sh` delegates to `get-workshopos.sh` (same pairing / `/connect` messaging).

### Option C — from a release tarball

1. On [GitHub Releases](../../releases) download `WorkshopOS-Server-v*.tar.gz`
2. Extract and run:

```bash
tar -xzf WorkshopOS-Server-v*.tar.gz
cd WorkshopOS-Server-v*
chmod +x scripts/install-server.sh scripts/get-workshopos.sh
./scripts/install-server.sh "$(pwd)"
```

### Option D — manual Docker Compose

```bash
cd docker
cp .env.example .env
# set POSTGRES_PASSWORD and JWT_SIGNING_KEY (long random)
docker compose --env-file .env up -d --build
```

Then open `http://<host>:5088/connect` for the pairing code.

API: `http://<host>:5088` · Health: `/api/health` · Discovery: `/api/discovery` · Connect: `/connect` · Swagger: `/swagger`

Open firewall port **5088** (or set `API_PORT` in `.env`) to your LAN.

## 2. Install the Windows client

Build the installer on a Windows machine (or download the release zip/setup from GitHub Releases).

### Download (after you publish a release)

1. Open the repo on GitHub → **Releases**
2. Download `WorkshopOS-Setup-*.exe` **or** `WorkshopOS-Client-win-x64-*.zip`
3. Run the setup (or unzip and run `WorkshopOS.Client.exe`)
4. On the connect screen:
   - Enter the **pairing code** from `/connect` → **Connect with code**, **or**
   - Tap **Find on this network** (same LAN), **or**
   - Paste `http://<server-lan-ip>:5088` → **Connect with URL**
5. Complete first-run business setup (owner account) on the first PC, then sign in

### Build the client yourself (Windows only)

Requirements: Windows 10/11 **x64**, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Visual Studio 2022 with **Windows App SDK / WinUI** workload. Optional: [Inno Setup 6](https://jrsoftware.org/isinfo.php) for `WorkshopOS-Setup.exe`.

```powershell
cd WorkshopOS
# portable zip (+ installer if Inno is installed)
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0

# zip only
.\packaging\build-client.ps1 -SkipInstaller
```

Outputs land in `packaging/dist/`.

Dev run without packaging:

```powershell
dotnet run --project apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj
```

## 3. Publish downloads on GitHub

1. Create the GitHub repository (use **Create repo** in Cursor if this project is still local-only).
2. Push `main`.
3. Tag a release:

```bash
git tag v1.2.0
git push origin v1.2.0
```

4. GitHub Actions (`.github/workflows/release.yml`) builds the **server tarball** and **Windows client zip** and attaches them to the release.
5. Optionally run `packaging\build-client.ps1` on a Windows PC with Inno Setup and upload `WorkshopOS-Setup-*.exe` to the same release.

Shops then: one-command server install → open `/connect` → Windows app with pairing code or LAN find.

## 4. Verify

```bash
curl http://127.0.0.1:5088/api/health
# {"status":"Healthy","database":true,...}

curl http://127.0.0.1:5088/api/discovery
# {"product":"WorkshopOS","pairingCode":"WOS-....","setupComplete":false,...}
```

Open `http://127.0.0.1:5088/connect` in a browser for the pairing portal.

```bash
dotnet test WorkshopOS.sln
```
