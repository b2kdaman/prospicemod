param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Prospice Demo')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Project Prospice' -ErrorAction SilentlyContinue) { throw 'Close Prospice before removing the mod.' }
$repoPath = Split-Path -Parent $PSScriptRoot
$plugin = Join-Path (Resolve-Path -LiteralPath $GameDir).Path 'BepInEx\plugins\Prospice.Placement.dll'
if (Test-Path -LiteralPath $plugin) {
    $archive = Join-Path $repoPath ('local\removed-plugins\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
    New-Item -ItemType Directory -Path $archive -Force | Out-Null
    Move-Item -LiteralPath $plugin -Destination $archive
    Write-Output "Placement removed and archived to $archive"
} else { Write-Output 'Placement DLL is already absent.' }
