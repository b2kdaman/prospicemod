param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$sdkPath = Join-Path $repoPath 'local\dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkPath)) { throw 'Install the documented repository-local .NET SDK first.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) { throw 'The verified BepInEx IL2CPP installation is required.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $repoPath 'local\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repoPath 'local\nuget'
& $sdkPath build (Join-Path $repoPath 'src\Prospice.Diagnostics\Prospice.Diagnostics.csproj') -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { throw "Diagnostic build failed with exit code $LASTEXITCODE" }
