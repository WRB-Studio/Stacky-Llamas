[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$AdbPath,
    [string]$DeviceSerial,
    [string]$BuildRoot,
    [switch]$Development,
    [switch]$ReleaseSigning,
    [switch]$ReplaceExistingApp,
    [switch]$CheckOnly,
    [switch]$NoLaunch,
    [ValidateRange(1, 200)][int]$MinimumFreeGB = 25,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 1800
)

. (Join-Path $PSScriptRoot 'ReleaseCommon.ps1')
. (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
$localSettingsPath = Join-Path $script:ProjectRoot 'Builds/Android/device-build.json'
if (-not (Test-Path -LiteralPath $localSettingsPath -PathType Leaf)) {
    $localSettingsPath = Join-Path (Split-Path -Parent $script:ReleaseConfigPath) 'device-build.json'
}
if (Test-Path -LiteralPath $localSettingsPath -PathType Leaf) {
    $localSettings = Get-Content -LiteralPath $localSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $UnityPath -and $localSettings.PSObject.Properties['UnityPath']) { $UnityPath = $localSettings.UnityPath }
    if (-not $AdbPath -and $localSettings.PSObject.Properties['AdbPath']) { $AdbPath = $localSettings.AdbPath }
    if (-not $BuildRoot -and $localSettings.PSObject.Properties['BuildRoot']) { $BuildRoot = $localSettings.BuildRoot }
}
Assert-UnityBuildHelper
$helper = Get-Content -LiteralPath (Join-Path $script:ProjectRoot 'Assets/Editor/UnityAndroidBuild.cs') -Raw
if ($helper -notmatch 'public const int DeviceBuildProtocolVersion = 1;') { throw 'Update the scripts and Unity helper together: the device build helper is missing.' }
$deviceTools = Resolve-AndroidDeviceTools -UnityPath $UnityPath -AdbPath $AdbPath
$unity = $deviceTools.Unity
$sdk = $deviceTools.Sdk
$AdbPath = $deviceTools.Adb
$aapt = Get-ChildItem -LiteralPath (Join-Path $sdk 'build-tools') -Filter aapt.exe -Recurse -File | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $aapt) { throw 'Android SDK aapt is missing; APK identity verification is required.' }
$discovery = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('devices')
$deviceLines = @($discovery.Output -split "`r?`n")
if ($discovery.ExitCode -ne 0) { throw 'ADB device discovery failed.' }
$selectedDevice = Get-AndroidDevice -Lines $deviceLines -DeviceSerial $DeviceSerial
$packageRead = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $selectedDevice, 'shell', 'dumpsys', 'package', $script:ReleaseProject.PackageName)
$installedText = $packageRead.Output
if ($packageRead.ExitCode -ne 0) { throw 'Could not read the installed Android package.' }
$installed = Get-InstalledAndroidVersion -PackageOutput $installedText
$original = Get-ProjectVersion
$code = Get-NextDeviceVersionCode -ProjectCode $original.VersionCode -InstalledCode $installed.Code
if (-not $BuildRoot) { $BuildRoot = Join-Path $env:LOCALAPPDATA "UnityAndroidRelease/$($script:ReleaseProject.SecretsKey)/BuildCache" }
$cache = Assert-AndroidBuildCache -SourceRoot $script:ProjectRoot -BuildRoot $BuildRoot
Write-Output ("Build cache volume: " + [IO.Path]::GetPathRoot($cache))
Assert-AndroidBuildDiskSpace -Paths @($cache, (Join-Path $script:ProjectRoot 'Builds/Android')) -MinimumFreeGB $MinimumFreeGB
$config = if ($ReleaseSigning) { Get-ReleaseConfig } else { $null }
if ($ReleaseSigning -and -not (Test-Path -LiteralPath $config.KeystorePath -PathType Leaf)) { throw 'Configured release keystore is missing.' }
if ($CheckOnly) {
    Write-Output "Ready: one authorized device; package $($script:ReleaseProject.PackageName); next Android code $code. No build, installation, deletion or upload."
    return
}

Write-Output 'DEVICE_STAGE|1|Handy geprüft · Projektdateien synchronisieren'
$cacheLease = Sync-AndroidBuildCache -SourceRoot $script:ProjectRoot -BuildRoot $cache
try {
    $attempt = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $resultRoot = Join-Path $script:ProjectRoot "Builds/Android/Device/$attempt"
    New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
    $output = Join-Path $resultRoot "$($script:ReleaseProject.ArtifactName)-v$($original.Version)-code$code.apk"
    $receiptPath = Join-Path $resultRoot 'build.json'
    $logPath = Join-Path $resultRoot 'unity.log'
    $environmentValues = @{
        UNITY_RELEASE_PROTOCOL_VERSION = $script:BuildProtocolVersion.ToString()
        UNITY_DEVICE_PROTOCOL_VERSION = '1'
        UNITY_DEVICE_OUTPUT = $output
        UNITY_DEVICE_RESULT = $receiptPath
        UNITY_DEVICE_PACKAGE = $script:ReleaseProject.PackageName
        UNITY_DEVICE_VERSION_CODE = $code.ToString()
        UNITY_DEVICE_DEVELOPMENT = $(if ($Development) { '1' } else { '0' })
        UNITY_DEVICE_SIGNING = $(if ($ReleaseSigning) { 'release' } else { 'debug' })
    }
    if ($ReleaseSigning) {
        $environmentValues.UNITY_RELEASE_KEYSTORE_PATH = $config.KeystorePath
        $environmentValues.UNITY_RELEASE_KEYSTORE_PASSWORD = ConvertFrom-SecureStringPlainText $config.KeystorePassword
        $environmentValues.UNITY_RELEASE_KEY_ALIAS = $config.KeyAlias
        $environmentValues.UNITY_RELEASE_KEY_ALIAS_PASSWORD = ConvertFrom-SecureStringPlainText $config.KeyAliasPassword
    }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $unity
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.Arguments = '-batchmode -nographics -quit -buildTarget Android -projectPath "' + $cache + '" -executeMethod UnityAndroidRelease.Editor.UnityAndroidBuild.BuildForDeviceFromEnvironment -logFile "' + $logPath + '"'
    foreach ($name in $environmentValues.Keys) { $start.EnvironmentVariables[$name] = $environmentValues[$name] }
    $tempDirectory = Join-Path $cache 'BuildTemp'
    New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null
    $start.EnvironmentVariables['TEMP'] = $tempDirectory
    $start.EnvironmentVariables['TMP'] = $tempDirectory
    $start.EnvironmentVariables['GRADLE_USER_HOME'] = if ($env:GRADLE_USER_HOME) { $env:GRADLE_USER_HOME } else { Join-Path $cache 'GradleCache' }
    $started = [DateTime]::UtcNow
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $process = $null
    try {
        Write-Output "Building one ARM64/IL2CPP APK, Android code $code; timeout $TimeoutSeconds seconds. No automatic retry or upload."
        Write-Output 'DEVICE_STAGE|2|Unity baut ARM64/IL2CPP · Import, Kompilierung und Android-Paket'
        $process = [Diagnostics.Process]::Start($start)
        $nextStatus = [DateTime]::UtcNow
        $buildStage = ""
        while (-not $process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -ge $deadline) { throw 'Android build timed out; no installation started.' }
            if ([DateTime]::UtcNow -ge $nextStatus -and (Test-Path -LiteralPath $logPath)) {
                $nextStatus = [DateTime]::UtcNow.AddSeconds(5)
                # Publish fixed labels only, never raw log lines or environment/signing data.
                $tail = (Get-Content -LiteralPath $logPath -Tail 60 -ErrorAction SilentlyContinue) -join "`n"
                $stage = if ($tail -match 'Gradle|:launcher:|:unityLibrary:') { 'Android-Paket wird mit Gradle erstellt' }
                    elseif ($tail -match 'il2cpp|IL2CPP|libil2cpp') { 'IL2CPP: nativen ARM64-Code kompilieren' }
                    elseif ($tail -match 'ScriptCompilation|Csc|Compiling scripts') { 'Unity: C#-Skripte kompilieren' }
                    elseif ($tail -match 'Importing|AssetImport|RefreshInfo') { 'Unity: Projektdateien importieren' }
                    else { $buildStage }
                if ($stage -and $stage -ne $buildStage) {
                    $buildStage = $stage
                    Write-Output "DEVICE_STAGE|2|$stage"
                }
            }
        }
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output -PathType Leaf) -or -not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw "Android build failed. Local log: $logPath" }
    } finally {
        if ($process) {
            if (-not $process.HasExited) { & taskkill.exe /PID $process.Id /T /F 2>&1 | Out-Null }
            $process.Dispose()
        }
        foreach ($name in $environmentValues.Keys) { $start.EnvironmentVariables.Remove($name) }
        $environmentValues.Clear()
    }
    Write-Output 'DEVICE_STAGE|3|APK erstellt · Paket und Version prüfen'
    $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($receipt.protocolVersion -ne 1 -or $receipt.packageName -cne $script:ReleaseProject.PackageName -or
        $receipt.versionName -cne $original.Version -or $receipt.versionCode -ne $code -or $receipt.outputPath -ne $output -or
        $receipt.format -cne 'apk') { throw 'The fresh build receipt does not match the requested app/version.' }
    $manifestRead = Invoke-AndroidCommand -Executable $aapt.FullName -NativeArgs @('dump', 'badging', $output)
    $badging = $manifestRead.Output
    if ($manifestRead.ExitCode -ne 0 -or $badging -notmatch "package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'" -or
        $matches[1] -cne $script:ReleaseProject.PackageName -or [int]$matches[2] -ne $code -or $matches[3] -cne $original.Version) { throw 'APK manifest does not match the fresh build receipt; installation blocked.' }
    $launcher = [regex]::Match($badging, "(?m)^launchable-activity: name='([^']+)'")
    $current = Get-ProjectVersion
    if ($current.Version -cne $original.Version -or $current.VersionCode -ne $original.VersionCode) { throw 'Project version changed during the build; no source overwrite or installation.' }
    Write-Output 'DEVICE_STAGE|4|APK auf dem Handy installieren'
    $installation = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $selectedDevice, 'install', '-r', $output)
    $installText = $installation.Output
    if ($installation.ExitCode -ne 0 -or $installText -notmatch '(?m)^Success\s*$') {
        if ($ReplaceExistingApp -and $installText.Contains('INSTALL_FAILED_UPDATE_INCOMPATIBLE')) {
            # Explicit opt-in only: remove exactly the verified target package, including its app data.
            $removal = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $selectedDevice, 'uninstall', $script:ReleaseProject.PackageName)
            $removed = $removal.Output
            if ($removal.ExitCode -ne 0 -or $removed -notmatch '(?m)^Success\s*$') { throw 'Removing the incompatible target app failed.' }
            $installation = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $selectedDevice, 'install', $output)
            $installText = $installation.Output
            if ($installation.ExitCode -ne 0 -or $installText -notmatch '(?m)^Success\s*$') { throw 'Installation after the authorized signature replacement failed.' }
        } else { throw 'Installation failed. For a signature mismatch use -ReplaceExistingApp only if deleting the target app data is intended. APK and local logs are retained.' }
    }
    Write-Output 'DEVICE_STAGE|5|Installierte Version auf dem Handy prüfen'
    $packageRead = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $selectedDevice, 'shell', 'dumpsys', 'package', $script:ReleaseProject.PackageName)
    $verifiedText = $packageRead.Output
    if ($packageRead.ExitCode -ne 0) { throw 'Could not verify the installed package.' }
    $verified = Get-InstalledAndroidVersion -PackageOutput $verifiedText
    if ($verified.Code -ne $code -or $verified.Name -cne $original.Version) { throw 'Installed version does not match the built APK.' }
    $result = [PSCustomObject]@{ Status = 'installed'; Package = $receipt.packageName; Version = $receipt.versionName;
        VersionCode = $code; BuildId = $receipt.buildId; Sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant();
        FinishedUtc = [DateTime]::UtcNow.ToString('O'); GameplayVerified = $false;
        LaunchRequested = -not $NoLaunch; Launched = $false; LaunchError = '' }
    Write-Output 'DEVICE_STAGE|6|Installation bestätigt · App starten und Ergebnis speichern'
    if (-not $NoLaunch) {
        try {
            if (-not $launcher.Success) { throw 'Launcher activity unavailable.' }
            $result.Launched = Start-InstalledAndroidApp -AdbPath $AdbPath -DeviceSerial $selectedDevice -PackageName $receipt.packageName -ActivityName $launcher.Groups[1].Value
            if (-not $result.Launched) { throw 'Android did not confirm application launch.' }
            Write-Output 'App launch: started'
        } catch {
            $result.LaunchError = 'launch_failed'
            Write-Warning 'Installation and version verification succeeded, but the app could not be started. Open it on the phone. No rebuild is needed.'
        }
    }
    [IO.File]::WriteAllText((Join-Path $resultRoot 'installation.json'), ($result | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    Write-Output 'DEVICE_STAGE|7|Fertig'
    Write-Output "Installed and verified: $($receipt.packageName), version $($receipt.versionName), Android code $code. Result: $resultRoot"
    if (-not $NoLaunch -and -not $result.Launched) { throw 'APK installed and verified, but app launch failed. See installation.json; no rebuild or uninstall is needed.' }
} finally { $cacheLease.Lock.Dispose() }
