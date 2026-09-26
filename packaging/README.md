# Packaging

| File | Purpose |
| --- | --- |
| `build-client.ps1` | Publish WinUI client (win-x64) + zip; optionally compile Inno installer (**v5**, UTF-8 BOM) |
| `rebuild-client.ps1` | Wipe local data, verify ServerConnect initial page, build 1.2.8, extract, launch (ASCII + UTF-8 BOM) |
| `build-client.cmd` | Double-click launcher for the PowerShell script |
| `WorkshopOS-Setup.iss` | Inno Setup 6 script for `WorkshopOS-Setup-*.exe` |
| `dist/` | Build outputs (gitignored) |

## Client build (Windows)

Use **Developer PowerShell for VS**. From repo root.

**Prerequisites:** Visual Studio 2022 (WinUI workload), .NET 8 SDK, and optionally [Inno Setup 6](https://jrsoftware.org/isdl.php) (`ISCC.exe` at `C:\Program Files (x86)\Inno Setup 6\` or `C:\Program Files\Inno Setup 6\`).

Portable zip only:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.8 -SkipInstaller
```

Zip **and** Inno Setup installer (omit `-SkipInstaller`; Inno must be installed):

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.8
```

Expect: `WorkshopOS client build script v5`. Outputs in `packaging\dist\`:

- `WorkshopOS-Client-win-x64-v1.2.8.zip` — portable
- `WorkshopOS-Setup-1.2.8.exe` — Inno installer (when ISCC is found)

The script always **publishes from source** into `packaging\out\client`, then zips that folder and (unless `-SkipInstaller`) compiles `WorkshopOS-Setup.iss` from the same publish dir. There is **no** “wrap an existing zip” path — you cannot feed a prebuilt `WorkshopOS-Client-*.zip` into the installer step alone. Packaging is **Inno Setup only** (not MSIX); `EnableMsixTooling` is for PRI build tasks, not an `.msix` output.

Failed publishes do not create a zip. If MSB4062 / Pri.Tasks.dll appears, install **Windows App Packaging** + the WinUI workload in Visual Studio Installer.

Server packaging: `../scripts/publish-server.sh` and `../scripts/install-server.sh`.
