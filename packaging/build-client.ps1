# Build WorkshopOS Windows client + optional Inno Setup installer
# Requires: Windows 10/11 x64, .NET 8 SDK, Windows App SDK workload
# Optional: Inno Setup 6 (ISCC.exe) for WorkshopOS-Setup.exe

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipInstaller,
    [string]$ApiDefaultUrl = "http://127.0.0.1:5088",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Proj = Join-Path $Root "apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj"
$PublishDir = Join-Path $Root "packaging/out/client"
$DistDir = Join-Path $Root "packaging/dist"

Write-Host "==> Restoring WorkshopOS.Client"
dotnet restore $Proj

Write-Host "==> Publishing self-contained win-x64 ($Configuration)"
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

dotnet publish $Proj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:WindowsAppSDKSelfContained=true `
    -p:WindowsPackageType=None `
    -o $PublishDir

New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$ZipPath = Join-Path $DistDir "WorkshopOS-Client-win-x64-v$Version.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath
Write-Host "Portable zip: $ZipPath"

if ($SkipInstaller) {
    Write-Host "Skipping Inno Setup (-SkipInstaller)."
    exit 0
}

$Iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    Write-Warning "Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php then re-run, or distribute the zip above."
    exit 0
}

$Iss = Join-Path $Root "packaging/WorkshopOS-Setup.iss"
Write-Host "==> Building installer with $Iscc"
& $Iscc /DMyAppVersion=$Version /DPublishDir=$PublishDir /DDistDir=$DistDir $Iss
Write-Host "Installer written under $DistDir"
Write-Host "Default API URL used at first connect: $ApiDefaultUrl"
