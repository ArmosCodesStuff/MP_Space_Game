# Cross-reference scan: public members of the game scripts that nothing uses, and those
# only the tests use (read-only observers are fine; anything else is a question).
import re, glob, os
root = os.path.join(os.path.dirname(__file__), '..', '..')
src = {f: open(f, encoding='utf-8').read() for f in glob.glob(os.path.join(root, 'scripts', '*.cs'))}
tests = ''.join(open(os.path.join(root, 'tools', p), encoding='utf-8').read() for p in ('smoketest/SmokeTest.cs.txt', 'screens/Shots.cs.txt'))
decl = re.compile(r'^\s*public\s+(?:static\s+|readonly\s+|const\s+|override\s+|virtual\s+|partial\s+|event\s+|new\s+)*([\w<>\[\],.?() ]+?)\s+(\w+)\s*(\(|=>|=|;|\{)', re.M)
# AN ENUM MEMBER IS A DECLARATION TOO. It carries no access modifier, so the pattern above --
# which starts at `public` -- could never see one, and every member of Status, Tag, Fit, ShotLook,
# FxShape and every other enum sat outside the UNUSED ANYWHERE gate. Status.Shielded was declared,
# shipped on the wire and implemented by nothing for as long as this script had been run.
# An enum body holds no braces, so the first closing one ends it; comments are dropped and what is
# left is split on commas, which reads a one-line body and a one-per-line body with values
# (`Pinned = 1,`) the same way. Members are then counted through the same reference count.
body = re.compile(r'\benum\s+(\w+)\s*(?::\s*[\w.]+\s*)?\{(.*?)\}', re.S)
note = re.compile(r'//.*')
skip = {'_Ready', '_Process', '_Draw', '_Input', '_UnhandledInput', '_GuiInput', '_ExitTree', '_EnterTree', '_PhysicsProcess', '_Notification', 'Position', 'Rotation', 'Visible', 'Name'}
game = '\n'.join(src.values())
dead, test_only = [], []

def declared(s):
    for m in decl.finditer(s):
        if m.group(2) not in skip: yield m.group(2), m.group(2)
    for m in body.finditer(s):
        for part in note.sub('', m.group(2)).split(','):
            n = part.split('=')[0].strip()
            if n.isidentifier() and n not in skip: yield n, m.group(1) + '.' + n

for f, s in src.items():
    for n, shown in declared(s):
        g = len(re.findall(r'\b' + re.escape(n) + r'\b', game)) - 1
        t = len(re.findall(r'\b' + re.escape(n) + r'\b', tests))
        if g <= 0: (test_only if t else dead).append(f"{os.path.basename(f)}: {shown}")
print('UNUSED ANYWHERE:', len(dead)); [print('  ' + x) for x in sorted(dead)]
print('USED ONLY BY TESTS:', len(test_only)); [print('  ' + x) for x in sorted(test_only)]
