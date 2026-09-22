# Windows client (WinUI 3)

Requires **Windows 10/11 x64**, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), and Visual Studio 2022 with the **Windows App SDK / WinUI** workload.

This project **cannot** be built on Linux CI — only the API and tests run there. Use a Windows PC or the `windows-latest` GitHub Actions job.

## Run (dev)

```powershell
cd apps\windows-client
dotnet restore WorkshopOS.Client\WorkshopOS.Client.csproj
dotnet run --project WorkshopOS.Client\WorkshopOS.Client.csproj
```

Connect screen default: `http://127.0.0.1:5088`.

## Build installer / portable zip

From repo root (optional: install [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
.\packaging\build-client.ps1 -Configuration Release -Version 1.2.0
```

- `packaging\dist\WorkshopOS-Client-win-x64-v1.2.0.zip` — portable  
- `packaging\dist\WorkshopOS-Setup-1.2.0.exe` — if Inno is installed  

Upload those files to a GitHub Release so shops can download them.

## First-run flow

1. Enter API URL  
2. Setup wizard (if server not configured) or Login  
3. Sidebar: Dashboard, Repairs, Customers, Quotes, Inventory, Invoices, etc.
