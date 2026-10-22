# Running WorkshopOS on Raspberry Pi

WorkshopOS **server** (API + worker + PostgreSQL) supports **Raspberry Pi 3B or greater** on 32-bit or 64-bit Raspberry Pi OS / Debian ARM.

The Windows **client** still runs on shop PCs (x64). Only the server is intended for the Pi.

## Supported hardware

| Board | RAM | Supported? | Notes |
| --- | --- | --- | --- |
| Pi 3B / 3B+ | 1 GB | Yes | Use the Pi compose overlay + 2 GB swap; first Docker build is slow |
| Pi 4 | 2–8 GB | Yes (recommended) | Comfortable for a small shop |
| Pi 5 | 4–8 GB | Yes | Best Raspberry Pi option |
| Pi Zero / 1 | ≤512 MB | **No** | ARMv6 / too little RAM — .NET 8 needs ARMv7+ |

**Prefer 64-bit Raspberry Pi OS** (`aarch64`). 32-bit (`armv7l`) works via `linux/arm/v7` images but builds are slower.

## One-command install

Same as any Linux host — the installer detects Pi and applies `docker/docker-compose.pi.yml` automatically:

```bash
curl -fsSL https://raw.githubusercontent.com/Ayden0726/repairos/main/scripts/get-workshopos.sh | bash
```

Or from a clone:

```bash
chmod +x scripts/*.sh
./scripts/get-workshopos.sh --dir "$(pwd)"
```

Manual compose (if you skip the install script):

```bash
cd docker
cp -n .env.example .env   # set POSTGRES_PASSWORD, JWT_SIGNING_KEY, Encryption__Key
docker compose -f docker-compose.yml -f docker-compose.pi.yml --env-file .env up -d --build
```

## What the Pi overlay does

`docker-compose.pi.yml`:

- Leaves **Redis off** (unused by WorkshopOS today)
- Tunes **PostgreSQL** for ~1 GB hosts (`shared_buffers=64MB`, fewer connections)
- Caps **API (~384 MB)** and **worker (~192 MB)** memory
- Lengthens healthcheck start periods (cold SD cards are slow)

Dockerfiles are multi-arch (`linux/amd64`, `linux/arm64`, `linux/arm/v7`) using .NET 8 bookworm-slim images.

## Before you install (Pi 3B checklist)

1. **64-bit OS** if possible (`uname -m` → `aarch64`).
2. **2 GB swap** (installer creates this on Pi when RAM < 2 GB and swap is small).
3. **Cooling / power**: use an official PSU; thermal throttling makes first builds crawl.
4. **Storage**: 32 GB+ SD (A2) or USB SSD strongly preferred over cheap Class 10 cards.
5. **Patience on first build**: compiling the API on a Pi 3B can take **30–90 minutes**. Later `--update` rebuilds are faster when layers cache.

Optional: build images on a faster ARM64 machine (Pi 5, Apple Silicon, cloud ARM VM), then:

```bash
docker save workshopos-api workshopos-worker | gzip > workshopos-images.tgz
# copy to Pi, then:
gunzip -c workshopos-images.tgz | docker load
```

(Tag names match whatever `docker compose build` produced — check with `docker images`.)

## After install

- Connect portal: `http://<pi-lan-ip>:5088/connect`
- Pair Windows clients with the code shown
- Keep `Encryption__Key` in `docker/.env` backed up — rotating it breaks encrypted API credentials

## Updates

```bash
cd ~/workshopos
./scripts/get-workshopos.sh --update
# or
./scripts/restart-workshopos.sh --update
```

Both scripts re-apply the Pi overlay when `docker/.use-pi-profile` is present (written on first Pi install).

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Build killed / OOM | Confirm 2 GB+ swap (`free -h`); close other apps; retry |
| `exec format error` | Image arch mismatch — rebuild on the Pi (`docker compose … up -d --build --force-recreate`) |
| API never healthy | `docker compose … logs api` — often Postgres still starting on slow SD |
| Very slow forever | Move data dir to USB SSD; reduce shop concurrency; prefer Pi 4/5 |
| ARMv6 / Pi Zero | Unsupported — use Pi 3B+ or x86_64/ARM64 mini PC |

## Resource expectations (Pi 3B, idle shop)

Rough resident use with the Pi overlay:

- PostgreSQL ≈ 100–200 MB  
- API ≈ 150–300 MB  
- Worker ≈ 80–150 MB  

Leave headroom for the OS and Docker. A quiet single-shop workload is the design point; heavy concurrent POS + website traffic is better on Pi 4 (4 GB+) or a small NUC/NAS.
