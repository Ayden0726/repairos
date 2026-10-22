#Requires -Version 5.1
# Superseded — client is now 1.2.34 (aligned with API).
Write-Host "Client 1.2.32 is superseded by 1.2.34. Delegating..." -ForegroundColor Yellow
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'rebuild-client-1.2.34.ps1') @args
exit $LASTEXITCODE
