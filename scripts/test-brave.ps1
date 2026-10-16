# Real Brave smoke on an isolated Windows runner profile. No personal browser data is used or published.
[CmdletBinding()]
param([ValidateSet('x86', 'x64')][string]$Arch = 'x64')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This fixture is restricted to disposable GitHub Actions runners, not personal desktops.' }
$root = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $root 'dist\brave-fixture'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
# Pin the current stable browser used for this evidence; it is not a shipped dependency.
$tag = 'v1.97.56'
$archive = "brave-$tag-win32-x64.zip"
& gh release download $tag --repo brave/brave-browser --pattern $archive --pattern ($archive + '.sha256') --dir $directory --clobber
if ($LASTEXITCODE -ne 0) { throw 'Could not download the isolated Brave fixture.' }
$expected = ((Get-Content -LiteralPath (Join-Path $directory ($archive + '.sha256')) -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath (Join-Path $directory $archive) -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'Brave archive checksum mismatch.' }
Expand-Archive -LiteralPath (Join-Path $directory $archive) -DestinationPath (Join-Path $directory 'browser') -Force
$brave = @(Get-ChildItem -LiteralPath (Join-Path $directory 'browser') -Filter brave.exe -Recurse -File)[0].FullName
$test = Join-Path $root "dist\tests\Xtension.WindowsTests-$Arch.exe"
$app = Join-Path $root "dist\Xtension-win-$Arch.exe"
$profile = Join-Path $directory "profile-$Arch"
try {
    $results = @(& $test --brave $brave $app $profile 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    $evidence = New-Object System.Collections.Generic.List[string]
    foreach ($line in $results) {
        if ($line -like 'PASS *' -or $line -like 'Brave binary*') { $evidence.Add($line) | Out-Null }
        elseif ($line -like 'FAIL *') { Write-Host ("::error title=Real Brave evidence::" + $line) }
        # Latency evidence is counters and milliseconds only; it carries no title, URL or provider text.
        elseif ($line -like 'INFO latency *') { Write-Host ("::notice title=Latency evidence::" + $line) }
        # Do not forward browser stderr/provider text to persistent workflow logs.
    }
    Write-Host ("::notice title=Real Brave evidence::" + ($evidence -join " | "))
    if ($code -ne 0) { throw "Real-Brave smoke failed ($code)." }
} finally {
    # This script is for a disposable CI runner, never a personal Windows desktop.
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $brave -or $_.Path -eq $app } catch { $false }
    } | Stop-Process -Force -ErrorAction SilentlyContinue
}
