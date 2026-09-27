# Packaging (maintainer / advanced)

Shop installs should download **`WorkshopOS-Setup-x.y.z.exe`** from  
[GitHub Releases](https://github.com/Ayden0726/repairos/releases) — not build locally.

This folder is for **maintainers** who publish a new client Release.

| File | Purpose |
| --- | --- |
| `build-client.ps1` | Publish WinUI client (win-x64) + zip; optionally compile Inno installer (**v6**) |
| `rebuild-client.ps1` | Wipe local data, verify ServerConnect, build 1.2.13, extract, launch |
| `build-client.cmd` | Double-click launcher for the PowerShell script |
| `WorkshopOS-Setup.iss` | Inno Setup 6 script for `WorkshopOS-Setup-*.exe` |
| `dist/` | Build outputs (gitignored) |

## Client build (Windows)

Use **Developer PowerShell for VS**. From repo root.

**Prerequisites:** Visual Studio 2022 (WinUI workload), .NET 8 SDK, and optionally [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
# Portable zip
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.13 -SkipInstaller

# Zip + Setup.exe (omit -SkipInstaller; needs Inno Setup 6)
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.13
```

Expect: `WorkshopOS client build script v6`. Outputs in `packaging\dist\`:

- `WorkshopOS-Client-win-x64-v1.2.13.zip` — portable
- `WorkshopOS-Setup-1.2.13.exe` — upload this to the GitHub Release

Server packaging: `../scripts/publish-server.sh` and `../scripts/install-server.sh`.  
End-user install: [docs/INSTALL.md](../docs/INSTALL.md).
