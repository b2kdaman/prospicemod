param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
    $changes = & git status --porcelain --untracked-files=normal
    if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit source and packaging changes before preparing a release.' }
    & (Join-Path $PSScriptRoot 'test-placement.ps1')
    & (Join-Path $PSScriptRoot 'build-placement.ps1') -GameDir $GameDir
    & python (Join-Path $PSScriptRoot 'package-release.py')
    if ($LASTEXITCODE -ne 0) { throw 'Release verification failed.' }
} finally { Pop-Location }
