<#
.SYNOPSIS
    Offscreen UI preview runner for the g0v0 client (see ../SKILL.md).
.DESCRIPTION
    Renders client UI on an offscreen GPU device and saves PNG captures with no visible window.
    Every argument the previewer accepts has a matching parameter here, so the CLI reference in
    SKILL.md applies unchanged; captures land in tools/Previewer/<Out>.
.EXAMPLE
    .\preview.ps1 -Scene SettingsPanel -Crop SettingsPanel -Locale zh
.EXAMPLE
    .\preview.ps1 -Scene TestSceneImagePreviewOverlay -Test TestShowAndHide -CaptureBefore "hide overlay"
.EXAMPLE
    .\preview.ps1 -Scripts "compose,timing" -Locale zh
#>

param(
    [string]$Scene = "editor",
    [string]$Scripts = "compose",
    [string]$Out = "captures",
    [string]$Size = "1920x1080",
    [string]$Locale = "zh",
    [string]$Ruleset = "osu",
    [int]$Seek = 13000,
    [string]$Crop,
    [string]$Test,
    [int]$Case = 0,
    [string]$StopBefore,
    [string]$CaptureBefore,
    [int]$Wait = 3000,
    [switch]$Build,
    [switch]$Help
)

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

$ErrorActionPreference = "Stop"

# Locate repo root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$skillDir = Split-Path -Parent $scriptDir
$previewerDir = Join-Path $skillDir "tools\Previewer"
$previewerProj = Join-Path $previewerDir "Previewer.csproj"
$exePath = Join-Path $previewerDir "bin\Debug\net10.0\win-x64\Previewer.exe"

# Check if build is needed
if ($Build -or -not (Test-Path $exePath)) {
    Write-Host "Building UI Previewer..."
    & dotnet build $previewerProj -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Previewer build failed"
        exit 1
    }
}

# Construct arguments
$forwardArgs = @(
    "--scene=$Scene",
    "--out=$Out",
    "--size=$Size",
    "--ruleset=$Ruleset",
    "--seek=$Seek",
    "--wait=$Wait"
)

if ($Locale) { $forwardArgs += "--locale=$Locale" }
if ($Scripts -and $Scene -eq "editor") { $forwardArgs += "--scripts=$Scripts" }
if ($Crop) { $forwardArgs += "--crop=$Crop" }
if ($Test) { $forwardArgs += "--test=$Test" }
if ($Case -gt 0) { $forwardArgs += "--case=$Case" }
if ($StopBefore) { $forwardArgs += "--stop-before=$StopBefore" }
if ($CaptureBefore) { $forwardArgs += "--capture-before=$CaptureBefore" }

# Run from the previewer directory so that a relative -Out lands beside the executable, as SKILL.md describes.
Push-Location $previewerDir
try {
    & $exePath @forwardArgs
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
