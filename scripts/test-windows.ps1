# Synthetic WinForms/UIA regressions, NOT real-Brave acceptance. Windows PowerShell 5.1 compatible.
[CmdletBinding()]
param([ValidateSet('x86', 'x64')][string]$Arch = 'x64')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchain.ps1')
try {
    & (Join-Path $PSScriptRoot 'update-version.ps1') -RepoRoot $root
    $framework = Get-FrameworkDirectory
    $uia = Get-UiaReferences $framework
    $directory = Join-Path $root 'dist\tests'
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $output = Join-Path $directory "Xtension.WindowsTests-$Arch.exe"
    $arguments = @(
        '/nologo', '/codepage:65001', '/target:exe', '/langversion:5', '/warn:4',
        '/main:Xtension.Tests.WindowsEntry', "/platform:$Arch", "/out:$output",
        '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
        "/r:$($uia[0])", "/r:$($uia[1])", "/r:$($uia[2])",
        "/win32manifest:$(Join-Path $root 'build\generated\app.manifest')"
    ) + (Get-SourceFiles $root) + @(
        (Join-Path $root 'build\generated\VersionInfo.g.cs'),
        (Join-Path $root 'tests\UnitTests\TestFramework.cs'),
        (Join-Path $root 'tests\WindowsTests\Program.cs')
    )
    Invoke-Csc (Join-Path $framework 'csc.exe') $arguments
    $results = @(& $output 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    $results | ForEach-Object {
        Write-Host $_
        if ($_ -like 'FAIL *') { Write-Host ("::error title=Windows regression::" + $_) }
        if ($_ -like 'Legacy cache failure:*' -or $_ -like '*passed, *failed.*') { Write-Host ("::notice title=Windows evidence::" + $_) }
    }
    if ($exitCode -ne 0) { throw "Windows regression tests failed ($exitCode)." }
}
catch {
    Write-Host ("::error title=Windows tests::" + $_.Exception.Message)
    throw
}
