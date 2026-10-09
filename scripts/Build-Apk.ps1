[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateRange(0, [int]::MaxValue)][int]$VersionCode
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
Invoke-UnityAndroidBuild -Format apk -UnityPath $UnityPath -VersionCode $VersionCode
