# Shared helpers for build.ps1 and test.ps1. Dot-source this file; it defines functions only.
Set-StrictMode -Version Latest

function Get-FrameworkDirectory {
    # The .NET Framework 4.x compiler and reference assemblies. Framework64 csc.exe can target x86 and x64.
    $directory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    if (-not (Test-Path -LiteralPath (Join-Path $directory 'csc.exe'))) {
        throw "csc.exe not found under $directory. Install the .NET Framework 4.8 Developer Pack or Build Tools."
    }
    return $directory
}

function Get-UiaReferences([string]$FrameworkDirectory) {
    $wpf = Join-Path $FrameworkDirectory 'WPF'
    $client = Join-Path $wpf 'UIAutomationClient.dll'
    $types = Join-Path $wpf 'UIAutomationTypes.dll'
    $base = Join-Path $FrameworkDirectory 'WindowsBase.dll'
    # Fall back to simple names; csc resolves them from the GAC when no file is found.
    return @(
        $(if (Test-Path -LiteralPath $client) { $client } else { 'UIAutomationClient.dll' }),
        $(if (Test-Path -LiteralPath $types) { $types } else { 'UIAutomationTypes.dll' }),
        $(if (Test-Path -LiteralPath $base) { $base } else { 'WindowsBase.dll' })
    )
}

function Get-SourceFiles([string]$Root) {
    # Every shipped C# file under src. The csproj list must match this set (see verify-source.ps1).
    return @(Get-ChildItem -LiteralPath (Join-Path $Root 'src') -Recurse -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object { $_.FullName })
}

function Invoke-Csc([string]$Csc, [string[]]$Arguments) {
    # Captures compiler output so every error line is printed and also raised as a GitHub annotation.
    $output = @(& $Csc @Arguments 2>&1 | ForEach-Object { "$_" })
    $output | ForEach-Object { Write-Host $_ }
    foreach ($line in $output) {
        if ($line -match 'error CS\d+') { Write-Host ("::error title=csc::" + $line) }
    }
    if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE." }
}
