# Push WorkshopOS to GitHub + deploy servers / Raspberry Pi

This tree is ready for **https://github.com/Ayden0726/repairos** (`main`).

**Current product: 1.2.42** — TechBench NQ native WinUI client with PIN login; home workshop profile from 1.2.41. See `BUILD_NATIVE.md` and `WINDOWS_CLIENT_1.2.42_REBUILD.txt`.

---

## What you will have after sync

| Target | How it runs |
| --- | --- |
| Normal Linux / NAS / WSL server | `docker compose` (API + Postgres + worker) |
| Raspberry Pi 3B+ | Same install — auto Pi overlay + swap |
| Windows shop PCs | `WorkshopOS-Setup-1.2.42.exe` from Releases |

---

## Fastest path — build client **and** update GitHub

**Developer PowerShell for VS 2022** (repo already on disk):

```powershell
cd C:\Users\ayden\src\repairos
powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1
```

That script will:

1. Commit pending changes (if any)
2. Push `main` to **https://github.com/Ayden0726/repairos**
3. Force-push tag **`v1.2.42`** (triggers Actions release workflow)
4. Build `WorkshopOS-Client-win-x64-v1.2.42.zip` + `WorkshopOS-Setup-1.2.42.exe`
5. If `gh` is installed/authenticated, create/update the GitHub Release with notes + upload the zip/Setup.exe

Skip GitHub (local build only):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\rebuild-client-1.2.42.ps1 -SkipGitHub
```

---

## Option A — Overlay sync zip then one-shot push + build

Requires: Git, GitHub auth (`gh auth login` or Credential Manager), Cloud Agent port **18765** forwarded.

```powershell
$r = 'C:\Users\ayden\src\repairos'
$z = "$env:TEMP\WorkshopOS-1.2.42-github-sync.zip"
New-Item -ItemType Directory -Force -Path $r | Out-Null
Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.42-github-sync.zip' -OutFile $z
Expand-Archive -Path $z -DestinationPath $r -Force
powershell -ExecutionPolicy Bypass -File "$r\scripts\update-github-and-build-client.ps1"
```

Outputs: GitHub `main` + tag `v1.2.42`, then
`packaging\dist\WorkshopOS-Client-win-x64-v1.2.42.zip` and
`packaging\dist\WorkshopOS-Setup-1.2.42.exe`.

### Manual overlay + push

```powershell
$repo = 'C:\Users\ayden\src\repairos'
$zip  = "$env:TEMP\WorkshopOS-1.2.42-github-sync.zip"
New-Item -ItemType Directory -Force -Path $repo | Out-Null

Invoke-WebRequest -Uri 'http://127.0.0.1:18765/WorkshopOS-1.2.42-github-sync.zip' -OutFile $zip

if (-not (Test-Path "$repo\.git")) {
  git clone https://github.com/Ayden0726/repairos.git $repo
}

Expand-Archive -Path $zip -DestinationPath $repo -Force

Remove-Item -Force -ErrorAction SilentlyContinue `
  "$repo\apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml", `
  "$repo\apps\windows-client\WorkshopOS.Client\Views\ServicePickerControl.xaml.cs"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
  "$repo\apps\windows-client\WorkshopOS.Client\obj", `
  "$repo\apps\windows-client\WorkshopOS.Client\bin"

Set-Location $repo
git remote set-url origin https://github.com/Ayden0726/repairos.git
git add -A
git commit -m "WorkshopOS 1.2.42: TechBench NQ PIN login + native client"
git push -u origin main
git tag -f v1.2.42 -m "WorkshopOS / TechBench NQ 1.2.42"
git push -f origin v1.2.42
```

Push-only helper (after overlay):

```powershell
powershell -ExecutionPolicy Bypass -File "$repo\scripts\sync-to-github.ps1"
```

---

## Option B — WSL / Linux / Mac (code push; client still builds on Windows)

```bash
REPO="${HOME}/src/repairos"
ZIP=/tmp/WorkshopOS-1.2.42-github-sync.zip

mkdir -p "$(dirname "$REPO")"
[[ -d "$REPO/.git" ]] || git clone https://github.com/Ayden0726/repairos.git "$REPO"

curl -fsSL -o "$ZIP" 'http://127.0.0.1:18765/WorkshopOS-1.2.42-github-sync.zip'
unzip -o "$ZIP" -d "$REPO"
cd "$REPO"

git remote set-url origin https://github.com/Ayden0726/repairos.git
git add -A
git commit -m "WorkshopOS 1.2.42: TechBench NQ PIN login + native client"
git push -u origin main
git tag -f v1.2.42 && git push -f origin v1.2.42

# Or: ./scripts/push-to-github.sh --tag v1.2.42
```

---

## After GitHub is updated

- Actions: https://github.com/Ayden0726/repairos/actions  
- Releases: https://github.com/Ayden0726/repairos/releases  
- Shop installs: download **WorkshopOS-Setup-1.2.42.exe** from the release  
- Server: `curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash`
