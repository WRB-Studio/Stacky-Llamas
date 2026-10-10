[CmdletBinding()]
param([Parameter(Mandatory)][string]$RequestFile)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$allowed = @('Build-Apk.ps1', 'Build-Aab.ps1', 'Build-ApkAndInstall.ps1', 'Get-AndroidDevices.ps1',
    'Build-AndroidAndExportToDrive.ps1', 'Build-AabAndSubmitToPlay.ps1', 'Set-DriveExportDirectory.ps1',
    'Set-AndroidDeviceBuildSettings.ps1', 'Test-ReleaseWorkflow.ps1')
try {
    $request = Get-Content -LiteralPath $RequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($request.ScriptName -notin $allowed) { throw 'Unknown Unity menu command.' }
    $parameters = @{}
    foreach ($property in $request.Parameters.PSObject.Properties) { $parameters[$property.Name] = $property.Value }
    & (Join-Path $PSScriptRoot $request.ScriptName) @parameters
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
