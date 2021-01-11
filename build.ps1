Param(
    [switch] $Release,
    [string] $SigningCertThumbprint,
    [string] $TimestampServer
)

$ErrorActionPreference = 'Stop'

# Options
$configuration = 'Release'
$artifactsDir = Join-Path (Resolve-Path .) 'artifacts'
$packagesDir = Join-Path $artifactsDir 'Packages'
$logsDir = Join-Path $artifactsDir 'Logs'

# Detection
. $PSScriptRoot\build\Get-DetectedCiVersion.ps1
$versionInfo = Get-DetectedCiVersion -Release:$Release
Update-CiServerBuildName $versionInfo.ProductVersion
Write-Host "Building using version $($versionInfo.ProductVersion)"

$dotnetArgs = @(
    '--configuration', $configuration
    '/p:RepositoryCommit=' + $versionInfo.CommitHash
    '/p:Version=' + $versionInfo.ProductVersion
    '/p:PackageVersion=' + $versionInfo.PackageVersion
    '/p:FileVersion=' + $versionInfo.FileVersion
    '/p:ContinuousIntegrationBuild=' + ($env:CI -or $env:TF_BUILD)
)

# Build
dotnet build /bl:$logsDir\build.binlog @dotnetArgs
if ($LastExitCode) { exit 1 }

if ($SigningCertThumbprint) {
    . build\SignTool.ps1
    SignTool $SigningCertThumbprint $TimestampServer (
        Get-ChildItem src\Techsola.InstantReplay\bin\$configuration -Recurse -Include Techsola.InstantReplay.dll)
}

# Pack
Remove-Item -Recurse -Force $packagesDir -ErrorAction Ignore

dotnet pack src\Techsola.InstantReplay --no-build --output $packagesDir /bl:$logsDir\pack.binlog @dotnetArgs
if ($LastExitCode) { exit 1 }

if ($SigningCertThumbprint) {
    # Waiting for 'dotnet sign' to become available (https://github.com/NuGet/Home/issues/7939)
    $nuget = 'tools\nuget.exe'
    if (-not (Test-Path $nuget)) {
        New-Item -ItemType Directory -Force -Path tools

        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nuget
    }

     # Workaround for https://github.com/NuGet/Home/issues/10446
    foreach ($extension in 'nupkg', 'snupkg') {
        & $nuget sign $packagesDir\*.$extension -CertificateFingerprint $SigningCertThumbprint -Timestamper $TimestampServer
    }
}
