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

# Seed sample characters/ only when missing — never clobber in-game user art/config.
$charsSrc = Join-Path $Root 'characters'
$charsDst = Join-Path $dest 'characters'
if (Test-Path $charsSrc) {
    New-Item -ItemType Directory -Force -Path $charsDst | Out-Null
    Get-ChildItem $charsSrc -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring((Resolve-Path $charsSrc).Path.Length).TrimStart('\', '/')
        $out = Join-Path $charsDst $rel
        if (Test-Path $out) { return }
        New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
        Copy-Item $_.FullName $out -Force
        Write-Host "    + characters/$($rel.Replace('\', '/'))"
    }
    Write-Host "    -> $charsDst (existing files preserved)"
}

Write-Host "Deployed '$ModId' to: $dest"
Write-Host "Restart Slay the Spire 2 to apply. Press F8 in-game for settings if ModConfig is absent."
