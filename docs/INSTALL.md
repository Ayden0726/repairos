# Install WorkshopOS

Two pieces: **server** (API + PostgreSQL, any Linux/Windows host with Docker) and **Windows client** (WinUI 3 app on each shop PC).

## 1. Install the server

### Option A — from a git clone (recommended)

```bash
git clone <your-github-repo-url> WorkshopOS
cd WorkshopOS
chmod +x scripts/install-server.sh
./scripts/install-server.sh
```

Defaults to `/opt/workshopos`. Override: `./scripts/install-server.sh ~/workshopos`.

### Option B — from a release tarball

1. On [GitHub Releases](../../releases) download `WorkshopOS-Server-v*.tar.gz`
2. Extract and run:

```bash
tar -xzf WorkshopOS-Server-v*.tar.gz
cd WorkshopOS-Server-v*
chmod +x scripts/install-server.sh
./scripts/install-server.sh "$(pwd)"
```

### Option C — manual Docker Compose

```bash
cd docker
cp .env.example .env
# set POSTGRES_PASSWORD and JWT_SIGNING_KEY (long random)
docker compose --env-file .env up -d --build
```

API: `http://<host>:5088` · Health: `/api/health` · Swagger: `/swagger`

Open firewall port **5088** (or set `API_PORT` in `.env`) to your LAN.

## 2. Install the Windows client

Build the installer on a Windows machine (or download the release zip/setup from GitHub Releases).

### Download (after you publish a release)

1. Open the repo on GitHub → **Releases**
2. Download `WorkshopOS-Setup-*.exe` **or** `WorkshopOS-Client-win-x64-*.zip`
3. Run the setup (or unzip and run `WorkshopOS.Client.exe`)
4. Enter server URL, e.g. `http://192.168.1.50:5088`
5. Complete first-run business setup (owner account), then sign in

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

Shops then: download server tarball → install with Docker → download Windows installer → connect to the API URL.

## 4. Verify

```bash
curl http://127.0.0.1:5088/api/health
# {"status":"Healthy","database":true,...}
```

```bash
dotnet test WorkshopOS.sln
```
