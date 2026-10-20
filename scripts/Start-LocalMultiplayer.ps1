param(
    [string]$InvokyrPath,
    [ValidateRange(1, 3)]
    [int]$Count = 2,
    [switch]$SkipBuild,
    [switch]$PrepareOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($InvokyrPath)) {
    $taskSettings = Join-Path $taskRoot '.local\settings.json'
    if (-not (Test-Path -LiteralPath $taskSettings)) { throw 'Pass -InvokyrPath, or set gamePath in .local/settings.json.' }
    $InvokyrPath = (Get-Content -LiteralPath $taskSettings -Raw -Encoding UTF8 | ConvertFrom-Json).gamePath
}
$taskGame = [IO.Path]::GetFullPath($InvokyrPath)
foreach ($taskRequired in @('Invokyr.exe', 'winhttp.dll', 'Invokyr_Data\boot.config', 'dotnet\coreclr.dll', 'BepInEx\core\BepInEx.Unity.IL2CPP.dll', 'BepInEx\interop\vhs.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskGame $taskRequired) -PathType Leaf)) { throw "Missing game/BepInEx file: $taskRequired" }
}
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -InvokyrPath $taskGame }
$taskDll = Join-Path $taskRoot 'bin\Release\net6.0\CalypsoExpansionSet.dll'
if (-not (Test-Path -LiteralPath $taskDll -PathType Leaf)) { throw 'Build output is missing; run scripts/Build.ps1.' }

function Copy-TestTree([string]$Source, [string]$Destination) {
    $taskItem = Get-Item -LiteralPath $Source
    if ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Cannot copy a reparse point: $Source" }
    if ($taskItem.PSIsContainer) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        foreach ($taskChild in Get-ChildItem -LiteralPath $Source -Force) {
            Copy-TestTree $taskChild.FullName (Join-Path $Destination $taskChild.Name)
        }
    } else { Copy-Item -LiteralPath $Source -Destination $Destination }
}

function Set-TestConfig([string]$Path, [string]$Section, [string]$Key, [string]$Value) {
    $taskLines = [Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $Path) {
        foreach ($taskLine in [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)) { $taskLines.Add($taskLine) }
    }
    $taskInSection = $false
    $taskInsert = -1
    for ($taskIndex = 0; $taskIndex -lt $taskLines.Count; $taskIndex++) {
        if ($taskLines[$taskIndex] -match '^\s*\[([^\]]+)\]') {
            if ($taskInSection) { break }
            $taskInSection = $Matches[1] -eq $Section
        }
        if (-not $taskInSection) { continue }
        $taskInsert = $taskIndex + 1
        if ($taskLines[$taskIndex] -match ('^\s*' + [regex]::Escape($Key) + '\s*=')) {
            $taskLines[$taskIndex] = "$Key = $Value"
            [IO.File]::WriteAllLines($Path, $taskLines, [Text.UTF8Encoding]::new($false))
            return
        }
    }
    if ($taskInsert -lt 0) { $taskLines.Add(''); $taskLines.Add("[$Section]"); $taskInsert = $taskLines.Count }
    $taskLines.Insert($taskInsert, "$Key = $Value")
    [IO.File]::WriteAllLines($Path, $taskLines, [Text.UTF8Encoding]::new($false))
}

$taskSession = Join-Path $taskRoot ('.local\local-multiplayer\' + (Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $taskSession -Force | Out-Null
$taskInstances = @()
for ($taskSlot = 1; $taskSlot -le $Count; $taskSlot++) {
    $taskInstance = Join-Path $taskSession "instance-$taskSlot"
    $taskBep = Join-Path $taskInstance 'BepInEx'
    New-Item -ItemType Directory -Path (Join-Path $taskBep 'config') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $taskBep 'plugins') -Force | Out-Null
    foreach ($taskTree in @('core', 'interop', 'unity-libs')) {
        Copy-TestTree (Join-Path $taskGame "BepInEx\$taskTree") (Join-Path $taskBep $taskTree)
    }
    foreach ($taskConfigName in @('BepInEx.cfg', 'calypso.invokyr.expansionset.cfg')) {
        $taskConfigSource = Join-Path $taskGame "BepInEx\config\$taskConfigName"
        if (Test-Path -LiteralPath $taskConfigSource -PathType Leaf) { Copy-TestTree $taskConfigSource (Join-Path $taskBep "config\$taskConfigName") }
    }
    $taskBepConfig = Join-Path $taskBep 'config\BepInEx.cfg'
    # Game updates invalidate copied interop metadata; regenerate in each isolated
    # instance before loading plugins instead of invoking stale native methods.
    Set-TestConfig $taskBepConfig 'IL2CPP' 'UpdateInteropAssemblies' 'true'
    Set-TestConfig $taskBepConfig 'Logging.Console' 'Enabled' (($taskSlot -eq 1).ToString().ToLowerInvariant())
    $taskModConfig = Join-Path $taskBep 'config\calypso.invokyr.expansionset.cfg'
    Set-TestConfig $taskModConfig 'Debug' 'DebugMode' 'true'
    Set-TestConfig $taskModConfig 'Debug' 'WindowKey' 'F8'
    Set-TestConfig $taskModConfig 'Debug' 'TestRoomEnabled' 'false'
    Copy-Item -LiteralPath $taskDll -Destination (Join-Path $taskBep 'plugins\CalypsoExpansionSet.dll')
    # Steam's normal App ID hint; no Steam API replacement or game binary changes.
    [IO.File]::WriteAllText((Join-Path $taskInstance 'steam_appid.txt'), '3883570', [Text.Encoding]::ASCII)
    $taskBootConfig = Join-Path $taskInstance 'boot.config'
    $taskBootLines = [IO.File]::ReadAllLines((Join-Path $taskGame 'Invokyr_Data\boot.config'), [Text.Encoding]::UTF8) |
        Where-Object { $_ -notmatch '^\s*single-instance\s*(=|$)' }
    [IO.File]::WriteAllLines($taskBootConfig, [string[]]$taskBootLines, [Text.UTF8Encoding]::new($false))
    $taskArguments = @(
        '--doorstop-enabled', 'true',
        '--doorstop-boot-config-override', ('"{0}"' -f $taskBootConfig),
        '--doorstop-target-assembly', ('"{0}"' -f (Join-Path $taskBep 'core\BepInEx.Unity.IL2CPP.dll')),
        '--doorstop-clr-runtime-coreclr-path', ('"{0}"' -f (Join-Path $taskGame 'dotnet\coreclr.dll')),
        '--doorstop-clr-corlib-dir', ('"{0}"' -f (Join-Path $taskGame 'dotnet')),
        '--ces-local-instance', "$taskSlot", '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '600',
        '-logFile', ('"{0}"' -f (Join-Path $taskInstance 'Player.log'))
    )
    $taskInstances += [pscustomobject]@{ Slot = $taskSlot; Directory = $taskInstance; Arguments = $taskArguments -join ' '; ProcessId = $null }
}

foreach ($taskInstance in $taskInstances) {
    if (-not $PrepareOnly) {
        $taskStart = [Diagnostics.ProcessStartInfo]::new()
        $taskStart.FileName = Join-Path $taskGame 'Invokyr.exe'
        $taskStart.WorkingDirectory = $taskInstance.Directory
        $taskStart.UseShellExecute = $false
        $taskStart.Arguments = $taskInstance.Arguments
        $taskStart.EnvironmentVariables['SteamAppId'] = '3883570'
        $taskStart.EnvironmentVariables['BEPINEX_PRELOADER_LOG'] = Join-Path $taskInstance.Directory 'preloader.log'
        $taskProcess = [Diagnostics.Process]::Start($taskStart)
        $taskInstance.ProcessId = $taskProcess.Id
    }
    Write-Output ("Local {0}: PID={1}, files={2}" -f $taskInstance.Slot, $taskInstance.ProcessId, $taskInstance.Directory)
}
$taskInstances | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $taskSession 'instances.json') -Encoding UTF8
Write-Output 'After a game update, BepInEx may take longer to rebuild outdated interfaces in each test window before loading the MOD.'
Write-Output 'In each window: F8 > Local multiplayer. Host in Local 1 for the native shop and 5000 shared funds, then join from the other windows before starting a board.'
