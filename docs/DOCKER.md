# Docker Compose Architecture

## Services

| Service | Image / build | Role |
| --- | --- | --- |
| `postgres` | postgres:16-alpine | Primary database |
| `redis` | redis:7-alpine | Optional cache / future SignalR backplane |
| `api` | `docker/Dockerfile.api` | WorkshopOS.Api |
| `worker` | `docker/Dockerfile.worker` | WorkshopOS.Worker |
| `caddy` | caddy:2-alpine | TLS termination (optional profile) |

## Volumes

- `postgres_data`
- `redis_data`
- `files_data` (attachments)
- `backup_data`

## Environment

See `/docker/.env.example`:

- `POSTGRES_PASSWORD`
- `ConnectionStrings__Default`
- `Jwt__SigningKey` (64+ hex/random chars)
- `Seed__Demo` (`false` in production)
- `ASPNETCORE_ENVIRONMENT`

## Local without Docker

API can run against host PostgreSQL:

```bash
dotnet run --project services/api/WorkshopOS.Api
```

Default development URLs: `http://127.0.0.1:5088`

## Client

The Windows installer only installs the client. Point it at `https://your-server` during first connection / setup.
