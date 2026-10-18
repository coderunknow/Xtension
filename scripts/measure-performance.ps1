# Resource comparison of two shipped Xtension EXEs on one disposable runner.
#
# It runs the published previous release and the current candidate back to back under identical conditions:
# first with no browser running, then in front of an isolated official Brave build. The measured binary is the
# shipped EXE, launched exactly as a user launches it, not a rebuilt test host.
#
# This is evidence, not a gate: the numbers are printed and uploaded, and a noisy runner does not fail the build.
# Only an infrastructure problem (harness missing, download or checksum failure, binary never starting) fails.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Baseline,
    [Parameter(Mandatory = $true)][string]$Candidate,
    [ValidateSet('x86', 'x64')][string]$Arch = 'x64',
    [int]$Seconds = 20,
    [ValidateRange(1, 5)][int]$Repetitions = 3,
    [string]$BaselineLabel = 'published-baseline',
    [string]$CandidateLabel = 'candidate',
    [string]$BraveTag = 'v1.97.56'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This measurement is restricted to disposable GitHub Actions runners, not personal desktops.' }
$root = Split-Path -Parent $PSScriptRoot
$harness = Join-Path $root "dist\tests\Xtension.WindowsTests-$Arch.exe"
if (-not (Test-Path -LiteralPath $harness)) { throw "Windows test harness not found: $harness (run scripts/test-windows.ps1 first)." }
foreach ($name in @('Baseline', 'Candidate')) {
    $path = (Get-Variable -Name $name).Value
    if (-not (Test-Path -LiteralPath $path)) { throw "Measured binary not found: $path" }
}
# Compare against absolute paths: a process path never matches a relative one.
$Baseline = (Resolve-Path -LiteralPath $Baseline).Path
$Candidate = (Resolve-Path -LiteralPath $Candidate).Path

$evidence = Join-Path $root 'dist\perf'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$report = Join-Path $evidence "performance-$Arch.txt"

# The isolated browser fixture is the same pinned build and the same checksum verification as the Brave smoke test.
$browserDirectory = Join-Path $root 'dist\brave-fixture'
$brave = $null
New-Item -ItemType Directory -Force -Path $browserDirectory | Out-Null
$archive = "brave-$BraveTag-win32-x64.zip"
$browserRoot = Join-Path $browserDirectory 'browser'
if (-not (Test-Path -LiteralPath $browserRoot)) {
    & gh release download $BraveTag --repo brave/brave-browser --pattern $archive --pattern ($archive + '.sha256') --dir $browserDirectory --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Could not download the isolated Brave fixture.' }
    $expected = ((Get-Content -LiteralPath (Join-Path $browserDirectory ($archive + '.sha256')) -Raw).Trim() -split '\s+')[0]
    $actual = (Get-FileHash -LiteralPath (Join-Path $browserDirectory $archive) -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw 'Brave archive checksum mismatch.' }
    Expand-Archive -LiteralPath (Join-Path $browserDirectory $archive) -DestinationPath $browserRoot -Force
}
$brave = @(Get-ChildItem -LiteralPath $browserRoot -Filter brave.exe -Recurse -File)[0].FullName
$profile = Join-Path $browserDirectory "perf-profile-$Arch"

function Stop-Xtension {
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $Baseline -or $_.Path -eq $Candidate } catch { $false }
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

# Both binaries must start from the same settings state: the documented defaults.
$settingsPath = Join-Path $env:LOCALAPPDATA 'Xtension\settings.ini'
if (Test-Path -LiteralPath $settingsPath) { Remove-Item -LiteralPath $settingsPath -Force }

function Invoke-Measurement {
    param([string]$Binary, [string]$Label, [string]$Scenario, [string]$BravePath, [string]$ProfilePath)
    Stop-Xtension
    if ($ProfilePath -and (Test-Path -LiteralPath $ProfilePath)) { Remove-Item -LiteralPath $ProfilePath -Recurse -Force }
    $arguments = @('--measure', $Binary, "$Seconds", "$Label-$Scenario")
    if ($BravePath) { $arguments += @($BravePath, $ProfilePath) }
    # The harness reads the log bytes this run appended and prints one MEASURE-LATENCY line; the script only has to
    # record and annotate it, so no second copy of the log offset is kept here.
    $lines = @(& $harness @arguments 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    foreach ($line in $lines) {
        if ($line -like 'MEASURE label=*' -or $line -like 'MEASURE-LATENCY label=*') {
            Write-Host $line
            Add-Content -LiteralPath $report -Value $line
            # The report travels as a download; an annotation travels to the checks page. Neither the label nor the
            # numbers contain a title or a URL, so the line is safe to publish.
            Write-Host ("::notice title=Measurement::" + $line)
        }
        elseif ($line -like 'MEASURE-INFO*') {
            Write-Host $line
        }
        elseif ($line -like 'FAIL *') {
            Write-Host ("::error title=Measurement::" + $line)
        }
    }
    if ($code -ne 0) { throw "Measurement of $Label ($Scenario) failed with exit code $code." }
}

Set-Content -LiteralPath $report -Value ("# Xtension resource comparison, arch $Arch, sample ${Seconds}s per measurement, one runner.") -Encoding ascii
Add-Content -LiteralPath $report -Value ("# baseline=" + (Split-Path -Leaf $Baseline) + " candidate=" + (Split-Path -Leaf $Candidate)) -Encoding ascii
Add-Content -LiteralPath $report -Value ("# os=" + [System.Environment]::OSVersion.Version + " cores=" + [System.Environment]::ProcessorCount) -Encoding ascii

try {
    for ($sample = 1; $sample -le $Repetitions; $sample++) {
        # Alternate order to expose warm-up/order effects instead of always favouring the second process.
        $pairs = if ($sample % 2 -eq 1) { @(@($Baseline, $BaselineLabel), @($Candidate, $CandidateLabel)) } else { @(@($Candidate, $CandidateLabel), @($Baseline, $BaselineLabel)) }
        foreach ($pair in $pairs) {
            Invoke-Measurement -Binary $pair[0] -Label ($pair[1] + '-r' + $sample) -Scenario 'idle-no-browser' -BravePath $null -ProfilePath $null
        }
        foreach ($pair in $pairs) {
            Invoke-Measurement -Binary $pair[0] -Label ($pair[1] + '-r' + $sample) -Scenario 'browser-foreground' -BravePath $brave -ProfilePath $profile
        }
    }
}
finally {
    Stop-Xtension
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $brave } catch { $false }
    } | Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host "Measured evidence written to $report"
Get-Content -LiteralPath $report | ForEach-Object { Write-Host $_ }
