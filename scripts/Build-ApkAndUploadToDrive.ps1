[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateRange(0, [int]::MaxValue)][int]$VersionCode = 0,
    [string]$DriveDirectory,
    [switch]$Force,
    [switch]$CheckOnly
)

Write-Warning 'Legacy command: this performs a local APK export, not a confirmed cloud upload. Prefer Build-AndroidAndExportToDrive.ps1.'
& (Join-Path $PSScriptRoot 'Build-AndroidAndExportToDrive.ps1') -Format apk @PSBoundParameters
