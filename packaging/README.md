# Packaging

| File | Purpose |
| --- | --- |
| `build-client.ps1` | Publish WinUI client (win-x64) + zip; optionally compile Inno installer (**v4**, UTF-8 BOM) |
| `build-client.cmd` | Double-click launcher for the PowerShell script |
| `WorkshopOS-Setup.iss` | Inno Setup 6 script for `WorkshopOS-Setup-*.exe` |
| `dist/` | Build outputs (gitignored) |

## Client build (Windows)

Use **Developer PowerShell for VS**. From repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -SkipInstaller
```

Expect: `WorkshopOS client build script v4`. Failed publishes do not create a zip.

Unpackaged publish uses `EnableMsixTooling=true` + `WindowsPackageType=None` so PRI tasks come from the Windows App SDK NuGet package (avoids missing `Microsoft.Build.Packaging.Pri.Tasks.dll` under the .NET SDK). If MSB4062 still appears, install **Windows App Packaging** + the WinUI workload in Visual Studio Installer.

Server packaging: `../scripts/publish-server.sh` and `../scripts/install-server.sh`.
