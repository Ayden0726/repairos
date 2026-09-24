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

From the **repo root** (Inno Setup optional):

```powershell
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

Zip only:

```powershell
.\packaging\build-client.ps1 -SkipInstaller -Version 1.2.0
```

Outputs in `packaging\dist\`:

- `WorkshopOS-Client-win-x64-v1.2.0.zip` — portable  
- `WorkshopOS-Setup-1.2.0.exe` — if [Inno Setup 6](https://jrsoftware.org/isdl.php) is installed  

Upload those to a [GitHub Release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) so shops can download them. CI also builds the zip on tag push (`.github/workflows/release.yml`).

## Related

- Server one-command install: [docs/INSTALL.md](../../docs/INSTALL.md)  
- Packaging scripts: [packaging/README.md](../../packaging/README.md)
