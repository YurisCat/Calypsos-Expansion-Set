param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskDotnet = Join-Path $taskRoot '.local\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.local\nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location -LiteralPath $taskRoot
try {
    & $taskDotnet build '.\CalypsoExpansionSet.csproj' -c $Configuration -p:DeployToGame=false --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
} finally {
    Pop-Location
}
