param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$gamePath = (Resolve-Path -LiteralPath $GameDir).Path
if (Get-Process -Name 'Project Prospice' -ErrorAction SilentlyContinue) { throw 'Close Prospice before installing the mod.' }
$expected = '511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D'
if ((Get-FileHash -LiteralPath (Join-Path $gamePath 'GameAssembly.dll') -Algorithm SHA256).Hash -ne $expected) { throw 'Unsupported game build. Reverify methods and regenerate matching bindings first.' }
$source = Join-Path $repoPath 'src\Prospice.Placement\bin\Release\net6.0\Prospice.Placement.dll'
if (-not (Test-Path -LiteralPath $source)) { throw 'Run tools/build-placement.ps1 first.' }
if (-not (Test-Path -LiteralPath (Join-Path $gamePath 'BepInEx\interop\Assembly-CSharp.dll'))) { throw 'The verified loader and generated bindings are required; see docs/metadata-workaround.md.' }
# Private mesh data is generated locally; never include it in distributable ZIPs.
& python -B (Join-Path $repoPath 'tools\build-cargo-cache.py') --game-dir $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Cargo surface cache generation failed; existing plugin was not replaced.' }
& python -B (Join-Path $repoPath 'tools\check-cargo-cache.py') --game-dir $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Cargo surface cache validation failed; existing plugin was not replaced.' }
& python -B (Join-Path $repoPath 'tools\build-paint-cache.py') --game-dir $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Paint surface cache generation failed; existing plugin was not replaced.' }
& python -B (Join-Path $repoPath 'tools\check-paint.py') --game-dir $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Paint assets/cache validation failed; existing plugin was not replaced.' }
$destination = Join-Path $gamePath 'BepInEx\plugins\Prospice.Placement.dll'
if (Test-Path -LiteralPath $destination) {
    $backup = Join-Path $repoPath ('local\install-backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination $backup
}
Copy-Item -LiteralPath $source -Destination $destination -Force
Write-Output "Installed: $destination"
Write-Output 'Configuration: BepInEx\config\prospice.gamepadplacement.cfg (created on first launch).'
