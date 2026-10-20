# Builds the two things BytePulse ships as.
#
#   portable  a self-contained folder that runs on a machine with no .NET and no
#             installer; zipped for a release page
#   msix      a package for machines that prefer an installed application
#
# Usage:
#   ./scripts/publish.ps1                 build both for win-x64
#   ./scripts/publish.ps1 -Runtime win-arm64
#   ./scripts/publish.ps1 -PortableOnly
#
# Set -Sign and pass a certificate to sign the package.

[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [switch]$PortableOnly,
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo "artifacts"
$app = Join-Path $repo "src/BytePulse.App/BytePulse.csproj"
$cli = Join-Path $repo "src/BytePulse.Cli/BytePulse.Cli.csproj"

if (Test-Path $output) {
    Remove-Item -Recurse -Force $output
}

New-Item -ItemType Directory -Path $output | Out-Null

function Get-Rid {
    param([string]$Value)
    if ($Value -eq "win-x64") { return "win-x64" }
    if ($Value -eq "win-arm64") { return "win-arm64" }
    throw "Unsupported runtime '$Value'. Use win-x64 or win-arm64."
}

$rid = Get-Rid $Runtime

Write-Host "Building the self-contained application for $rid..."
dotnet publish $app `
    -c $Configuration `
    -r $rid `
    -p:Version=$Version `
    -p:WindowsPackageType=None `
    -p:SelfContained=true `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=true `
    -o (Join-Path $output "app-$rid") | Out-Host

Write-Host "Building the command line tool for $rid..."
dotnet publish $cli `
    -c $Configuration `
    -r $rid `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -o (Join-Path $output "cli-$rid") | Out-Host

$portable = Join-Path $output "BytePulse-$Version-$rid"
New-Item -ItemType Directory -Path $portable | Out-Null
Copy-Item -Recurse (Join-Path $output "app-$rid/*") $portable
Copy-Item (Join-Path $output "cli-$rid/bytepulse.exe") $portable
Copy-Item (Join-Path $repo "LICENSE") $portable
Copy-Item (Join-Path $repo "README.md") $portable

$zip = "$portable.zip"
Compress-Archive -Path "$portable/*" -DestinationPath $zip
Write-Host "Portable build: $zip"

if ($PortableOnly) {
    return
}

Write-Host "Building the package for $rid..."
$signing = @()

if ($CertificatePath) {
    $signing = @(
        "-p:AppxPackageSigningEnabled=true",
        "-p:PackageCertificateKeyFile=$CertificatePath"
    )

    if ($CertificatePassword) {
        $signing += "-p:PackageCertificatePassword=$CertificatePassword"
    }
}
else {
    $signing = @("-p:AppxPackageSigningEnabled=false")
}

dotnet publish $app `
    -c $Configuration `
    -r $rid `
    -p:Version=$Version `
    -p:WindowsPackageType=MSIX `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir=(Join-Path $output "msix/") `
    @signing | Out-Host

Write-Host "Package written under $(Join-Path $output 'msix')"
Write-Host "Done."
