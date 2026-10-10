[CmdletBinding()]
param([Parameter(Mandatory)][string]$UnityPath, [ValidateRange(30, 600)][int]$TimeoutSeconds = 120)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$editor = Get-Item -LiteralPath $UnityPath
if ($editor.PSIsContainer -or $editor.Name -ine 'Unity.exe') { throw 'Specify the installed Editor/Unity.exe.' }
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-menu-registration-' + [guid]::NewGuid().ToString('N'))
$process = $null
try {
    New-Item -ItemType Directory -Path (Join-Path $testDirectory 'Assets/Editor'), (Join-Path $testDirectory 'Packages'), (Join-Path $testDirectory 'ProjectSettings') -Force | Out-Null
    Copy-Item -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Editor/*.cs') -Destination (Join-Path $testDirectory 'Assets/Editor')
    [IO.File]::WriteAllText((Join-Path $testDirectory 'Packages/manifest.json'), '{"dependencies":{}}')
    [IO.File]::WriteAllText((Join-Path $testDirectory 'ProjectSettings/ProjectVersion.txt'), ('m_EditorVersion: ' + $editor.VersionInfo.ProductVersion.Split('_')[0]))
    @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class UnityMenuRegistrationTest {
    public static void Run() {
        var attributes = typeof(UnityAndroidRelease.Editor.UnityAndroidReleaseMenu)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>()).ToArray();
        var commands = attributes.Where(item => !item.validate).Select(item => item.menuItem).ToArray();
        if (commands.Length != 18 || commands.Distinct().Count() != commands.Length || commands.Any(item => !item.StartsWith("Tools/Unity Android Release Tools/")))
            throw new Exception("Invalid tool menu declarations.");
        var inventory = typeof(Unsupported).GetMethod("GetSubmenus", BindingFlags.Public | BindingFlags.Static);
        if (inventory == null) throw new Exception("Menu inventory API unavailable for this Editor.");
        var registered = (string[])inventory.Invoke(null, new object[] { "Tools" });
        if (commands.Any(command => !registered.Contains(command))) throw new Exception("A declared menu action was not registered.");
        File.WriteAllText(Path.Combine(Application.dataPath, "../menu-check.txt"), string.Join("\n", commands));
        Debug.Log("MENU_REGISTRATION_PASSED: 18 actions registered. No action executed.");
    }
}
'@ | Set-Content -LiteralPath (Join-Path $testDirectory 'Assets/Editor/UnityMenuRegistrationTest.cs') -Encoding UTF8
    $log = Join-Path $testDirectory 'editor.log'
    $process = Start-Process -FilePath $editor.FullName -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $testDirectory + '"'),
        '-executeMethod', 'UnityMenuRegistrationTest.Run', '-logFile', ('"' + $log + '"')) -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Unity menu registration test timed out.' }
    }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $testDirectory 'menu-check.txt'))) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 30 | Write-Output }
        throw 'Unity menu registration failed. Check Editor/license availability and compiler diagnostics.'
    }
    Get-Content -LiteralPath (Join-Path $testDirectory 'menu-check.txt')
    Write-Output 'Passed: 18 menu actions registered in a disposable Unity Editor project. No menu action, Android build, installation or upload executed.'
} finally {
    if ($process) {
        if (-not $process.HasExited) { & taskkill.exe /PID $process.Id /T /F 2>&1 | Out-Null }
        $process.Dispose()
    }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $testDirectory)) {
        # Unity import workers may release their folder handles shortly after the Editor exits.
        for ($attempt = 0; $attempt -lt 8; $attempt++) {
            try { Remove-Item -LiteralPath $testDirectory -Recurse -Force; break }
            catch {
                if ($attempt -eq 7) { Write-Warning 'Unity menu test succeeded, but its temporary folder could not yet be removed.' }
                else { Start-Sleep -Milliseconds 500 }
            }
        }
    }
}
