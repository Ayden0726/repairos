# Database

EF Core migrations live in:

`services/shared/WorkshopOS.Infrastructure/Persistence/Migrations`

Apply automatically on API startup (`Database.MigrateAsync`), or:

```bash
dotnet ef database update \
  --project services/shared/WorkshopOS.Infrastructure \
  --startup-project services/api/WorkshopOS.Api
```

Development database (local): `workshopos_net`  
Docker Compose database: `workshopos`
