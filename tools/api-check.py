#!/usr/bin/env python3
"""Local compile-risk audit for the Xtension sources (development helper, never shipped).

The verification environment for this project has no C# compiler, so this script
approximates the parts of a compile that catch the mistakes this change set is
most exposed to:

1. every `Type.Member` reference must name a member declared by an Xtension type;
2. every `_host.<member>` call must exist on ISettingsHost (the Settings window
   talks to the application only through that interface);
3. every `<known variable>.<member>` call must exist on that variable's type,
   which is what catches a window calling a host or controller method that was
   never written;
4. every `StringId` used anywhere must exist in the enum, and the text table must
   have exactly one row per enum value, in enum order.

Member attribution is per file: nested classes make exact attribution fragile,
and over-approximating can only hide a finding, never invent one. Every reported
problem is a real identifier, interface or table mismatch.
"""
import re
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]

DECL_TYPES = re.compile(r'\b(?:(?:internal|public|sealed|static|abstract|partial)\s+)*(?:class|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)')
MODIFIERS = r'(?:public|internal|protected|private|static|readonly|sealed|override|virtual|abstract|const|volatile|new|partial|extern|unsafe|event|async)'
MEMBER = re.compile(
    r'^\s*(?:' + MODIFIERS + r'\s+)*'
    r'(?:[A-Za-z_][A-Za-z0-9_<>,\.\[\]\?\s]*?\s+)?'
    r'([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\{|=|;|,|<|$)'
)
KEYWORDS = set('return if while for foreach switch using new else case var do lock throw get set out ref in is as this base null true false try catch finally default'.split())

# Members inherited from the framework bases these types derive from. Declared-member extraction only sees members
# written in this repository, so inherited Control/Form/NotifyIcon/Timer members are listed explicitly.
FRAMEWORK_MEMBERS = set('''
IsDisposed Visible VisibleChanged Show Hide Close Activate BringToFront SendToBack Text Width Height Left Top Right
Bottom Bounds ClientSize Size Location Controls Parent Font ForeColor BackColor Enabled Tag Padding Margin Dock Anchor
AutoSize AutoSizeMode MinimumSize MaximumSize StartPosition FormBorderStyle ShowInTaskbar MaximizeBox MinimizeBox
WindowState DialogResult KeyPreview TopMost Shown FormClosing FormClosed KeyDown KeyUp KeyPress MouseUp MouseDown
MouseMove MouseEnter MouseLeave Click MouseClick Count Length Add AddRange Contains Remove RemoveAt Clear Dispose Set
Reset WaitOne Interval Start Stop Tick Value Minimum Maximum Increment Checked CheckedChanged ValueChanged PerformClick
Invoke BeginInvoke IsHandleCreated CreateControl Handle Items CheckOnClick Icon ShowBalloonTip BalloonTipTitle
BalloonTipText ContextMenuStrip ToString Equals GetHashCode GetType Refresh Invalidate Update Focus Select Save Load
TabStop Cursor FlatStyle UseVisualStyleBackColor FlatAppearance Multiline ReadOnly BorderStyle WordWrap ScrollBars
AccessibleName AccessibleDescription AccessibleRole RightToLeft CausesValidation AutoScroll AutoScrollMinSize
SelectNextControl DialogResult Image ImageIndex ToolTipText ToolTipTitle DropDownItems Owner ShowDialog
'''.split())

# Variables whose type is unambiguous in this code base, plus the types they may hold.
VARIABLE_TYPES = {
    '_host': ['ISettingsHost'],
    '_overlay': ['CloseButtonOverlay'],
    '_controller': ['ActiveTabController'],
    '_app': ['TrayAppContext'],
    '_settings': ['AppSettings'],
    '_settingsStore': ['SettingsStore'],
    '_mainForm': ['MainForm'],
    '_diagnostics': ['DiagnosticsForm'],
    '_settingsForm': ['SettingsForm'],
    '_hover': ['HoverTracker'],
    '_overlayWorker': ['ScanWorker'],
    '_closeWorker': ['ScanWorker'],
    'snapshot': ['ActiveTabSnapshot'],
    'settings': ['AppSettings', 'SettingsStore'],
    'request': ['ScanRequest', 'CloseRequest'],
    'check': ['CloseRequest'],
    'decision': ['CloseDecision'],
    'scan': ['ScanResult'],
    'result': ['ScanResult'],
    'defaults': ['AppSettings'],
    'loaded': ['AppSettings'],
    'store': ['SettingsStore'],
}


def strip_comments(text):
    """Remove // and /* */ comments and string/char literals so text cannot fake declarations."""
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == '/' and i + 1 < n and text[i + 1] == '/':
            while i < n and text[i] != '\n':
                i += 1
        elif c == '/' and i + 1 < n and text[i + 1] == '*':
            i += 2
            while i + 1 < n and not (text[i] == '*' and text[i + 1] == '/'):
                i += 1
            i += 2
        elif c == '"' or (c == '@' and i + 1 < n and text[i + 1] == '"'):
            verbatim = c == '@'
            out.append('""')
            i += 2 if verbatim else 1
            while i < n:
                if not verbatim and text[i] == '\\':
                    i += 2
                    continue
                if text[i] == '"':
                    if verbatim and i + 1 < n and text[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
        elif c == "'":
            out.append("''")
            i += 1
            while i < n:
                if text[i] == '\\':
                    i += 2
                    continue
                if text[i] == "'":
                    i += 1
                    break
                i += 1
        else:
            out.append(c)
            i += 1
    return ''.join(out)


def source_files():
    return sorted(ROOT.glob('src/**/*.cs')) + sorted(ROOT.glob('tests/**/*.cs'))


def main():
    problems = []
    types = {}
    members = {}
    texts = {}

    for path in source_files():
        text = strip_comments(path.read_text(encoding='utf-8'))
        texts[path] = text
        names = set(DECL_TYPES.findall(text))
        file_members = set()
        for line in text.split('\n'):
            match = MEMBER.match(line)
            if match and match.group(1) not in KEYWORDS:
                file_members.add(match.group(1))
            stripped = line.strip()
            enum_member = re.match(r'([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*[^,]+)?,?\s*$', stripped)
            if enum_member and enum_member.group(1) not in KEYWORDS:
                file_members.add(enum_member.group(1))
        for name in names:
            types.setdefault(name, set()).add(str(path.relative_to(ROOT)))
            members.setdefault(name, set()).update(file_members)

    # 1. Static `Type.Member` references.
    reference = re.compile(r'\b([A-Z][A-Za-z0-9_]*)\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)')
    for path, text in texts.items():
        for match in reference.finditer(text):
            owner, member = match.group(1), match.group(2)
            if owner not in types:
                continue
            if member not in members.get(owner, set()):
                problems.append('%s: %s.%s is not declared anywhere in %s'
                                % (path.relative_to(ROOT), owner, member, ', '.join(sorted(types[owner]))))

    # 2 and 3. Instance members of known variables, including ISettingsHost.
    for path, text in texts.items():
        for variable, candidates in VARIABLE_TYPES.items():
            allowed = set()
            for candidate in candidates:
                allowed |= members.get(candidate, set())
            if not allowed:
                continue
            pattern = re.compile(r'(?<![A-Za-z0-9_.])' + re.escape(variable) + r'\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)')
            for match in pattern.finditer(text):
                member = match.group(1)
                if member in allowed or member in FRAMEWORK_MEMBERS:
                    continue
                problems.append('%s: %s.%s is not a member of %s'
                                % (path.relative_to(ROOT), variable, member, '/'.join(candidates)))

    # 4. StringId table alignment and usage.
    strings_path = ROOT / 'src/Core/Strings.cs'
    text = texts[strings_path]
    enum_body = text.split('enum StringId', 1)[1].split('\n    }', 1)[0]
    ids = []
    for line in enum_body.split('\n'):
        stripped = line.strip()
        if not stripped or stripped.startswith('///'):
            continue
        match = re.match(r'^([A-Z][A-Za-z0-9_]*),?$', stripped)
        if match:
            ids.append(match.group(1))
    table = text.split('new string[][]', 1)[1].split('\n        };', 1)[0]
    rows = re.findall(r'new string\[\]\s*\{', table)
    if len(ids) != len(rows):
        problems.append('Strings.cs: %d enum members but %d table rows' % (len(ids), len(rows)))

    used = set()
    for path, text in texts.items():
        if path == strings_path:
            continue
        used.update(re.findall(r'StringId\.([A-Za-z_][A-Za-z0-9_]*)', text))
    for name in sorted(used - set(ids)):
        problems.append('StringId.%s is used but not declared in Strings.cs' % name)

    if problems:
        print('API AUDIT: %d problem(s)' % len(problems))
        for line in problems:
            print('  - ' + line)
        return 1
    print('API audit clean: %d types, %d StringIds, %d table rows' % (len(types), len(ids), len(rows)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
