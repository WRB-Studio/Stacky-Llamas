[CmdletBinding()]
param(
    [ValidateSet('apk', 'aab')][string]$Format = 'apk',
    [string]$UnityPath,
    [ValidateRange(0, [int]::MaxValue)][int]$VersionCode = 0,
    [string]$DriveDirectory,
    [switch]$Force,
    [switch]$CheckOnly
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'DriveExport.ps1')

$directory = Resolve-DriveExportDirectory -DriveDirectory $DriveDirectory
Assert-UnityBuildHelper
if ($CheckOnly) {
    Write-Host "Local export configuration valid: $($script:ReleaseProject.PackageName); format: $Format; directory: $directory"
    Write-Host 'No build, copy or network access. Signing, write access and cloud synchronization have not been tested.'
    return
}

$build = Invoke-UnityAndroidBuild -Format $Format -UnityPath $UnityPath -VersionCode $VersionCode
Export-AndroidBuildToDriveDirectory -Build $build -Format $Format -DriveDirectory $directory -Force:$Force
