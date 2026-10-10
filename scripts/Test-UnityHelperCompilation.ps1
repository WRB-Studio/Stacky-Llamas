[CmdletBinding()]
param([Parameter(Mandatory)][string]$UnityPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$editor = Get-Item -LiteralPath $UnityPath
if ($editor.PSIsContainer -or $editor.Name -ine 'Unity.exe') { throw 'Specify the installed Editor/Unity.exe.' }
$data = Join-Path $editor.DirectoryName 'Data'
$compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
$runtime = Join-Path $data 'NetCoreRuntime/dotnet.exe'
$framework = Join-Path $data 'UnityReferenceAssemblies/unity-4.8-api'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $data 'Tools/Roslyn/csc.exe'
    $runtime = $null
    $framework = Join-Path $data 'MonoBleedingEdge/lib/mono/4.7.1-api'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf) -or -not (Test-Path -LiteralPath $framework -PathType Container)) {
    throw 'Unsupported Unity compiler layout. Compile the helper in a disposable Unity project with this Editor instead.'
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-compile-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    $outputFile = Join-Path $testDirectory 'UnityAndroidRelease.dll'
    $references = @(Get-ChildItem -LiteralPath $framework -Filter '*.dll')
    $references += @(Get-ChildItem -LiteralPath (Join-Path $framework 'Facades') -Filter '*.dll')
    $references += @(Get-ChildItem -LiteralPath (Join-Path $data 'Managed/UnityEngine') -Filter '*.dll')
    if (-not (Test-Path -LiteralPath (Join-Path $data 'Managed/UnityEngine/UnityEditor.CoreModule.dll'))) {
        $references += Get-Item -LiteralPath (Join-Path $data 'Managed/UnityEditor.dll')
    }
    $arguments = @('-nologo', '-target:library', '-nostdlib+', '-warnaserror+', ('-out:"' + $outputFile + '"'))
    if ($editor.VersionInfo.FileMajorPart -gt 2021 -or
        ($editor.VersionInfo.FileMajorPart -eq 2021 -and $editor.VersionInfo.FileMinorPart -ge 3)) {
        $arguments += '-define:UNITY_2021_3_OR_NEWER'
    }
    $arguments += @($references | ForEach-Object { '-r:"' + $_.FullName + '"' })
    $arguments += @(Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/Editor') -Filter '*.cs' |
        ForEach-Object { '"' + $_.FullName + '"' })
    $responseFile = Join-Path $testDirectory 'compile.rsp'
    [IO.File]::WriteAllLines($responseFile, $arguments)
    if ($runtime) { & $runtime $compiler ('@' + $responseFile) }
    else { & $compiler ('@' + $responseFile) }
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputFile -PathType Leaf)) { throw 'Unity helper API compilation failed.' }
    Write-Output "Passed: helper compiled against Unity $($editor.VersionInfo.ProductVersion) with warnings as errors. No Editor launch, Android build or upload."
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
