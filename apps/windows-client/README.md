# Windows client (WinUI 3)

Build and run on **Windows 10 version 1809+** or **Windows 11**, **64-bit only**.  
This project **cannot** be built on Linux — use a Windows PC or the `windows-latest` GitHub Actions job.

## What to install (with links)

Install these in order on the build PC:

| # | Tool | Why | Download / docs |
| --- | --- | --- | --- |
| 1 | **Git for Windows** | Clone the repo | [git-scm.com/download/win](https://git-scm.com/download/win) |
| 2 | **Visual Studio 2022** (Community is free) | WinUI / Windows App SDK tooling | [visualstudio.microsoft.com/downloads](https://visualstudio.microsoft.com/downloads/) |
| 3 | VS workload **WinUI application development** | Compiles WinUI 3 / Windows App SDK apps | In VS Installer → Workloads. Guide: [Install tools for the Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment) · WinUI tutorial: [Create a WinUI 3 app](https://learn.microsoft.com/en-us/visualstudio/get-started/csharp/tutorial-wasdk?view=vs-2022) |
| 4 | **.NET 8 SDK** (x64) | `dotnet build` / `dotnet publish` | [.NET 8 downloads](https://dotnet.microsoft.com/download/dotnet/8.0) → **SDK** → Windows x64 installer |
| 5 | **Windows 10/11 SDK** (via VS) | Target `net8.0-windows10.0.19041.0` | Included with the WinUI workload; details: [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) |
| 6 | **Inno Setup 6** *(optional)* | Builds `WorkshopOS-Setup-*.exe` | [jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php) · [Download Inno Setup](https://jrsoftware.org/isdl.php) |

### Visual Studio workload checklist

In **Visual Studio Installer** → **Modify** → **Workloads**, enable:

- **WinUI application development** (in VS 17.10–17.12 this was named **Windows application development**)

Under **Individual components** (if not pulled in automatically), confirm:

- .NET 8.0 SDK / runtime  
- Windows App SDK C# templates  
- Windows 10 SDK (10.0.19041.0 or newer)

Official setup: [Set up your development environment](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment)

### Verify tools

```powershell
git --version
dotnet --list-sdks
# expect an 8.0.x SDK line
```

## Get the source

```powershell
git clone https://github.com/Ayden0726/repairos.git
cd repairos
```

(If the GitHub repo is empty, use the `repairos-main.bundle` from the agent Artifacts, or clone from your Cursor remote.)

## Run (dev)

```powershell
cd apps\windows-client
dotnet restore WorkshopOS.Client\WorkshopOS.Client.csproj
dotnet run --project WorkshopOS.Client\WorkshopOS.Client.csproj
```

### Startup / connect screen

On launch (**Client 1.2.6+**):

1. **Always** opens **ServerConnect** (pairing code / Find on network / URL). Banner shows **Client 1.2.6**. No “Connecting to server…” splash — Bootstrap is never the initial page.
2. After you connect successfully → **Setup** (first-run shop + owner) or **Login**.

Connect options on ServerConnect:

1. Enter a **pairing code** from `http://<server>:5088/connect`, **or**
2. Tap **Find on this network**, **or**
3. Paste the server URL (`http://127.0.0.1:5088` for local API)

**Change server** (Login / Settings → Connection) and **Clear saved server** wipe URL + tokens (JSON + WinRT LocalSettings + PasswordVault). Theme preference is kept.

### Shell navigation (1.2.6)

Slim left nav — AI Assist and Knowledge Base are **not** in the sidebar:

| Primary | Nested / notes |
| --- | --- |
| Dashboard | Includes **Upcoming calendar** (next 7 days) |
| Work | **Tickets** (filters, New ticket, detail workflow), Quotes, Invoices, Calendar, PC Builds |
| Inventory | Inventory, Purchasing, Used Tech |
| Customers | — |
| Reports | Top-level |
| **Settings** (gear) | App (theme) · Users & Roles · Backups · Connection |

Notifications stay on the **Alerts** button in the shell header (not a sidebar item).

### App settings & theme

**Settings → App**: Light / Dark / System. Persisted as `Theme` in:

```text
%LOCALAPPDATA%\WorkshopOS\client-settings.json
```

Applied via `RequestedTheme` on the main window root.

### Users & Roles

**Settings → Users & Roles**: list staff, create accounts, assign roles / suspend. Uses `GET/POST /api/users` and `PUT /api/users/{id}` (permissions `staff.view` / `staff.manage`). Role catalogue from `GET /api/roles`.

**Settings → Backups**: create/list backups (moved out of primary nav).

### Reset local client config (Windows)

```powershell
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\WorkshopOS"
Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'WorkshopOS' } |
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
```

Then relaunch a **1.2.6+** build — you always land on Connect / pairing UI.

### Account / shop setup wizard?

**Yes for first-run shop setup** (not a separate multi-step “accounts wizard”):

- After a successful server connect, if `GET /api/setup/status` says incomplete, the client shows **SetupPage** (business profile + owner account).
- That is the only first-run wizard. Completing it creates the owner; later staff accounts are managed in **Settings → Users & Roles**.
- There is **no** guided wizard for adding every staff account at install time.

## Build installer / portable zip

1. Sync latest packaging scripts (`git pull`, or overwrite `packaging\build-client.ps1` / `build-client.cmd` from the remote if GitHub looks stale).
2. Open **Developer PowerShell for VS 2022** (Start menu) — preferred over a normal PowerShell window.
3. From the **repo root**, confirm the banner prints **`WorkshopOS client build script v5`**:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.6 -SkipInstaller
```

Or double-click `packaging\build-client.cmd`.

Optional Setup.exe (needs [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.6
```

Outputs in `packaging\dist\` (only after a successful publish — failed builds do **not** zip stale output):

- `WorkshopOS-Client-win-x64-v1.2.6.zip` — portable  
- `WorkshopOS-Setup-1.2.6.exe` — if Inno is installed  

### ExpandPriContent / Pri.Tasks.dll

The project sets `<EnableMsixTooling>true</EnableMsixTooling>` with `<WindowsPackageType>None</WindowsPackageType>` so PRI generation uses the Windows App SDK NuGet tasks instead of VS `Microsoft.Build.Packaging.Pri.Tasks.dll`. That alone usually avoids MSB4062 under `C:\Program Files\dotnet\sdk\...\AppxPackage\`.

If the error persists:

1. Confirm the script banner is **v5** and the csproj has `EnableMsixTooling` = `true`.
2. Visual Studio Installer → **Modify** → enable workload **WinUI application development**, plus Individual components **Windows App Packaging** and a **Windows 10/11 SDK**.
3. Rebuild from Developer PowerShell for VS.

## Related

- Server one-command install: [docs/INSTALL.md](../../docs/INSTALL.md)  
- Packaging scripts: [packaging/README.md](../../packaging/README.md)
