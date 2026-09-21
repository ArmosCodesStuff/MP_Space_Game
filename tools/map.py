# THE MAP -- every script, member, RPC, spawn site, event hookup, resource and asset in the game,
# and who uses each. Generated, never edited by hand:
#
#     python tools/map.py          rewrites version/MAP.md   (verify.ps1 -Update runs it)
#
# It reads the source as text, so "used by" is a whole-word match on the member's name: a name
# shared by two classes (Hp, Id) is credited to both. It is a map for finding your way, not a
# compiler; the build, the analysers and tools/analyse/xref.py are what prove something unused.
import glob, os, re, collections

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
os.chdir(root)

def read(p): return open(p, encoding='utf-8').read()

scripts = {os.path.basename(p): read(p) for p in sorted(glob.glob('scripts/*.cs'))}
tests = {p: read(p) for p in ('tools/smoketest/SmokeTest.cs.txt', 'tools/screens/Shots.cs.txt') if os.path.exists(p)}
scenes = {p.replace('\\', '/'): read(p) for p in sorted(glob.glob('*.tscn'))}
project = read('project.godot')

def strip(s):
    # comments and string contents out, so a word in a comment or a message is not a use
    s = re.sub(r'//[^\n]*', '', s)
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return re.sub(r'"(?:\\.|[^"\\])*"', '""', s)

code = {f: strip(s) for f, s in scripts.items()}
test_code = {f: strip(s) for f, s in tests.items()}

def line_of(text, idx): return text.count('\n', 0, idx) + 1

# ── classes ──────────────────────────────────────────────────────────────────────────────────
cls_re = re.compile(r'^(?:public |internal )?(?:static |partial |abstract |sealed )*(class|struct|enum|interface|record)\s+(\w+)(?:\s*:\s*([\w.<>, ]+))?', re.M)
classes = {}          # name -> (file, kind, base, line)
for f, s in scripts.items():
    for m in cls_re.finditer(s):
        classes[m.group(2)] = (f, m.group(1), (m.group(3) or '').split(',')[0].strip(), line_of(s, m.start()))

def is_node(c, seen=()):
    if c not in classes: return False
    b = classes[c][2]
    if not b or c in seen: return False
    if b in classes: return is_node(b, seen + (c,))
    return b not in ('IHittable', 'Resource', 'RefCounted', 'GodotObject') and not b.startswith('I')

# ── members, at class-body depth (four spaces) ─────────────────────────────────────────────────
mem_re = re.compile(
    r'^    (?:\[[^\]\n]*\]\s*)?(?P<vis>public|private|protected|internal)?\s*'
    r'(?P<mods>(?:(?:static|readonly|const|override|virtual|partial|event|new|async|abstract|volatile|sealed)\s+)*)'
    r'(?P<type>[\w<>\[\],.?()]+(?:<[^;=\n]*?>)?(?:\[\])?)\s+(?P<name>\w+)\s*(?P<tail>\(|=>|=|;|\{|,)', re.M)
KEYWORDS = {'return', 'if', 'for', 'foreach', 'while', 'switch', 'case', 'var', 'new', 'else', 'using', 'throw', 'await', 'yield', 'base', 'this'}

def kind_of(m):
    mods, tail = m.group('mods'), m.group('tail')
    if 'const' in mods: return 'const'
    if 'event' in mods: return 'event'
    if tail == '(': return 'method'
    if tail in ('=>', '{'): return 'property'
    return 'field'

members = []          # (file, class, name, kind, vis, static, line)
for f, s in scripts.items():
    owners = sorted((v[3], k) for k, v in classes.items() if v[0] == f)
    for m in mem_re.finditer(s):
        if m.group('type') in KEYWORDS or m.group('name') in KEYWORDS: continue
        ln = line_of(s, m.start())
        owner = [c for l, c in owners if l <= ln]
        owner = owner[-1] if owner else '?'
        k = kind_of(m)
        names = [m.group('name')]
        if k == 'field' and m.group('tail') == ',':   # int _a, _b, _c;
            rest = s[m.end():s.find(';', m.end())]
            names += re.findall(r'(?:^|,)\s*(\w+)\s*(?==|,|$)', rest)
        for n in names:
            members.append((f, owner, n, k, m.group('vis') or 'private', 'static' in m.group('mods') or k == 'const', ln))

def uses(name, own_file):
    w = re.compile(r'\b' + re.escape(name) + r'\b')
    inside = len(w.findall(code[own_file])) - 1
    others = sorted(f for f, s in code.items() if f != own_file and w.search(s))
    in_tests = any(w.search(s) for s in test_code.values())
    return inside, others, in_tests

# ── RPCs ──────────────────────────────────────────────────────────────────────────────────────
rpc_re = re.compile(r'\[Rpc\((?:MultiplayerApi\.RpcMode\.)?(\w+)[^\]]*?TransferMode\s*=\s*(?:MultiplayerPeer\.TransferModeEnum\.)?(\w+)[^\]]*\]\s*\n\s*(?:private|public)\s+\w+\s+(\w+)\s*\(([^)]*)\)')
rpcs = []
for f, s in scripts.items():
    for m in rpc_re.finditer(s):
        name = m.group(3)
        senders = sorted({f2 for f2, s2 in code.items() if re.search(r'nameof\((?:\w+\.)*' + name + r'\)', s2)})
        rpcs.append((f, line_of(s, m.start()), name, m.group(1), m.group(2), m.group(4).strip(), senders))

# ── spawn sites: a node made with `new` ───────────────────────────────────────────────────────
godot_nodes = set('''Node Node2D Control Label Button Panel PanelContainer VBoxContainer HBoxContainer GridContainer
MarginContainer CenterContainer ScrollContainer ColorRect TextureRect Sprite2D Camera2D CanvasLayer Line2D Polygon2D
AudioStreamPlayer AudioStreamPlayer2D Timer HttpRequest LineEdit TextEdit OptionButton CheckBox CheckButton HSlider
VSlider ProgressBar TabContainer ItemList RichTextLabel Window AcceptDialog ConfirmationDialog SubViewport
SubViewportContainer GpuParticles2D CpuParticles2D Area2D CollisionShape2D HSeparator VSeparator'''.split())
spawn_re = re.compile(r'\bnew\s+([A-Z]\w*)\s*[({]')
spawns = collections.defaultdict(list)     # class -> [file:line]
for f, s in code.items():
    for m in spawn_re.finditer(s):
        c = m.group(1)
        if is_node(c) or c in godot_nodes: spawns[c].append(f"{f}:{line_of(s, m.start())}")
inst = collections.defaultdict(list)
for f, s in code.items():
    for m in re.finditer(r'\.Instantiate', s): inst[f].append(line_of(s, m.start()))

# ── event hookups: += with a matching -= ──────────────────────────────────────────────────────
declared_events = {n for (_, _, n, k, *_ ) in members if k == 'event'}
signals = set('''Pressed Toggled Timeout ValueChanged TextChanged TextSubmitted ItemSelected PeerConnected PeerDisconnected
ConnectedToServer ConnectionFailed ServerDisconnected RequestCompleted MouseEntered MouseExited GuiInput Finished Resized
FocusEntered FocusExited IdPressed VisibilityChanged TreeExiting TreeExited ButtonDown ButtonUp CloseRequested
Confirmed Canceled'''.split())
hook_re = re.compile(r'([\w.?\[\]()]+?)\.?(\b\w+)\s*(\+=|-=)\s*([^;]+);')
hooks = []
for f, s in code.items():
    subs, unsubs = collections.Counter(), collections.Counter()
    for m in hook_re.finditer(s):
        ev = m.group(2)
        if ev not in declared_events and ev not in signals: continue
        key = (m.group(1).rstrip('.?') + '.' + ev).lstrip('.')
        (subs if m.group(3) == '+=' else unsubs)[key] += 1
    for k in sorted(subs):
        hooks.append((f, k, subs[k], unsubs.get(k, 0), '=>' in s and True))

# ── resources and assets ──────────────────────────────────────────────────────────────────────
res_re = re.compile(r'res://([^"\s)]+)')
refs = collections.defaultdict(set)        # asset path -> {who}
for f, s in scripts.items():
    for m in res_re.finditer(s): refs[m.group(1)].add(f"scripts/{f}:{line_of(s, m.start())}")
for f, s in scenes.items():
    for m in res_re.finditer(s): refs[m.group(1)].add(f)
for m in res_re.finditer(project): refs[m.group(1)].add('project.godot')
# templated loads: res://sfx/{s}.wav with s drawn from a list in the same file
templated = []
for f, s in scripts.items():
    for m in re.finditer(r'res://([\w/]*)\{\w+\}(\.\w+)', s): templated.append((f, m.group(1), m.group(2)))
asset_ext = ('.png', '.ogg', '.wav', '.tscn', '.ttf', '.otf', '.tres', '.svg')
assets = sorted(p.replace('\\', '/') for p in glob.glob('**/*', recursive=True)
                if p.endswith(asset_ext) and not p.replace('\\', '/').startswith(('.godot/', 'bin/', 'obj/')))

def asset_users(a):
    who = set(refs.get(a, set()))
    for f, folder, ext in templated:
        if a.startswith(folder) and a.endswith(ext):
            stem = a[len(folder):-len(ext)]
            if re.search(r'"' + re.escape(stem) + r'"', scripts[f]): who.add(f"scripts/{f} (by name)")
    return sorted(who)

# ── write it ──────────────────────────────────────────────────────────────────────────────────
out = []
w = out.append
total_lines = sum(s.count('\n') + 1 for s in scripts.values())
w('# Warships -- the map')
w('')
w('Generated by `python tools/map.py`; do not edit. "Used by" is a whole-word match on the name, so a')
w('name two classes share is credited to both -- the build, the analysers and `tools/analyse/xref.py`')
w('are what prove something unused.')
w('')
w(f'{len(scripts)} scripts, {total_lines} lines; {len(classes)} types; {len(members)} members; {len(rpcs)} RPCs; '
  f'{sum(len(v) for v in spawns.values())} node spawn sites; {len(assets)} asset files.')
w('')
w('## Scripts')
w('')
w('| file | lines | types | depends on |')
w('|---|---:|---|---|')
for f, s in scripts.items():
    ts = ', '.join(f"{k} ({v[1]}{': ' + v[2] if v[2] else ''})" for k, v in classes.items() if v[0] == f)
    deps = sorted({classes[c][0] for c in classes if classes[c][0] != f and re.search(r'\b' + c + r'\b', code[f])})
    w(f"| {f} | {s.count(chr(10)) + 1} | {ts} | {', '.join(d[:-3] for d in deps)} |")
w('')
w('## Members')
w('')
w('Per type: kind, visibility, where declared, and who else uses it (T = the test harness).')
for f in scripts:
    for c in [k for k, v in classes.items() if v[0] == f]:
        ms = [m for m in members if m[0] == f and m[1] == c]
        if not ms: continue
        w('')
        w(f'### {c} -- {f}')
        w('')
        w('| member | kind | line | used in this file | used by |')
        w('|---|---|---:|---:|---|')
        for (_, _, n, k, vis, st, ln) in ms:
            inside, others, t = uses(n, f)
            by = ', '.join(o[:-3] for o in others) + (' T' if t else '')
            w(f"| {n} | {vis} {'static ' if st and k != 'const' else ''}{k} | {ln} | {inside} | {by.strip() or '-'} |")
w('')
w('## RPCs')
w('')
w('| rpc | file:line | who may call | transfer | arguments | sent from |')
w('|---|---|---|---|---|---|')
for (f, ln, n, mode, tm, args, senders) in rpcs:
    w(f"| {n} | {f}:{ln} | {mode} | {tm} | {args or '-'} | {', '.join(x[:-3] for x in senders) or '-'} |")
w('')
w('## Spawn sites (nodes made with `new`)')
w('')
w('| node | made at |')
w('|---|---|')
for c in sorted(spawns, key=lambda c: (c in godot_nodes, c)):
    w(f"| {c}{'' if c in godot_nodes else ' *'} | {', '.join(spawns[c])} |")
w('')
w('`*` = a project class. Scenes instantiated from a PackedScene: ' + (', '.join(f"{f}:{ls}" for f, ls in inst.items()) or 'none') + '.')
w('')
w('## Event hookups')
w('')
w('Every `+=` on an event or signal, and whether the same file has the matching `-=`. A hookup on a node')
w('that dies with its subscriber needs none; one on an autoload or a static event does (invariant A).')
w('')
w('| file | event | += | -= |')
w('|---|---|---:|---:|')
for (f, k, a, b, _) in hooks:
    w(f"| {f} | {k} | {a} | {b} |")
w('')
w('## Resources and assets')
w('')
w('| asset | used by |')
w('|---|---|')
for a in assets:
    u = asset_users(a)
    parked = a.startswith(('retired/', 'art_unused/'))
    w(f"| {a} | {', '.join(u) if u else ('parked (not shipped in play)' if parked else '**UNREFERENCED**')} |")
missing = sorted(p for p in refs if not os.path.exists(p) and '{' not in p)
w('')
w('res:// paths that name a file that does not exist: ' + (', '.join(missing) if missing else 'none') + '.')
w('')

os.makedirs('version', exist_ok=True)
open('version/MAP.md', 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
print(f"MAP: {len(scripts)} scripts, {len(members)} members, {len(rpcs)} RPCs, {len(assets)} assets, "
      f"{len(missing)} missing, {sum(1 for a in assets if not asset_users(a) and not a.startswith(('retired/', 'art_unused/')))} unreferenced")
