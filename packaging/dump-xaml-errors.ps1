#Requires -Version 5.1
<#
.SYNOPSIS
  Print real XamlCompiler / MSBuild errors from packaging\out\logs\publish-last.log

.DESCRIPTION
  After a failed packaging\build-client.ps1 / dotnet publish, run:

    powershell -ExecutionPolicy Bypass -File .\packaging\dump-xaml-errors.ps1

  Optional:

    .\packaging\dump-xaml-errors.ps1 -Tail 200
    .\packaging\dump-xaml-errors.ps1 -LogPath C:\path\to\publish-last.log

.NOTES
  ASCII-only + UTF-8 BOM friendly for Windows PowerShell 5.1.
#>
param(
    [string]$LogPath = "",
    [int]$Tail = 120,
    [int]$Context = 2
)

$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here '..')
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $repo 'packaging\out\logs\publish-last.log'
}

Write-Host "=== WorkshopOS XamlCompiler / publish error dump ===" -ForegroundColor Cyan
Write-Host "Log: $LogPath"
Write-Host ""

if (-not (Test-Path $LogPath)) {
    Write-Host "MISSING log file. Run a publish first:" -ForegroundColor Red
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -Configuration Release -Version 1.2.21 -SkipInstaller"
    exit 1
}

$lines = Get-Content -Path $LogPath -ErrorAction Stop
Write-Host ("Total lines: {0}" -f $lines.Count) -ForegroundColor DarkGray
Write-Host ""

# Patterns that surface the real failure (not just MSB3073 wrap).
$patterns = @(
    'error\s+(WMC|XSG|CS|MC|MSB)\d+',
    ':\s*error\s+',
    'XamlCompiler',
    'MarkupCompilePass',
    'CompileXaml',
    'MSB3073',
    'failed with exit code',
    'Exception:',
    'fatal error'
)

$hitIdx = New-Object System.Collections.Generic.List[int]
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    foreach ($p in $patterns) {
        if ($line -match $p) {
            $hitIdx.Add($i) | Out-Null
            break
        }
    }
}

if ($hitIdx.Count -eq 0) {
    Write-Host "No XamlCompiler/error keywords matched. Dumping last $Tail lines:" -ForegroundColor Yellow
    Write-Host ("-" * 72)
    $lines | Select-Object -Last $Tail
    Write-Host ("-" * 72)
    Write-Host ""
    Write-Host "Tip: also try: Select-String -Path '$LogPath' -Pattern 'error','WMC','CS','Xaml' -CaseSensitive:`$false"
    exit 2
}

Write-Host ("Matched {0} diagnostic line(s). Showing with +/- {1} context:" -f $hitIdx.Count, $Context) -ForegroundColor Yellow
Write-Host ("-" * 72)

$shown = New-Object 'System.Collections.Generic.HashSet[int]'
foreach ($idx in $hitIdx) {
    $start = [Math]::Max(0, $idx - $Context)
    $end = [Math]::Min($lines.Count - 1, $idx + $Context)
    for ($j = $start; $j -le $end; $j++) {
        if ($shown.Add($j)) {
            $prefix = if ($hitIdx -contains $j) { '>>' } else { '  ' }
            '{0} {1,5}: {2}' -f $prefix, ($j + 1), $lines[$j]
        }
    }
    Write-Host ""
}

Write-Host ("-" * 72)
Write-Host ""
Write-Host "Last $Tail lines of full log:" -ForegroundColor DarkCyan
Write-Host ("-" * 72)
$lines | Select-Object -Last $Tail
Write-Host ("-" * 72)
Write-Host ""
Write-Host "Done. Fix the >> lines above, then rebuild 1.2.21." -ForegroundColor Green
