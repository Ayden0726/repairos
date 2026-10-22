# Push WorkshopOS to GitHub + deploy servers / Raspberry Pi

This tree is ready for **https://github.com/Ayden0726/repairos** (`main`).

**Current product: 1.2.35** — encrypted API credentials, persistent settings fixes, business expenses, Raspberry Pi 3B+ server support.

---

## What you will have after sync

| Target | How it runs |
| --- | --- |
| Normal Linux / NAS / WSL server | `docker compose` (API + Postgres + worker) |
| Raspberry Pi 3B+ | Same install — auto Pi overlay + swap |
| Windows shop PCs | `WorkshopOS-Setup-*.exe` from Releases (rebuild client after push) |

---

## Option A — PowerShell on your Windows PC (recommended)

Requires: Git, GitHub auth (`gh auth login` or Credential Manager), and this sync zip.

### Fastest path — push + Setup.exe in one go

**Developer PowerShell for VS 2022.** Keep Cloud Agent port **18765** forwarded.

```powershell
$r = 'C:\Users\ayden\src\repairos'
$z = "$env:TEMP\WorkshopOS-1.2.35-github-sync.zip"
New-Item -ItemType Directory -Force -Path $r | Out-Null
Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.35-github-sync.zip' -OutFile $z
Expand-Archive -Path $z -DestinationPath $r -Force
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1"
```

Outputs: GitHub `main` + tag `v1.2.35`, then
`packaging\dist\WorkshopOS-Client-win-x64-v1.2.35.zip` and
`packaging\dist\WorkshopOS-Setup-1.2.35.exe`.

### Manual overlay + push

```powershell
# 1) Folder for the real GitHub clone
$repo = 'C:\Users\ayden\src\repairos'
$zip  = "$env:TEMP\WorkshopOS-1.2.35-github-sync.zip"
New-Item -ItemType Directory -Force -Path $repo | Out-Null

# 2) Get the sync zip (Cloud Agent Ports → 18765)
Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.35-github-sync.zip' -OutFile $zip

# 3) If the folder is empty, clone first; otherwise overlay the zip onto your clone
if (-not (Test-Path "$repo\.git")) {
  git clone https://github.com/Ayden0726/repairos.git $repo
}

Expand-Archive -Path $zip -DestinationPath $repo -Force

# 4) Remove known orphans Expand-Archive never deletes
Remove-Item -Force -ErrorAction SilentlyContinue `
  "$repo\apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml", `
  "$repo\apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml.cs"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  "$repo\apps\windows-client\WorkshopOS.Client\obj", `
  "$repo\apps\windows-client\WorkshopOS.Client\bin"

# 5) Commit + push
Set-Location $repo
git remote set-url origin https://github.com/Ayden0726/repairos.git
git add -A
git status
git commit -m "WorkshopOS 1.2.35: encrypted credentials, expenses, Raspberry Pi server"
git push -u origin main

# 6) Tag a release (optional — triggers GitHub Actions release workflow)
git tag -f v1.2.35
git push -f origin v1.2.35
```

Push-only helper (after overlay):

```powershell
powershell -ExecutionPolicy Bypass -File "$repo\scripts\sync-to-github.ps1"
```

Rebuild Windows client after push (VS 2022 Developer PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File "$repo\scripts\rebuild-client-1.2.35.ps1"
# or:
powershell -ExecutionPolicy Bypass -File "$repo\packaging\build-client.ps1" -Configuration Release -Version 1.2.35
```

---

## Option B — WSL / Linux / Mac

```bash
REPO="${HOME}/src/repairos"
ZIP=/tmp/WorkshopOS-1.2.35-github-sync.zip

# Clone if needed
mkdir -p "$(dirname "$REPO")"
[[ -d "$REPO/.git" ]] || git clone https://github.com/Ayden0726/repairos.git "$REPO"

# Download sync zip (agent serve port 18765, or copy from artifacts)
curl -fsSL -o "$ZIP" 'http://127.0.0.1:18765/WorkshopOS-1.2.35-github-sync.zip'
# Or: cp /opt/cursor/artifacts/WorkshopOS-1.2.35-github-sync.zip "$ZIP"

unzip -o "$ZIP" -d "$REPO"
cd "$REPO"
rm -f apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml \
      apps/windows-client/WorkshopOS.Client/Views/ServicePickerControl.xaml.cs
rm -rf apps/windows-client/WorkshopOS.Client/obj apps/windows-client/WorkshopOS.Client/bin

git remote set-url origin https://github.com/Ayden0726/repairos.git
git add -A
git commit -m "WorkshopOS 1.2.35: encrypted credentials, expenses, Raspberry Pi server"
git push -u origin main
git tag -f v1.2.35 && git push -f origin v1.2.35

chmod +x scripts/*.sh
```

Or use the helper:

```bash
./scripts/sync-to-github.sh
```

---

## Option C — git bundle

```bash
REPO="${HOME}/src/repairos"
curl -fsSL -o /tmp/repairos-main.bundle 'http://127.0.0.1:18765/repairos-main.bundle'
cd "$REPO"
git fetch /tmp/repairos-main.bundle main:refs/remotes/agent/main
git merge --ff-only refs/remotes/agent/main
git push origin main
```

---

## After GitHub has `main` — deploy / update servers

### Normal server (x86_64 / ARM64 NAS / WSL)

```bash
# Fresh install
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash

# Existing install
cd ~/workshopos   # or your install dir
git pull origin main
chmod +x scripts/*.sh
./scripts/restart-workshopos.sh --update
```

Ensure `docker/.env` has (installer creates these on first run):

```bash
POSTGRES_PASSWORD=...
JWT_SIGNING_KEY=...          # 32+ chars, keep stable
Encryption__Key=...          # 32+ chars, SEPARATE from JWT — keep forever
Encryption__KeyVersion=1
API_PORT=5088
```

### Raspberry Pi 3B+

Same command — Pi is auto-detected:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Force Pi overlay on any low-RAM host:

```bash
./scripts/get-workshopos.sh --pi
```

Details: [RASPBERRY_PI.md](RASPBERRY_PI.md) · Security: [SETTINGS_AND_SECURITY.md](SETTINGS_AND_SECURITY.md)

---

## Files in this sync (highlights)

- `docs/SETTINGS_AND_SECURITY.md` — settings-reset root cause + encryption
- `docs/RASPBERRY_PI.md` — Pi 3B+ install
- `docker/docker-compose.pi.yml` — low-memory overlay
- `docker/Dockerfile.api` / `Dockerfile.worker` — multi-arch slim images
- Expenses + Integrations UI/API
- Migration `SecurityCredentialsAndExpenses`

---

## Build sync artifacts yourself (from this tree)

```bash
chmod +x scripts/build-github-sync-artifacts.sh
./scripts/build-github-sync-artifacts.sh
# Writes:
#   /opt/cursor/artifacts/repairos-github-sync.zip
#   /opt/cursor/artifacts/repairos-main.bundle
#   /opt/cursor/artifacts/GITHUB_SYNC_INSTRUCTIONS.txt
```
