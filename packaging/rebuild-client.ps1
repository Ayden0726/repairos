#Requires -Version 5.1
<#
.SYNOPSIS
  Redirect: use scripts\rebuild-client-1.2.20.ps1 for Client 1.2.20.

.NOTES
  If publish fails:
    Get-Content packaging\out\logs\publish-last.log -Tail 80
    Look for error / XamlCompiler / WMC / CS lines.
#>
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..')
$script = Join-Path $repo 'scripts\rebuild-client-1.2.20.ps1'
if (-not (Test-Path $script)) { throw "Missing $script" }
Write-Host "Delegating to scripts\rebuild-client-1.2.20.ps1 ..." -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File $script @args
