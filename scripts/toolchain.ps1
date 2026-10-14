# Shared helpers for build.ps1 and test.ps1. Dot-source this file; it defines functions only.
Set-StrictMode -Version Latest

function Get-FrameworkDirectory {
    # The .NET Framework 4.x compiler and reference assemblies. Framework64 csc.exe can target x86 and x64.
    foreach ($name in @('Framework64', 'Framework')) {
        $directory = Join-Path $env:WINDIR "Microsoft.NET\$name\v4.0.30319"
        if (Test-Path -LiteralPath (Join-Path $directory 'csc.exe')) { return $directory }
    }
    throw 'The .NET Framework compiler was not found. Install the .NET Framework 4.8 Developer Pack or Build Tools.'

}

function Resolve-FrameworkAssembly([string]$FrameworkDirectory, [string]$FileName) {
    # On .NET 4.x, WindowsBase and the UIA assemblies live under the WPF subfolder. Check there first.
    foreach ($candidate in @((Join-Path $FrameworkDirectory "WPF\$FileName"), (Join-Path $FrameworkDirectory $FileName))) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "Reference assembly $FileName not found under $FrameworkDirectory or its WPF subfolder."
}

function Get-UiaReferences([string]$FrameworkDirectory) {
    # Returns UIAutomationClient, UIAutomationTypes, WindowsBase as full paths. Fails if any is missing.
    return @(
        (Resolve-FrameworkAssembly $FrameworkDirectory 'UIAutomationClient.dll'),
        (Resolve-FrameworkAssembly $FrameworkDirectory 'UIAutomationTypes.dll'),
        (Resolve-FrameworkAssembly $FrameworkDirectory 'WindowsBase.dll')
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
