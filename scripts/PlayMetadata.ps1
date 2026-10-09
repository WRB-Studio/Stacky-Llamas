function Read-PlayMetadata {
    param([Parameter(Mandatory)][string]$MetadataFile)

    $metadata = Get-Content -LiteralPath $MetadataFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($field in $metadata.PSObject.Properties.Name) {
        if ($field -notin @('packageName', 'listings')) { throw "Unknown metadata field: $field" }
    }
    if (-not $metadata.PSObject.Properties['packageName'] -or $metadata.packageName -cne $script:ReleaseProject.PackageName) {
        throw 'Metadata packageName does not match this project.'
    }
    if (-not $metadata.PSObject.Properties['listings'] -or @($metadata.listings).Count -eq 0) {
        throw 'Metadata requires at least one listing.'
    }
    $languages = @{}
    $hasListings = $false
    $hasNotes = $false
    $limits = @{ title = 30; shortDescription = 80; fullDescription = 4000; releaseNotes = 500 }
    foreach ($listing in $metadata.listings) {
        foreach ($field in $listing.PSObject.Properties.Name) {
            if ($field -ne 'language' -and -not $limits.ContainsKey($field)) { throw "Unknown listing field: $field" }
        }
        if (-not $listing.PSObject.Properties['language'] -or $listing.language -cnotmatch '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$') {
            throw 'Invalid metadata language.'
        }
        if ($languages.ContainsKey($listing.language)) { throw "Duplicate metadata language: $($listing.language)" }
        $languages[$listing.language] = $true
        $hasContent = $false
        foreach ($field in $limits.Keys) {
            if (-not $listing.PSObject.Properties[$field]) { continue }
            $value = $listing.$field
            if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value) -or $value.Length -gt $limits[$field]) {
                throw "Invalid $field for $($listing.language); expected 1-$($limits[$field]) characters."
            }
            $hasContent = $true
            if ($field -eq 'releaseNotes') { $hasNotes = $true } else { $hasListings = $true }
        }
        if (-not $hasContent) { throw "No text supplied for $($listing.language)." }
    }
    if ($hasNotes) {
        foreach ($listing in $metadata.listings) {
            if (-not $listing.PSObject.Properties['releaseNotes']) {
                throw 'When changing release notes, supply releaseNotes for every listed language; Fastlane otherwise sends empty notes.'
            }
        }
    }
    return [PSCustomObject]@{ Data = $metadata; HasListings = $hasListings; HasNotes = $hasNotes }
}

function Show-PlayMetadata {
    param([Parameter(Mandatory)]$Metadata, [Parameter(Mandatory)][int]$VersionCode)
    Write-Host "Metadata package: $($Metadata.Data.packageName); versioncode: $VersionCode"
    foreach ($listing in $Metadata.Data.listings) {
        Write-Host "Language: $($listing.language)"
        foreach ($field in @('title', 'shortDescription', 'fullDescription', 'releaseNotes')) {
            if ($listing.PSObject.Properties[$field]) { Write-Host "${field}:`n$($listing.$field)" }
        }
    }
}

function Write-PlayMetadata {
    param([Parameter(Mandatory)]$Metadata, [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$VersionCode)
    # A fresh directory prevents files from an older release being uploaded accidentally.
    $directory = Join-Path $script:ProjectRoot ('Builds/Android/PlayMetadata/' + [guid]::NewGuid().ToString('N'))
    $fields = @{ title = 'title.txt'; shortDescription = 'short_description.txt'; fullDescription = 'full_description.txt' }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    foreach ($listing in $Metadata.Data.listings) {
        $localeDirectory = Join-Path $directory $listing.language
        New-Item -ItemType Directory -Path $localeDirectory -Force | Out-Null
        foreach ($field in $fields.Keys) {
            if ($listing.PSObject.Properties[$field]) {
                [IO.File]::WriteAllText((Join-Path $localeDirectory $fields[$field]), $listing.$field, $encoding)
            }
        }
        if ($listing.PSObject.Properties['releaseNotes']) {
            $changelogs = Join-Path $localeDirectory 'changelogs'
            New-Item -ItemType Directory -Path $changelogs -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $changelogs "$VersionCode.txt"), $listing.releaseNotes, $encoding)
        }
    }
    return $directory
}

function Get-PlayMetadataArguments {
    param([Parameter(Mandatory)]$Metadata, [Parameter(Mandatory)][string]$Directory)
    return @('--metadata_path', $Directory,
        '--skip_upload_metadata', (-not $Metadata.HasListings).ToString().ToLowerInvariant(),
        '--skip_upload_changelogs', (-not $Metadata.HasNotes).ToString().ToLowerInvariant())
}
