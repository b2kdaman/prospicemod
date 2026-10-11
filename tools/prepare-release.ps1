param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
    $changes = & git status --porcelain --untracked-files=normal
    if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit source and packaging changes before preparing a release.' }
    $testOutput = & (Join-Path $PSScriptRoot 'test-placement.ps1')
    $testOutput | Write-Output
    $summary = @($testOutput | Select-String -Pattern '^(\d+) checks passed\.$')
    if ($summary.Count -ne 1) { throw 'Missing unambiguous standalone test summary.' }
    $checkCount = [int]$summary[0].Matches[0].Groups[1].Value
    if ($checkCount -lt 1) { throw 'No standalone checks passed.' }
    & (Join-Path $PSScriptRoot 'build-placement.ps1') -GameDir $GameDir
    & python (Join-Path $PSScriptRoot 'package-release.py') --standalone-checks $checkCount
    if ($LASTEXITCODE -ne 0) { throw 'Release verification failed.' }
} finally { Pop-Location }
