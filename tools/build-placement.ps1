param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$sdkPath = Join-Path $repoPath 'local\dotnet-sdk\dotnet.exe'
$env:DOTNET_ROOT = Split-Path -Parent $sdkPath
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $repoPath 'local\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repoPath 'local\nuget'
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\interop\Assembly-CSharp.dll'))) { throw 'Generated bindings for the verified game are required.' }
& $sdkPath build (Join-Path $repoPath 'src\Prospice.Placement\Prospice.Placement.csproj') -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Placement build failed.' }
