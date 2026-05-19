$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = '1.0.5'
$release = Join-Path $root 'release'
$payload = Join-Path $release 'payload'
$installerPayload = Join-Path $root 'src\DesktopClock.Installer\Payload'
$appIcon = Join-Path $root 'Assets\Icons\app-dark.ico'

if (Test-Path -LiteralPath $release) {
    $resolvedRelease = (Resolve-Path -LiteralPath $release).Path
    if (-not $resolvedRelease.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected release path: $resolvedRelease"
    }
    Remove-Item -LiteralPath $resolvedRelease -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $payload | Out-Null
if (Test-Path -LiteralPath $installerPayload) {
    Remove-Item -LiteralPath $installerPayload -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $installerPayload | Out-Null

$commonPublishArgs = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'false',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

dotnet publish (Join-Path $root 'DesktopClock.csproj') @commonPublishArgs -o $payload
dotnet publish (Join-Path $root 'src\DesktopClock.Uninstaller\DesktopClock.Uninstaller.csproj') @commonPublishArgs -o $payload
Copy-Item -LiteralPath $appIcon -Destination (Join-Path $payload 'Desktop Clock.ico') -Force
Copy-Item -LiteralPath (Join-Path $root 'desktop-image-clock.json') -Destination (Join-Path $payload 'desktop-image-clock.json') -Force

Get-ChildItem -LiteralPath $payload -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installerPayload -Force
}

dotnet publish (Join-Path $root 'src\DesktopClock.Installer\DesktopClock.Installer.csproj') @commonPublishArgs -o $release

$installerExe = Join-Path $release 'Desktop Clock Installer.exe'
$downloadExe = Join-Path $release 'Desktop.Clock.Installer.exe'
Copy-Item -LiteralPath $installerExe -Destination $downloadExe -Force

$zipPath = Join-Path $release "DesktopClock-$version-win-x64.zip"
Compress-Archive -LiteralPath $downloadExe -DestinationPath $zipPath -Force

Get-ChildItem -LiteralPath $release -Force
