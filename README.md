# TechBench NQ · WorkshopOS 1.2.42

Native **Windows desktop app** (WinUI 3) for TechBench NQ repair shops, plus a self-hosted Docker API.

- **Shop PCs:** install `WorkshopOS-Setup-1.2.42.exe`
- **Server:** Docker Compose API on port **5088** + PostgreSQL
- **Locale:** AUD · Australia-friendly GST settings · home workshop profile from 1.2.41

## Build / install (native)

See **[BUILD_NATIVE.md](./BUILD_NATIVE.md)** and **[WINDOWS_CLIENT_1.2.42_REBUILD.txt](./WINDOWS_CLIENT_1.2.42_REBUILD.txt)**.

```powershell
# On Windows — push GitHub (main + v1.2.42) + Setup.exe + Release notes
powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1
```

```bash
# Server (Linux / WSL / NAS)
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

## 1.2.42 highlights

- TechBench NQ login with **PIN keypad** or password
- Optional owner PIN at first-run setup
- Keeps 1.2.41 home / side-hustle workshop profile (no board-level PCB scope)

## Docs

- [docs/INSTALL.md](./docs/INSTALL.md)
- [SPEC.md](./SPEC.md) (web M1 notes — optional preview in `apps/web`)
- [docs/GITHUB_SYNC.md](./docs/GITHUB_SYNC.md)

`apps/web` remains available for browser testing; it is **not** the production shop client.
