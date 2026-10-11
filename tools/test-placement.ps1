param()
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$env:DOTNET_ROOT = Join-Path $repoPath 'local\dotnet-sdk'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $repoPath 'local\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repoPath 'local\nuget'
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') run --project (Join-Path $repoPath 'tests\Prospice.Placement.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Placement state tests failed.' }
