Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$script:ReleaseProject = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release.config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($field in @('PackageName', 'ArtifactName', 'SecretsKey')) {
    if (-not $script:ReleaseProject.PSObject.Properties[$field] -or
        [string]::IsNullOrWhiteSpace($script:ReleaseProject.$field)) { throw "Missing release configuration field: $field" }
}
if ($script:ReleaseProject.PackageName -notmatch '^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$') { throw 'Invalid Android package name.' }
foreach ($field in @('ArtifactName', 'SecretsKey')) {
    if ($script:ReleaseProject.$field -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') { throw "Invalid release configuration field: $field" }
}
$script:ReleaseConfigPath = Join-Path $env:LOCALAPPDATA "UnityAndroidRelease\$($script:ReleaseProject.SecretsKey)\release-secrets.xml"
$script:DriveConfigPath = Join-Path (Split-Path -Parent $script:ReleaseConfigPath) 'drive-export.json'
$script:BuildProtocolVersion = 1

function ConvertFrom-SecureStringPlainText {
    param([Parameter(Mandatory)][Security.SecureString]$Value)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Get-ReleaseConfig {
    param([switch]$RequirePlay)

    if (-not (Test-Path -LiteralPath $script:ReleaseConfigPath)) {
        throw "Release configuration is missing. Run scripts\Set-ReleaseSecrets.ps1 once."
    }

    $config = Import-Clixml -LiteralPath $script:ReleaseConfigPath
    if ($RequirePlay -and (-not $config.PSObject.Properties['ServiceAccountJsonPath'] -or
        [string]::IsNullOrWhiteSpace($config.ServiceAccountJsonPath) -or
        -not (Test-Path -LiteralPath $config.ServiceAccountJsonPath -PathType Leaf))) {
        throw 'Google Play service-account file is missing. Configure ServiceAccountJsonPath with Set-ReleaseSecrets.ps1.'
    }
    return $config
}

function Assert-UnityBuildHelper {
    $helperPath = Join-Path $script:ProjectRoot 'Assets\Editor\UnityAndroidBuild.cs'
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
        throw 'Unity build helper is missing. Install Assets/Editor/UnityAndroidBuild.cs together with scripts/ from the same tool revision.'
    }
    $helper = Get-Content -LiteralPath $helperPath -Raw
    $protocol = [regex]::Match($helper, 'public const int ProtocolVersion = (\d+);')
    if (-not $protocol.Success -or [int]$protocol.Groups[1].Value -ne $script:BuildProtocolVersion) {
        throw 'Unity build helper is incompatible. Update scripts/ and Assets/Editor/UnityAndroidBuild.cs together; preserve release.config.json and the existing .meta file. See UPDATING.md.'
    }
}

function Resolve-UnityEditor {
    param([string]$UnityPath)

    $versionFile = Join-Path $script:ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    $unityVersion = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), 'm_EditorVersion: (.+)').Groups[1].Value.Trim()
    $candidate = if ($UnityPath) { $UnityPath } else { $env:UNITY_EDITOR_PATH }
    if (-not $candidate) {
        $installRoots = @("$env:ProgramFiles\Unity\Hub\Editor")
        $secondaryInstallPath = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
        if (Test-Path -LiteralPath $secondaryInstallPath) {
            $installRoots += Get-Content -LiteralPath $secondaryInstallPath -Raw | ConvertFrom-Json
        }

        $candidate = $installRoots |
            ForEach-Object { Join-Path $_ "$unityVersion\Editor\Unity.exe" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }
    if (-not $candidate) {
        throw "Unity $unityVersion was not found automatically. Run the script with -UnityPath 'C:\path\to\Unity.exe' or set UNITY_EDITOR_PATH."
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity executable not found: $candidate"
    }

    return (Resolve-Path -LiteralPath $candidate).Path
}

function Get-ProjectVersion {
    $settings = Get-Content -LiteralPath (Join-Path $script:ProjectRoot 'ProjectSettings\ProjectSettings.asset') -Raw
    $version = [regex]::Match($settings, '(?m)^  bundleVersion: (.+)$').Groups[1].Value.Trim()
    $versionCode = [regex]::Match($settings, '(?m)^  AndroidBundleVersionCode: (\d+)\r?$').Groups[1].Value
    if (-not $version -or -not $versionCode) {
        throw 'Android version information could not be read from ProjectSettings.asset.'
    }

    return [PSCustomObject]@{ Version = $version; VersionCode = [int]$versionCode }
}

function Resolve-ReleaseVersionCode {
    param([int]$HighestPlayVersion, [int]$RequestedVersion = 0, [int]$ProjectVersion = 1)
    if ($HighestPlayVersion -eq [int]::MaxValue) { throw 'Android version codes are exhausted.' }
    if ($RequestedVersion -gt 0) {
        if ($RequestedVersion -le $HighestPlayVersion) { throw "Versioncode must be greater than $HighestPlayVersion." }
        return $RequestedVersion
    }
    return [Math]::Max($ProjectVersion, $HighestPlayVersion + 1)
}

function Resolve-Fastlane {
    $command = Get-Command fastlane, fastlane.bat -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    $candidate = Get-ChildItem -Path 'C:\Ruby*\bin\fastlane.bat' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $candidate) { throw 'fastlane is missing. Install fastlane and restart PowerShell.' }
    return $candidate
}

function Get-HighestPlayVersionCode {
    $config = Get-ReleaseConfig -RequirePlay
    $ruby = Get-Command ruby -ErrorAction SilentlyContinue
    $rubyPath = if ($ruby) { $ruby.Source } else { Join-Path (Split-Path -Parent (Resolve-Fastlane)) 'ruby.exe' }
    $oldKey = $env:UNITY_RELEASE_PLAY_KEY
    $oldPackage = $env:UNITY_RELEASE_PACKAGE_NAME
    try {
        $env:UNITY_RELEASE_PLAY_KEY = $config.ServiceAccountJsonPath
        $env:UNITY_RELEASE_PACKAGE_NAME = $script:ReleaseProject.PackageName
        $result = & $rubyPath (Join-Path $PSScriptRoot 'GetPlayVersionCodes.rb')
        if ($LASTEXITCODE -ne 0) { throw 'Could not query Google Play version codes. No build or upload was started.' }
        return [int](($result -join "`n" | ConvertFrom-Json).HighestVersionCode)
    }
    finally {
        $env:UNITY_RELEASE_PLAY_KEY = $oldKey
        $env:UNITY_RELEASE_PACKAGE_NAME = $oldPackage
    }
}

function Invoke-UnityAndroidBuild {
    param(
        [Parameter(Mandatory)][ValidateSet('apk', 'aab')][string]$Format,
        [string]$UnityPath,
        [ValidateRange(0, [int]::MaxValue)][int]$VersionCode,
        [string]$BuildRoot
    )

    Assert-UnityBuildHelper
    $Format = $Format.ToLowerInvariant()
    $config = Get-ReleaseConfig
    foreach ($path in @($config.KeystorePath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required release file is missing: $path"
        }
    }

    $unity = Resolve-UnityEditor -UnityPath $UnityPath
    $extension = if ($Format -eq 'apk') { 'apk' } else { 'aab' }
    $artifactDirectory = Join-Path $script:ProjectRoot 'Builds\Android'
    $outputPath = Join-Path $artifactDirectory "$($script:ReleaseProject.ArtifactName).$extension"
    $logPath = Join-Path $artifactDirectory "unity-$Format.log"
    $receiptPath = Join-Path $artifactDirectory "build-$Format.json"
    $unityLockFile = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    if (-not $BuildRoot -and (Test-Path -LiteralPath $unityLockFile)) {
        throw 'This project is currently open in Unity. Save and close its Editor before starting an automated build.'
    }
    New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
    if (Test-Path -LiteralPath $receiptPath) { Remove-Item -LiteralPath $receiptPath }

    $previousEnvironment = @{}
    $cacheLease = $null
    $environmentValues = @{
        UNITY_RELEASE_PROTOCOL_VERSION = $script:BuildProtocolVersion.ToString()
        UNITY_RELEASE_BUILD_OUTPUT = $outputPath
        UNITY_RELEASE_BUILD_RESULT = $receiptPath
        UNITY_RELEASE_PACKAGE_NAME = $script:ReleaseProject.PackageName
        UNITY_RELEASE_BUILD_FORMAT = $Format
        UNITY_RELEASE_KEYSTORE_PATH = $config.KeystorePath
        UNITY_RELEASE_KEYSTORE_PASSWORD = ConvertFrom-SecureStringPlainText $config.KeystorePassword
        UNITY_RELEASE_KEY_ALIAS = $config.KeyAlias
        UNITY_RELEASE_KEY_ALIAS_PASSWORD = ConvertFrom-SecureStringPlainText $config.KeyAliasPassword
        UNITY_RELEASE_VERSION_CODE = if ($VersionCode) { $VersionCode.ToString() } else { '' }
    }

    try {
        $buildProject = $script:ProjectRoot
        if ($BuildRoot) {
            . (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
            Write-Host 'DEVICE_STAGE|1|Projekt für den isolierten Build synchronisieren'
            Assert-AndroidBuildDiskSpace -Paths @($BuildRoot, $artifactDirectory)
            $cacheLease = Sync-AndroidBuildCache -SourceRoot $script:ProjectRoot -BuildRoot $BuildRoot
            $buildProject = $cacheLease.Path
        }
        foreach ($name in $environmentValues.Keys) {
            $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $environmentValues[$name], 'Process')
        }

        $process = Start-Process -FilePath $unity -ArgumentList @(
            '-batchmode', '-nographics', '-quit',
            '-buildTarget', 'Android',
            '-projectPath', ('"' + $buildProject + '"'),
            '-executeMethod', 'UnityAndroidRelease.Editor.UnityAndroidBuild.BuildFromEnvironment',
            '-logFile', ('"' + $logPath + '"')
        ) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $receiptPath)) {
            throw "Unity build failed. See $logPath"
        }
        $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $receipt.PSObject.Properties['protocolVersion'] -or
            $receipt.protocolVersion -ne $script:BuildProtocolVersion -or
            -not $receipt.PSObject.Properties['format'] -or $receipt.format -cne $Format -or
            $receipt.packageName -cne $script:ReleaseProject.PackageName -or
            $receipt.versionCode -lt 1 -or [string]::IsNullOrWhiteSpace($receipt.versionName) -or
            ($VersionCode -and $receipt.versionCode -ne $VersionCode) -or
            $receipt.outputPath -ne $outputPath) { throw 'Build receipt does not match the requested release.' }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
        if ($cacheLease) { $cacheLease.Lock.Dispose() }
    }

    return [PSCustomObject]@{ ArtifactPath = $outputPath; Version = [PSCustomObject]@{ Version = $receipt.versionName; VersionCode = $receipt.versionCode } }
}
