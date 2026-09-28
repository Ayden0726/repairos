#Requires -Version 5.1
<#
.SYNOPSIS
  Redirect: use scripts\rebuild-client-1.2.22.ps1 for Client 1.2.22.

.NOTES
  If publish fails:
    powershell -ExecutionPolicy Bypass -File .\packaging\dump-xaml-errors.ps1
#>
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..')
$script = Join-Path $repo 'scripts\rebuild-client-1.2.22.ps1'
if (-not (Test-Path $script)) { throw "Missing $script" }
Write-Host "Delegating to scripts\rebuild-client-1.2.22.ps1 ..." -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File $script @args
