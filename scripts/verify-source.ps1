<#
.SYNOPSIS
  Static checks that need no compiler: required files, single version source, C# 5 language rules,
  csproj/source agreement, manifest and workflow sanity, and no secrets or private data in the tree.
  Passing these checks does NOT prove that the code compiles or that Brave behaves as expected.
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$Message) { $failures.Add($Message) | Out-Null }

$required = @(
    'LICENSE', 'VERSION', 'README.md', 'CHANGELOG.md', 'DESIGN_LOG.md', 'AGENTS_LOG.md', 'AGENTS.md',
    'Xtension.csproj', 'app.manifest.template', '.gitignore', '.gitattributes',
    'scripts\build.ps1', 'scripts\test.ps1', 'scripts\toolchain.ps1', 'scripts\update-version.ps1', 'scripts\verify-source.ps1',
    'scripts\package.ps1', 'scripts\measure-performance.ps1', 'docs\RELEASE_CAPABILITIES.md', 'docs\DEV3_AUDIT.md',
    'tests\UnitTests\TestFramework.cs', 'tests\UnitTests\Program.cs',
    '.github\workflows\ci.yml', '.github\workflows\release.yml'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) { Add-Failure "Required file missing: $relative" }
}

# Version: one source (VERSION). Literal versions elsewhere would drift.
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') { Add-Failure "VERSION is not SemVer: '$version'" }
$versionedFiles = @('README.md', 'CHANGELOG.md')
foreach ($file in $versionedFiles) {
    $path = Join-Path $root $file
    if (Test-Path -LiteralPath $path) {
        $text = Get-Content -LiteralPath $path -Raw
        if ($text -match '\b0\.1\.0\b(?!-)' ) { Add-Failure "$file contains a stale bare 0.1.0 version literal." }
    }
}
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -Filter '*.cs' -File)
$sourceTexts = @{}
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($root.Length + 1)
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $sourceTexts[$relative] = $text
    if ($text -cmatch '\b0\.1\.0\b') { Add-Failure "$relative contains a hard-coded version literal; use ProductVersion." }
    if ($text -cmatch 'BraveActiveCloseButton|Brave Active Tab|MainForm\.AppVersion') { Add-Failure "$relative contains a legacy name." }
    if ($text -cmatch 'Process\.GetProcessById') { Add-Failure "$relative calls Process.GetProcessById; use ProcessIdentity." }
    if ($text -cmatch 'TryCloseActiveTab') { Add-Failure "$relative references the removed unsafe close path." }
    if ($text -cmatch 'Thread\.Sleep') { Add-Failure "$relative uses Thread.Sleep; use an event or timer." }
}

# C# 5 rules: the .NET Framework 4.8 csc used by build.ps1 is C# 5 at most.
# dev3: every user-visible string lives in the string table, so a language switch cannot miss a string in UI code.
foreach ($entry in $sourceTexts.GetEnumerator()) {
    if ($entry.Key -eq 'src\Core\Strings.cs') { continue }
    if ($entry.Value -match '[\u0102\u0103\u0110\u0111\u0128\u0129\u0168\u0169\u01A0\u01A1\u01AF\u01B0\u1EA0-\u1EF9]') {
        Add-Failure "$($entry.Key) contains Vietnamese text outside src\Core\Strings.cs; user-visible text belongs in the string table."
    }
}
if ($sourceTexts.ContainsKey('src\UI\SettingsForm.cs')) {
    if ($sourceTexts['src\UI\SettingsForm.cs'] -cmatch 'TrayAppContext') {
        Add-Failure 'The settings window must talk to the application through ISettingsHost only.'
    }
}
if ($sourceTexts.ContainsKey('src\Interop\NativeMethods.cs')) {
    if ($sourceTexts['src\Interop\NativeMethods.cs'] -cmatch 'SetCursorPos|mouse_event|SendInput\s*\(\s*MOUSE') {
        Add-Failure 'NativeMethods must not move the pointer or inject pointer input; hover reads the pointer only.'
    }
}

$csharp5Bans = @(
    @{ Pattern = '\?\.'; Name = 'null-conditional operator (?.)' },
    @{ Pattern = '\$"'; Name = 'interpolated string' },
    @{ Pattern = '\bnameof\s*\('; Name = 'nameof' },
    @{ Pattern = '\bout\s+var\b'; Name = 'out var' },
    @{ Pattern = '\bwhen\s*\('; Name = 'pattern when filter' }
)
foreach ($entry in $sourceTexts.GetEnumerator()) {
    # Strip comments and string literals before checking, so documentation does not trip the rules.
    $code = [regex]::Replace($entry.Value, '//[^\r\n]*', '')
    $code = [regex]::Replace($code, '"(?:\\.|[^"\\])*"', '""')
    foreach ($ban in $csharp5Bans) {
        if ($code -cmatch $ban.Pattern) { Add-Failure "$($entry.Key) uses $($ban.Name), which is not allowed in C# 5." }
    }
    if ($code -cmatch '(?m)^\s*(public|private|internal|protected)\b[^=;{]*\)\s*=>') {
        Add-Failure "$($entry.Key) appears to use an expression-bodied member, which is not allowed in C# 5."
    }
}

# csproj and build.ps1 must agree with the source tree.
$projectPath = Join-Path $root 'Xtension.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$compileItems = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object { $_.GetAttribute('Include').Replace('/', '\') })
$expected = @($sourceFiles | ForEach-Object { $_.FullName.Substring($root.Length + 1) })
$expected += 'build\generated\VersionInfo.g.cs'
foreach ($item in $expected) { if ($compileItems -notcontains $item) { Add-Failure "Xtension.csproj does not compile $item" } }
foreach ($item in $compileItems) { if ($expected -notcontains $item) { Add-Failure "Xtension.csproj lists a missing or unexpected file: $item" } }
$langVersion = $project.SelectSingleNode("//*[local-name()='LangVersion']")
if (-not $langVersion -or $langVersion.InnerText -ne '5') { Add-Failure 'Xtension.csproj must set LangVersion 5.' }
$framework = $project.SelectSingleNode("//*[local-name()='TargetFrameworkVersion']")
if (-not $framework -or $framework.InnerText -ne 'v4.8') { Add-Failure 'Xtension.csproj must target v4.8.' }
$buildScript = Get-Content -LiteralPath (Join-Path $root 'scripts\build.ps1') -Raw
if ($buildScript -cnotmatch "'/langversion:5'") { Add-Failure 'scripts/build.ps1 does not pass /langversion:5.' }

# Manifest: a template with a version token, and a sane identity.
$manifestTemplate = Get-Content -LiteralPath (Join-Path $root 'app.manifest.template') -Raw
if ($manifestTemplate -notmatch '@ASSEMBLY_VERSION@') { Add-Failure 'app.manifest.template has no @ASSEMBLY_VERSION@ token.' }
if ($manifestTemplate -notmatch 'requestedExecutionLevel level="asInvoker"') { Add-Failure 'Manifest must request asInvoker (no administrator rights).' }
if ($manifestTemplate -notmatch 'PerMonitorV2') { Add-Failure 'Manifest must declare PerMonitorV2 DPI awareness.' }
if ($manifestTemplate -match 'uiAccess="true"') { Add-Failure 'Manifest must not request uiAccess.' }

# Product rules that must not regress.
$nativeText = if ($sourceTexts.ContainsKey('src\Interop\NativeMethods.cs')) { $sourceTexts['src\Interop\NativeMethods.cs'] } else { '' }
if ($nativeText -cmatch 'Mouse|MOUSEEVENTF') { Add-Failure 'Mouse injection found in NativeMethods; the close path must use keyboard only.' }
$overlayText = if ($sourceTexts.ContainsKey('src\UI\CloseButtonOverlay.cs')) { $sourceTexts['src\UI\CloseButtonOverlay.cs'] } else { '' }
if ($overlayText -cnotmatch 'WsExNoActivate' -or $overlayText -cnotmatch 'WmMouseActivate') { Add-Failure 'Overlay must be non-activating (WS_EX_NOACTIVATE and WM_MOUSEACTIVATE).' }
$logText = if ($sourceTexts.ContainsKey('src\Core\AppLog.cs')) { $sourceTexts['src\Core\AppLog.cs'] } else { '' }
$logCode = [regex]::Replace($logText, '//[^\r\n]*', '')
if ($logCode -cmatch 'Title') { Add-Failure 'AppLog code references window or tab titles; private titles must stay out of logs.' }
$injectorText = if ($sourceTexts.ContainsKey('src\Interop\KeyboardInjector.cs')) { $sourceTexts['src\Interop\KeyboardInjector.cs'] } else { '' }
if ($injectorText -cnotmatch 'VkShift' -or $injectorText -cnotmatch 'VkMenu' -or $injectorText -cnotmatch 'VkLeftWin' -or $injectorText -cnotmatch 'VkRightWin') { Add-Failure 'KeyboardInjector must block on Shift, Alt and Win.' }
$startupText = if ($sourceTexts.ContainsKey('src\App\StartupRegistration.cs')) { $sourceTexts['src\App\StartupRegistration.cs'] } else { '' }
if ($startupText -cnotmatch 'CurrentUser') { Add-Failure 'Startup registration must use the per-user Run key only.' }

# Workflows: tag-triggered release, prerelease handling, checksums, and no secrets in logs.
$release = Get-Content -LiteralPath (Join-Path $root '.github\workflows\release.yml') -Raw
if ($release -notmatch "tags:\s*\r?\n\s*-\s*'v\*'") { Add-Failure 'release.yml must trigger on v* tags.' }
if ($release -notmatch 'prerelease') { Add-Failure 'release.yml must publish a prerelease.' }
$packageScript = Get-Content -LiteralPath (Join-Path $root 'scripts\package.ps1') -Raw
if ($packageScript -notmatch 'SHA256SUMS') { Add-Failure 'scripts/package.ps1 must write SHA256SUMS.txt.' }
if ($release -notmatch 'SHA256SUMS') { Add-Failure 'release.yml must show or publish SHA256SUMS.txt.' }
if ($release -match 'make_latest:\s*true|--latest(?!=false)') { Add-Failure 'release.yml must never mark the release as latest.' }
$ci = Get-Content -LiteralPath (Join-Path $root '.github\workflows\ci.yml') -Raw
if ($ci -notmatch 'verify-source\.ps1') { Add-Failure 'ci.yml does not run verify-source.ps1.' }
if ($ci -notmatch 'test\.ps1') { Add-Failure 'ci.yml does not run the unit tests.' }
if ($ci -notmatch 'x86' -or $ci -notmatch 'x64') { Add-Failure 'ci.yml must build both x86 and x64.' }
if ($ci -notmatch 'measure-performance\.ps1') { Add-Failure 'ci.yml does not run the published-vs-candidate resource measurement.' }

# Windows PowerShell 5.1 reads BOM-less scripts as ANSI, so every .ps1 must stay pure ASCII.
foreach ($script in @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1' -File)) {
    $bytes = [System.IO.File]::ReadAllBytes($script.FullName)
    foreach ($byte in $bytes) {
        if ($byte -gt 127) { Add-Failure "scripts/$($script.Name) contains non-ASCII bytes; keep PowerShell scripts ASCII."; break }
    }
}

# No secrets or private artifacts in the tree.
$secretPatterns = @('ghp_[A-Za-z0-9]{20,}', 'github_pat_[A-Za-z0-9_]{20,}', 'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY', 'AKIA[0-9A-Z]{16}')
$trackedFiles = @(git -C $root ls-files --cached --others --exclude-standard 2>$null)
foreach ($relative in $trackedFiles) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    if ((Get-Item -LiteralPath $path).Length -gt 1048576) { continue }
    $text = Get-Content -LiteralPath $path -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $secretPatterns) {
        if ($text -cmatch $pattern) { Add-Failure "Possible secret in $relative (pattern $pattern)." }
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "FAIL: $failure" -ForegroundColor Red
        Write-Host ("::error title=source check::" + $failure)
    }
    throw "Source verification failed with $($failures.Count) problem(s)."
}

Write-Host 'Xtension static source checks passed.' -ForegroundColor Green
Write-Host "Version: v$version"
Write-Host 'Scope: static checks only. They do not prove compilation, unit test results, or Brave/UI behaviour.' -ForegroundColor Yellow
