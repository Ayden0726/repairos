#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..')
$script = Join-Path $repo 'scripts\rebuild-client-1.2.35.ps1'
if (-not (Test-Path $script)) { throw "Missing $script" }
Write-Host "Delegating to scripts\rebuild-client-1.2.35.ps1 ..." -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File $script @args
