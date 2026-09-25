# Build WorkshopOS Windows client + optional Inno Setup installer
# Prefers Visual Studio MSBuild; falls back to `dotnet publish` when
# EnableMsixTooling=true (avoids ExpandPriContent / Pri.Tasks.dll).
#
# Use: double-click packaging\build-client.cmd
#   or: powershell -ExecutionPolicy Bypass -File .\packaging\build-client.ps1 -SkipInstaller
# Prefer: "Developer PowerShell for VS 2022" from the Start menu.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipInstaller,
    [string]$ApiDefaultUrl = "http://127.0.0.1:5088",
    [string]$Version = "1.2.6"
)

$ErrorActionPreference = "Stop"
$ScriptVersion = "v5"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Proj = Join-Path $Root "apps\windows-client\WorkshopOS.Client\WorkshopOS.Client.csproj"
$PublishDir = Join-Path $Root "packaging\out\client"
$DistDir = Join-Path $Root "packaging\dist"

Write-Host "WorkshopOS client build script $ScriptVersion" -ForegroundColor Cyan
Write-Host "  Unpackaged zip publish (WindowsPackageType=None, EnableMsixTooling=true)" -ForegroundColor DarkCyan
Write-Host "  Repo: $Root"

function Assert-LastExit([string]$Step) {
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE"
    }
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        $vswhere = Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe"
    }
    if (-not (Test-Path $vswhere)) { return $null }

    $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
    if ($found) { return ($found | Select-Object -First 1) }
    return $null
}

function Get-VsMsBuildExtensionsPath([string]$MSBuildPath) {
    # ...\MSBuild\Current\Bin\MSBuild.exe -> ...\MSBuild
    $bin = Split-Path $MSBuildPath -Parent
    $current = Split-Path $bin -Parent
    $msbuildRoot = Split-Path $current -Parent
    if (Test-Path $msbuildRoot) { return $msbuildRoot }
    return $null
}

function Find-PriTasksDll([string]$MSBuildPath) {
    $ext = Get-VsMsBuildExtensionsPath $MSBuildPath
    $candidates = @()
    if ($ext) {
        foreach ($ver in @("v18.0", "v17.0", "v16.0")) {
            $candidates += (Join-Path $ext "Microsoft\VisualStudio\$ver\AppxPackage\Microsoft.Build.Packaging.Pri.Tasks.dll")
        }
    }
    foreach ($base in @(
        "${env:ProgramFiles}\Microsoft Visual Studio",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio"
    )) {
        if (-not (Test-Path $base)) { continue }
        $candidates += Get-ChildItem -Path $base -Recurse -Filter "Microsoft.Build.Packaging.Pri.Tasks.dll" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName -First 3
    }
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return (Resolve-Path $c).Path }
    }
    return $null
}

# Shared publish properties - keep in sync with WorkshopOS.Client.csproj
$PublishProps = @(
    "/p:Configuration=$Configuration"
    "/p:Platform=x64"
    "/p:RuntimeIdentifier=win-x64"
    "/p:TargetFramework=net8.0-windows10.0.19041.0"
    "/p:SelfContained=true"
    "/p:WindowsAppSDKSelfContained=true"
    "/p:WindowsPackageType=None"
    "/p:EnableMsixTooling=true"
    "/p:GenerateAppxPackageOnBuild=false"
    "/p:AppxPackageSigningEnabled=false"
    "/p:AppxPackage=false"
    "/p:PublishSingleFile=false"
    "/p:PublishReadyToRun=false"
    "/p:PublishProtocol=FileSystem"
    "/p:DeployOnBuild=false"
    "/p:Version=$Version"
    "/p:InformationalVersion=$Version"
)

Write-Host "==> Restoring WorkshopOS.Client"
dotnet restore $Proj
Assert-LastExit "dotnet restore"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

$MSBuild = Find-MSBuild
$usedEngine = $null

if ($MSBuild) {
    Write-Host "Using Visual Studio MSBuild: $MSBuild" -ForegroundColor Green
    $extPath = Get-VsMsBuildExtensionsPath $MSBuild
    if ($extPath) {
        # Keep MSBuild from resolving Appx/PRI tasks under the .NET SDK tree
        $env:MSBuildExtensionsPath = $extPath
        $env:MSBuildExtensionsPath32 = $extPath
        Write-Host "MSBuildExtensionsPath: $extPath"
    }
    $env:MSBUILD_EXE_PATH = $MSBuild

    $priDll = Find-PriTasksDll $MSBuild
    if ($priDll) {
        Write-Host "Found VS PRI tasks (optional with EnableMsixTooling): $priDll"
    } else {
        Write-Host "VS Microsoft.Build.Packaging.Pri.Tasks.dll not found - OK; EnableMsixTooling uses NuGet Msix tasks." -ForegroundColor DarkYellow
    }

    Write-Host "==> Publishing self-contained win-x64 ($Configuration) via VS MSBuild"
    & $MSBuild $Proj `
        /restore `
        /t:Restore,Publish `
        @PublishProps `
        /p:PublishDir="$PublishDir\" `
        /p:OutDir="$PublishDir\" `
        /v:m
    Assert-LastExit "MSBuild Publish"
    $usedEngine = "VS MSBuild"
} else {
    Write-Warning @"
Visual Studio MSBuild was not found.
Falling back to ``dotnet publish`` (works when EnableMsixTooling=true).

For best results install VS Community + workload ``WinUI application development``,
then re-run from ``Developer PowerShell for VS``.
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment
"@

    Write-Host "==> Publishing self-contained win-x64 ($Configuration) via dotnet publish"
    & dotnet publish $Proj `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $PublishDir `
        /p:Platform=x64 `
        /p:WindowsAppSDKSelfContained=true `
        /p:WindowsPackageType=None `
        /p:EnableMsixTooling=true `
        /p:GenerateAppxPackageOnBuild=false `
        /p:AppxPackageSigningEnabled=false `
        /p:AppxPackage=false `
        /p:PublishSingleFile=false `
        /p:PublishReadyToRun=false
    Assert-LastExit "dotnet publish"
    $usedEngine = "dotnet publish"
}

$Exe = Join-Path $PublishDir "WorkshopOS.Client.exe"
if (-not (Test-Path $Exe)) {
    $candidates = @(
        (Join-Path $Root "apps\windows-client\WorkshopOS.Client\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"),
        (Join-Path $Root "apps\windows-client\WorkshopOS.Client\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64"),
        (Join-Path $Root "apps\windows-client\WorkshopOS.Client\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish")
    )
    foreach ($Fallback in $candidates) {
        if (Test-Path (Join-Path $Fallback "WorkshopOS.Client.exe")) {
            Write-Warning "Copying output from $Fallback"
            Copy-Item -Path (Join-Path $Fallback "*") -Destination $PublishDir -Recurse -Force
            break
        }
    }
}

$Exe = Join-Path $PublishDir "WorkshopOS.Client.exe"
if (-not (Test-Path $Exe)) {
    Write-Host "Publish folder contents:"
    Get-ChildItem $PublishDir -ErrorAction SilentlyContinue | Format-Table Name, Length
    throw @"
WorkshopOS.Client.exe missing after $usedEngine - refusing to create a zip.

If you still see ExpandPriContent / Pri.Tasks.dll errors:
  1. Confirm this script printed ``WorkshopOS client build script v4``
  2. Confirm csproj has ``<EnableMsixTooling>true</EnableMsixTooling>``
  3. Open Visual Studio Installer -> Modify -> enable:
       - Workload: WinUI application development
       - Individual: Windows App Packaging, Windows 10/11 SDK
  4. Re-run from Developer PowerShell for VS

Guide: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment
"@
}

$fileCount = (Get-ChildItem $PublishDir -Recurse -File | Measure-Object).Count
if ($fileCount -lt 5) {
    throw "Publish output looks empty ($fileCount files) - refusing to create a zip. Engine=$usedEngine"
}

Write-Host "==> Published $fileCount files via $usedEngine -> $PublishDir" -ForegroundColor Green

New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$ZipPath = Join-Path $DistDir "WorkshopOS-Client-win-x64-v$Version.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath
Write-Host "Portable zip: $ZipPath" -ForegroundColor Green

if ($SkipInstaller) {
    Write-Host "Skipping Inno Setup. Extract the zip and run WorkshopOS.Client.exe"
    exit 0
}

$Iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    Write-Warning "Inno Setup not found - zip is enough. https://jrsoftware.org/isdl.php"
    exit 0
}

$Iss = Join-Path $Root "packaging\WorkshopOS-Setup.iss"
Write-Host "==> Building installer"
& $Iscc "/DMyAppVersion=$Version" "/DPublishDir=$PublishDir" "/DDistDir=$DistDir" $Iss
Assert-LastExit "Inno Setup"

Write-Host "Installer: $(Join-Path $DistDir "WorkshopOS-Setup-$Version.exe")" -ForegroundColor Green
Write-Host "Connect tip: http://<server>:5088/connect"
