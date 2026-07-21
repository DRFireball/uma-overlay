<#
.SYNOPSIS
    Builds the shippable UmaCompanion.exe and zips it for release.

.DESCRIPTION
    Produces a single self-contained executable (the .NET runtime is bundled, so the machine that
    runs it needs nothing pre-installed) plus a zip ready to attach to a GitHub Release.

    Tagging a release runs this same profile in CI (.github/workflows/release.yml); this script is
    for building one locally to test the packaged app before you tag.

.PARAMETER Output
    Directory to write the publish output and zip to. Default: artifacts/ under the repo root.

.PARAMETER SkipTests
    Publish without running the test suite first. Handy for a quick smoke build; don't ship it.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Output D:\builds -SkipTests
#>
[CmdletBinding()]
param(
    [string]$Output,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $Output) { $Output = Join-Path $root "artifacts" }

# The app locks its own exe while running, which fails the build with a confusing MSB3027.
Get-Process -Name "UmaCompanion", "UmaCompanion.App" -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $SkipTests) {
    Write-Host "==> Running tests" -ForegroundColor Cyan
    # Integration tests that need the game's master.mdb skip themselves when it isn't found.
    dotnet test (Join-Path $root "UmaCompanion.sln") --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Tests failed - not publishing." }
}

$publishDir = Join-Path $Output "win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "==> Publishing self-contained win-x64" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src\UmaCompanion.App\UmaCompanion.App.csproj") `
    -p:PublishProfile=win-x64 `
    -o $publishDir `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exe = Join-Path $publishDir "UmaCompanion.exe"
if (-not (Test-Path $exe)) { throw "Expected $exe - publish produced something else." }

$version = (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', ''
$zip = Join-Path $Output "UmaCompanion-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "==> Zipping" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zip

"{0}  ({1:N1} MB)" -f $exe, ((Get-Item $exe).Length / 1MB)
"{0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)
