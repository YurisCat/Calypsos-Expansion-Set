param([Parameter(Mandatory)][string]$UnityEditor)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $taskRoot '.local\lobby-box-builder'
foreach ($directory in @('Assets\Editor', 'Assets\Source', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $taskProject $directory) -Force | Out-Null
}
foreach ($file in @('box.fbx', 'BOX_B.png', 'BOX_S.png', 'BOX_upper_B.png')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot "Assets\LobbyBox\Source\$file") -Destination (Join-Path $taskProject "Assets\Source\$file") -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LobbyBoxBuild.cs') -Destination (Join-Path $taskProject 'Assets\Editor\LobbyBoxBuild.cs') -Force
[IO.File]::WriteAllText((Join-Path $taskProject 'Packages\manifest.json'), '{"dependencies":{}}')
$taskVersion = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $UnityEditor))
[IO.File]::WriteAllText((Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt'), "m_EditorVersion: $taskVersion`n")
$env:CES_BOX_OUTPUT = Join-Path $taskProject 'box.cesmodel'
$taskLog = Join-Path $taskProject 'build.log'
$taskProcess = Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-noUpm', '-quit', '-projectPath', "`"$taskProject`"",
    '-executeMethod', 'LobbyBoxBuild.Build', '-logFile', "`"$taskLog`""
)
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $taskLog -SimpleMatch 'CES_BOX_READY' -Quiet)) {
    throw "Model export failed; inspect $taskLog"
}
Copy-Item -LiteralPath $env:CES_BOX_OUTPUT -Destination (Join-Path $taskRoot 'Assets\LobbyBox\box.cesmodel') -Force
Get-Content -LiteralPath "$env:CES_BOX_OUTPUT.txt" -Encoding UTF8
Get-FileHash -LiteralPath (Join-Path $taskRoot 'Assets\LobbyBox\box.cesmodel')
