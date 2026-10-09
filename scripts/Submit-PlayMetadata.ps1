[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MetadataFile,
    [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$VersionCode,
    [ValidateSet('production', 'internal', 'alpha', 'beta')][string]$Track = 'production',
    [switch]$ConfirmMetadata,
    [switch]$ConfirmProduction,
    [switch]$ChangesNotSentForReview,
    [switch]$CheckOnly
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'PlayMetadata.ps1')

if ($Track -eq 'production' -and -not $ConfirmProduction -and -not $CheckOnly) {
    throw 'Production upload blocked. Use -ConfirmProduction for the requested production changes.'
}
if (-not $ConfirmMetadata -and -not $CheckOnly) {
    throw 'Metadata upload blocked. Review with -CheckOnly, then use -ConfirmMetadata for approved texts.'
}
$metadata = Read-PlayMetadata -MetadataFile $MetadataFile
Show-PlayMetadata -Metadata $metadata -VersionCode $VersionCode
if ($CheckOnly) { return }

$fastlanePath = Resolve-Fastlane
$config = Get-ReleaseConfig -RequirePlay
$directory = Write-PlayMetadata -Metadata $metadata -VersionCode $VersionCode
$arguments = @('supply', '--package_name', $script:ReleaseProject.PackageName,
    '--json_key', $config.ServiceAccountJsonPath, '--track', $Track, '--version_code', $VersionCode.ToString(),
    '--skip_upload_aab', 'true', '--skip_upload_apk', 'true', '--skip_upload_images', 'true', '--skip_upload_screenshots', 'true',
    '--changes_not_sent_for_review', $ChangesNotSentForReview.IsPresent.ToString().ToLowerInvariant(),
    '--rescue_changes_not_sent_for_review', 'false')
$arguments += Get-PlayMetadataArguments -Metadata $metadata -Directory $directory
& $fastlanePath @arguments
if ($LASTEXITCODE -ne 0) { throw "Google Play metadata upload failed with exit code $LASTEXITCODE. Inspect Play Console before retrying." }
Write-Host 'Metadata committed to Google Play. Public availability depends on review and publishing settings.'
