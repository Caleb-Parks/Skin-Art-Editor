<#
.SYNOPSIS
  Build SkinArtEditor.pck (animator scripts + template scenes only).
#>
[CmdletBinding()]
param(
    [string] $Root = $(if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }),
    [string] $GodotExe = "",
    [string] $Gdre = "",
    [string] $GodotProj = "godot",
    [string] $PckRoot = "build/pck_root",
    [string] $OutPck = "mod/SkinArtEditor.pck",
    [string] $EngineVer = "4.5.1"
)

$ErrorActionPreference = 'Stop'
Set-Location $Root

# Prefer local Cassiopeia tooling if not overridden.
if (-not $GodotExe) {
    $cand = "D:\citrus_dev\repos\personal\cassiopeia\tools\bin\godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
    if (Test-Path $cand) { $GodotExe = $cand }
}
if (-not $Gdre) {
    $cand = "D:\citrus_dev\repos\personal\cassiopeia\tools\bin\gdre\gdre_tools.exe"
    if (Test-Path $cand) { $Gdre = $cand }
}
if (-not (Test-Path $GodotExe)) { throw "Godot not found. Pass -GodotExe." }
if (-not (Test-Path $Gdre)) { throw "GDRE not found. Pass -Gdre." }

$godot = (Resolve-Path $GodotExe).Path
$gdre = (Resolve-Path $Gdre).Path

function Copy-ResTree {
    param([string] $RelPath)
    $src = Join-Path $GodotProj $RelPath
    if (-not (Test-Path $src)) { return }
    $dstRoot = Join-Path $PckRoot $RelPath
    $srcFull = (Resolve-Path $src).Path
    Get-ChildItem $src -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($srcFull.Length).TrimStart('\', '/')
        $out = Join-Path $dstRoot $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
        Copy-Item $_.FullName $out -Force
        Write-Host ("    + res://{0}/{1}" -f $RelPath.Replace('\', '/'), $rel.Replace('\', '/'))
    }
}

Write-Host "[1/3] Godot import (scripts/scenes) ..."
New-Item -ItemType Directory -Force -Path build | Out-Null
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $godot --headless --path $GodotProj --import 2>&1 | Out-File -Encoding utf8 build/godot_import.log
$godotExit = $LASTEXITCODE
$ErrorActionPreference = $prevEap
if ($godotExit -ne 0) {
    Write-Warning "Godot import exited $godotExit (continuing; see build/godot_import.log)"
}

Write-Host "[2/3] Staging pck root ..."
if (Test-Path $PckRoot) { Remove-Item -Recurse -Force $PckRoot }
New-Item -ItemType Directory -Force -Path $PckRoot | Out-Null

Copy-ResTree 'scripts'
Copy-ResTree 'scenes'
Copy-Item (Join-Path $GodotProj "mod_manifest.json") (Join-Path $PckRoot "mod_manifest.json") -Force
Write-Host "    + res://mod_manifest.json"

Write-Host "[3/3] Creating pck with GDRE ..."
$outFull = Join-Path $Root $OutPck
New-Item -ItemType Directory -Force -Path (Split-Path $outFull) | Out-Null
& $gdre --headless --pck-create="$((Resolve-Path $PckRoot).Path)" --output="$outFull" --pck-version=2 --pck-engine-version=$EngineVer 2>&1 |
    Out-File -Encoding utf8 build/pck_create.log
if (-not (Test-Path $outFull)) { throw "pck creation failed; see build/pck_create.log" }

Write-Host ("Done: {0} ({1:N0} bytes)" -f $OutPck, (Get-Item $outFull).Length)
