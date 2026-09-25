# THE BOSSES' SOUNDS: one for every special move, synthesised here (no samples, nothing to license),
# written to sfx\<name>.wav at the level each is meant to be heard at (Sfx plays every file at 0 dB).
# Deterministic: the same file every run. Mono, 16-bit, 44.1 kHz, like every other effect.
#
#   boss_beam_charge  the Lancer's death beam charging: a deep hum and a whine climbing through it
#   boss_beam         the beam firing: a low growling buzz, a sawtooth pair on a wobble, driven hard
#   boss_ram          the ram: an engine's roar surging up
#   boss_shockwave    the shockwave landing: a deep falling boom
#   boss_trident      the trident volley: three heavy launches and their whoosh
#   drake_gun         the Drake's main gun: a deep cannon whump with a metal ring
#   drake_warp        its warp: a falling zap and a thud
#   drake_scrap       the scrap shotgun: a blast and a spray of clanking metal
#   drake_tractor     the tractor taking the rock: a pulsing hum
#   drake_throw       the throw: a heavy rising whoosh
#   drake_rock        the rock breaking apart: a crunch over a low boom
#
# Run: python tools\make_sounds.py
import math, os, random, struct, wave

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'sfx')


def samples(seconds):
    return [i / SR for i in range(int(seconds * SR))]


def lowpass(xs, cutoff):
    """one pole; `cutoff` a number or a function of the sample index"""
    out, y = [], 0.0
    for i, x in enumerate(xs):
        fc = cutoff(i) if callable(cutoff) else cutoff
        a = 1 - math.exp(-2 * math.pi * fc / SR)
        y += a * (x - y)
        out.append(y)
    return out


def highpass(xs, cutoff):
    lo = lowpass(xs, cutoff)
    return [x - l for x, l in zip(xs, lo)]


def saw(phase):
    return 2 * (phase - math.floor(phase + 0.5))


def noise(n, rng):
    return [rng.uniform(-1, 1) for _ in range(n)]


def swept(ts, f0, f1, curve=1.0):
    """a phase accumulator for a frequency moving from f0 to f1 over the clip (curve > 1: late)"""
    ph, out, T = 0.0, [], ts[-1] if ts else 1
    for t in ts:
        f = f0 + (f1 - f0) * (t / T) ** curve
        ph += f / SR
        out.append(ph)
    return out


def env(ts, attack, release):
    T = ts[-1] if ts else 1
    return [min(1.0, t / attack if attack > 0 else 1.0) * min(1.0, (T - t) / release if release > 0 else 1.0) for t in ts]


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def write(name, xs, peak):
    m = max(abs(x) for x in xs) or 1.0
    data = b''.join(struct.pack('<h', int(round(max(-1.0, min(1.0, x / m * peak)) * 32767))) for x in xs)
    with wave.open(os.path.join(ROOT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data)
    print(f'{name:18s} {len(xs) / SR:.2f} s, peak {peak:.4g}')


def beam_charge(rng):
    ts = samples(2.2)
    hum_ph = swept(ts, 42, 58)
    whine_ph = swept(ts, 260, 980, 1.6)
    hum = lowpass([saw(p) for p in hum_ph], 500)
    out = []
    for i, t in enumerate(ts):
        k = t / ts[-1]
        trem = 0.75 + 0.25 * math.sin(2 * math.pi * (4 + 14 * k) * t)       # quickening as it builds
        out.append(0.7 * hum[i] * (0.4 + 0.6 * k) + 0.35 * math.sin(2 * math.pi * whine_ph[i]) * k * k * trem)
    return [x * e for x, e in zip(out, env(ts, 0.3, 0.08))]


def beam(rng):
    # THE GROWL: two sawtooths a fifth apart on a slow wobble and a fast rattle, a sub under them,
    # driven into saturation and darkened -- a low buzz that sounds like it does a lot of damage
    ts = samples(1.9)
    ph1 = ph2 = 0.0
    raw = []
    for t in ts:
        f = 56 * (1 + 0.07 * math.sin(2 * math.pi * 6.5 * t) + 0.02 * math.sin(2 * math.pi * 1.3 * t))
        ph1 += f / SR; ph2 += 1.5 * f / SR
        rattle = 0.7 + 0.3 * math.sin(2 * math.pi * 23 * t)
        x = (saw(ph1) + 0.6 * saw(ph2)) * rattle + 0.8 * math.sin(2 * math.pi * 28 * t)
        raw.append(math.tanh(2.6 * x))
    crackle = highpass(noise(len(ts), rng), 1800)
    crackle = [c * (0.15 if rng.random() < 0.08 else 0.03) for c in crackle]
    out = mix(lowpass(raw, 1100), crackle)
    return [x * e for x, e in zip(out, env(ts, 0.03, 0.55))]


def ram(rng):
    ts = samples(1.3)
    n = len(ts)
    roar = lowpass(noise(n, rng), lambda i: 180 + 1600 * min(1.0, i / (0.55 * n)))
    eng = lowpass([saw(p) for p in swept(ts, 38, 72, 0.7)], 400)
    out = mix([0.9 * r for r in roar], [0.6 * e for e in eng])
    return [math.tanh(1.5 * x) * e for x, e in zip(out, env(ts, 0.18, 0.5))]


def shockwave(rng):
    ts = samples(1.7)
    ph = swept(ts, 75, 26, 0.5)
    boom = [math.sin(2 * math.pi * p) * math.exp(-t * 2.2) for p, t in zip(ph, ts)]
    burst = lowpass([x * math.exp(-t * 6) for x, t in zip(noise(len(ts), rng), ts)], 700)
    out = mix(boom, [1.4 * b for b in burst])
    return [math.tanh(1.8 * x) for x in out]


def trident(rng):
    ts = samples(1.0)
    out = [0.0] * len(ts)
    for k, start in enumerate((0.0, 0.11, 0.22)):
        for i, t in enumerate(ts):
            u = t - start
            if u < 0: continue
            f = 130 - 60 * min(1.0, u / 0.12)
            out[i] += math.sin(2 * math.pi * f * u) * math.exp(-u * 16) * (1 - 0.1 * k)
    whoosh = lowpass(highpass(noise(len(ts), rng), 300), 2200)
    whoosh = [w * 0.35 * math.sin(math.pi * min(1.0, t / 0.9)) for w, t in zip(whoosh, ts)]
    return mix(out, whoosh)


def gun(rng):
    ts = samples(0.8)
    ph = swept(ts, 95, 40, 0.4)
    whump = [math.sin(2 * math.pi * p) * math.exp(-t * 9) for p, t in zip(ph, ts)]
    attack = lowpass([x * math.exp(-t * 40) for x, t in zip(noise(len(ts), rng), ts)], 2500)
    ring = [0.18 * math.sin(2 * math.pi * 330 * t) * math.exp(-t * 6) for t in ts]
    return [math.tanh(1.6 * x) for x in mix(whump, [0.9 * a for a in attack], ring)]


def warp(rng):
    ts = samples(0.95)
    ph = swept(ts, 1300, 110, 0.35)
    zap = [(0.6 * math.sin(2 * math.pi * p) + 0.4 * saw(p)) * (1 - t / 0.95) for p, t in zip(ph, ts)]
    sizzle = [x * 0.25 * math.exp(-t * 4) for x, t in zip(highpass(noise(len(ts), rng), 3000), ts)]
    thud = [0.0] * len(ts)
    for i, t in enumerate(ts):
        u = t - 0.62
        if u >= 0: thud[i] = math.sin(2 * math.pi * 55 * u) * math.exp(-u * 12)
    out = mix(lowpass(zap, 4000), sizzle, thud)
    return [x * e for x, e in zip(out, env(ts, 0.01, 0.1))]


def scrap(rng):
    ts = samples(1.1)
    blast = lowpass(highpass([x * math.exp(-t * 11) for x, t in zip(noise(len(ts), rng), ts)], 250), 3200)
    clank = [0.0] * len(ts)
    for _ in range(26):                                   # the spray: short pings of scrap metal
        start, f, d = rng.uniform(0.02, 0.5), rng.uniform(600, 2600), rng.uniform(25, 60)
        a = rng.uniform(0.12, 0.3)
        for i in range(int(start * SR), min(len(ts), int((start + 0.18) * SR))):
            u = ts[i] - start
            clank[i] += a * math.sin(2 * math.pi * f * u) * math.exp(-u * d)
    thump = [math.sin(2 * math.pi * 60 * t) * math.exp(-t * 14) for t in ts]
    return [math.tanh(1.4 * x) for x in mix([1.3 * b for b in blast], clank, thump)]


def tractor(rng):
    ts = samples(2.0)
    out = []
    for t in ts:
        pulse = 0.6 + 0.4 * math.sin(2 * math.pi * 6 * t)
        x = (math.sin(2 * math.pi * 110 * t) + 0.6 * math.sin(2 * math.pi * 165 * t)) * pulse \
            + 0.12 * math.sin(2 * math.pi * 880 * t + 3 * math.sin(2 * math.pi * 0.7 * t))
        out.append(x)
    return [x * e for x, e in zip(out, env(ts, 0.35, 0.6))]


def throw(rng):
    ts = samples(1.25)
    n = len(ts)
    def cut(i):
        k = i / n
        return 250 + 2800 * (k / 0.45 if k < 0.45 else max(0.0, 1 - (k - 0.45) / 0.55))
    whoosh = lowpass(noise(n, rng), cut)
    rumble = lowpass([saw(p) for p in swept(ts, 45, 30)], 200)
    out = mix([1.4 * w for w in whoosh], [0.5 * r for r in rumble])
    return [x * e for x, e in zip(out, env(ts, 0.25, 0.45))]


def rock(rng):
    ts = samples(1.3)
    crunch = [0.0] * len(ts)
    src = noise(len(ts), rng)
    for start in (0.0, 0.06, 0.13, 0.22, 0.34):              # the rock coming apart in pieces
        a = rng.uniform(0.6, 1.0)
        for i in range(int(start * SR), len(ts)):
            u = ts[i] - start
            crunch[i] += a * src[i] * math.exp(-u * 18)
    crunch = lowpass(crunch, 1500)
    boom = [math.sin(2 * math.pi * 52 * t) * math.exp(-t * 3.5) for t in ts]
    return [math.tanh(1.5 * x) for x in mix([1.6 * c for c in crunch], boom)]


# A BEAM IS HEARD A QUARTER UNDER THE LEVEL IT WAS MADE AT (the owner: "lower the volume default of
# beams by 25%"). Each peak below is the level a sound was made at; a beam's is that times BEAM, so
# the cut is one number and the level it was cut from stays on its row. The other beam files
# (laser_*) are not made here: tools/gain.ps1 cut them by the same 0.75. Sfx plays every file at
# 0 dB and holds no beam volume of its own, so the files are the only place the cut is made.
BEAM = 0.75

SOUNDS = [
    ('boss_beam_charge', beam_charge, 0.55 * BEAM),
    ('boss_beam', beam, 0.85 * BEAM),
    ('boss_ram', ram, 0.75),
    ('boss_shockwave', shockwave, 0.9),
    ('boss_trident', trident, 0.7),
    ('drake_gun', gun, 0.6),
    ('drake_warp', warp, 0.6),
    ('drake_scrap', scrap, 0.85),
    ('drake_tractor', tractor, 0.45 * BEAM),
    ('drake_throw', throw, 0.75),
    ('drake_rock', rock, 0.9),
]

if __name__ == '__main__':
    for i, (name, make, peak) in enumerate(SOUNDS):
        write(name, make(random.Random(1000 + i)), peak)
