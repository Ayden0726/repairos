# WorkshopOS

Self-hosted repair-shop operations for Australian electronics / computer repair businesses.

**Architecture:** Windows WinUI 3 client → REST + SignalR → ASP.NET Core API → PostgreSQL.

**Live API (this environment):** [Health](http://127.0.0.1:5088/api/health) · [Connect / pairing](http://127.0.0.1:5088/connect) · [Swagger](http://127.0.0.1:5088/swagger)

## Features

See **[docs/FEATURES.md](docs/FEATURES.md)**.

## Install server — one command

Needs [Docker](https://docs.docker.com/get-docker/) + [Git](https://git-scm.com/downloads):

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Then open **`http://<server-ip>:5088/connect`** — you’ll get a pairing code (`WOS-XXXX`) for the Windows app.

Full guide: **[docs/INSTALL.md](docs/INSTALL.md)**

## Connect Windows PCs

1. Start the **server** first; open `http://<server-ip>:5088/connect` for the pairing code.  
2. Install the WorkshopOS client (Release zip/setup, or build below).  
3. On the connect screen (appears within a few seconds — tap **Enter server / pairing code** if needed): enter the **pairing code**, or **Find on this network**, or paste the server URL.  
4. **First PC** runs the shop **setup wizard** (business + owner). Other PCs sign in; add staff under **Users**.  
5. Stuck on an old URL? Delete `%LOCALAPPDATA%\WorkshopOS\client-settings.json` and relaunch.

## Build the Windows client

**Windows 10/11 x64 only.** Install these first (all linked):

| Tool | Link |
| --- | --- |
| Git for Windows | https://git-scm.com/download/win |
| Visual Studio 2022 Community | https://visualstudio.microsoft.com/downloads/ |
| WinUI / Windows App SDK tools | https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment |
| .NET 8 SDK (x64) | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Inno Setup 6 (optional `.exe` installer) | https://jrsoftware.org/isdl.php |
| Windows SDK | https://developer.microsoft.com/windows/downloads/windows-sdk/ |

In Visual Studio Installer, enable workload **WinUI application development**.

```powershell
git clone https://github.com/Ayden0726/repairos.git
cd repairos
# Developer PowerShell for VS; expect banner "WorkshopOS client build script v4"
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.2 -SkipInstaller
```

Step-by-step + workload checklist: **[apps/windows-client/README.md](apps/windows-client/README.md)**

## Repository layout

```
apps/windows-client/   WinUI 3 desktop app
services/api/          ASP.NET Core API + /connect portal
docker/                Compose + Dockerfiles
packaging/             Windows client build + Inno Setup
scripts/               get-workshopos.sh (one-liner), install, publish
docs/                  Features, install, architecture
```

## Develop / test API

```bash
cd services/api/WorkshopOS.Api && dotnet run
dotnet test WorkshopOS.sln
```
