param([Parameter(Mandatory)][string]$UnityEditor)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $taskRoot '.local\ping-builder'
$taskOutput = Join-Path $taskProject 'BundleOutput'
$taskLog = Join-Path $taskProject 'build.log'
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {throw 'Unity editor executable not found.'}
foreach ($directory in @('Assets\Editor','ProjectSettings','Packages','BundleOutput')) {
    New-Item -ItemType Directory -Path (Join-Path $taskProject $directory) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'Assets\Pings\SharedPing.shader') -Destination (Join-Path $taskProject 'Assets\SharedPing.shader') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PingBuild.cs') -Destination (Join-Path $taskProject 'Assets\Editor\PingBuild.cs') -Force
[IO.File]::WriteAllText((Join-Path $taskProject 'Packages\manifest.json'), '{"dependencies":{}}', [Text.UTF8Encoding]::new($false))
$taskEditorVersion = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $UnityEditor))
if ($taskEditorVersion -notmatch '^\d{4}\.\d+\.\d+[abfp]\d+(c\d+)?$') {throw 'Cannot identify Unity editor version directory.'}
[IO.File]::WriteAllText((Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt'), "m_EditorVersion: $taskEditorVersion`n", [Text.UTF8Encoding]::new($false))
$env:CES_PING_BUNDLE_OUTPUT = $taskOutput
$taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList @(
    '-batchmode','-nographics','-noUpm','-quit','-projectPath',"`"$taskProject`"",
    '-executeMethod','PingBuild.Build','-logFile',"`"$taskLog`""
) -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $taskLog -SimpleMatch 'CES_PING_BUNDLE_READY' -Quiet)) {
    throw "Unity ping build failed; inspect $taskLog"
}
$taskDestination = Join-Path $taskRoot 'Assets\Pings\shared_pings.bundle'
Copy-Item -LiteralPath (Join-Path $taskOutput 'shared_pings.bundle') -Destination $taskDestination -Force
Get-FileHash -LiteralPath $taskDestination
