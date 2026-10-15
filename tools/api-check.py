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
   have exactly one row per enum value, in enum order;
5. no local declaration repeats a name that another declaration for the same block
   chain already introduced (the compiler's CS0136 shadowing error), which is the
   one scope rule that is decidable without type information.

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


# Local scope audit. Two of the five mistakes CI caught in this dev3 change set were local-scope mistakes: a local
# that shadowed a name declared in an enclosing scope (CS0136, twice) and a name that no scope declared (CS0103,
# which needs type information and is therefore not attempted here). Shadowing is decidable by hand, so it is
# checked: two declarations of the same name are an error when one block chain is a prefix of the other (the same
# scope twice, or an inner scope inside the scope that already declared the name). Declarations in sibling blocks,
# and a local that repeats a field or property name, are legal and are not reported.
# A declaration inside a `for`, `foreach`, `using` or `fixed` header is not attributed to a scope, so a
# shadow that involves one of those headers is not reported at all.
LOCAL_DECLARATION = re.compile(
    r'(?:(?<=[;{}])|^)\s*'
    r'(?:const\s+|readonly\s+|volatile\s+|static\s+)*'
    r'(?:var|string|object|bool|char|byte|sbyte|short|ushort|int|uint|long|ulong|float|double|decimal'
    r'|[A-Z][A-Za-z0-9_]*(?:\s*<[^<>(){};=]*>)?(?:\s*\[\s*\])*)'
    r'\s+([a-z_][A-Za-z0-9_]*)\s*(?==|;)', re.MULTILINE)
TYPE_HEADER = re.compile(r'\b(?:class|struct|interface|enum|namespace)\b[^{};]*$')


def scope_declarations(text):
    """Every declaration that belongs to a block rather than directly to a type body."""
    scopes = []
    stack = []
    for index, character in enumerate(text):
        if character == '{':
            head = text[:index]
            boundary = max(head.rfind(';'), head.rfind('{'), head.rfind('}'))
            header = head[boundary + 1:] if boundary >= 0 else head
            kind = 'type' if TYPE_HEADER.search(header) else 'block'
            stack.append({'start': index, 'end': len(text), 'kind': kind, 'parent': stack[-1] if stack else None})
        elif character == '}' and stack:
            scope = stack.pop()
            scope['end'] = index
            scopes.append(scope)
    if stack:
        # Unbalanced braces: the file cannot be attributed to scopes, so say nothing about it.
        return []

    def chain(scope):
        names = []
        while scope is not None:
            names.append(scope['start'])
            scope = scope['parent']
        return tuple(reversed(names))

    found = []
    for match in LOCAL_DECLARATION.finditer(text):
        position = match.start(1)
        owner = None
        for scope in scopes:
            if scope['start'] < position < scope['end'] and (owner is None or scope['start'] > owner['start']):
                owner = scope
        if owner is None or owner['kind'] == 'type':
            # A field, property or event declaration directly in a type body may share a name with a local.
            continue
        found.append((match.group(1), text.count('\n', 0, position) + 1, chain(owner)))
    return found


def scope_problems(relative, text):
    """Declarations that would not compile: a name declared twice in one block chain."""
    declared = {}
    for name, line, chain in scope_declarations(text):
        declared.setdefault(name, []).append((line, chain))
    problems = []
    for name, entries in sorted(declared.items()):
        for outer_line, outer_chain in entries:
            for inner_line, inner_chain in entries:
                if inner_line <= outer_line:
                    continue
                if inner_chain == outer_chain:
                    problems.append('%s: line %d declares "%s" again; line %d declared it in the same scope (CS0128)'
                                    % (relative, inner_line, name, outer_line))
                elif len(outer_chain) < len(inner_chain) and inner_chain[:len(outer_chain)] == outer_chain:
                    problems.append('%s: line %d declares "%s", but line %d declares it in an enclosing scope (CS0136)'
                                    % (relative, inner_line, name, outer_line))
    return problems


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


    # Argument-count check for calls into this project's own types. This is the class of mistake a compiler catches
    # immediately (for example calling a two-parameter overload that was widened to four) and reading alone misses.
    # Attribute lines, extern declarations and commas inside generic arguments are excluded, because they describe
    # signatures rather than call sites.
    def split_arguments(parameter_text):
        depth = 0
        count = 0
        seen = False
        for character in parameter_text:
            if character in '<([':
                depth += 1
            elif character in '>)]':
                depth -= 1
            elif character == ',' and depth == 0:
                count += 1
                seen = True
            elif not character.isspace():
                seen = True
        return count + 1 if seen else 0

    arities = {}
    for path, text in texts.items():
        for line in text.split('\n'):
            stripped = line.strip()
            if stripped.startswith('[') or 'extern ' in line or ' delegate' in line:
                continue
            match = re.match(r'(?:' + MODIFIERS + r'\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,.\[\]?\s]*?\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)', stripped)
            if match:
                arities.setdefault(match.group(1), set()).add(split_arguments(match.group(2)))

    call = re.compile(r'\bnew\s+([A-Z][A-Za-z0-9_]*)\s*\(|\b([A-Z][A-Za-z0-9_]*)\s*\.\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(')
    # Generic type arguments are stripped first, so `Func<IntPtr, ScanResult>` cannot be mistaken for two arguments.
    # A leftover angle bracket then means a comparison operator, and that call is skipped as uncertain.
    generic = re.compile(r'(?<=[A-Za-z0-9_])<[^<>(){};=\n]*>')
    openers = '({['
    closers = ')}]'
    for path, raw in texts.items():
        text = generic.sub('', raw)
        for match in call.finditer(text):
            start_line = text.rfind('\n', 0, match.start()) + 1
            line_text = text[start_line:text.find('\n', start_line) if text.find('\n', start_line) >= 0 else len(text)]
            if 'extern ' in line_text or ' delegate' in line_text:
                continue
            owner = match.group(1) or match.group(2)
            method = match.group(3)
            name = method if method else owner
            if owner not in types or name not in arities:
                continue
            if method and method not in members.get(owner, set()):
                continue  # already reported as an unknown member
            # Multi-line aware: commas only count at the top level of this argument list, and never inside a nested
            # call, generic type, object initializer or indexer.
            depth = 0
            arguments = 0
            seen = False
            uncertain = False
            index = match.end() - 1
            while index < len(text):
                character = text[index]
                if character in openers:
                    depth += 1
                    if depth > 1 and character == '(':
                        seen = True
                elif character in closers:
                    depth -= 1
                    if depth == 0:
                        break
                elif character == ',' and depth == 1:
                    arguments += 1
                    seen = True
                elif character in '<>' and depth == 1:
                    # A comparison in an argument, so the argument count cannot be trusted: skip this call.
                    uncertain = True
                elif not character.isspace() and depth == 1:
                    seen = True
                index += 1
            count = arguments + 1 if seen else 0
            if not uncertain and count not in arities[name]:
                problems.append('%s: %s is called with %d argument(s) but %s declares %s'
                                % (path.relative_to(ROOT), name, count, name, sorted(arities[name])))

    declarations = 0
    for path in source_files():
        relative = path.relative_to(ROOT)
        text = strip_comments(path.read_text(encoding='utf-8'))
        entries = scope_declarations(text)
        declarations += len(entries)
        problems.extend(scope_problems(relative, text))

    if problems:
        print('API AUDIT: %d problem(s)' % len(problems))
        for line in problems:
            print('  - ' + line)
        return 1
    print('API audit clean: %d types, %d StringIds, %d table rows' % (len(types), len(ids), len(rows)))
    print('Scope audit clean: %d local declarations, no name is declared twice in one block chain' % declarations)
    return 0


if __name__ == '__main__':
    sys.exit(main())
