param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$InvokyrPath,
    [switch]$SkipBuild,
    [string]$InPlacePlugin
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($InvokyrPath)) {
    $taskSettings = Join-Path $taskRoot '.local\settings.json'
    if (-not (Test-Path -LiteralPath $taskSettings)) {
        throw 'Pass -InvokyrPath or create .local/settings.json with a gamePath property.'
    }
    $InvokyrPath = (Get-Content -LiteralPath $taskSettings -Raw -Encoding UTF8 | ConvertFrom-Json).gamePath
}
$taskGame = [System.IO.Path]::GetFullPath($InvokyrPath)
if (-not (Test-Path -LiteralPath (Join-Path $taskGame 'Invokyr.exe'))) { throw 'Invokyr.exe not found.' }
if (Get-Process -Name Invokyr -ErrorAction SilentlyContinue) { throw 'Close Invokyr before deployment.' }
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration }
$taskSource = Join-Path $taskRoot "bin\$Configuration\net6.0\CalypsoExpansionSet.dll"
if (-not (Test-Path -LiteralPath $taskSource)) { throw "Build output is missing: $taskSource" }
$taskPlugins = Join-Path $taskGame 'BepInEx\plugins'
$taskDestination = Join-Path $taskPlugins 'CalypsoExpansionSet'
$taskBackupRoot = Join-Path $taskRoot ('.local\backups\deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
# Downloaded DLLs can retain names such as "CalypsoExpansionSet (5).dll".
# An explicit in-place target avoids leaving that older assembly enabled beside
# a newly installed canonical filename. Back up by copying before replacement.
if (-not [string]::IsNullOrWhiteSpace($InPlacePlugin)) {
    $taskDeployed = [IO.Path]::GetFullPath($InPlacePlugin)
    if (-not $taskDeployed.StartsWith($taskPlugins.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'InPlacePlugin must be inside this game installation''s BepInEx/plugins directory.'
    }
    $taskItem = Get-Item -LiteralPath $taskDeployed
    if ($taskItem.PSIsContainer -or $taskItem.Extension -ne '.dll') { throw 'InPlacePlugin must be an existing DLL.' }
    $taskCheck = $taskItem
    while ($null -ne $taskCheck) {
        if ($taskCheck.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'InPlacePlugin cannot traverse a reparse point.' }
        $taskCheck = if ($taskCheck -is [IO.FileInfo]) { $taskCheck.Directory } else { $taskCheck.Parent }
    }
    $taskOldAssembly = [Reflection.AssemblyName]::GetAssemblyName($taskDeployed)
    $taskNewAssembly = [Reflection.AssemblyName]::GetAssemblyName($taskSource)
    if ($taskOldAssembly.Name -ne 'CalypsoExpansionSet' -or $taskNewAssembly.Name -ne $taskOldAssembly.Name) {
        throw 'In-place replacement requires the CalypsoExpansionSet assembly on both sides.'
    }
    New-Item -ItemType Directory -Path $taskBackupRoot -Force | Out-Null
    Copy-Item -LiteralPath $taskDeployed -Destination (Join-Path $taskBackupRoot $taskItem.Name)
    Copy-Item -LiteralPath $taskSource -Destination $taskDeployed -Force
    if ((Get-FileHash -LiteralPath $taskSource).Hash -ne (Get-FileHash -LiteralPath $taskDeployed).Hash) {
        throw 'Deployed DLL does not match the build; the original is retained in the backup directory.'
    }
    Write-Output "Deployed in place: $taskDeployed ($($taskNewAssembly.Version))"
    Write-Output "Previous plugin copied to: $taskBackupRoot"
    Write-Output 'Start Invokyr normally through File Explorer when ready.'
    return
}
$taskCandidates = @(
    (Join-Path $taskPlugins 'MoreIdols.dll'),
    (Join-Path $taskPlugins 'MoreIdols\MoreIdols.dll'),
    (Join-Path $taskPlugins 'CalypsoExpansionSet.dll'),
    (Join-Path $taskDestination 'CalypsoExpansionSet.dll'),
    (Join-Path $taskDestination 'CalypsoExpansionSet.pdb')
)
foreach ($taskExisting in $taskCandidates) {
    if (-not (Test-Path -LiteralPath $taskExisting)) { continue }
    $taskItem = Get-Item -LiteralPath $taskExisting
    if ($taskItem.PSIsContainer -or ($taskItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "Expected a regular plugin file: $taskExisting"
    }
    $taskRelative = $taskItem.FullName.Substring($taskPlugins.Length).TrimStart('\')
    $taskBackup = Join-Path $taskBackupRoot $taskRelative
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskBackup) -Force | Out-Null
    Move-Item -LiteralPath $taskItem.FullName -Destination $taskBackup
}
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskDestination 'CalypsoExpansionSet.dll')
$taskPdb = [System.IO.Path]::ChangeExtension($taskSource, '.pdb')
if (Test-Path -LiteralPath $taskPdb) { Copy-Item -LiteralPath $taskPdb -Destination $taskDestination }
$taskDeployed = Join-Path $taskDestination 'CalypsoExpansionSet.dll'
if ((Get-FileHash -LiteralPath $taskSource).Hash -ne (Get-FileHash -LiteralPath $taskDeployed).Hash) {
    throw 'Deployed DLL does not match the build.'
}
Write-Output "Deployed: $taskDeployed"
Write-Output "Existing plugin backups, if any: $taskBackupRoot"
Write-Output 'Start Invokyr normally through File Explorer when ready.'
