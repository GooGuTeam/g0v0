param(
    [string]$Scene = "editor",
    [string]$Scripts = "compose",
    [string]$Out = "captures",
    [string]$Size = "1920x1080",
    [string]$Locale = "zh",
    [string]$Ruleset = "osu",
    [string]$Crop,
    [string]$Test,
    [int]$Case = 0,
    [string]$StopBefore,
    [int]$Wait = 1500,
    [switch]$Build,
    [switch]$Help
)

<#
.SYNOPSIS
    Offscreen UI preview runner for the g0v0 client (see ../SKILL.md).
.DESCRIPTION
    Renders client UI on an offscreen GPU device and saves PNG captures with no visible window.
.EXAMPLE
    .\preview.ps1 -Scene TestSceneSettingsPanel -Crop SettingsPanel -Locale zh
    .\preview.ps1 -Scripts "compose,timing" -Locale zh
    .\preview.ps1 -Scene TestSceneSongSelect -Size 1280x720
#>

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path
    exit 0
}

$ErrorActionPreference = "Stop"

# Locate repo root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$skillDir = Split-Path -Parent $scriptDir
$previewerDir = Join-Path $skillDir "tools\Previewer"
$previewerProj = Join-Path $previewerDir "Previewer.csproj"
$exePath = Join-Path $previewerDir "bin\Debug\net10.0\win-x64\Previewer.exe"

$repoRoot = $skillDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot ".git"))) {
    $parent = Split-Path -Parent $repoRoot
    if ($parent -eq $repoRoot) { break }
    $repoRoot = $parent
}

# Check if build is needed
if ($Build -or -not (Test-Path $exePath)) {
    Write-Host "Building UI Previewer..."
    $buildArgs = @("build", $previewerProj, "-c", "Debug")

    # If source framework exists beside repository or G0V0SourceBuild is set, pass source build
    $frameworkSibling = Join-Path (Split-Path -Parent $repoRoot) "g0v0-framework"
    if ($env:G0V0SourceBuild -eq "true" -or (Test-Path $frameworkSibling)) {
        $buildArgs += "-p:G0V0SourceBuild=true"
    }

    & dotnet @buildArgs
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
    "--wait=$Wait"
)

if ($Locale) { $forwardArgs += "--locale=$Locale" }
if ($Scripts -and $Scene -eq "editor") { $forwardArgs += "--scripts=$Scripts" }
if ($Crop) { $forwardArgs += "--crop=$Crop" }
if ($Test) { $forwardArgs += "--test=$Test" }
if ($Case -gt 0) { $forwardArgs += "--case=$Case" }
if ($StopBefore) { $forwardArgs += "--stop-before=$StopBefore" }

Push-Location $previewerDir
try {
    & $exePath @forwardArgs
} finally {
    Pop-Location
}
