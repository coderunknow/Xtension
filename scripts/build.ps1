<#
.SYNOPSIS
  Builds Xtension.exe for one Windows architecture: x86 or x64.
.EXAMPLE
  pwsh scripts/build.ps1 -Arch x64
.NOTES
  Output: dist/Xtension-win-<arch>.exe. The version comes only from the VERSION file.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('x86', 'x64')]
    [string]$Arch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchain.ps1')

try {
    & (Join-Path $PSScriptRoot 'update-version.ps1') -RepoRoot $root

    $framework = Get-FrameworkDirectory
    $csc = Join-Path $framework 'csc.exe'
    $sources = Get-SourceFiles $root
    $generated = Join-Path $root 'build\generated\VersionInfo.g.cs'
    $manifest = Join-Path $root 'build\generated\app.manifest'
    if (-not (Test-Path -LiteralPath $generated)) { throw 'VersionInfo.g.cs was not generated.' }
    if (-not (Test-Path -LiteralPath $manifest)) { throw 'app.manifest was not generated.' }
    if ($sources.Count -eq 0) { throw 'No C# sources found under src.' }

    $dist = Join-Path $root 'dist'
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $output = Join-Path $dist ("Xtension-win-$Arch.exe")
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }

    $uia = Get-UiaReferences $framework
    $version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
    Write-Host "Building Xtension $version for Windows $Arch with $csc"

    $arguments = @(
        '/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/deterministic',
        '/debug:pdbonly', '/langversion:5', '/warn:4',
        "/platform:$Arch", "/win32manifest:$manifest",
        '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
        "/r:$($uia[2])", "/r:$($uia[0])", "/r:$($uia[1])",
        "/out:$output"
    ) + $sources + @($generated)

    Invoke-Csc $csc $arguments

    if (-not (Test-Path -LiteralPath $output)) { throw 'Compiler reported success but the output is missing.' }
    $size = (Get-Item -LiteralPath $output).Length
    Write-Host "BUILD SUCCEEDED: $output ($size bytes)"
}
catch {
    Write-Host ("::error title=build script::" + $_.Exception.Message)
    throw
}
