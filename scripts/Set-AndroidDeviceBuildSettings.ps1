[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityPath,
    [string]$AdbPath,
    [string]$BuildRoot
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
$editor = Resolve-UnityEditor -UnityPath $UnityPath
if ($AdbPath -and -not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) { throw 'The selected ADB executable is missing.' }
if ($BuildRoot) { $BuildRoot = Assert-AndroidBuildCache -SourceRoot $script:ProjectRoot -BuildRoot $BuildRoot }
$settings = @{ UnityPath = $editor }
if ($AdbPath) { $settings.AdbPath = [IO.Path]::GetFullPath($AdbPath) }
if ($BuildRoot) { $settings.BuildRoot = $BuildRoot }
$folder = Split-Path -Parent $script:ReleaseConfigPath
New-Item -ItemType Directory -Path $folder -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $folder 'device-build.json'), ($settings | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
$projectSettingsFolder = Join-Path $script:ProjectRoot 'Builds/Android'
New-Item -ItemType Directory -Path $projectSettingsFolder -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $projectSettingsFolder 'device-build.json'), ($settings | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
Write-Output 'Device build settings saved locally outside Git. No device identifier, build, installation or upload.'
