# Docker Compose Architecture

## Services

| Service | Image / build | Role | Default |
| --- | --- | --- | --- |
| `postgres` | postgres:16-alpine | Primary database | Always on |
| `api` | `docker/Dockerfile.api` | WorkshopOS.Api (multi-arch) | Always on |
| `worker` | `docker/Dockerfile.worker` | Outbox + recurring expenses | Always on |
| `redis` | redis:7-alpine | Optional / future use | Profile `redis` only |

## Platforms

Dockerfiles target **linux/amd64**, **linux/arm64**, and **linux/arm/v7** (.NET 8 bookworm-slim).

| Host | Compose files |
| --- | --- |
| Normal PC / NAS / WSL | `docker-compose.yml` |
| Raspberry Pi 3B+ | `docker-compose.yml` + `docker-compose.pi.yml` (auto via installer) |

Pi overlay: lower Postgres memory, cap API/worker RAM, skip Redis. See [RASPBERRY_PI.md](RASPBERRY_PI.md).

## Volumes

- `postgres_data` — durable shop database (do not delete)
- `files_data` — expense receipts / attachments
- `backup_data` — backup markers / dumps
- `redis_data` — only if `--profile redis` is used

## Environment (`docker/.env`)

Copy from `.env.example`. Critical keys:

| Variable | Purpose |
| --- | --- |
| `POSTGRES_PASSWORD` | Database password |
| `JWT_SIGNING_KEY` | JWT signing (32+ chars). Rotating logs everyone out. |
| `Encryption__Key` | AES-GCM master key for API credentials (32+ chars). **Keep forever** — separate from JWT. |
| `Encryption__KeyVersion` | Key version tag (default `1`) |
| `API_PORT` | Host port (default `5088`) |
| `SEED_DEMO` | `false` in production |

See [SETTINGS_AND_SECURITY.md](SETTINGS_AND_SECURITY.md).

## Commands

```bash
cd docker

# Normal host
docker compose --env-file .env up -d --build

# Raspberry Pi / low RAM
docker compose -f docker-compose.yml -f docker-compose.pi.yml --env-file .env up -d --build

# Logs / stop
docker compose --env-file .env logs -f api
docker compose --env-file .env down   # keeps volumes
```

Installer wrappers (preferred):

```bash
./scripts/get-workshopos.sh            # fresh
./scripts/get-workshopos.sh --update   # pull + rebuild
./scripts/restart-workshopos.sh --update
```

## Local without Docker

```bash
dotnet run --project services/api/WorkshopOS.Api
```

Default development URL: `http://127.0.0.1:5088`

## Client

Install **`WorkshopOS-Setup-x.y.z.exe`** from [GitHub Releases](https://github.com/Ayden0726/repairos/releases).  
Point it at your server during pairing (`/connect` or LAN discovery). See [INSTALL.md](INSTALL.md).
