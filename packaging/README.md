# Packaging

| File | Purpose |
| --- | --- |
| `build-client.ps1` | Publish WinUI client (win-x64) + zip; optionally compile Inno installer |
| `WorkshopOS-Setup.iss` | Inno Setup 6 script for `WorkshopOS-Setup-*.exe` |
| `dist/` | Build outputs (gitignored) |

Server packaging: `../scripts/publish-server.sh` and `../scripts/install-server.sh`.
