# Build the native TechBench NQ app (1.2.42)

**Product shape:** Windows desktop client (WinUI 3) + self-hosted Docker API.

The `apps/web` folder is a browser preview only — day-to-day shop use is the Windows app.

Full command sheet: [`WINDOWS_CLIENT_1.2.42_REBUILD.txt`](./WINDOWS_CLIENT_1.2.42_REBUILD.txt)

## Quick path

### 1. Server

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
# connect UI: http://127.0.0.1:5088/connect
```

### 2. Windows client + GitHub update (on Windows)

```powershell
# Pushes main, tags v1.2.42, builds Setup.exe, updates GitHub Release notes/assets
powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1
```

Install `packaging\dist\WorkshopOS-Setup-1.2.42.exe` (or the copy on
https://github.com/Ayden0726/repairos/releases), pair with the code from `/connect`,
complete setup (optional PIN), sign in.

Build without touching GitHub: add `-SkipGitHub`.
