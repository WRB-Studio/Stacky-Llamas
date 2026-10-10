Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('unity-menu-tests-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Invoke-UnityMenuCommand.ps1') -Destination $testDirectory
    @'
param([string]$UnityPath, [string]$BuildRoot, [switch]$CheckOnly)
if (-not $CheckOnly -or $BuildRoot -cne "folder with 'quotes' and spaces") { throw 'Menu arguments changed.' }
Write-Output 'Fixture menu action completed.'
'@ | Set-Content -LiteralPath (Join-Path $testDirectory 'Build-Apk.ps1') -Encoding UTF8
    $request = Join-Path $testDirectory 'request.json'
    @{ ScriptName = 'Build-Apk.ps1'; Parameters = @{ CheckOnly = $true; BuildRoot = "folder with 'quotes' and spaces" } } |
        ConvertTo-Json | Set-Content -LiteralPath $request -Encoding UTF8
    foreach ($shell in @((Get-Process -Id $PID).Path, (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe')) | Select-Object -Unique) {
        $result = & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $testDirectory 'Invoke-UnityMenuCommand.ps1') -RequestFile $request
        if ($LASTEXITCODE -ne 0 -or $result -notcontains 'Fixture menu action completed.') { throw 'Menu request dispatch failed.' }
        '{"ScriptName":"../unknown.ps1","Parameters":{}}' | Set-Content -LiteralPath $request -Encoding UTF8
        $start = New-Object Diagnostics.ProcessStartInfo
        $start.FileName = $shell; $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $testDirectory 'Invoke-UnityMenuCommand.ps1') + '" -RequestFile "' + $request + '"'
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($start)
        try {
            $errors = $process.StandardError.ReadToEnd(); $process.WaitForExit()
            if ($process.ExitCode -ne 1 -or $errors -notmatch 'Unknown Unity menu command') { throw 'Menu command whitelist did not block an unknown script.' }
        } finally { $process.Dispose() }
        @{ ScriptName = 'Build-Apk.ps1'; Parameters = @{ CheckOnly = $true; BuildRoot = "folder with 'quotes' and spaces" } } | ConvertTo-Json | Set-Content -LiteralPath $request -Encoding UTF8
    }
    Write-Output 'Passed: Unity menu request dispatch, typed switch/path arguments and unknown-script rejection in child PowerShell processes. No real build or upload.'
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetFullPath($testDirectory).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
