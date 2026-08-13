<#
.SYNOPSIS
  Locate the Slay the Spire 2 install directory.
#>
[CmdletBinding()]
param(
    [string[]] $SteamRoots = @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam",
        "$env:ProgramFiles\Steam",
        "D:\citrus_steam_games"
    )
)

$ErrorActionPreference = 'Stop'

function Get-SteamLibraries {
    param([string[]] $Roots)
    $libs = New-Object System.Collections.Generic.List[string]
    foreach ($root in $Roots) {
        if (Test-Path (Join-Path $root 'steamapps\common\Slay the Spire 2\SlayTheSpire2.exe')) {
            $libs.Add($root) | Out-Null
        }
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            $libs.Add($root) | Out-Null
            foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' -AllMatches)) {
                foreach ($mm in $m.Matches) {
                    $libs.Add(($mm.Groups[1].Value -replace '\\\\', '\')) | Out-Null
                }
            }
        }
    }
    # Also treat D:\citrus_steam_games as a library root
    $libs | Select-Object -Unique
}

$gameDir = $null
foreach ($lib in (Get-SteamLibraries -Roots $SteamRoots)) {
    foreach ($candidate in @(
        (Join-Path $lib 'steamapps\common\Slay the Spire 2'),
        (Join-Path $lib 'common\Slay the Spire 2'),
        (Join-Path $lib 'Slay the Spire 2')
    )) {
        if (Test-Path (Join-Path $candidate 'SlayTheSpire2.exe')) {
            $gameDir = $candidate
            break
        }
    }
    if ($gameDir) { break }
}

# Hardcoded fallback used by this workspace
if (-not $gameDir) {
    $fallback = 'D:\citrus_steam_games\steamapps\common\Slay the Spire 2'
    if (Test-Path (Join-Path $fallback 'SlayTheSpire2.exe')) {
        $gameDir = $fallback
    }
}

if (-not $gameDir) {
    throw "Could not locate 'Slay the Spire 2'."
}

Write-Output $gameDir
