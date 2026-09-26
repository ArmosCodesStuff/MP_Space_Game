"""fails_diff.py <run dir>... [--prev <run dir>...]: the round's reds as a signature table, so a triage reads ~100 lines,
not 3,000 (fable_retro_tokens.md, 2026-09-25). A run dir is %TEMP%\\warships_rungs\\<tag>; every <n>_<step>.fails.txt in it is
read. A signature is a verdict line with its numbers replaced by #, its role tag and (xN) stripped. Output, one line per
signature sorted by count: count | steps | status (new / persisting / gone against --prev) | the signature (140 chars) |
one evidence pointer <tag>/<file>:<line>. FAIL LANE and Exception lines come first. Read a fails file only for the
evidence line of a signature you task."""
import glob, os, re, sys

NUM = re.compile(r"-?\d+(?:[.,]\d+)?")
TAG = re.compile(r"^\s*\[[a-z0-9]+\]\s*")
XN = re.compile(r"\s*\(x\d+\)\s*$")


def sig(line):
    s = TAG.sub("", line.strip())
    s = XN.sub("", s)
    return NUM.sub("#", s)


def read(dirs):
    out = {}
    for d in dirs:
        tag = os.path.basename(d.rstrip("\\/"))
        for f in sorted(glob.glob(os.path.join(d, "*_*.fails.txt"))):
            step = os.path.basename(f).split(".")[0]
            with open(f, encoding="utf-8", errors="replace") as h:
                for i, line in enumerate(h, 1):
                    if not re.search(r"\bFAIL\b|Exception|FAIL LANE", line) or line.startswith(("FAIL ", "Exception ")) and "/" in line and i == 1:
                        continue
                    k = sig(line)
                    e = out.setdefault(k, {"n": 0, "steps": set(), "ev": f"{tag}/{os.path.basename(f)}:{i}"})
                    m = XN.search(line)
                    e["n"] += int(m.group(0).strip()[2:-1]) if m else 1
                    e["steps"].add(step.split("_", 1)[1] if "_" in step else step)
    return out


def main(argv):
    if "--prev" in argv:
        i = argv.index("--prev")
        cur, prev = argv[:i], argv[i + 1:]
    else:
        cur, prev = argv, []
    now, before = read(cur), read(prev)
    rows = []
    for k, e in now.items():
        status = "persisting" if k in before else ("new" if before else "")
        rank = 0 if ("FAIL LANE" in k or "Exception" in k) else 1
        rows.append((rank, -e["n"], k, e, status))
    rows.sort()
    print(f"signatures {len(now)} | lines {sum(e['n'] for e in now.values())} | gone since prev {len([k for k in before if k not in now])}")
    for rank, _, k, e, status in rows:
        print(f"{e['n']:4d} | {','.join(sorted(e['steps'])):14s} | {status:10s} | {k[:140]} | {e['ev']}")
    for k in before:
        if k not in now:
            print(f"   0 | {'':14s} | gone       | {k[:140]} |")


if __name__ == "__main__":
    main(sys.argv[1:])
