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

On first launch:

1. Enter a **pairing code** from `http://<server>:5088/connect`, **or**
2. Tap **Find on this network**, **or**
3. Paste the server URL (`http://127.0.0.1:5088` for local API)

## Build installer / portable zip

1. Sync latest packaging scripts (`git pull`, or overwrite `packaging\build-client.ps1` / `build-client.cmd` from the remote if GitHub looks stale).
2. Open **Developer PowerShell for VS 2022** (Start menu) — preferred over a normal PowerShell window.
3. From the **repo root**, confirm the banner prints **`WorkshopOS client build script v4`**:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.0 -SkipInstaller
```

Or double-click `packaging\build-client.cmd`.

Optional Setup.exe (needs [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

Outputs in `packaging\dist\` (only after a successful publish — failed builds do **not** zip stale output):

- `WorkshopOS-Client-win-x64-v1.2.0.zip` — portable  
- `WorkshopOS-Setup-1.2.0.exe` — if Inno is installed  

### ExpandPriContent / Pri.Tasks.dll

The project sets `<EnableMsixTooling>true</EnableMsixTooling>` with `<WindowsPackageType>None</WindowsPackageType>` so PRI generation uses the Windows App SDK NuGet tasks instead of VS `Microsoft.Build.Packaging.Pri.Tasks.dll`. That alone usually avoids MSB4062 under `C:\Program Files\dotnet\sdk\...\AppxPackage\`.

If the error persists:

1. Confirm the script banner is **v4** and the csproj has `EnableMsixTooling` = `true`.
2. Visual Studio Installer → **Modify** → enable workload **WinUI application development**, plus Individual components **Windows App Packaging** and a **Windows 10/11 SDK**.
3. Rebuild from Developer PowerShell for VS.

## Related

- Server one-command install: [docs/INSTALL.md](../../docs/INSTALL.md)  
- Packaging scripts: [packaging/README.md](../../packaging/README.md)
