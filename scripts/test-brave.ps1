# Real Brave smoke on an isolated Windows runner profile. No personal browser data is used or published.
[CmdletBinding()]
param([ValidateSet('x86', 'x64')][string]$Arch = 'x64', [ValidateSet('brave', 'chrome', 'edge')][string]$Browser = 'brave')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This fixture is restricted to disposable GitHub Actions runners, not personal desktops.' }
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'fixture-process.ps1')
$directory = Join-Path $root 'dist\brave-fixture'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
if ($Browser -eq 'brave') {
    # Official pinned archive, verified using the publisher's checksum.
    $tag = 'v1.97.56'
    $archive = "brave-$tag-win32-x64.zip"
    & gh release download $tag --repo brave/brave-browser --pattern $archive --pattern ($archive + '.sha256') --dir $directory --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Could not download the isolated Brave fixture.' }
    $expected = ((Get-Content -LiteralPath (Join-Path $directory ($archive + '.sha256')) -Raw).Trim() -split '\s+')[0]
    $actual = (Get-FileHash -LiteralPath (Join-Path $directory $archive) -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw 'Brave archive checksum mismatch.' }
    Expand-Archive -LiteralPath (Join-Path $directory $archive) -DestinationPath (Join-Path $directory 'browser') -Force
    $brave = @(Get-ChildItem -LiteralPath (Join-Path $directory 'browser') -Filter brave.exe -Recurse -File)[0].FullName
} else {
    # Official, signed browsers supplied by the hosted Windows image. No third-party browser download.
    $relative = if ($Browser -eq 'chrome') { 'Google\Chrome\Application\chrome.exe' } else { 'Microsoft\Edge\Application\msedge.exe' }
    $brave = @(@($env:ProgramFiles, ${env:ProgramFiles(x86)}) | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ })[0]
    if (-not $brave) { throw "Hosted image has no $Browser installation." }
    Write-Host "::notice title=Browser fixture phase::Validate signature for $Browser"
    $signatureJob = Start-Job -ScriptBlock {
        param($path)
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        [pscustomobject]@{ Status = [string]$signature.Status; Subject = $signature.SignerCertificate.Subject }
    } -ArgumentList $brave
    try {
        if (-not (Wait-Job -Job $signatureJob -Timeout 60)) { throw 'Browser signature validation exceeded 60 seconds.' }
        $signatureResult = Receive-Job -Job $signatureJob -ErrorAction Stop
        $publisher = if ($Browser -eq 'chrome') { 'Google LLC' } else { 'Microsoft Corporation' }
        if ($signatureResult.Status -ne 'Valid' -or $signatureResult.Subject -notlike "*$publisher*") { throw 'Browser signature/publisher validation failed.' }
    } finally {
        Stop-Job -Job $signatureJob -ErrorAction SilentlyContinue
        Remove-Job -Job $signatureJob -Force -ErrorAction SilentlyContinue
    }
}
Write-Host ("::notice title=Browser fixture::token=" + $Browser + " version=" + [System.Diagnostics.FileVersionInfo]::GetVersionInfo($brave).ProductVersion + " sha256=" + (Get-FileHash -LiteralPath $brave -Algorithm SHA256).Hash)
$test = Join-Path $root "dist\tests\Xtension.WindowsTests-$Arch.exe"
$app = Join-Path $root "dist\Xtension-win-$Arch.exe"
$profile = Join-Path $directory "profile-$Browser-$Arch"
try {
    Write-Host "::notice title=Browser fixture phase::Start harness for $Browser $Arch"
    $execution = Invoke-FixtureProcess -Executable $test -Arguments @('--brave', $brave, $app, $profile) -OutputPrefix (Join-Path $directory "acceptance-$Browser-$Arch") -TimeoutSeconds 330
    $results = $execution.Lines
    $code = $execution.ExitCode
    Write-Host "::notice title=Browser fixture phase::Harness exited for $Browser $Arch (code $code)"
    $evidence = New-Object System.Collections.Generic.List[string]
    foreach ($line in $results) {
        if ($line -like 'PASS *' -or $line -like 'Browser binary*') { $evidence.Add($line) | Out-Null }
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
