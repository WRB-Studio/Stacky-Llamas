function Resolve-DriveExportDirectory {
    param([string]$DriveDirectory)

    if ([string]::IsNullOrWhiteSpace($DriveDirectory)) {
        if (Test-Path -LiteralPath $script:DriveConfigPath -PathType Leaf) {
            $localConfig = Get-Content -LiteralPath $script:DriveConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if (-not $localConfig.PSObject.Properties['DriveDirectory'] -or $localConfig.DriveDirectory -isnot [string]) {
                throw 'Invalid local Drive configuration. Run Set-DriveExportDirectory.ps1 again.'
            }
            $DriveDirectory = $localConfig.DriveDirectory
        }
    }
    if ([string]::IsNullOrWhiteSpace($DriveDirectory)) {
        throw 'No local Drive export directory configured. Run Set-DriveExportDirectory.ps1 -DriveDirectory <folder> or pass -DriveDirectory. Move any old DriveDirectory value out of release.config.json.'
    }
    if ($DriveDirectory -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+[\\/]?)') {
        throw 'DriveDirectory must be an absolute Windows filesystem path.'
    }
    if (-not (Test-Path -LiteralPath $DriveDirectory -PathType Container)) {
        throw 'Local Drive export directory does not exist. Create/select the synchronized folder first.'
    }
    $directory = Get-Item -LiteralPath $DriveDirectory
    if ($directory.PSProvider.Name -ne 'FileSystem') { throw 'DriveDirectory must be a filesystem directory.' }
    return $directory.FullName
}

function Export-AndroidBuildToDriveDirectory {
    param(
        [Parameter(Mandatory)]$Build,
        [Parameter(Mandatory)][ValidateSet('apk', 'aab')][string]$Format,
        [Parameter(Mandatory)][string]$DriveDirectory,
        [switch]$Force
    )

    $directory = Resolve-DriveExportDirectory -DriveDirectory $DriveDirectory
    $Format = $Format.ToLowerInvariant()
    $version = [string]$Build.Version.Version
    if ([string]::IsNullOrWhiteSpace($version) -or $version -match '[<>:"/\\|?*\x00-\x1F]') {
        throw 'Unity bundleVersion contains characters that cannot be used in an export filename.'
    }
    if ($Build.Version.VersionCode -lt 1) { throw 'Build has no valid Android versioncode.' }
    if (-not (Test-Path -LiteralPath $Build.ArtifactPath -PathType Leaf) -or
        [IO.Path]::GetExtension($Build.ArtifactPath) -ine ".$Format") {
        throw 'Build artifact is missing or does not match the requested format.'
    }
    $source = (Get-Item -LiteralPath $Build.ArtifactPath).FullName
    $fileName = "$($script:ReleaseProject.ArtifactName)-$version-$($Build.Version.VersionCode).$Format"
    $destination = Join-Path $directory $fileName
    if ($source -ieq $destination) { throw 'Build source and export destination must be different files.' }
    if ((Test-Path -LiteralPath $destination) -and -not $Force) {
        throw 'An export with this name already exists. Use another versioncode or -Force to replace it.'
    }

    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    # File.Copy also rejects a destination created between the existence check and copy.
    [IO.File]::Copy($source, $destination, $Force.IsPresent)
    $destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) { throw 'Local export verification failed: SHA-256 mismatch.' }

    Write-Host "Local copy verified (SHA-256): $destination"
    Write-Host 'Cloud upload NOT confirmed. Google Drive for desktop controls synchronization; verify its status separately.'
    return [PSCustomObject]@{
        ArtifactPath = $source
        DestinationPath = $destination
        Format = $Format
        Version = $Build.Version
        Sha256 = $destinationHash
        LocalCopySucceeded = $true
        CloudUploadConfirmed = $false
    }
}
