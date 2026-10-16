#!/usr/bin/env python3
"""Local mirror of scripts/verify-source.ps1 (development helper, never shipped).

The real static gate runs in Windows CI. This mirror exists because the audit
environment for this project has no PowerShell, and because a failing static
check should be found before a push. It implements the same rules, plus the
localisation rule added in dev3 (Vietnamese characters are only allowed in
src/Core/Strings.cs, so user-visible text cannot drift back into UI code).

Usage: python3 tools/verify-source.py   (exit code 0 = all rules pass)
"""
import pathlib
import re
import struct
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
failures = []

REQUIRED = [
    'LICENSE', 'VERSION', 'README.md', 'CHANGELOG.md', 'DESIGN_LOG.md', 'AGENTS_LOG.md', 'AGENTS.md',
    'Xtension.csproj', 'app.manifest.template', '.gitignore', '.gitattributes',
    'scripts/build.ps1', 'scripts/test.ps1', 'scripts/toolchain.ps1', 'scripts/update-version.ps1',
    'scripts/verify-source.ps1', 'scripts/package.ps1', 'scripts/measure-performance.ps1',
    'docs/RELEASE_CAPABILITIES.md', 'docs/DEV3_AUDIT.md', 'docs/DEV4_AUDIT.md',
    'assets/xtension.ico', 'assets/xtension-256.png', 'tools/generate-icon.py',
    'tests/UnitTests/TestFramework.cs', 'tests/UnitTests/Program.cs',
    '.github/workflows/ci.yml', '.github/workflows/release.yml',
]

C_SHARP5_BANS = [
    (r'\?\.', 'null-conditional operator (?.)'),
    (r'\$"', 'interpolated string'),
    (r'\bnameof\s*\(', 'nameof'),
    (r'\bout\s+var\b', 'out var'),
    (r'\bwhen\s*\(', 'pattern when filter'),
]

# Letters that only occur in Vietnamese text (plus the Latin Extended Additional range used by it).
VIETNAMESE = re.compile('[\u0102\u0103\u0110\u0111\u0128\u0129\u0168\u0169\u01a0\u01a1\u01af\u01b0\u1ea0-\u1ef9]')


def strip_code(text):
    text = re.sub(r'//[^\r\n]*', '', text)
    text = re.sub(r'"(?:\\.|[^"\\])*"', '""', text)
    return text


def main():
    for relative in REQUIRED:
        if not (ROOT / relative).is_file():
            failures.append('Required file missing: %s' % relative)

    version = (ROOT / 'VERSION').read_text(encoding='utf-8').strip()
    if not re.match(r'^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$', version):
        failures.append("VERSION is not SemVer: '%s'" % version)
    for name in ('README.md', 'CHANGELOG.md'):
        text = (ROOT / name).read_text(encoding='utf-8')
        if re.search(r'\b0\.1\.0\b(?!-)', text):
            failures.append('%s contains a stale bare 0.1.0 version literal.' % name)

    sources = {}
    for path in sorted((ROOT / 'src').rglob('*.cs')):
        text = path.read_text(encoding='utf-8')
        relative = str(path.relative_to(ROOT)).replace('/', '\\')
        sources[relative] = text
        if re.search(r'\b0\.1\.0\b', text):
            failures.append('%s contains a hard-coded version literal; use ProductVersion.' % relative)
        if re.search(r'BraveActiveCloseButton|Brave Active Tab|MainForm\.AppVersion', text):
            failures.append('%s contains a legacy name.' % relative)
        if 'Process.GetProcessById' in text:
            failures.append('%s calls Process.GetProcessById; use ProcessIdentity.' % relative)
        if 'TryCloseActiveTab' in text:
            failures.append('%s references the removed unsafe close path.' % relative)
        if 'Thread.Sleep' in text:
            failures.append('%s uses Thread.Sleep; use an event or timer.' % relative)
        if relative.replace('\\', '/') != 'src/Core/Strings.cs' and VIETNAMESE.search(text):
            failures.append('%s contains Vietnamese text outside src/Core/Strings.cs; user-visible text belongs in the '
                            'string table.' % relative)

        code = strip_code(text)
        for pattern, name in C_SHARP5_BANS:
            if re.search(pattern, code):
                failures.append('%s uses %s, which is not allowed in C# 5.' % (relative, name))
        if re.search(r'(?m)^\s*(public|private|internal|protected)\b[^=;{]*\)\s*=>', code):
            failures.append('%s appears to use an expression-bodied member, which is not allowed in C# 5.' % relative)

    csproj = (ROOT / 'Xtension.csproj').read_text(encoding='utf-8')
    compile_items = re.findall(r'<Compile Include="([^"]+)"', csproj)
    compile_items = [item.replace('/', '\\') for item in compile_items]
    expected = [str(path.relative_to(ROOT)).replace('/', '\\') for path in sorted((ROOT / 'src').rglob('*.cs'))]
    expected.append('build\\generated\\VersionInfo.g.cs')
    for item in expected:
        if item not in compile_items:
            failures.append('Xtension.csproj does not compile %s' % item)
    for item in compile_items:
        if item not in expected:
            failures.append('Xtension.csproj lists a missing or unexpected file: %s' % item)
    if '<LangVersion>5</LangVersion>' not in csproj:
        failures.append('Xtension.csproj must set LangVersion 5.')
    if '<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>' not in csproj:
        failures.append('Xtension.csproj must target v4.8.')

    # The application icon: one multi-size resource, embedded under the name the loader asks for, referenced by both
    # build scripts. A missing or renamed resource would silently fall back to the painted tray icon, which is exactly
    # the "crisp small sizes" requirement failing quietly.
    icon_path = ROOT / 'assets/xtension.ico'
    if icon_path.is_file():
        data = icon_path.read_bytes()
        if len(data) < 22 or data[0] != 0 or data[1] != 0 or data[2] != 1 or data[3] != 0:
            failures.append('assets/xtension.ico is not a Windows icon file.')
        else:
            count = struct.unpack_from('<H', data, 4)[0]
            documented = {16, 20, 24, 32, 40, 48, 64, 128, 256}
            sizes = set()
            for index in range(count):
                if 22 + 16 * index > len(data):
                    failures.append('assets/xtension.ico is truncated.')
                    break
                width, height, colours, reserved, planes, bits, length, offset = struct.unpack_from('<BBBBHHII', data, 6 + 16 * index)
                sizes.add(width or 256)
                if bits != 32:
                    failures.append('assets/xtension.ico frame %d is not a 32-bit image.' % (width or 256))
                if offset + length > len(data):
                    failures.append('assets/xtension.ico frame %d runs past the end of the file.' % (width or 256))
            if count != len(documented):
                failures.append('assets/xtension.ico must contain the %d documented sizes (found %d).' % (len(documented), count))
            if sizes != documented:
                failures.append('assets/xtension.ico sizes are %s, expected %s.' % (sorted(sizes), sorted(documented)))
    icon_loader = sources.get('src\\UI\\AppIcon.cs', '')
    resource_name = None
    match = re.search(r'ResourceName\s*=\s*"([^"]+)"', icon_loader)
    if match:
        resource_name = match.group(1)
    if not resource_name:
        failures.append('src/UI/AppIcon.cs must declare the resource name it loads.')
    elif '<LogicalName>%s</LogicalName>' % resource_name not in csproj:
        failures.append('Xtension.csproj must embed assets/xtension.ico as %s.' % resource_name)
    if 'assets\\xtension.ico' not in csproj:
        failures.append('Xtension.csproj must reference assets/xtension.ico.')
    for script in ('scripts/build.ps1', 'scripts/test-windows.ps1'):
        text = (ROOT / script).read_text(encoding='utf-8')
        if '/resource:' not in text or '/win32icon:' not in text or 'assets\\xtension.ico' not in text:
            failures.append('%s must embed the application icon resource.' % script)

    # Multi-platform claims: the product is Windows-only, and no document may advertise a platform that has no
    # implementation. Every line that names macOS or Linux has to carry one of the negative markers below, so a future
    # optimistic sentence fails this check instead of shipping as an unverified claim.
    negative = ('not supported', 'unsupported', 'blocked', 'not implemented', 'not verified', 'cannot', 'no ',
                'nothing', 'would ', 'plan', 'limit', 'deferred', 'without')
    for name in ('README.md', 'CHANGELOG.md', 'docs/RELEASE_CAPABILITIES.md', 'docs/DEV3_AUDIT.md', 'docs/DEV4_AUDIT.md'):
        path = ROOT / name
        if not path.is_file():
            continue
        for number, line in enumerate(path.read_text(encoding='utf-8').split('\n'), 1):
            if 'macOS' not in line and 'Linux' not in line:
                continue
            lowered = line.lower()
            if not any(marker in lowered for marker in negative):
                failures.append('%s line %d names a platform without stating that it is not supported: %s'
                                % (name, number, line.strip()[:120]))

    harness = (ROOT / 'scripts/test-windows.ps1').read_text(encoding='utf-8')
    for path in sorted((ROOT / 'tests/WindowsTests').glob('*.cs')):
        if path.name not in harness:
            failures.append('scripts/test-windows.ps1 does not compile tests\\WindowsTests\\%s.' % path.name)
    for script in sorted((ROOT / 'scripts').glob('*.ps1')):
        text = script.read_text(encoding='utf-8')
        for found in re.finditer(r'tests\\+[A-Za-z0-9_\\]+\.cs', text):
            relative = re.sub(r'\\+', '\\\\', found.group(0))
            if not (ROOT / relative.replace('\\', '/')).is_file():
                failures.append('%s references a missing file: %s' % (script.name, relative))

    build_script = (ROOT / 'scripts/build.ps1').read_text(encoding='utf-8')
    if "'/langversion:5'" not in build_script:
        failures.append('scripts/build.ps1 does not pass /langversion:5.')

    manifest = (ROOT / 'app.manifest.template').read_text(encoding='utf-8')
    if '@ASSEMBLY_VERSION@' not in manifest:
        failures.append('app.manifest.template has no @ASSEMBLY_VERSION@ token.')
    if 'requestedExecutionLevel level="asInvoker"' not in manifest:
        failures.append('Manifest must request asInvoker (no administrator rights).')
    if 'PerMonitorV2' not in manifest:
        failures.append('Manifest must declare PerMonitorV2 DPI awareness.')
    if 'uiAccess="true"' in manifest:
        failures.append('Manifest must not request uiAccess.')

    native = sources.get('src\\Interop\\NativeMethods.cs', '')
    if re.search('Mouse|MOUSEEVENTF', native):
        failures.append('Mouse injection found in NativeMethods; the close path must use keyboard only.')
    if re.search(r'SetCursorPos|mouse_event|SendInput\s*\(\s*MOUSE', native):
        failures.append('NativeMethods must not move the pointer or inject pointer input; hover reads the pointer only.')
    if 'TrayAppContext' in sources.get('src\\UI\\SettingsForm.cs', ''):
        failures.append('The settings window must talk to the application through ISettingsHost only.')
    overlay = sources.get('src\\UI\\CloseButtonOverlay.cs', '')
    if 'WsExNoActivate' not in overlay or 'WmMouseActivate' not in overlay:
        failures.append('Overlay must be non-activating (WS_EX_NOACTIVATE and WM_MOUSEACTIVATE).')
    log_code = strip_code(sources.get('src\\Core\\AppLog.cs', ''))
    if re.search('Title', log_code):
        failures.append('AppLog code references window or tab titles; private titles must stay out of logs.')
    injector = sources.get('src\\Interop\\KeyboardInjector.cs', '')
    for symbol in ('VkShift', 'VkMenu', 'VkLeftWin', 'VkRightWin'):
        if symbol not in injector:
            failures.append('KeyboardInjector must block on Shift, Alt and Win.')
    if 'CurrentUser' not in sources.get('src\\App\\StartupRegistration.cs', ''):
        failures.append('Startup registration must use the per-user Run key only.')

    release = (ROOT / '.github/workflows/release.yml').read_text(encoding='utf-8')
    if not re.search(r"tags:\s*\r?\n\s*-\s*'v\*'", release):
        failures.append('release.yml must trigger on v* tags.')
    if 'prerelease' not in release:
        failures.append('release.yml must publish a prerelease.')
    if 'SHA256SUMS' not in (ROOT / 'scripts/package.ps1').read_text(encoding='utf-8'):
        failures.append('scripts/package.ps1 must write SHA256SUMS.txt.')
    if 'SHA256SUMS' not in release:
        failures.append('release.yml must show or publish SHA256SUMS.txt.')
    if re.search(r'make_latest:\s*true|--latest(?!=false)', release):
        failures.append('release.yml must never mark the release as latest.')
    ci = (ROOT / '.github/workflows/ci.yml').read_text(encoding='utf-8')
    if 'verify-source.ps1' not in ci:
        failures.append('ci.yml does not run verify-source.ps1.')
    if not re.search(r'test\.ps1', ci):
        failures.append('ci.yml does not run the unit tests.')
    if 'x86' not in ci or 'x64' not in ci:
        failures.append('ci.yml must build both x86 and x64.')
    if not re.search(r'measure-performance\.ps1', ci):
        failures.append('ci.yml does not run the published-vs-candidate resource measurement.')

    for script in sorted((ROOT / 'scripts').glob('*.ps1')):
        data = script.read_bytes()
        if any(byte > 127 for byte in data):
            failures.append('scripts/%s contains non-ASCII bytes; keep PowerShell scripts ASCII.' % script.name)
            break

    secret_patterns = [r'ghp_[A-Za-z0-9]{20,}', r'github_pat_[A-Za-z0-9_]{20,}',
                       r'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY', r'AKIA[0-9A-Z]{16}']
    try:
        tracked = subprocess.check_output(['git', '-C', str(ROOT), 'ls-files', '--cached', '--others',
                                           '--exclude-standard'], text=True).split('\n')
    except subprocess.CalledProcessError:
        tracked = []
    for relative in tracked:
        path = ROOT / relative
        if not relative or not path.is_file() or path.stat().st_size > 1048576:
            continue
        try:
            text = path.read_text(encoding='utf-8', errors='ignore')
        except OSError:
            continue
        for pattern in secret_patterns:
            if re.search(pattern, text):
                failures.append('Possible secret in %s (pattern %s).' % (relative, pattern))

    if failures:
        print('SOURCE CHECK: %d problem(s)' % len(failures))
        for line in failures:
            print('  FAIL: ' + line)
        return 1
    print('Xtension static source checks passed (local mirror). Version: v%s' % version)
    return 0


if __name__ == '__main__':
    sys.exit(main())
