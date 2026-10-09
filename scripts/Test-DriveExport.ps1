Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Message = '*')
    $rejected = $false
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notlike $Message) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Expected rejection: $Message" }
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-drive-tests-' + [guid]::NewGuid().ToString('N'))
$previousLocalAppData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $testDirectory 'local-settings'
    $fixtureScripts = Join-Path $testDirectory 'scripts'
    $fixtureEditor = Join-Path $testDirectory 'Assets/Editor'
    $drive = Join-Path $testDirectory 'local destination [test]'
    $overrideDrive = Join-Path $testDirectory 'override destination'
    New-Item -ItemType Directory -Path $fixtureScripts, $fixtureEditor, $drive, $overrideDrive -Force | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot '*.ps1') -Destination $fixtureScripts
    @{ PackageName = 'com.example.game'; ArtifactName = 'Game'; SecretsKey = 'DriveTest' } |
        ConvertTo-Json | Set-Content (Join-Path $fixtureScripts 'release.config.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Editor/UnityAndroidBuild.cs') -Destination $fixtureEditor

    # Keep the real configuration/export code; replace only the build and Play boundaries.
    @'
function Invoke-UnityAndroidBuild {
    param($Format, $UnityPath, $VersionCode)
    $artifact = Join-Path $script:ProjectRoot "fixture.$Format"
    [IO.File]::WriteAllText($artifact, "simulated signed $Format")
    @{ Format = $Format; UnityPath = $UnityPath; VersionCode = $VersionCode } |
        ConvertTo-Json | Set-Content (Join-Path $script:ProjectRoot 'build-called.json') -Encoding UTF8
    return [pscustomobject]@{ ArtifactPath = $artifact; Version = [pscustomobject]@{
        Version = '1.2.3'; VersionCode = if ($VersionCode) { $VersionCode } else { 17 }
    } }
}
function Resolve-Fastlane { throw 'Unexpected Fastlane access.' }
function Get-HighestPlayVersionCode { throw 'Unexpected Play access.' }
'@ | Add-Content (Join-Path $fixtureScripts 'ReleaseCommon.ps1') -Encoding UTF8
    . (Join-Path $fixtureScripts 'ReleaseCommon.ps1')
    . (Join-Path $fixtureScripts 'DriveExport.ps1')
    $command = Join-Path $fixtureScripts 'Build-AndroidAndExportToDrive.ps1'
    $marker = Join-Path $testDirectory 'build-called.json'
    Assert-Rejected { & $command } '*No local Drive export directory*'
    Assert-Rejected { & $command -DriveDirectory (Join-Path $testDirectory 'missing') } '*does not exist*'
    Assert-Rejected { & $command -DriveDirectory '.' } '*absolute Windows filesystem path*'
    if (Test-Path -LiteralPath $marker) { throw 'Invalid destination started a build.' }

    & (Join-Path $fixtureScripts 'Set-DriveExportDirectory.ps1') -DriveDirectory $drive 6>$null
    if ((Resolve-DriveExportDirectory) -ne $drive) { throw 'Local destination was not persisted.' }
    $storedProject = Get-Content (Join-Path $fixtureScripts 'release.config.json') -Raw | ConvertFrom-Json
    if ($storedProject.PSObject.Properties['DriveDirectory']) { throw 'Stored a private path in project configuration.' }
    $script:DriveConfigPath = Join-Path $env:LOCALAPPDATA 'UnityAndroidRelease/AnotherProject/drive-export.json'
    Assert-Rejected { Resolve-DriveExportDirectory } '*No local Drive export directory*'
    . (Join-Path $fixtureScripts 'ReleaseCommon.ps1')
    & $command -CheckOnly 6>$null
    if ((Test-Path -LiteralPath $marker) -or @(Get-ChildItem -LiteralPath $drive).Count) { throw 'CheckOnly built or copied files.' }

    $apk = & $command -UnityPath 'unused-test-editor' 6>$null
    if (-not $apk.LocalCopySucceeded -or $apk.CloudUploadConfirmed -or
        (Split-Path -Leaf $apk.DestinationPath) -ne 'Game-1.2.3-17.apk' -or
        $apk.Sha256 -ne (Get-FileHash -LiteralPath $apk.ArtifactPath -Algorithm SHA256).Hash) { throw 'APK export result is incorrect.' }
    $aab = & $command -Format AAB -VersionCode 42 -DriveDirectory $overrideDrive -UnityPath 'unused-test-editor' 6>$null
    $call = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ((Split-Path -Leaf $aab.DestinationPath) -ne 'Game-1.2.3-42.aab' -or
        $call.UnityPath -ne 'unused-test-editor' -or $call.VersionCode -ne 42 -or
        (Resolve-DriveExportDirectory) -ne $drive -or $aab.CloudUploadConfirmed) { throw 'AAB override/parameter forwarding failed.' }

    [IO.File]::WriteAllText($apk.DestinationPath, 'existing export')
    Assert-Rejected { & $command } '*already exists*'
    if ([IO.File]::ReadAllText($apk.DestinationPath) -ne 'existing export') { throw 'Replaced an export without Force.' }
    $replaced = & $command -Force 6>$null
    if ((Get-FileHash -LiteralPath $replaced.DestinationPath).Hash -ne $replaced.Sha256) { throw 'Force did not replace the local copy.' }
    $legacy = & (Join-Path $fixtureScripts 'Build-ApkAndUploadToDrive.ps1') -VersionCode 43 -DriveDirectory $overrideDrive 3>$null 6>$null
    if ((Split-Path -Leaf $legacy.DestinationPath) -ne 'Game-1.2.3-43.apk' -or $legacy.CloudUploadConfirmed) { throw 'Legacy wrapper failed.' }

    $badBuild = [pscustomobject]@{ ArtifactPath = $apk.ArtifactPath; Version = [pscustomobject]@{ Version = '../escape'; VersionCode = 18 } }
    Assert-Rejected { Export-AndroidBuildToDriveDirectory -Build $badBuild -Format apk -DriveDirectory $drive } '*export filename*'
    $badBuild.Version.Version = '1.2.3'
    Assert-Rejected { Export-AndroidBuildToDriveDirectory -Build $badBuild -Format aab -DriveDirectory $drive } '*does not match*'
    $badBuild.ArtifactPath = Join-Path $testDirectory 'missing.apk'
    Assert-Rejected { Export-AndroidBuildToDriveDirectory -Build $badBuild -Format apk -DriveDirectory $drive } '*missing*'
    Assert-Rejected { & $command -VersionCode -1 }
    Assert-Rejected { & $command -Format zip }

    '{"DriveDirectory":42}' | Set-Content -LiteralPath $script:DriveConfigPath -Encoding UTF8
    Assert-Rejected { & $command -CheckOnly } '*Invalid local Drive configuration*'
    & $command -CheckOnly -DriveDirectory $drive 6>$null
    Remove-Item -LiteralPath $script:DriveConfigPath
    $storedProject | Add-Member -NotePropertyName DriveDirectory -NotePropertyValue $drive
    $storedProject | ConvertTo-Json | Set-Content (Join-Path $fixtureScripts 'release.config.json') -Encoding UTF8
    Assert-Rejected { & $command -CheckOnly } '*Move any old DriveDirectory value*'

    # Signing-only setup needs no Play key and must preserve an existing Play path.
    function Read-Host { param($Prompt, [switch]$AsSecureString) return (ConvertTo-SecureString 'test-only' -AsPlainText -Force) }
    $keystore = Join-Path $testDirectory 'dummy.keystore'
    [IO.File]::WriteAllText($keystore, 'not-a-real-keystore')
    & (Join-Path $fixtureScripts 'Set-ReleaseSecrets.ps1') -KeystorePath $keystore -KeyAlias 'test' 6>$null
    $signing = Get-ReleaseConfig
    if ($signing.ServiceAccountJsonPath -or $signing.KeystorePassword -isnot [Security.SecureString]) { throw 'Signing-only setup failed.' }
    Assert-Rejected { Get-ReleaseConfig -RequirePlay } '*Google Play service-account file is missing*'
    $playKey = Join-Path $testDirectory 'dummy-play.json'
    '{}' | Set-Content -LiteralPath $playKey
    & (Join-Path $fixtureScripts 'Set-ReleaseSecrets.ps1') -KeystorePath $keystore -KeyAlias 'test' -ServiceAccountJsonPath $playKey 6>$null
    & (Join-Path $fixtureScripts 'Set-ReleaseSecrets.ps1') -KeystorePath $keystore -KeyAlias 'test' 6>$null
    if ((Get-ReleaseConfig -RequirePlay).ServiceAccountJsonPath -ne $playKey) { throw 'Signing setup lost the existing Play key path.' }

    Write-Output 'Passed: APK/AAB local exports, local configuration isolation/override, CheckOnly, legacy wrapper, copy hashes, overwrite guard, invalid input and signing-only setup. Only temporary fixture files copied; no Unity/network/cloud upload.'
} finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $testDirectory)) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
