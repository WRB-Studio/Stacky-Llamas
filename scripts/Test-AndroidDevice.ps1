Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
if ((Get-AndroidDevice -Lines @('List of devices attached', 'fixture-device device')) -cne 'fixture-device') { throw 'Single-device selection failed.' }
if ((Get-AndroidDevice -Lines @('fixture-one device', 'fixture-two device') -DeviceSerial 'fixture-two') -cne 'fixture-two') { throw 'Explicit device selection failed.' }
foreach ($lines in @(@(''), @('fixture-one device', 'fixture-two device'), @('fixture-device unauthorized'), @('fixture-device offline'))) {
    $blocked = $false
    try { Get-AndroidDevice -Lines $lines | Out-Null } catch { $blocked = $true }
    if (-not $blocked) { throw 'Unsafe device selection was accepted.' }
}
$installed = Get-InstalledAndroidVersion -PackageOutput "versionCode=42 minSdk=25`nversionName=2.3.4"
if ($installed.Code -ne 42 -or $installed.Name -cne '2.3.4') { throw 'Installed package parsing failed.' }
if ((Get-InstalledAndroidVersion -PackageOutput 'Unable to find package').Code -ne 0) { throw 'Absent package parsing failed.' }
if ((Get-NextDeviceVersionCode -ProjectCode 41 -InstalledCode 42) -ne 43 -or
    (Get-NextDeviceVersionCode -ProjectCode 50 -InstalledCode 42) -ne 51) { throw 'Next version code failed.' }
$blocked = $false
try { Get-NextDeviceVersionCode -ProjectCode ([int]::MaxValue) -InstalledCode 0 | Out-Null } catch { $blocked = $true }
if (-not $blocked) { throw 'Version overflow was accepted.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) 'android-device-fixture'
$source = Join-Path $fixture 'source'
foreach ($cache in @($source, (Join-Path $source 'cache'), $fixture, [IO.Path]::GetPathRoot($source))) {
    $blocked = $false
    try { Assert-AndroidBuildCache -SourceRoot $source -BuildRoot $cache | Out-Null } catch { $blocked = $true }
    if (-not $blocked) { throw 'Unsafe cache root was accepted.' }
}
Assert-AndroidBuildCache -SourceRoot $source -BuildRoot (Join-Path $fixture 'cache') | Out-Null
Write-Output 'Passed: device selection, authorization guards, installed versions, increasing version codes and cache containment. No ADB, build, installation or upload.'
