Set-StrictMode -Version Latest

function Invoke-AndroidCommand {
    param([string]$Executable, [string[]]$NativeArgs, [ValidateRange(1, 600)][int]$TimeoutSeconds = 300)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Executable
    # Windows native quoting, including embedded quotes and trailing backslashes.
    $quoted = @($NativeArgs | ForEach-Object {
        $value = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        '"' + [regex]::Replace($value, '(\\+)$', '$1$1') + '"'
    })
    $start.Arguments = $quoted -join ' '
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = $null
    try {
        # PowerShell 5.1 turns redirected native stderr into ErrorRecords. ADB writes
        # benign daemon-start notices there, so capture both streams outside PowerShell.
        $process = [Diagnostics.Process]::Start($start)
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw 'Android SDK command timed out.' }
        return [PSCustomObject]@{ ExitCode = $process.ExitCode;
            Output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult() }
    } finally {
        if ($process) {
            if (-not $process.HasExited) { $process.Kill() }
            $process.Dispose()
        }
    }
}

function Get-AndroidDevice {
    param([string[]]$Lines, [string]$DeviceSerial)
    $devices = @($Lines | ForEach-Object {
        if ($_ -match '^([^\s]+)\s+(device|unauthorized|offline)(?:\s+.*)?$') {
            [PSCustomObject]@{ Serial = $matches[1]; State = $matches[2] }
        }
    })
    if ($DeviceSerial) { $devices = @($devices | Where-Object { $_.Serial -ceq $DeviceSerial }) }
    if ($devices.Count -ne 1) { throw 'Connect exactly one Android device, or select it with -DeviceSerial.' }
    if ($devices[0].State -ne 'device') { throw 'Unlock the phone and authorize USB debugging; the device is not ready.' }
    return $devices[0].Serial
}

function Sync-AndroidBuildCache {
    param([string]$SourceRoot, [string]$BuildRoot)
    $cache = Assert-AndroidBuildCache -SourceRoot $SourceRoot -BuildRoot $BuildRoot
    $source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\', '/')
    $marker = Join-Path $cache '.unity-android-release-cache'
    if (Test-Path -LiteralPath $marker -PathType Leaf) {
        if ([IO.File]::ReadAllText($marker) -ine $source) { throw 'This build cache belongs to another project. Choose a different BuildRoot.' }
    } elseif ((Test-Path -LiteralPath $cache) -and @(Get-ChildItem -LiteralPath $cache -Force).Count) {
        throw 'BuildRoot is not an empty, owned tool cache. Choose a new empty folder; existing files will not be replaced.'
    }
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $lease = [IO.File]::Open((Join-Path $cache '.build-workflow.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $lockFile = Join-Path $cache 'Temp/UnityLockfile'
        if (Test-Path -LiteralPath $lockFile) {
            $unityLock = [IO.File]::Open($lockFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            $unityLock.Dispose()
        }
        foreach ($directory in @('Assets', 'Packages', 'ProjectSettings')) {
            if (-not (Test-Path -LiteralPath (Join-Path $source $directory) -PathType Container)) { throw "Unity project directory is missing: $directory" }
        }
        foreach ($item in Get-ChildItem -LiteralPath $cache -Recurse -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The build cache must not contain file or directory links.' }
        }
        [IO.File]::WriteAllText($marker, $source)
        foreach ($directory in @('Assets', 'Packages', 'ProjectSettings')) {
            $target = [IO.Path]::GetFullPath((Join-Path $cache $directory))
            if (-not $target.StartsWith($cache + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cache synchronization target.' }
            & robocopy (Join-Path $source $directory) $target /MIR /XJ /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -gt 7) { throw 'Build cache synchronization failed.' }
            $global:LASTEXITCODE = 0
        }
        $manifestPath = Join-Path $cache 'Packages/manifest.json'
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $changed = $false
            if ($manifest.PSObject.Properties['dependencies']) {
                foreach ($dependency in $manifest.dependencies.PSObject.Properties) {
                    if ($dependency.Value -is [string] -and $dependency.Value.StartsWith('file:') -and -not $dependency.Value.StartsWith('file://')) {
                        $path = $dependency.Value.Substring(5)
                        if (-not [IO.Path]::IsPathRooted($path)) {
                            $localPath = [IO.Path]::GetFullPath((Join-Path (Join-Path $source 'Packages') $path))
                            if ($localPath.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                                $copiedPath = Join-Path $cache $localPath.Substring($source.Length + 1)
                                if (Test-Path -LiteralPath $copiedPath) { $localPath = $copiedPath }
                            }
                            $dependency.Value = 'file:' + $localPath.Replace('\', '/')
                            $changed = $true
                        }
                    }
                }
            }
            if ($changed) { [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false))) }
        }
        return [PSCustomObject]@{ Path = $cache; Lock = $lease }
    } catch { $lease.Dispose(); throw }
}

function Resolve-AndroidDeviceTools {
    param([string]$UnityPath, [string]$AdbPath)
    $localSettingsPath = Join-Path $script:ProjectRoot 'Builds/Android/device-build.json'
    if (-not (Test-Path -LiteralPath $localSettingsPath -PathType Leaf)) {
        $localSettingsPath = Join-Path (Split-Path -Parent $script:ReleaseConfigPath) 'device-build.json'
    }
    if (Test-Path -LiteralPath $localSettingsPath -PathType Leaf) {
        $localSettings = Get-Content -LiteralPath $localSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $UnityPath -and $localSettings.PSObject.Properties['UnityPath']) { $UnityPath = $localSettings.UnityPath }
        if (-not $AdbPath -and $localSettings.PSObject.Properties['AdbPath']) { $AdbPath = $localSettings.AdbPath }
    }
    $unity = Resolve-UnityEditor -UnityPath $UnityPath
    $sdk = Join-Path (Split-Path -Parent $unity) 'Data/PlaybackEngines/AndroidPlayer/SDK'
    if (-not $AdbPath) { $AdbPath = if ($env:ANDROID_ADB_PATH) { $env:ANDROID_ADB_PATH } else { Join-Path $sdk 'platform-tools/adb.exe' } }
    if (-not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) { throw 'ADB is missing. Install Unity Android SDK support or pass -AdbPath.' }
    return [PSCustomObject]@{ Unity = $unity; Adb = (Resolve-Path -LiteralPath $AdbPath).Path; Sdk = $sdk }
}

function Get-InstalledAndroidVersion {
    param([string]$PackageOutput)
    $code = [regex]::Match($PackageOutput, 'versionCode=(\d+)')
    $name = [regex]::Match($PackageOutput, 'versionName=([^\r\n]+)')
    return [PSCustomObject]@{ Code = $(if ($code.Success) { [int]$code.Groups[1].Value } else { 0 }); Name = $name.Groups[1].Value.Trim() }
}

function Get-NextDeviceVersionCode {
    param([int]$ProjectCode, [int]$InstalledCode)
    $current = [Math]::Max($ProjectCode, $InstalledCode)
    if ($current -eq [int]::MaxValue) { throw 'Android version codes are exhausted.' }
    return $current + 1
}

function Assert-AndroidBuildCache {
    param([string]$SourceRoot, [string]$BuildRoot)
    if ($BuildRoot -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+[\\/]?)') { throw 'BuildRoot must be an absolute Windows filesystem path.' }
    $source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\', '/')
    $cache = [IO.Path]::GetFullPath($BuildRoot).TrimEnd('\', '/')
    $separator = [IO.Path]::DirectorySeparatorChar
    if ($source -eq $cache -or $cache -eq [IO.Path]::GetPathRoot($cache).TrimEnd('\', '/') -or
        ($cache + $separator).StartsWith($source + $separator, [StringComparison]::OrdinalIgnoreCase) -or
        ($source + $separator).StartsWith($cache + $separator, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use a dedicated build cache outside the source project; never its parent or a drive root.'
    }
    foreach ($path in @($cache, (Join-Path $cache 'Assets'), (Join-Path $cache 'Packages'), (Join-Path $cache 'ProjectSettings'))) {
        if (Test-Path -LiteralPath $path) {
            if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The build cache must not contain directory links.' }
        }
    }
    $ancestor = Split-Path -Parent $cache
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'BuildRoot must not pass through a directory link.' }
        $parent = Split-Path -Parent $ancestor
        if ($parent -eq $ancestor) { break }
        $ancestor = $parent
    }
    return $cache
}

function Assert-AndroidBuildDiskSpace {
    param([string[]]$Paths, [ValidateRange(1, 200)][int]$MinimumFreeGB = 25)
    $volumes = @($Paths | ForEach-Object { [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($_)) } | Select-Object -Unique)
    foreach ($volume in $volumes) {
        $drive = New-Object IO.DriveInfo($volume)
        if (-not $drive.IsReady) { throw 'The selected build volume is not ready.' }
        $free = [Math]::Round($drive.AvailableFreeSpace / 1GB, 1)
        if ($free -lt $MinimumFreeGB) {
            throw "Not enough build disk space on $volume ($free GB free; $MinimumFreeGB GB required). Configure a dedicated cache on another volume with Set-AndroidDeviceBuildSettings.ps1 -BuildRoot. No build started."
        }
    }
}

function Start-InstalledAndroidApp {
    param([string]$AdbPath, [string]$DeviceSerial, [string]$PackageName, [string]$ActivityName)
    if ($PackageName -notmatch '^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$' -or
        $ActivityName -notmatch '^[A-Za-z0-9_.$]+$') { throw 'Invalid Android launcher identity.' }
    # Remote shell quoting protects nested activity names containing a dollar sign.
    $component = "'" + $PackageName + '/' + $ActivityName + "'"
    $start = Invoke-AndroidCommand -Executable $AdbPath -NativeArgs @('-s', $DeviceSerial, 'shell', 'am', 'start', '-W',
        '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', '-n', $component) -TimeoutSeconds 30
    return $start.ExitCode -eq 0 -and $start.Output -match '(?m)^Status:\s*ok\s*$'
}
