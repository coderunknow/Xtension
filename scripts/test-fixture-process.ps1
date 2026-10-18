# Regression for bounded fixture process I/O. No browser or personal data.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'fixture-process.ps1')
$directory = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\fixture-process-tests'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$parent = Join-Path $directory 'parent.ps1'
@'
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = 'powershell.exe'
$info.Arguments = '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"'
$info.UseShellExecute = $false
$child = [System.Diagnostics.Process]::Start($info)
Write-Output ('CHILD_PID=' + $child.Id)
Write-Output 'PARENT_COMPLETE'
exit 7
'@ | Set-Content -LiteralPath $parent -Encoding ASCII
$childId = 0
try {
    $result = Invoke-FixtureProcess -Executable 'powershell.exe' -Arguments @('-NoProfile', '-NonInteractive', '-File', $parent) -OutputPrefix (Join-Path $directory 'capture') -TimeoutSeconds 15
    $pidLine = @($result.Lines | Where-Object { $_ -like 'CHILD_PID=*' })
    if ($pidLine.Count -ne 1) { throw 'Fixture child identity missing.' }
    $childId = [int]($pidLine[0].Substring(10))
    if ($result.ExitCode -ne 7 -or $result.Lines -notcontains 'PARENT_COMPLETE') { throw 'Fixture exit code/output was lost.' }
    if (-not (Get-Process -Id $childId -ErrorAction SilentlyContinue)) { throw 'Child must still be running when parent result is collected.' }
    $timedOut = $false
    try {
        Invoke-FixtureProcess -Executable 'powershell.exe' -Arguments @('-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 30') -OutputPrefix (Join-Path $directory 'timeout') -TimeoutSeconds 1 | Out-Null
    } catch {
        if ($_.Exception.Message -ne 'Fixture process exceeded its bounded execution deadline.') { throw }
        $timedOut = $true
    }
    if (-not $timedOut) { throw 'A hung fixture did not reach its deadline.' }
    Write-Host '::notice title=Fixture process evidence::Exit code, output, surviving descendant and deadline regression passed.'
} finally {
    if ($childId -gt 0) { Stop-Process -Id $childId -Force -ErrorAction SilentlyContinue }
}
