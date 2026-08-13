<#
.SYNOPSIS
  Build DLL + PCK and optionally deploy to STS2 mods folder.
#>
[CmdletBinding()]
param(
    [string] $Root = $(if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }),
    [switch] $Deploy,
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'
Set-Location $Root

Write-Host "[build] Compiling SkinArtEditor.dll ..."
dotnet build (Join-Path $Root 'src/SkinArtEditor/SkinArtEditor.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$dllSrc = Join-Path $Root 'build/dll/SkinArtEditor.dll'
if (-not (Test-Path $dllSrc)) { throw "Missing $dllSrc" }
Copy-Item $dllSrc (Join-Path $Root 'mod/SkinArtEditor.dll') -Force
Write-Host "    -> mod/SkinArtEditor.dll"

Write-Host "[build] Building PCK ..."
& (Join-Path $Root 'tools/build-pck.ps1') -Root $Root

if ($Deploy) {
    & (Join-Path $Root 'tools/deploy.ps1') -Root $Root -GameDir $GameDir
}

Write-Host "Build complete."
