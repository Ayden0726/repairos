# Install WorkshopOS

Two pieces: **server** (one command, Docker) and **Windows client** (shop PCs).

## 1. Install the server (one command)

On a Linux PC / NAS / VM with [Docker](https://docs.docker.com/get-docker/) + [Git](https://git-scm.com/downloads):

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Also installs **Git** and **Docker** (Compose v2) automatically on common Linux distros when missing (Ubuntu/Debian/Raspberry Pi OS, Fedora/RHEL, Arch). On macOS it can install via Homebrew if present.

When it finishes, open (same machine or another device on the Wi‑Fi):

- `http://127.0.0.1:5088/connect`  
- or `http://<server-lan-ip>:5088/connect`  

You’ll see a code like **`WOS-AB12`**.

### Options

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash -s -- --dir ~/workshopos --port 5088
```

### Manual / local clone

```bash
git clone https://github.com/Ayden0726/repairos.git
cd repairos
chmod +x scripts/get-workshopos.sh scripts/install-server.sh
./scripts/get-workshopos.sh --dir "$(pwd)"
```

## 2. Connect the Windows client

1. Install the client (from a [GitHub Release](https://github.com/Ayden0726/repairos/releases) zip/setup, or build it — see below).  
2. On the connect screen:
   - Enter the **pairing code** from `/connect`, **or**
   - Tap **Find on this network** (same LAN), **or**
   - Paste `http://<server-ip>:5088`  
3. First PC completes the business + owner setup wizard.  
4. Other PCs sign in with staff accounts.

## 3. Build the Windows client yourself

Full prerequisite **download links**: **[apps/windows-client/README.md](../apps/windows-client/README.md)**

Short list:

| Tool | Link |
| --- | --- |
| Git for Windows | https://git-scm.com/download/win |
| Visual Studio 2022 Community | https://visualstudio.microsoft.com/downloads/ |
| WinUI / Windows App SDK setup | https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment |
| .NET 8 SDK (Windows x64) | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Inno Setup 6 (optional installer) | https://jrsoftware.org/isdl.php |
| Windows SDK | https://developer.microsoft.com/windows/downloads/windows-sdk/ |

```powershell
git clone https://github.com/Ayden0726/repairos.git
cd repairos
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

## 4. Publish downloads on GitHub

```bash
git tag v1.2.0
git push origin v1.2.0
```

Actions builds the **server tarball** + **Windows client zip** onto the Release.

## Verify

```bash
curl http://127.0.0.1:5088/api/health
curl http://127.0.0.1:5088/api/discovery
```
