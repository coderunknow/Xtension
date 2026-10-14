<#
.SYNOPSIS
  Compiles and runs the unit tests. Tests cover only the pure core (src/Core); UI and UI Automation are not exercised here.
.NOTES
  Output: dist/tests/Xtension.Tests.exe. Exit code is non-zero if any test fails.
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchain.ps1')

try {
    & (Join-Path $PSScriptRoot 'update-version.ps1') -RepoRoot $root

    $framework = Get-FrameworkDirectory
    $csc = Join-Path $framework 'csc.exe'
    $testDir = Join-Path $root 'dist\tests'
    New-Item -ItemType Directory -Force -Path $testDir | Out-Null
    $output = Join-Path $testDir 'Xtension.Tests.exe'
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }

    $coreSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\Core') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests\UnitTests') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    $generated = Join-Path $root 'build\generated\VersionInfo.g.cs'
    if ($testSources.Count -eq 0) { throw 'No unit test sources found.' }
    Write-Host ("Compiling " + $coreSources.Count + " core and " + $testSources.Count + " test files with " + $csc)

    $arguments = @(
        '/nologo', '/codepage:65001', '/target:exe', '/debug:pdbonly', '/langversion:5', '/warn:4',
        '/r:System.dll', '/r:System.Drawing.dll',
        "/out:$output"
    ) + $coreSources + $testSources + @($generated)

    Invoke-Csc $csc $arguments

    $results = @(& $output 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    $results | ForEach-Object { Write-Host $_ }
    foreach ($line in $results) {
        if ($line -match '^\d+ passed, \d+ failed\.$') { Write-Host ("::notice title=Core test evidence::" + $line) }
        if ($line -like 'FAIL *') { Write-Host ("::error title=unit test::" + $line) }
    }
    if ($exitCode -ne 0) { throw "Unit tests failed (exit code $exitCode)." }
    Write-Host 'UNIT TESTS PASSED'
}
catch {
    Write-Host ("::error title=test script::" + $_.Exception.Message)
    throw
}
