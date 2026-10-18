# Disposable-runner fixture execution. Files avoid inherited stdout pipes held by browser descendants.
# Dot-source only. Callers filter returned lines before publishing evidence; stderr is never forwarded.
function Invoke-FixtureProcess {
    param([string]$Executable, [string[]]$Arguments, [string]$OutputPrefix, [int]$TimeoutSeconds)
    $stdout = $OutputPrefix + '.stdout.txt'
    $stderr = $OutputPrefix + '.stderr.txt'
    $quoted = @($Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $process = Start-Process -FilePath $Executable -ArgumentList $quoted -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    try {
        # Cache the handle before waiting so Windows PowerShell retains a readable ExitCode after process exit.
        $process.Handle | Out-Null
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            throw 'Fixture process exceeded its bounded execution deadline.'
        }
        $exitCode = $process.ExitCode
        # A browser descendant can still have the file open. Read available bytes with sharing rather than waiting
        # for every inherited pipe handle to close. Browser cleanup remains the owning script's finally action.
        $stream = New-Object System.IO.FileStream($stdout, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $reader = New-Object System.IO.StreamReader($stream)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{ ExitCode = $exitCode; Lines = @($text -split '\r?\n') }
    } finally { $process.Dispose() }
}
