# Cross-reference scan: public members of the game scripts that nothing uses, and those
# only the tests use (read-only observers are fine; anything else is a question).
import re, glob, os
root = os.path.join(os.path.dirname(__file__), '..', '..')
src = {f: open(f, encoding='utf-8').read() for f in glob.glob(os.path.join(root, 'scripts', '*.cs'))}
tests = ''.join(open(os.path.join(root, 'tools', p), encoding='utf-8').read() for p in ('smoketest/SmokeTest.cs.txt', 'screens/Shots.cs.txt'))
decl = re.compile(r'^\s*public\s+(?:static\s+|readonly\s+|const\s+|override\s+|virtual\s+|partial\s+|event\s+|new\s+)*([\w<>\[\],.?() ]+?)\s+(\w+)\s*(\(|=>|=|;|\{)', re.M)
skip = {'_Ready', '_Process', '_Draw', '_Input', '_UnhandledInput', '_GuiInput', '_ExitTree', '_EnterTree', '_PhysicsProcess', '_Notification', 'Position', 'Rotation', 'Visible', 'Name'}
game = '\n'.join(src.values())
dead, test_only = [], []
for f, s in src.items():
    for m in decl.finditer(s):
        n = m.group(2)
        if n in skip: continue
        g = len(re.findall(r'\b' + re.escape(n) + r'\b', game)) - 1
        t = len(re.findall(r'\b' + re.escape(n) + r'\b', tests))
        if g <= 0: (test_only if t else dead).append(f"{os.path.basename(f)}: {n}")
print('UNUSED ANYWHERE:', len(dead)); [print('  ' + x) for x in sorted(dead)]
print('USED ONLY BY TESTS:', len(test_only)); [print('  ' + x) for x in sorted(test_only)]
