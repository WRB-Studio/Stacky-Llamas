Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-build-tests-' + [guid]::NewGuid().ToString('N'))
$previousProtocol = $env:UNITY_RELEASE_PROTOCOL_VERSION
$previousPassword = $env:UNITY_RELEASE_KEYSTORE_PASSWORD
try {
    $script:ProjectRoot = $testDirectory
    $settingsDirectory = Join-Path $testDirectory 'ProjectSettings'
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    foreach ($newline in @("`n", "`r`n")) {
        [IO.File]::WriteAllText((Join-Path $settingsDirectory 'ProjectSettings.asset'),
            ((@('  bundleVersion: 1.7', '  AndroidBundleVersionCode: 7', '') -join $newline)))
        $version = Get-ProjectVersion
        if ($version.Version -ne '1.7' -or $version.VersionCode -ne 7) { throw 'Project version parsing failed for LF/CRLF.' }
    }
    Write-Output 'Passed: project version parsing with LF and CRLF.'
    $script:ReleaseProject = [pscustomobject]@{ PackageName = 'com.example.game'; ArtifactName = 'Game'; SecretsKey = 'BuildTest' }
    $editorDirectory = Join-Path $testDirectory 'Assets/Editor'
    New-Item -ItemType Directory -Path $editorDirectory -Force | Out-Null
    $helperPath = Join-Path $editorDirectory 'UnityAndroidBuild.cs'
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Editor/UnityAndroidBuild.cs') -Destination $helperPath
    $keystore = Join-Path $testDirectory 'dummy.keystore'
    [IO.File]::WriteAllText($keystore, 'not-a-real-keystore')
    function Get-ReleaseConfig {
        return [pscustomobject]@{
            KeystorePath = $keystore; KeyAlias = 'test'
            KeystorePassword = (ConvertTo-SecureString 'test-only' -AsPlainText -Force)
            KeyAliasPassword = (ConvertTo-SecureString 'test-only' -AsPlainText -Force)
        }
    }
    function Resolve-UnityEditor { param($UnityPath) return 'fake-unity-never-executed' }
    $script:Scenario = 'success'
    $script:StartCount = 0
    function Start-Process {
        param($FilePath, $ArgumentList, $WindowStyle, [switch]$Wait, [switch]$PassThru)
        $script:StartCount++
        if ($ArgumentList -notcontains '-buildTarget' -or $ArgumentList -notcontains 'Android' -or
            $WindowStyle -ne 'Hidden' -or $env:UNITY_RELEASE_PROTOCOL_VERSION -ne '1') { throw 'Invalid Unity invocation.' }
        if (Test-Path -LiteralPath $env:UNITY_RELEASE_BUILD_RESULT) { throw 'Stale receipt was not removed.' }
        if ($script:Scenario -eq 'process-failure') { return [pscustomobject]@{ ExitCode = 1 } }
        [IO.File]::WriteAllText($env:UNITY_RELEASE_BUILD_OUTPUT, 'simulated artifact')
        if ($script:Scenario -eq 'no-receipt') { return [pscustomobject]@{ ExitCode = 0 } }
        $receipt = @{
            packageName = $env:UNITY_RELEASE_PACKAGE_NAME; versionName = ('1.0.' + [char]0x03b2)
            versionCode = if ($env:UNITY_RELEASE_VERSION_CODE) { [int]$env:UNITY_RELEASE_VERSION_CODE } else { 7 }
            outputPath = $env:UNITY_RELEASE_BUILD_OUTPUT; protocolVersion = 1; format = $env:UNITY_RELEASE_BUILD_FORMAT
        }
        switch ($script:Scenario) {
            'wrong-package' { $receipt.packageName = 'com.other.game' }
            'wrong-version' { $receipt.versionCode = 999 }
            'wrong-format' { $receipt.format = 'zip' }
            'wrong-output' { $receipt.outputPath = 'other.apk' }
            'wrong-protocol' { $receipt.protocolVersion = 99 }
            'old-helper' { $receipt.Remove('protocolVersion') }
        }
        [IO.File]::WriteAllText($env:UNITY_RELEASE_BUILD_RESULT, ($receipt | ConvertTo-Json))
        return [pscustomobject]@{ ExitCode = 0 }
    }
    $env:UNITY_RELEASE_PROTOCOL_VERSION = 'original-protocol'
    $env:UNITY_RELEASE_KEYSTORE_PASSWORD = 'original-test-value'
    foreach ($format in @('apk', 'aab')) {
        $build = Invoke-UnityAndroidBuild -Format $format -VersionCode 42
        if ($build.Version.VersionCode -ne 42 -or $build.ArtifactPath -notlike "*.$format" -or
            $build.Version.Version -cne ('1.0.' + [char]0x03b2)) { throw 'Shared build returned wrong artifact/version or lost UTF-8 characters.' }
    }
    $build = Invoke-UnityAndroidBuild -Format APK
    if ($build.Version.VersionCode -ne 7 -or $build.ArtifactPath -notlike '*.apk') { throw 'Default Unity versioncode was lost.' }
    foreach ($scenario in @('process-failure', 'no-receipt', 'wrong-package', 'wrong-version', 'wrong-format', 'wrong-output', 'wrong-protocol', 'old-helper')) {
        $script:Scenario = $scenario
        $rejected = $false
        try { Invoke-UnityAndroidBuild -Format apk -VersionCode 42 | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw "Accepted invalid build: $scenario" }
        if ($env:UNITY_RELEASE_PROTOCOL_VERSION -ne 'original-protocol' -or $env:UNITY_RELEASE_KEYSTORE_PASSWORD -ne 'original-test-value') {
            throw 'Build environment was not restored after failure.'
        }
    }
    $startCount = $script:StartCount
    [IO.File]::WriteAllText($helperPath, 'public const int ProtocolVersion = 99;')
    $blocked = $false
    try { Invoke-UnityAndroidBuild -Format apk | Out-Null } catch { $blocked = $_.Exception.Message -like 'Unity build helper is incompatible.*' }
    if (-not $blocked -or $script:StartCount -ne $startCount) { throw 'Incompatible helper was not blocked before Unity launch.' }
    Write-Output 'Passed: shared APK/AAB build invocation, actual/default versioncodes, 8 failed/mismatched receipt cases, stale receipt removal, environment restoration and helper compatibility guard. Unity was simulated; no real build/upload.'
} finally {
    $env:UNITY_RELEASE_PROTOCOL_VERSION = $previousProtocol
    $env:UNITY_RELEASE_KEYSTORE_PASSWORD = $previousPassword
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $testDirectory)) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
