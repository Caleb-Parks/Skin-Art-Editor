<#
.SYNOPSIS
  Deploy SkinArtEditor into the STS2 mods folder (including sample characters/).
#>
[CmdletBinding()]
param(
    [string] $Root = $(if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }),
    [string] $GameDir,
    [string] $ModId = "SkinArtEditor"
)

$ErrorActionPreference = 'Stop'
Set-Location $Root

if (-not $GameDir) {
    $GameDir = (& (Join-Path $PSScriptRoot 'detect-sts2.ps1')).Trim()
}
if (-not (Test-Path (Join-Path $GameDir 'SlayTheSpire2.exe'))) {
    throw "STS2 not found at '$GameDir'. Pass -GameDir explicitly."
}

$dest = Join-Path $GameDir "mods/$ModId"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$files = @(
    @{ src = "mod/$ModId.json"; required = $true },
    @{ src = "mod/$ModId.pck";  required = $true },
    @{ src = "mod/$ModId.dll";  required = $true }
)
foreach ($f in $files) {
    $src = Join-Path $Root $f.src
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $dest (Split-Path $f.src -Leaf)) -Force
        Write-Host "    -> $dest\$(Split-Path $f.src -Leaf)"
    }
    elseif ($f.required) {
        throw "Missing required file: $($f.src). Run tools/build.ps1 first."
    }
}

# Deploy character configs + art (do not wipe user edits if present — merge by copy)
$charsSrc = Join-Path $Root 'characters'
$charsDst = Join-Path $dest 'characters'
if (Test-Path $charsSrc) {
    New-Item -ItemType Directory -Force -Path $charsDst | Out-Null
    Copy-Item (Join-Path $charsSrc '*') $charsDst -Recurse -Force
    Write-Host "    -> $charsDst"
}

Write-Host "Deployed '$ModId' to: $dest"
Write-Host "Restart Slay the Spire 2 to apply. Press F8 in-game for settings if ModConfig is absent."
