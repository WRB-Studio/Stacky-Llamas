Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'PlayMetadata.ps1')

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-play-metadata-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$originalRoot = $script:ProjectRoot
$originalPackage = $script:ReleaseProject.PackageName
try {
    $script:ProjectRoot = $testDirectory
    $script:ReleaseProject.PackageName = 'com.example.game'
    $inputFile = Join-Path $testDirectory 'metadata.json'
    $source = @{ packageName = 'com.example.game'; listings = @(
        @{ language = 'de-DE'; shortDescription = 'Bunte Figuren antippen!'; releaseNotes = "Neue Figuren!`nViel Spaß." },
        @{ language = 'en-US'; fullDescription = 'Tap the colourful characters!'; releaseNotes = 'New characters!' }
    ) }
    $source | ConvertTo-Json -Depth 6 | Set-Content $inputFile -Encoding UTF8
    $metadata = Read-PlayMetadata $inputFile
    $directory = Write-PlayMetadata -Metadata $metadata -VersionCode 42
    $notesPath = Join-Path $directory 'de-DE/changelogs/42.txt'
    if ([IO.File]::ReadAllText($notesPath) -cne $source.listings[0].releaseNotes) { throw 'Release notes changed during conversion.' }
    if ([IO.File]::ReadAllText((Join-Path $directory 'en-US/changelogs/42.txt')) -cne 'New characters!') { throw 'English release notes changed.' }
    if (Test-Path (Join-Path $directory 'de-DE/title.txt')) { throw 'Generated unrequested listing field.' }
    $bytes = [IO.File]::ReadAllBytes($notesPath)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { throw 'Generated UTF-8 BOM.' }
    $secondDirectory = Write-PlayMetadata -Metadata $metadata -VersionCode 43
    if ($directory -eq $secondDirectory -or (Test-Path (Join-Path $secondDirectory 'de-DE/changelogs/42.txt'))) { throw 'Reused stale metadata.' }
    $arguments = @(Get-PlayMetadataArguments -Metadata $metadata -Directory $directory)
    if ($arguments[3] -ne 'false' -or $arguments[5] -ne 'false') { throw 'Metadata upload flags are incorrect.' }

    $invalidCases = @(
        @{ packageName = 'com.other.game'; listings = $source.listings },
        @{ packageName = 'com.example.game'; listings = @() },
        @{ packageName = 'com.example.game'; listings = @(@{ language = '../escape'; title = 'Game' }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; title = 'Game' }, @{ language = 'de-DE'; title = 'Game' }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; shortDescription = ('x' * 81) }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; releaseNotes = ('x' * 501) }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; fullDescription = ' ' }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; title = 123 }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE' }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; title = 'Game'; typo = 'unexpected' }) },
        @{ packageName = 'com.example.game'; listings = @(@{ language = 'de-DE'; releaseNotes = 'Neu!' }, @{ language = 'en-US'; title = 'Game' }) }
    )
    foreach ($invalid in $invalidCases) {
        $invalid | ConvertTo-Json -Depth 6 | Set-Content $inputFile -Encoding UTF8
        $rejected = $false
        try { Read-PlayMetadata $inputFile | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Accepted invalid metadata.' }
    }
    foreach ($scriptName in @('Build-AabAndSubmitToPlay.ps1', 'Submit-PlayMetadata.ps1')) {
        $blocked = $false
        try {
            if ($scriptName -eq 'Submit-PlayMetadata.ps1') {
                & (Join-Path $PSScriptRoot $scriptName) -MetadataFile $inputFile -VersionCode 42 -Track internal
            } else {
                & (Join-Path $PSScriptRoot $scriptName) -MetadataFile $inputFile -Track internal
            }
        } catch { $blocked = $_.Exception.Message -like 'Metadata upload blocked.*' }
        if (-not $blocked) { throw "Metadata guard failed: $scriptName" }
    }

    # Exercise the entry points against a fake CLI, without credentials or network access.
    $fixtureScripts = Join-Path $testDirectory 'scripts'
    New-Item -ItemType Directory -Path $fixtureScripts | Out-Null
    foreach ($name in @('Build-AabAndSubmitToPlay.ps1', 'Submit-PlayMetadata.ps1', 'PlayMetadata.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $fixtureScripts
    }
    @'
$script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$script:ReleaseProject = [pscustomobject]@{ PackageName = 'com.example.game' }
function Resolve-Fastlane { return (Join-Path $PSScriptRoot 'fake-fastlane.ps1') }
function Get-ReleaseConfig { return [pscustomobject]@{ ServiceAccountJsonPath = 'example-key-not-read.json' } }
function Get-HighestPlayVersionCode { return 5 }
function Get-ProjectVersion { return [pscustomobject]@{ VersionCode = 3 } }
function Resolve-ReleaseVersionCode { param($HighestPlayVersion, $RequestedVersion, $ProjectVersion) return 6 }
function Invoke-UnityAndroidBuild { param($Format, $UnityPath, $VersionCode, $BuildRoot) return [pscustomobject]@{ ArtifactPath = 'example-not-uploaded.aab' } }
'@ | Set-Content (Join-Path $fixtureScripts 'ReleaseCommon.ps1') -Encoding UTF8
    @'
ConvertTo-Json -InputObject @($args) | Set-Content (Join-Path $PSScriptRoot 'arguments.json') -Encoding UTF8
$global:LASTEXITCODE = 0
'@ | Set-Content (Join-Path $fixtureScripts 'fake-fastlane.ps1') -Encoding UTF8
    $source | ConvertTo-Json -Depth 6 | Set-Content $inputFile -Encoding UTF8
    & (Join-Path $fixtureScripts 'Build-AabAndSubmitToPlay.ps1') -MetadataFile $inputFile -Track internal -ConfirmMetadata -ReleaseStatus draft -ChangesNotSentForReview 6>$null
    $cli = Get-Content (Join-Path $fixtureScripts 'arguments.json') -Raw | ConvertFrom-Json
    foreach ($option in @('--skip_upload_images', '--skip_upload_screenshots', '--changes_not_sent_for_review')) {
        if ($cli[[array]::IndexOf($cli, $option) + 1] -ne 'true') { throw "Unsafe CLI option: $option" }
    }
    if ($cli[[array]::IndexOf($cli, '--release_status') + 1] -ne 'draft') { throw 'Draft option was lost.' }
    & (Join-Path $fixtureScripts 'Submit-PlayMetadata.ps1') -MetadataFile $inputFile -VersionCode 42 -Track internal -ConfirmMetadata 6>$null
    $cli = Get-Content (Join-Path $fixtureScripts 'arguments.json') -Raw | ConvertFrom-Json
    foreach ($option in @('--skip_upload_aab', '--skip_upload_apk')) {
        if ($cli[[array]::IndexOf($cli, $option) + 1] -ne 'true') { throw "Metadata-only command could upload binaries: $option" }
    }
    if ($cli[[array]::IndexOf($cli, '--version_code') + 1] -ne '42') { throw 'Metadata-only command targets the wrong release.' }
    & (Join-Path $fixtureScripts 'Build-AabAndSubmitToPlay.ps1') -Track internal 6>$null
    $cli = Get-Content (Join-Path $fixtureScripts 'arguments.json') -Raw | ConvertFrom-Json
    foreach ($option in @('--skip_upload_metadata', '--skip_upload_changelogs')) {
        if ($cli[[array]::IndexOf($cli, $option) + 1] -ne 'true') { throw "Default upload unexpectedly changes texts: $option" }
    }
    Write-Output 'Passed: metadata validation (11 rejection cases), UTF-8 conversion, exact version notes, stale-file isolation, confirmation guards and 3 mocked CLI workflows. No network/build/upload.'
} finally {
    $script:ProjectRoot = $originalRoot
    $script:ReleaseProject.PackageName = $originalPackage
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
