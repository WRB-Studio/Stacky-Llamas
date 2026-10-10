Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.ps1) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count) { throw "Invalid PowerShell syntax: $($file.Name)" }
}
. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
Assert-UnityBuildHelper

foreach ($case in @(
    @{ Highest = 5; Project = 3; Requested = 0; Expected = 6 },
    @{ Highest = 5; Project = 9; Requested = 0; Expected = 9 },
    @{ Highest = 5; Project = 3; Requested = 8; Expected = 8 },
    @{ Highest = 0; Project = 1; Requested = 0; Expected = 1 }
)) {
    $actual = Resolve-ReleaseVersionCode -HighestPlayVersion $case.Highest -ProjectVersion $case.Project -RequestedVersion $case.Requested
    if ($actual -ne $case.Expected) { throw "Unexpected release versioncode: $actual" }
}
foreach ($used in @(1, 4, 5)) {
    $rejected = $false
    try { Resolve-ReleaseVersionCode -HighestPlayVersion 5 -RequestedVersion $used | Out-Null }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Accepted used versioncode: $used" }
}
$blocked = $false
try { & (Join-Path $PSScriptRoot 'Build-AabAndSubmitToPlay.ps1') }
catch { $blocked = $_.Exception.Message -like 'Production upload blocked.*' }
if (-not $blocked) { throw 'Production guard did not stop execution before network/build access.' }
Write-Output 'Passed: script syntax, 4 versioncode cases, 3 reused-code rejections, production guard. No build or upload started.'
& (Join-Path $PSScriptRoot 'Test-PlayMetadata.ps1')
& (Join-Path $PSScriptRoot 'Test-UnityAndroidBuild.ps1')
& (Join-Path $PSScriptRoot 'Test-DriveExport.ps1')

& (Join-Path $PSScriptRoot 'Test-AndroidDevice.ps1')
& (Join-Path $PSScriptRoot 'Test-AndroidDeviceWorkflow.ps1')
& (Join-Path $PSScriptRoot 'Test-UnityMenuCommand.ps1')
