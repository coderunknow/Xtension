<#
.SYNOPSIS
  Assembles release assets into dist/release: versioned x86 and x64 EXEs, a source ZIP from git archive,
  RELEASE_NOTES.md (CHANGELOG section plus fixed capability and limitation text), and SHA256SUMS.txt.
.NOTES
  Requires both dist/Xtension-win-x86.exe and dist/Xtension-win-x64.exe, and a clean git HEAD for the source ZIP.
#>
[CmdletBinding()]
param(
    [switch]$AllowDirtySource
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$version = ([System.IO.File]::ReadAllText((Join-Path $root 'VERSION'), $utf8NoBom)).Trim()
$tag = 'v' + $version
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not identify package source commit.' }
$dirty = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect package source tree.' }
if ($dirty.Count -gt 0 -and -not $AllowDirtySource) { throw 'Working tree is not clean; source ZIP would not match HEAD.' }

# Validate provenance BEFORE deleting old packages or copying binaries. A successful old build is not this candidate.
foreach ($arch in @('x86', 'x64')) {
    $built = Join-Path $root "dist\Xtension-win-$arch.exe"
    $stampPath = $built + '.build.json'
    if (-not (Test-Path -LiteralPath $built -PathType Leaf)) { throw "Missing $arch build: $built" }
    if (-not (Test-Path -LiteralPath $stampPath -PathType Leaf)) { throw "Missing build provenance: $stampPath" }
    $stamp = Get-Content -LiteralPath $stampPath -Raw | ConvertFrom-Json
    if ($stamp.commit -ne $commit -or $stamp.version -ne $version -or $stamp.architecture -ne $arch) {
        throw "Stale or mismatched $arch build provenance. Rebuild this commit."
    }
    if ($stamp.dirty -and -not $AllowDirtySource) { throw "The $arch binary was built from a dirty source tree." }
    if ($stamp.sha256 -ne (Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw "Build provenance checksum mismatch for $arch."
    }
    if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($built).ProductVersion -ne $version) {
        throw "Binary version mismatch for $arch."
    }
    $stream = [System.IO.File]::OpenRead($built)
    $reader = New-Object System.IO.BinaryReader($stream)
    try {
        $stream.Position = 0x3c
        $pe = $reader.ReadInt32()
        $stream.Position = $pe
        if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE signature for $arch." }
        $machine = $reader.ReadUInt16()
        $expected = if ($arch -eq 'x86') { 0x14c } else { 0x8664 }
        if ($machine -ne $expected) { throw "Binary architecture mismatch for $arch." }
    } finally { $reader.Dispose(); $stream.Dispose() }
}

$releaseDir = Join-Path $root 'dist\release'
if (Test-Path -LiteralPath $releaseDir) { Remove-Item -LiteralPath $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$assets = @{
    x86 = Join-Path $releaseDir "Xtension-$tag-win-x86.exe"
    x64 = Join-Path $releaseDir "Xtension-$tag-win-x64.exe"
}
foreach ($arch in @('x86', 'x64')) {
    Copy-Item -LiteralPath (Join-Path $root "dist\Xtension-win-$arch.exe") -Destination $assets[$arch]
}
$sourceZip = Join-Path $releaseDir "Xtension-$tag-source.zip"
$gitArgs = @('-C', $root, 'archive', '--format=zip', "--output=$sourceZip", 'HEAD')
& git @gitArgs
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }

# Release notes: the CHANGELOG section for this version, then the fixed capability and limitation statement.
$changelog = [System.IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'), $utf8NoBom)
$pattern = '(?ms)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)'
$match = [regex]::Match($changelog, $pattern)
if (-not $match.Success) { throw "CHANGELOG.md has no section for $version." }
$capabilities = [System.IO.File]::ReadAllText((Join-Path $root 'docs\RELEASE_CAPABILITIES.md'), $utf8NoBom)
$notes = "# Xtension $tag`n`n" + $match.Groups[1].Value.Trim() + "`n`n" + $capabilities.Trim() + "`n"
[System.IO.File]::WriteAllText((Join-Path $releaseDir 'RELEASE_NOTES.md'), $notes, $utf8NoBom)

$lines = New-Object System.Collections.Generic.List[string]
foreach ($file in @($assets['x86'], $assets['x64'], $sourceZip)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add("$hash  $(Split-Path -Leaf $file)") | Out-Null
}
$sumsPath = Join-Path $releaseDir 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($sumsPath, (($lines -join "`n") + "`n"), $utf8NoBom)

# Re-verify the manifest against the files it names, so a mismatch fails before anything is published.
foreach ($line in Get-Content -LiteralPath $sumsPath) {
    $parts = $line -split '  ', 2
    $actual = (Get-FileHash -LiteralPath (Join-Path $releaseDir $parts[1]) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $parts[0]) { throw "Checksum mismatch for $($parts[1])." }
}

Get-ChildItem -LiteralPath $releaseDir | ForEach-Object { Write-Host ("{0}  {1} bytes" -f $_.Name, $_.Length) }
Write-Host "Release assets for $tag are ready in $releaseDir"
