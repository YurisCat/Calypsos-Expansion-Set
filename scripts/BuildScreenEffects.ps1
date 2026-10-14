param([Parameter(Mandatory)][string]$UnityEditor)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $taskRoot '.local\screen-effects-builder'
$taskOutput = Join-Path $taskProject 'BundleOutput'
$taskLog = Join-Path $taskProject 'build.log'
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {throw 'Unity editor executable not found.'}
foreach ($directory in @('Assets\Editor','ProjectSettings','Packages','BundleOutput')) {
    New-Item -ItemType Directory -Path (Join-Path $taskProject $directory) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'Assets\ScreenEffects\IdolScreen.shader') -Destination (Join-Path $taskProject 'Assets\IdolScreen.shader') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenEffectsBuild.cs') -Destination (Join-Path $taskProject 'Assets\Editor\ScreenEffectsBuild.cs') -Force
[IO.File]::WriteAllText((Join-Path $taskProject 'Packages\manifest.json'), '{"dependencies":{}}', [Text.UTF8Encoding]::new($false))
$taskEditorVersion = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $UnityEditor))
if ($taskEditorVersion -notmatch '^\d{4}\.\d+\.\d+[abfp]\d+(c\d+)?$') {throw 'Cannot identify Unity editor version directory.'}
[IO.File]::WriteAllText((Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt'),
    "m_EditorVersion: $taskEditorVersion`n", [Text.UTF8Encoding]::new($false))
$env:CES_SCREEN_BUNDLE_OUTPUT = $taskOutput
$taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList @(
    '-batchmode','-nographics','-noUpm','-quit','-projectPath',"`"$taskProject`"",
    '-executeMethod','ScreenEffectsBuild.Build','-logFile',"`"$taskLog`""
) -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $taskLog -SimpleMatch 'CES_SCREEN_BUNDLE_READY' -Quiet)) {
    throw "Unity resource build failed; inspect $taskLog"
}
$taskBundle = Join-Path $taskOutput 'idol_screen.bundle'
$taskDestination = Join-Path $taskRoot 'Assets\ScreenEffects\idol_screen.bundle'
Copy-Item -LiteralPath $taskBundle -Destination $taskDestination -Force
Get-FileHash -LiteralPath $taskDestination
