# Requires both current-commit builds and their provenance stamps. Does not publish anything.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $PSScriptRoot 'package.ps1'
& $package
$stampPath = Join-Path $root 'dist\Xtension-win-x86.exe.build.json'
$original = [System.IO.File]::ReadAllText($stampPath)
try {
    foreach ($kind in @('commit', 'version', 'architecture', 'sha256', 'dirty')) {
        $stamp = $original | ConvertFrom-Json
        if ($kind -eq 'dirty') { $stamp.dirty = $true }
        else { $stamp.$kind = 'invalid-regression-fixture' }
        $stamp | ConvertTo-Json | Set-Content -LiteralPath $stampPath -Encoding ASCII
        $rejected = $false
        try { & $package }
        catch { $rejected = $true; Write-Host "PASS: rejected mismatched $kind" }
        if (-not $rejected) { throw "Package accepted invalid $kind provenance." }
    }
} finally { [System.IO.File]::WriteAllText($stampPath, $original) }
& $package
Write-Host 'Packaging and checksum verification passed; five invalid-provenance cases rejected.'
