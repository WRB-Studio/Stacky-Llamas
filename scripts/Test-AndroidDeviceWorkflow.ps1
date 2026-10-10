Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-device-workflow-' + [guid]::NewGuid().ToString('N'))
$oldScenario = $env:UNITY_RELEASE_TEST_SCENARIO
$oldState = $env:UNITY_RELEASE_TEST_STATE
$oldLog = $env:UNITY_RELEASE_TEST_LOG
$oldLocalAppData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $testDirectory 'local-settings'
    $project = Join-Path $testDirectory 'project'
    $fixtureScripts = Join-Path $project 'scripts'
    $sdk = Join-Path $project 'Data/PlaybackEngines/AndroidPlayer/SDK'
    $aaptFolder = Join-Path $sdk 'build-tools/35.0.0'
    $adbFolder = Join-Path $sdk 'platform-tools'
    New-Item -ItemType Directory -Path $fixtureScripts, $aaptFolder, $adbFolder, (Join-Path $project 'Assets/Editor'), (Join-Path $project 'Packages'), (Join-Path $project 'ProjectSettings') -Force | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot '*.ps1') -Destination $fixtureScripts
    # Tiny simulated artifacts do not need the real Android toolchain's disk budget.
    'function Assert-AndroidBuildDiskSpace { param($Paths, $MinimumFreeGB) }' | Add-Content -LiteralPath (Join-Path $fixtureScripts 'AndroidDeviceCommon.ps1') -Encoding UTF8
    Copy-Item -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Editor/*.cs') -Destination (Join-Path $project 'Assets/Editor')
    @{ PackageName = 'com.example.game'; ArtifactName = 'Game'; SecretsKey = 'DeviceFixture' } | ConvertTo-Json | Set-Content (Join-Path $fixtureScripts 'release.config.json') -Encoding UTF8
    $settingsPath = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
    [IO.File]::WriteAllText($settingsPath, "  bundleVersion: 1.2.3`n  AndroidBundleVersionCode: 3`n")
    'm_EditorVersion: fixture' | Set-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt')
    $sourceHash = (Get-FileHash -LiteralPath $settingsPath).Hash
    $manifestPath = Join-Path $project 'Packages/manifest.json'
    New-Item -ItemType Directory -Path (Join-Path $project 'Packages/com.example.embedded'), (Join-Path $testDirectory 'shared-package') | Out-Null
    @{ dependencies = @{ embedded = 'file:com.example.embedded'; external = 'file:../../shared-package' } } | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $manifestHash = (Get-FileHash -LiteralPath $manifestPath).Hash
    . (Join-Path $PSScriptRoot 'AndroidDeviceCommon.ps1')
    $guardCache = Join-Path $testDirectory 'guard-cache'
    $lease = Sync-AndroidBuildCache -SourceRoot $project -BuildRoot $guardCache
    try {
        $cachedManifest = Get-Content -LiteralPath (Join-Path $guardCache 'Packages/manifest.json') -Raw | ConvertFrom-Json
        if ($cachedManifest.dependencies.embedded -ne ('file:' + (Join-Path $guardCache 'Packages/com.example.embedded').Replace('\', '/')) -or
            $cachedManifest.dependencies.external -ne ('file:' + (Join-Path $testDirectory 'shared-package').Replace('\', '/'))) { throw 'Cache broke local package paths.' }
        $blocked = $false
        try { Sync-AndroidBuildCache -SourceRoot $project -BuildRoot $guardCache | Out-Null } catch { $blocked = $true }
        if (-not $blocked) { throw 'Concurrent cache synchronization was allowed.' }
    } finally { $lease.Lock.Dispose() }
    $foreign = Join-Path $testDirectory 'foreign-cache'
    New-Item -ItemType Directory -Path $foreign | Out-Null
    [IO.File]::WriteAllText((Join-Path $foreign 'keep.txt'), 'must remain')
    $blocked = $false
    try { Sync-AndroidBuildCache -SourceRoot $project -BuildRoot $foreign | Out-Null } catch { $blocked = $true }
    if (-not $blocked -or [IO.File]::ReadAllText((Join-Path $foreign 'keep.txt')) -ne 'must remain') { throw 'Cache accepted or modified an unrelated folder.' }
    $toolSource = Join-Path $testDirectory 'FakeAndroid.cs'
    @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
public class FakeAndroid {
  static JavaScriptSerializer json = new JavaScriptSerializer();
  public static int Main(string[] args) {
    string scenario = Environment.GetEnvironmentVariable("UNITY_RELEASE_TEST_SCENARIO");
    string state = Environment.GetEnvironmentVariable("UNITY_RELEASE_TEST_STATE");
    File.AppendAllText(Environment.GetEnvironmentVariable("UNITY_RELEASE_TEST_LOG"), Path.GetFileName(Environment.GetCommandLineArgs()[0]) + " " + string.Join(" ", args) + "\n");
    string tool = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
    if (tool == "Unity") {
      if (scenario == "build-failure") return 1;
      bool device = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNITY_DEVICE_OUTPUT"));
      string output = Environment.GetEnvironmentVariable(device ? "UNITY_DEVICE_OUTPUT" : "UNITY_RELEASE_BUILD_OUTPUT");
      string codeText = Environment.GetEnvironmentVariable(device ? "UNITY_DEVICE_VERSION_CODE" : "UNITY_RELEASE_VERSION_CODE");
      var receipt = new Dictionary<string,object> { {"packageName", scenario == "wrong-receipt" ? "com.other.game" : Environment.GetEnvironmentVariable(device ? "UNITY_DEVICE_PACKAGE" : "UNITY_RELEASE_PACKAGE_NAME")},
        {"versionName", "1.2.3"}, {"versionCode", string.IsNullOrEmpty(codeText) ? 3 : int.Parse(codeText)},
        {"protocolVersion", 1}, {"format", device ? "apk" : Environment.GetEnvironmentVariable("UNITY_RELEASE_BUILD_FORMAT")}, {"buildId", "fixture-build"}, {"outputPath", output} };
      File.WriteAllText(output, json.Serialize(receipt));
      File.WriteAllText(Environment.GetEnvironmentVariable(device ? "UNITY_DEVICE_RESULT" : "UNITY_RELEASE_BUILD_RESULT"), json.Serialize(receipt)); return 0;
    }
    if (tool == "aapt") {
      var apk = json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[args.Length-1]));
      Console.WriteLine("package: name='" + (scenario == "wrong-manifest" ? "com.other.game" : apk["packageName"]) + "' versionCode='" + apk["versionCode"] + "' versionName='" + apk["versionName"] + "'");
      Console.WriteLine("launchable-activity: name='com.example.game.MainActivity'"); return 0;
    }
    if (args[0] == "devices") { Console.WriteLine("List of devices attached\nfixture-device " + (scenario == "unauthorized" ? "unauthorized" : "device")); return 0; }
    if (Array.IndexOf(args, "uninstall") >= 0) { File.WriteAllText(state + ".removed", "1"); Console.WriteLine("Success"); return 0; }
    if (Array.IndexOf(args, "install") >= 0) {
      if (scenario == "signature-mismatch" && !File.Exists(state + ".removed")) { Console.WriteLine("INSTALL_FAILED_UPDATE_INCOMPATIBLE"); return 1; }
      File.Copy(args[args.Length-1], state, true); Console.WriteLine("Success"); return 0;
    }
    if (Array.IndexOf(args, "dumpsys") >= 0) {
      int code = 4; string version = "1.2.3";
      if (File.Exists(state)) { var apk = json.Deserialize<Dictionary<string,object>>(File.ReadAllText(state)); code = Convert.ToInt32(apk["versionCode"]); version = (string)apk["versionName"]; }
      Console.WriteLine("versionCode=" + code + " minSdk=25\nversionName=" + version); return 0;
    }
    if (Array.IndexOf(args, "am") >= 0) { Console.WriteLine(scenario == "launch-failure" ? "Error: unable to start activity" : "Status: ok"); return scenario == "launch-failure" ? 1 : 0; }
    return 1;
  }
}
'@ | Set-Content -LiteralPath $toolSource -Encoding UTF8
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $fakeUnity = Join-Path $project 'Unity.exe'
    & $compiler /nologo /target:exe /reference:System.Web.Extensions.dll "/out:$fakeUnity" $toolSource
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile isolated Android workflow fixture.' }
    Copy-Item -LiteralPath $fakeUnity -Destination (Join-Path $adbFolder 'adb.exe')
    Copy-Item -LiteralPath $fakeUnity -Destination (Join-Path $aaptFolder 'aapt.exe')
    $env:UNITY_RELEASE_TEST_STATE = Join-Path $testDirectory 'installed.json'
    $env:UNITY_RELEASE_TEST_LOG = Join-Path $testDirectory 'commands.log'
    $env:UNITY_RELEASE_TEST_SCENARIO = 'success'
    $secretFolder = Join-Path $env:LOCALAPPDATA 'UnityAndroidRelease/DeviceFixture'
    New-Item -ItemType Directory -Path $secretFolder, (Join-Path $project 'Temp') -Force | Out-Null
    $fixtureKeystore = Join-Path $testDirectory 'fixture.keystore'
    [IO.File]::WriteAllText($fixtureKeystore, 'not-a-real-keystore')
    [PSCustomObject]@{ KeystorePath = $fixtureKeystore; KeyAlias = 'fixture';
        KeystorePassword = (ConvertTo-SecureString 'fixture-only' -AsPlainText -Force);
        KeyAliasPassword = (ConvertTo-SecureString 'fixture-only' -AsPlainText -Force) } | Export-Clixml -LiteralPath (Join-Path $secretFolder 'release-secrets.xml')
    [IO.File]::WriteAllText((Join-Path $project 'Temp/UnityLockfile'), 'simulated open source Editor')
    foreach ($format in @('apk', 'aab')) {
        $build = & (Join-Path $fixtureScripts "Build-$format.ps1") -UnityPath $fakeUnity -VersionCode 9 -BuildRoot (Join-Path $testDirectory "release-cache-$format") 6>$null
        if ($build.Version.VersionCode -ne 9 -or $build.ArtifactPath -notlike "*.$format" -or
            (Get-FileHash -LiteralPath $settingsPath).Hash -ne $sourceHash) { throw 'Isolated release build failed with an open source Editor.' }
    }
    $command = Join-Path $fixtureScripts 'Build-ApkAndInstall.ps1'
    function Run-DeviceCase {
        param([string]$Scenario, [hashtable]$Extra = @{}, [switch]$ExpectFailure)
        $env:UNITY_RELEASE_TEST_SCENARIO = $Scenario
        foreach ($path in @($env:UNITY_RELEASE_TEST_STATE, ($env:UNITY_RELEASE_TEST_STATE + '.removed'), $env:UNITY_RELEASE_TEST_LOG)) {
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
        $failed = $false
        try { & $command -UnityPath $fakeUnity -BuildRoot (Join-Path $testDirectory ('cache-' + [guid]::NewGuid().ToString('N'))) -MinimumFreeGB 1 @Extra *> $null }
        catch { $failed = $true }
        if ($failed -ne $ExpectFailure.IsPresent) { throw "Unexpected device workflow outcome: $Scenario" }
        if ((Get-FileHash -LiteralPath $settingsPath).Hash -ne $sourceHash) { throw 'Device workflow changed source project settings.' }
        if ((Get-FileHash -LiteralPath $manifestPath).Hash -ne $manifestHash) { throw 'Device workflow changed source package dependencies.' }
        return [IO.File]::ReadAllText($env:UNITY_RELEASE_TEST_LOG)
    }
    $trace = Run-DeviceCase -Scenario success -Extra @{ CheckOnly = $true }
    if ($trace -match 'Unity.exe| install | am ') { throw 'CheckOnly built, installed or launched an app.' }
    $trace = Run-DeviceCase -Scenario success
    if ($trace -notmatch ' install -r ' -or $trace -notmatch ' am start -W ' -or $trace -match ' uninstall ') { throw 'Successful workflow did not install and launch safely.' }
    $receipt = Get-ChildItem -Path (Join-Path $project 'Builds/Android/Device/*/installation.json') | Select-Object -Last 1 | Get-Content -Raw | ConvertFrom-Json
    if (-not $receipt.Launched -or $receipt.GameplayVerified -or $receipt.VersionCode -ne 5) { throw 'Installation receipt is incorrect.' }
    $trace = Run-DeviceCase -Scenario success -Extra @{ NoLaunch = $true }
    if ($trace -match ' am ') { throw 'NoLaunch started the app.' }
    foreach ($scenario in @('unauthorized', 'build-failure', 'wrong-receipt', 'wrong-manifest')) {
        $trace = Run-DeviceCase -Scenario $scenario -ExpectFailure
        if ($trace -match ' install | uninstall | am ') { throw "Failed preflight/build reached installation: $scenario" }
    }
    $trace = Run-DeviceCase -Scenario signature-mismatch -ExpectFailure
    if ($trace -match ' uninstall | am ') { throw 'Signature mismatch silently deleted or launched the app.' }
    $trace = Run-DeviceCase -Scenario signature-mismatch -Extra @{ ReplaceExistingApp = $true }
    if ($trace -notmatch ' uninstall com.example.game' -or $trace -notmatch ' am start ') { throw 'Explicit replacement failed.' }
    $trace = Run-DeviceCase -Scenario launch-failure -ExpectFailure
    if ($trace -match ' uninstall ') { throw 'Launch failure deleted the app.' }
    $results = @(Get-ChildItem -Path (Join-Path $project 'Builds/Android/Device/*/installation.json') | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    if (-not @($results | Where-Object { $_.LaunchError -eq 'launch_failed' -and -not $_.Launched }).Count) { throw 'Launch failure was not recorded.' }
    Write-Output 'Passed: isolated simulated build/install/start workflow, CheckOnly, NoLaunch, authorization/receipt/manifest guards, explicit replacement, source settings preservation and launch failure receipt. No real Unity, ADB device or upload.'
} finally {
    $env:UNITY_RELEASE_TEST_SCENARIO = $oldScenario; $env:UNITY_RELEASE_TEST_STATE = $oldState; $env:UNITY_RELEASE_TEST_LOG = $oldLog; $env:LOCALAPPDATA = $oldLocalAppData
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $testDirectory)) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
