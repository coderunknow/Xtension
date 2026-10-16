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
    $icon = Join-Path $root 'assets\xtension.ico'
    if (-not (Test-Path -LiteralPath $icon)) { throw 'assets\xtension.ico is missing; run tools/generate-icon.py.' }
    $version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
    Write-Host "Building Xtension $version for Windows $Arch with $csc"

    $arguments = @(
        '/nologo', '/codepage:65001', '/target:winexe', '/optimize+',
        '/debug:pdbonly', '/langversion:5', '/warn:4',
        "/platform:$Arch", "/win32manifest:$manifest",
        "/win32icon:$icon",
        "/resource:$icon,Xtension.AppIcon.ico",
        '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
        "/r:$($uia[2])", "/r:$($uia[0])", "/r:$($uia[1])",
        "/out:$output"
    ) + $sources + @($generated)

    Invoke-Csc $csc $arguments

    if (-not (Test-Path -LiteralPath $output)) { throw 'Compiler reported success but the output is missing.' }
    $commit = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify build source commit.' }
    $dirty = @(& git -C $root status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect build source tree.' }
    $stamp = [ordered]@{
        version = $version
        architecture = $Arch
        commit = $commit
        dirty = ($dirty.Count -gt 0)
        sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $stamp | ConvertTo-Json | Set-Content -LiteralPath ($output + '.build.json') -Encoding ASCII
    $size = (Get-Item -LiteralPath $output).Length
    Write-Host "BUILD SUCCEEDED: $output ($size bytes)"
}
catch {
    Write-Host ("::error title=build script::" + $_.Exception.Message)
    throw
}
