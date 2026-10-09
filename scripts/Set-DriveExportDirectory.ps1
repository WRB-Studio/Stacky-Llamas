[CmdletBinding()]
param([Parameter(Mandatory)][string]$DriveDirectory)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'DriveExport.ps1')

$directory = Resolve-DriveExportDirectory -DriveDirectory $DriveDirectory
New-Item -ItemType Directory -Path (Split-Path -Parent $script:DriveConfigPath) -Force | Out-Null
[PSCustomObject]@{ DriveDirectory = $directory } |
    ConvertTo-Json | Set-Content -LiteralPath $script:DriveConfigPath -Encoding UTF8
Write-Host "Local Drive export directory saved for project key '$($script:ReleaseProject.SecretsKey)'. No files copied or uploaded."
