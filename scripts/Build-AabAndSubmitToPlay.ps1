[CmdletBinding()]
param(
    [ValidateRange(0, [int]::MaxValue)][int]$VersionCode = 0,
    [string]$UnityPath,
    [ValidateSet('production', 'internal', 'alpha', 'beta')][string]$Track = 'production',
    [switch]$ConfirmProduction,
    [string]$MetadataFile,
    [switch]$ConfirmMetadata,
    [ValidateSet('completed', 'draft')][string]$ReleaseStatus = 'completed',
    [switch]$ChangesNotSentForReview,
    [switch]$CheckOnly
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'PlayMetadata.ps1')

if ($Track -eq 'production' -and -not $ConfirmProduction -and -not $CheckOnly) {
    throw 'Production upload blocked. Use -ConfirmProduction when this release is ready for Google Play review/release.'
}

if ($MetadataFile -and -not $ConfirmMetadata -and -not $CheckOnly) {
    throw 'Metadata upload blocked. Review with -CheckOnly, then use -ConfirmMetadata for approved texts.'
}
$metadata = if ($MetadataFile) { Read-PlayMetadata -MetadataFile $MetadataFile } else { $null }
$fastlanePath = Resolve-Fastlane
$config = Get-ReleaseConfig -RequirePlay
$highest = Get-HighestPlayVersionCode
$VersionCode = Resolve-ReleaseVersionCode -HighestPlayVersion $highest -RequestedVersion $VersionCode -ProjectVersion (Get-ProjectVersion).VersionCode
Write-Host "Package: $($script:ReleaseProject.PackageName); track: $Track; next versioncode: $VersionCode (Google Play maximum: $highest)."
if ($metadata) { Show-PlayMetadata -Metadata $metadata -VersionCode $VersionCode }
if ($CheckOnly) { return }

$build = Invoke-UnityAndroidBuild -Format aab -UnityPath $UnityPath -VersionCode $VersionCode
$arguments = @('supply', '--aab', $build.ArtifactPath, '--track', $Track, '--release_status', $ReleaseStatus,
    '--json_key', $config.ServiceAccountJsonPath, '--package_name', $script:ReleaseProject.PackageName,
    '--skip_upload_images', 'true', '--skip_upload_screenshots', 'true', '--skip_upload_apk', 'true',
    '--changes_not_sent_for_review', $ChangesNotSentForReview.IsPresent.ToString().ToLowerInvariant(),
    '--rescue_changes_not_sent_for_review', 'false')
if ($metadata) {
    $directory = Write-PlayMetadata -Metadata $metadata -VersionCode $VersionCode
    $arguments += Get-PlayMetadataArguments -Metadata $metadata -Directory $directory
} else {
    $arguments += @('--skip_upload_metadata', 'true', '--skip_upload_changelogs', 'true')
}
& $fastlanePath @arguments
if ($LASTEXITCODE -ne 0) { throw "Google Play upload failed with exit code $LASTEXITCODE." }
Write-Host "AAB committed to $Track (versioncode $VersionCode; status $ReleaseStatus): $($build.ArtifactPath)"
Write-Host 'Public availability depends on Google review and publishing settings; deferred changes require submission in Play Console.'
