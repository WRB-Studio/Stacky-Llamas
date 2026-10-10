[CmdletBinding()]
param([string]$UnityPath, [ValidateRange(0, [int]::MaxValue)][int]$VersionCode, [string]$BuildRoot)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
Invoke-UnityAndroidBuild -Format aab -UnityPath $UnityPath -VersionCode $VersionCode -BuildRoot $BuildRoot
