# Windows client (WinUI 3)

Requires Windows 10/11 x64, Visual Studio 2022 with **Windows App SDK / WinUI** workload, and .NET 8.

```powershell
dotnet restore
dotnet run --project WorkshopOS.Client.csproj
```

Point the connect screen at your API (default `http://127.0.0.1:5088`).

This project is not built in Linux CI — the API and tests are.
