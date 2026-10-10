[CmdletBinding()]
param([string]$UnityPath, [string]$AdbPath)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
$deviceTools = Resolve-AndroidDeviceTools -UnityPath $UnityPath -AdbPath $AdbPath
$discovery = Invoke-AndroidCommand -Executable $deviceTools.Adb -NativeArgs @('devices', '-l')
if ($discovery.ExitCode -ne 0) { throw 'ADB device discovery failed.' }
$devices = @($discovery.Output -split "`r?`n" | ForEach-Object {
    if ($_ -match '^([^\s]+)\s+(device|unauthorized|offline)(?:\s+.*)?$') {
        [PSCustomObject]@{ Serial = $matches[1]; State = $matches[2] }
    }
})
ConvertTo-Json -InputObject ([PSCustomObject]@{ Devices = $devices }) -Depth 3 -Compress
