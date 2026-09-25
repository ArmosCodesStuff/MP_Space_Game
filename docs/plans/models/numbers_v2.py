# NUMBERS v2: one consistent model for the curve (base-class reference, level walls, no chips), the boss-fight
# adds / raids, and the per-hull-category items (+10% compounding tiers). Read-only design model; see numbers_v2.md.
# Imports ../curve.py (ladder, affordability, points split), ../player_model.py (fleet income) and
# ../raids_v2_model.py (squad travel geometry, enemy rows) and changes nothing in them.
# usage: python numbers_v2.py [all|curve|classes|chips|raids|items]
# Every assumption below is a ROW; nothing is an `if` on a class name except where a row names the class.
import math, random, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); UP = os.path.dirname(HERE)
sys.path.insert(0, UP)
import curve as cv
import player_model as pm
import raids_v2_model as RV

# ================================================================ RULES (the owner's rulings, as rows)
KILL, FIRST, DONE, EXPLV = 300, 250, 100, 1000        # Missions / Progression (unchanged)
REGEN = 0.005                                          # PlayerShip.RegenInCombat
FIGHT, SHARE, MAXLEVEL = 60.0, 0.65, 40                # 60 s at par; par at 65% of the salvage gate; gate cap
STEP = 0.03                                            # salvage: +3% of a part's ups per slot level (slot ladders)
GROW, UP1, CHIP1 = 1.10, 0.25, 0.08                    # tiers compound +10%; T1 headline cap +25%; a chip's T1 lean
MULT1 = 0.20                                           # T1 headline of a MULTIPLIER lean (rate, cooldown, duration)
BASIC = (0.05, 0.05)                                   # the kit's Basic Combat Chip (+5% damage, +5% hull), NOT baked
WALL_AB = (1, 3, 6)                                    # ability 1 / 2 / 3 open at pilot level
WALL_CHIP = (2, 4, 8, 10, 12, 14)                      # chip slots 1-6 open at pilot level (6 on every hull)
COMBAT_CAP = UTILITY_CAP = 3
DRAKE_R = 700 / 760                                    # Drake row / Lancer row (kept from curve.md)
TTD_TARGET = {'lancer': 31.0, 'drake': 42.0}           # the agreed time to die at par, sheet ("stops dodging")
LADDER = (500, 1.10)                                   # salvage level n -> n+1 costs 500 x 1.10^n, gated
REF = 'DESTROYER'                                      # the reference class (curve.md's median row)
Q_STEP = 1.01                                          # Missions.Quicken: wind-ups / 1.01^(L-1)

# ---- the 12 base classes, kits v3 + the owner's later rulings. (category, base hull, weapon realistic DPS vs a
# lone boss, [ability 1, 2, 3 in LEARN order: (name, realistic DPS share)]). No chips anywhere in these numbers.
CLASSES = {
    'BATTLESHIP': ('capital', 500, 22.1, [('Broadside', 23.9), ('Brace', 0.0), ('CIWS', 0.0)]),
    'CARRIER':    ('capital', 425, 22.5, [('Bomber strike', 15.4), ('Warp gunships', 8.5), ('Supercarrier', 6.2)]),
    'DESTROYER':  ('capital', 395, 40.15, [('Long Lance', 14.2), ('Suppressing fire', 0.0), ('Grapnel', 0.65)]),
    'FREIGHTER':  ('freighter', 450, 48.0, [('Time on target (hitscan)', 8.7), ('Bubble', 0.0), ('Redeploy', 0.0)]),
    'TENDER':     ('freighter', 380, 38.2, [('Overdrive', 6.1), ('Repair field', 0.0), ('Resupply', 1.7)]),
    'BASTION':    ('freighter', 420, 21.15, [('Bunker buster', 25.85), ('Shockwave', 0.0), ('Gravity well', 0.0)]),
    'WARRIOR':    ('heavy', 300, 40.0, [('Lunge', 5.9), ('Whirlwind', 0.0), ('Prism stance', 4.1)]),
    'SNIPER':     ('heavy', 240, 34.3, [('Anchor', 16.7), ('Tether mine', 0.0), ('Flares', 0.0)]),
    'WARDEN':     ('heavy', 270, 31.4, [('Hunters', 17.6), ('Taunt', 0.0), ('Flak curtain', 0.0)]),
    'DART':       ('light', 200, 39.0, [('Rod from God', 22.0), ('Ramjet', 5.8), ('Slingshot', 0.0)]),
    'ECHO':       ('light', 180, 61.2, [('Reverb', 10.8), ('Rewind', 0.0), ('EMP', 0.0)]),
    'WRAITH':     ('light', 220, 54.4, [('Veil', 4.4), ('Venom', 5.2), ('Shadow step', 0.0)]),
}
def full_dps(c): return CLASSES[c][2] + sum(a for _, a in CLASSES[c][3])
# the DD's grapnel rip (ruling): 1% of the target's TOTAL hull + 10, bosses and structures only, on ability 3
RIP = {'DESTROYER': dict(frac=0.01, flat=10.0, cycle=21.0, use=0.8, nth=3)}

# ---- boss rows per unit of scale (boss_model.md 1.3, re-read in Lancer.cs / Drake.cs). The escorts leave the
# Lancer's sheet (they are squad wave 1 now). The beam is 5 ticks = 250 (fix B). Supers stay at their rows.
LANCER = {'guns': (3.00, 1.00, False), 'trident': (3.00, 0.50, False), 'wave': (2.65, 0.50, False),
          'beam': (250 / 30, 0.25, True), 'ram': (40 / 30, 0.25, True)}          # (sheet, land rate, super)
DRAKE = {'gun': (2.40, 0.50, False), 'scrap': (2.50, 0.25, False), 'rock': (250 / 30, 0.25, True)}

# ================================================================ THE ADDS SCHEDULE (raids_v2 + the rulings)
def adds(L): return 0 if L < 6 else min(12, (L - 6) // 3 + 1)            # H,L,L,L every 3 levels from L6
def rusty(L): return (L - 1) % 2 == 0                                      # odd levels are the Rusty Bucket
def squad_kinds(L):
    """[(heavy?, lights)] per squad slot. Slot 0 = gunship + webifiers, 1 = cross + talons, 2 = lancerkin + pods.
    RULING: on the Rusty Bucket the two beam escorts ARE squad wave 1 from level 1 (at least 2 webifiers)."""
    n = adds(L); out = []
    for k in range((n + 3) // 4):
        out.append((True, max(0, min(3, n - 4 * k - 1))))
    if rusty(L):
        if not out: out = [(False, 2)]
        else: out[0] = (out[0][0], max(2, out[0][1]))
    return out
EXP_ADD = {'heavy': 18, 'light': 6}

# ================================================================ THE PACE (4 clears a level, adds' first fill pays)
def fights(top=100, kills=4):
    pl, exp, out = 1, 0, {}
    for L in range(1, top + 1):
        out[L] = []
        for k in range(kills):
            out[L].append(pl)
            ok = L >= pl * 0.5
            if ok:
                roster = sum((EXP_ADD['heavy'] if h else 0) + EXP_ADD['light'] * nl for h, nl in squad_kinds(L))
                exp += round(KILL * L / pl) + (FIRST if k == 0 else 0) + DONE + round(roster * L / pl)
            while exp >= EXPLV: exp -= EXPLV; pl += 1
    return out
FIGHTS = fights()
def opened(walls, PL): return sum(1 for w in walls if w <= PL)

# ================================================================ ITEMS: the law and the catalogue
def P(t): return GROW ** (t - 1) if t >= 1 else 0.0
def G(g): return 1 + STEP * g
def base_tier(L): return min(10, 1 + (max(1, L) - 1) // 4)
BAND = (0.20, 0.70, 0.10)                                                # one below / on / one above
def roll_tier(L, rng):
    b = base_tier(L); r = rng.random()
    return max(1, min(10, b - 1 if r < BAND[0] else b if r < BAND[0] + BAND[1] else b + 1))
def scrap(t, k=1.25): return 100 * k ** (t - 1)          # scrap grows faster than power: it buys the ladder
def count_up(t, base):
    """the +n of a count rider at tier t on a count row of `base` (>= 4): round(P), never over 25% x P x base."""
    n = max(1, int(math.floor(P(t) + 0.5)))
    return min(n, max(1, int(math.floor(UP1 * P(t) * base + 1e-9))))

# (id, name, slot, par, lean at T1, price (fixed), fits (None = the whole category), why)
# par: the Par axis the line lifts IN FULL ('W' primary damage-equivalent, 'U' ability damage-equivalent, 'Sh'/'Fr'
# hull), or None. slot: W Weapon, U Utility, Sh Shield, Fr Hull (frame), E Engines.
CAT = {
 'capital': [
  ('cap_heavy', 'Heavy Battery', 'W', 'W', 'primary damage +25%', 'primary tracking -33%', None, 'REF. The line of battle: fewer, heavier hits from the BB twins, the DD director, the CV fighters'),
  ('cap_director', 'Director Suite', 'W', None, 'primary tracking +25%, shell / craft speed +25%, primary damage +5%', 'primary range -28%', None, 'Hits what jinks: raiders, a Drake after its warp, a talon on the burn; the DD leading from its Grapnel orbit'),
  ('cap_escort', 'Escort Hunter', 'W', None, 'against craft only: damage +25%, tracking +25%', 'none (conditional)', None, 'The raid guard: shreds the squads that web the party. Worth nothing on a boss, so Par never wears it'),
  ('cap_salvo', 'Salvo Core', 'U', 'U', 'every ability output +25%', 'ability reach / area -28%', None, 'REF. Alpha strike: Broadside, Bomber strike, Long Lance hit harder; you must be closer'),
  ('cap_magazine', 'Magazine Core', 'U', 'U', 'every ability output +25% AND +1 on the ability count (Broadside volleys 6 -> 7, torpedoes a bomber 4 -> 5)', 'ability reach / area -28%, and the rider price: top speed -17%', ['BATTLESHIP', 'CARRIER'], 'RIDER (+25% and +1). The broadside and the strike wing. The Lance is a system of one, so the DD cannot fit it'),
  ('cap_tempo', 'Tempo Core', 'U', 'U', 'every ability cooldown recovers +20% faster (a multiplier lean: T1 +20%)', 'top speed -24%', None, 'More Broadsides, Lances and Supercarriers; the same damage as Salvo, spent in more, smaller windows'),
  ('cap_bulwark', 'Bulwark Belt', 'Sh', 'Sh', 'hull +25%', 'top speed -24%', None, 'REF. The fortress that soaks the supers'),
  ('cap_aegis', 'Aegis Array', 'Sh', None, 'point defence damage +25%, PD range +18%', 'acceleration -33%', None, 'The escort screen: PD kills tridents, raider missiles and fighters over the party (never the BB\'s CIWS burst)'),
  ('cap_citadel', 'Armoured Citadel', 'Fr', 'Fr', 'hull +25%', 'turn rate -28%', None, 'REF. The second hull source; the tank\'s pick'),
  ('cap_warp', 'Warp Spine', 'Fr', None, 'warp safe limit +18% (2400 -> 2832 u), warp charge rate +25%', 'top speed -24%', None, 'CAPITAL ONLY (only capitals warp). Warp assault: slower on the helm, further and sooner on the jump'),
  ('cap_helm', 'Helm Drive', 'E', None, 'turn rate +25%, turning radius 25% tighter', 'top speed -24%', None, 'The broadside dancer: brings the BB\'s side round, keeps the DD\'s bow on the Lancer'),
 ],
 'freighter': [
  ('frt_heavy', 'Heavy Ordnance', 'W', 'W', 'weapon damage +25% (the spotter and its sentries; the lance\'s harm; the mortar)', 'primary tracking -33%', None, 'REF. The heavy hauler\'s guns'),
  ('frt_long', 'Long Arc', 'W', None, 'primary range +18%, shell speed +25%, primary damage +5%', 'primary tracking -33%', None, 'Artillery: the spotter paints from 944 u, the mortar lobs to 1298 u, the lance reaches 767 u'),
  ('frt_spinup', 'Spin-up Feed', 'W', 'W', 'primary damage ramps +5% a second on one target, to +25% after 5 s; drops back 1 s after the gun stops', 'primary range -28%', None, 'The long grind: the Tender\'s beam, the spotter holding paint, the mortar\'s rhythm. Reuses F1\'s Ramp lift'),
  ('frt_field', 'Field Core', 'U', 'U', 'every ability output +25% (Time on target, Overdrive, the Buster; field strength where a field deals no damage)', 'ability reach / area -28%', None, 'REF. The convergence, the overdrive, the buster, harder'),
  ('frt_emitter', 'Field Emitter', 'U', None, 'ability area +18%, ability duration +20% (Bubble, Repair and Overdrive fields, Gravity well, Shockwave)', 'top speed -24%', None, 'The field engineer: bigger, longer zones for the party'),
  ('frt_cargo', 'Cargo Plating', 'Sh', 'Sh', 'hull +25%', 'acceleration -33%', None, 'REF. The armoured hauler'),
  ('frt_buffer', 'Buffer Array', 'Sh', None, 'hull +12.5%', 'none (half the lean, R7)', None, 'The no-compromise hauler: less hull, no helm price, for a hull that already crawls'),
  ('frt_spine', 'Cargo Spine', 'Fr', 'Fr', 'hull +25%', 'turn rate -28%', None, 'REF. The second hull source'),
  ('frt_convoy', 'Convoy Rig', 'Fr', None, 'V boost lasts +20% (3 -> 3.6 s) and strafes +25% harder (+50% -> +62.5%)', 'top speed -24%', None, 'Freighters lost warp: this is how an 85 u/s hull steps out of a beam lane'),
  ('frt_hauler', 'Hauler Drive', 'E', None, 'top speed +22%, acceleration +22%', 'turn rate -28%', None, 'The slowest hulls in the game with no warp: 85 -> 104 u/s at T1'),
 ],
 'heavy': [
  ('hvy_heavy', 'Heavy Barrel', 'W', 'W', 'weapon damage +25% (blade, railgun, flak)', 'primary tracking -33% (the blade\'s sweep)', None, 'REF. Hit harder'),
  ('hvy_rapid', 'Rapid Action', 'W', 'W', 'primary rate +20% (swings, charge rate, flak bursts; a multiplier lean)', 'primary range -28%', None, 'Tempo: faster swings, faster rail charges (Overcharge reads charge rate), more flak'),
  ('hvy_exec', 'Executioner', 'W', None, 'against a target under 35% hull: damage +25%', 'none (conditional)', None, 'The finisher: raiders mid-fight, a boss\'s last third. Worth about +9% on a boss, so Par never wears it'),
  ('hvy_tactical', 'Tactical Core', 'U', 'U', 'every ability output +25%', 'ability reach / area -28%', None, 'REF. Hunters, Anchor lines, the Lunge, harder'),
  ('hvy_endurance', 'Endurance Core', 'U', None, 'every ability duration +20% (Anchor 8 -> 9.6 s, Taunt 6 -> 7.2 s, Prism and Whirlwind 2 -> 2.4 s)', 'top speed -24%', None, 'Hold the stance longer: plant longer, taunt longer, spin longer'),
  ('hvy_swarm', 'Swarm Rack', 'U', 'U', 'every ability output +25% AND +1 on the ability count (Hunters 6 -> 7, Flares 6 -> 7)', 'ability reach / area -28%, and the rider price: top speed -17%', ['SNIPER', 'WARDEN'], 'RIDER (+25% and +1). The Warden\'s swarm and the Sniper\'s flare ring. The Warrior has no count of 4+'),
  ('hvy_plating', 'Heavy Plating', 'Sh', 'Sh', 'hull +25%', 'astern speed -42%', None, 'REF. Stand and trade'),
  ('hvy_webbreak', 'Web Breaker', 'Sh', None, 'a web on you holds 25% shorter and slows you 25% less (the Hold ceiling, 60%)', 'none (conditional)', None, 'The raid survivor: webs are the danger in every add fight (pinned share 0-20%). Never a class\'s web-break button'),
  ('hvy_bulkhead', 'Bulkhead Frame', 'Fr', 'Fr', 'hull +25%', 'turn rate -28%', None, 'REF. The second hull source'),
  ('hvy_quickboost', 'Quick-Boost Frame', 'Fr', None, 'V boost recharges +20% faster (15 -> 12.5 s; a multiplier lean)', 'top speed -24%', None, 'More boosts: a Warrior closing, a Sniper leaving a lane, a Warden re-posting under Taunt'),
  ('hvy_vector', 'Vector Drive', 'E', None, 'strafe thrust +25%, astern speed +25%', 'top speed -24%', None, 'The side-stepper: circle the target with the nose on it'),
 ],
 'light': [
  ('lgt_heavy', 'Hot Barrel', 'W', 'W', 'weapon damage +25%', 'shot speed -33%', None, 'REF. Hit harder (the Dart\'s Pepperbox and rod caps are unchanged)'),
  ('lgt_redline', 'Redline', 'W', None, 'while your hull is under 50%: damage +25%', 'none (conditional)', None, 'The glass cannon\'s gamble: strongest when it should be running'),
  ('lgt_burst', 'Burst Feed', 'W', None, 'for 4 s after a V boost ends: primary rate +20%', 'none (conditional)', None, 'Hit and run: boost in, unload, boost out (about 27% uptime on a 15 s boost)'),
  ('lgt_ace', 'Ace Core', 'U', 'U', 'every ability output +25%', 'ability reach / area -28%', None, 'REF. Rod, Reverb, Venom, harder'),
  ('lgt_reset', 'Reset Core', 'U', None, 'a kill takes 25% off every ability cooldown left', 'none (conditional)', None, 'The raid dancer: chain kills into abilities; nothing on a lone boss'),
  ('lgt_plating', 'Reactive Plating', 'Sh', 'Sh', 'hull +25%', 'acceleration -33%', None, 'REF. A light that can take a beam tick'),
  ('lgt_ablative', 'Ablative Skin', 'Sh', None, 'hits under 10% of your hull deal 20% less (a multiplier lean; ceiling 40%)', 'none (conditional)', None, 'Swarm armour: raider lasers, the Lancer\'s bolts and tridents; never a super'),
  ('lgt_skin', 'Stressed Skin', 'Fr', 'Fr', 'hull +25%', 'turn rate -28%', None, 'REF. The second hull source'),
  ('lgt_feather', 'Featherweight Frame', 'Fr', None, 'acceleration +22%, turn rate +25%', 'hull -15%', None, 'The dogfighter: gives up hull for the tightest, snappiest helm'),
  ('lgt_burner', 'Burner Drive', 'E', None, 'V boost top speed +25% (+50% -> +62.5%)', 'turn rate -28%', None, 'Straight-line burst. Gear top speed never prices the Dart\'s damage (risk 4)'),
 ],
}
CHIPS = [   # generic: fit every hull; at most 3 combat and 3 utility fitted; NO salvage ladder (decision 3)
  ('chip_combat', 'Combat Chip', 'combat', 'C', 'every weapon\'s damage +8%', 'top speed -4%'),
  ('chip_gunner', 'Gunner Chip', 'combat', 'C', 'primary damage +10% (abilities unchanged)', 'turn rate -4%'),
  ('chip_hunter', 'Hunter Chip', 'combat', None, 'against craft only: damage +16%', 'none (conditional)'),
  ('chip_armour', 'Armour Chip', 'utility', 'A', 'hull +8%', 'turn rate -4%'),
  ('chip_engine', 'Engine Chip', 'utility', None, 'top speed +5%, turn rate +6%', 'none'),
  ('chip_target', 'Targeting Chip', 'utility', None, 'every reach +6% (the Supercarrier ring included)', 'none'),
]
BASIC_CHIPS = 3                                        # the starting kit: 3 Basic Combat Chips (combat kind)

def wearable(cls):
    cat = CLASSES[cls][0]
    return [l for l in CAT[cat] if l[6] is None or cls in l[6]]

# ================================================================ THE DROP SIM (what the reference wears, per fight)
def drop_sim(cls, runs=300, seed=7, top=80):
    """E[P(worn tier)] per power slot, and the best 3 combat / 3 armour chips' E[P], at the START of every fight.
    A crate: 70% from the pilot's own category (its core lines + the 6 chips), 30% from anything that drops."""
    rng = random.Random(seed + sum(map(ord, cls)))
    cat = CLASSES[cls][0]; own = CAT[cat] + [(c[0], c[1], 'chip', c[3]) for c in CHIPS]
    every = [l for c in CAT for l in CAT[c]] + [(c[0], c[1], 'chip', c[3]) for c in CHIPS]
    fits = {l[0] for l in wearable(cls)} | {c[0] for c in CHIPS}
    acc = {}
    for _ in range(runs):
        best = {'W': 0, 'U': 0, 'Sh': 0, 'Fr': 0}; cc = [0, 0, 0]; aa = [0, 0, 0]
        for L in range(1, top + 1):
            for k in range(4):
                a = acc.setdefault((L, k), {'W': 0.0, 'U': 0.0, 'Sh': 0.0, 'Fr': 0.0, 'C': [0.0] * 3, 'A': [0.0] * 3, 'own': 0.0, 'tier': 0.0})
                for s in best: a[s] += P(best[s])
                if best['Sh']: a['own'] += 1; a['tier'] += best['Sh']
                for i in range(3): a['C'][i] += P(cc[i]); a['A'][i] += P(aa[i])
                for _c in range(pm.crates(L)):
                    line = rng.choice(own) if rng.random() < 0.70 else rng.choice(every)
                    if line[0] not in fits: continue
                    t = roll_tier(L, rng)
                    if line[2] == 'chip':
                        tgt = cc if line[3] == 'C' else aa if line[3] == 'A' else None
                        if tgt is not None:
                            i = min(range(3), key=lambda j: tgt[j])
                            if t > tgt[i]: tgt[i] = t
                    elif line[3] in best and t > best[line[3]]:
                        best[line[3]] = t
    for a in acc.values():
        for s in ('W', 'U', 'Sh', 'Fr'): a[s] /= runs
        a['tier'] = a['tier'] / a['own'] if a['own'] else 0.0; a['own'] /= runs
        a['C'] = sorted((x / runs for x in a['C']), reverse=True); a['A'] = sorted((x / runs for x in a['A']), reverse=True)
    return acc
SIM = {}
def sim(cls):
    if cls not in SIM: SIM[cls] = drop_sim(cls)
    return SIM[cls]

# ================================================================ THE PILOT (one fight)
def pilot(cls, L, k, chips=False, g=None, gear=None, hull_lines=True, chip_mode=('C', 'C', 'C', 'A', 'A', 'A'), w_kind='dmg', u_kind='dmg'):
    """(hull, DPS vs craft, DPS vs a boss (flat part), rip rate r (share of boss TOTAL hull per s)) at boss level L,
    fight k. gear: None = the drop sim's E[P]; else a dict W/U/Sh/Fr/C/A of P values to force."""
    cat, B, wpn, ab = CLASSES[cls]
    PL = FIGHTS[L][k]; w, h = cv.split_points(PL - 1)
    na = opened(WALL_AB, PL); nc = opened(WALL_CHIP, PL)
    gr = gear if gear is not None else sim(cls)[(L, k)]
    if g is None: g = SHARE * min(MAXLEVEL, L)                           # the gate at boss L is L (cleared L-1, +1)
    Gg = G(g)
    dsh = 0.03 * w; hsh = UP1 * Gg * ((gr['Sh'] + gr['Fr']) if hull_lines else 0.0)
    if chips:
        ci = ai = 0; basic_left = BASIC_CHIPS
        for s in range(nc):
            kind = chip_mode[s]
            if kind == 'C':
                p = gr['C'][ci]; ci += 1
                if CHIP1 * p >= BASIC[0] or not basic_left: dsh += CHIP1 * p
                else: dsh += BASIC[0]; hsh += BASIC[1]; basic_left -= 1     # the kit's Basic chip until one beats it
            elif kind == 'A':
                p = gr['A'][ai]; ai += 1
                if p > 0: hsh += CHIP1 * p
    # a damage lean ADDS to the damage share; a multiplier lean (rate, cooldown) MULTIPLIES everything under it
    Dw = wpn * ((1 + dsh + UP1 * Gg * gr['W']) if w_kind == 'dmg' else (1 + dsh) * (1 + MULT1 * Gg * gr['W']))
    Da = sum(a for _, a in ab[:na]) * ((1 + dsh + UP1 * Gg * gr['U']) if u_kind == 'dmg' else (1 + dsh) * (1 + MULT1 * Gg * gr['U']))
    hull = (B + 5 * h) * (1 + hsh)
    r = flat = 0.0
    rp = RIP.get(cls)
    if rp and na >= rp['nth']:
        r = rp['use'] * rp['frac'] / rp['cycle']; flat = rp['use'] * rp['flat'] / rp['cycle'] * (1 + dsh + UP1 * Gg * gr['U'])
    return hull, Dw + Da, Dw + Da + flat, r

def kill_time(H, D, r): return H / (D + r * H)
def hull_in_60(D, r): return FIGHT * D / (1 - FIGHT * r)

# ================================================================ PAR: the reference, averaged over the level's 4 fights
_PAR = {}
def par(L):
    if L not in _PAR:
        rows = [pilot(REF, L, k) for k in range(4)]
        _PAR[L] = dict(H=sum(x[0] for x in rows) / 4, Dc=sum(x[1] for x in rows) / 4,
                       boss=sum(hull_in_60(x[2], x[3]) for x in rows) / 4, rows=rows)
    return _PAR[L]
def hull_scale(L): return par(L)['boss'] / par(1)['boss']                # boss hull growth (the rip counts on bosses)
def craft_scale(L): return par(L)['Dc'] / par(1)['Dc']                   # raiders' hull growth (no rip on craft)
def dmg_scale(L): return par(L)['H'] / par(1)['H']                       # every boss and raider move's damage growth
def boss_hull(L, who='lancer'): return par(L)['boss'] * (1 if who == 'lancer' else DRAKE_R)

# ---- the damage anchor: supers (the 250 burn, the 250 rock, the ram) stay at their rows; every other row x k
def sheet(rows, k):
    return sum(s if sup else s * k for s, land, sup in rows.values())
def solve_k(rows, target):
    H1 = par(1)['H']; need = H1 / target + REGEN * H1
    sup = sum(s for s, l, x in rows.values() if x); non = sum(s for s, l, x in rows.values() if not x)
    return (need - sup) / non
K = {}
def anchors():
    if not K:
        K['lancer'] = solve_k(LANCER, TTD_TARGET['lancer']); K['drake'] = solve_k(DRAKE, TTD_TARGET['drake'])
    return K
def boss_sheet(who):
    return sheet(LANCER if who == 'lancer' else DRAKE, anchors()[who])
def boss_real(who, band='long'):
    rows = LANCER if who == 'lancer' else DRAKE; k = anchors()[who]
    tot = 0.0
    for n, (s, land, sup) in rows.items():
        if n == 'wave' and band == 'long': continue
        tot += (s if sup else s * k) * land
    return tot
def ttd(H, S): return H / max(1e-9, S - REGEN * H)

# ================================================================ SECTION 1: THE CURVE
def curve_section():
    A = anchors(); p1 = par(1)
    print("== 1 · THE CURVE (reference = DESTROYER base class, no chips, walls 1/3/6; items +10% a tier; salvage step", STEP, ")")
    print(f"   pace: pilot level entering boss L: " + ', '.join(f"L{L}:PL{FIGHTS[L][0]}" for L in (1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 40, 60, 80)))
    for lv in WALL_AB + WALL_CHIP:
        for L in FIGHTS:
            if any(p >= lv for p in FIGHTS[L]):
                k = next(i for i, p in enumerate(FIGHTS[L]) if p >= lv); break
        print(f"   PL{lv:<2} ({'ability ' + str(WALL_AB.index(lv) + 1) if lv in WALL_AB and lv not in WALL_CHIP else ''}"
              f"{'chip ' + str(WALL_CHIP.index(lv) + 1) if lv in WALL_CHIP else ''}) reached at boss L{L} fight {k + 1}")
    print(f"   L1 par: hull {p1['H']:.1f}, DPS vs craft {p1['Dc']:.2f}; LANCER L1 = 60 s x par = {p1['boss']:.0f}; DRAKE = {p1['boss'] * DRAKE_R:.0f}")
    print(f"   (the approved 3820 assumed 3 kit chips + 1 Weapons point: x{3820 / p1['boss']:.3f} of this)")
    print(f"   damage anchor on NON-super rows: Lancer x{A['lancer']:.3f}, Drake x{A['drake']:.3f}; sheets {boss_sheet('lancer'):.2f} / {boss_sheet('drake'):.2f};"
          f" realistic long {boss_real('lancer'):.2f} (short {boss_real('lancer', 'short'):.2f}) / {boss_real('drake'):.2f}")
    print(f"   rows: Lancer guns 3.6 -> {3.6 * A['lancer']:.2f}, trident 15 -> {15 * A['lancer']:.1f}, wave 45 -> {45 * A['lancer']:.1f};"
          f" beam tick 50 / burn 250 and ram 40 unchanged. Drake gun 6 -> {6 * A['drake']:.2f}, scrap 18.75 -> {18.75 * A['drake']:.2f}; rock 250 unchanged")
    print("\n   L  | PL in | worn P W/U/Sh/Fr (E)      | g    | HullScale DamageScale craft | Lancer  Drake | par TTK L/D | TTD L / D | w h")
    for L in list(range(1, 41)) + [45, 50, 60, 80]:
        p = par(L); gr = sim(REF)[(L, 0)]; w, h = cv.split_points(FIGHTS[L][0] - 1)
        tk = sum(kill_time(p['boss'], x[2], x[3]) for x in p['rows']) / 4
        S_L = boss_sheet('lancer') * dmg_scale(L); S_D = boss_sheet('drake') * dmg_scale(L)
        print(f"   {L:<3}| {FIGHTS[L][0]:>2}-{FIGHTS[L][-1]:<3}| {gr['W']:.2f} {gr['U']:.2f} {gr['Sh']:.2f} {gr['Fr']:.2f}      | {SHARE * min(40, L):4.1f} | "
              f"{hull_scale(L):6.3f}   {dmg_scale(L):6.3f}   {craft_scale(L):6.3f} | {boss_hull(L):6.0f} {boss_hull(L, 'drake'):6.0f} | "
              f"{tk:4.1f} / {tk * DRAKE_R:4.1f} | {ttd(p['H'], S_L):4.1f} / {ttd(p['H'], S_D):4.1f} | {w} {h}")
    print("   level skipping (+2 on the TIO): the level-L par against the boss k levels up, TTK s / TTD s (Lancer)")
    for L in (5, 10, 20, 30, 40):
        p = par(L); cells = []
        for kk in (1, 2, 3):
            H = boss_hull(L + kk)
            tk = sum(kill_time(H, x[2], x[3]) for x in p['rows']) / 4
            cells.append(f"+{kk}: {tk:4.1f} / {ttd(p['H'], boss_sheet('lancer') * dmg_scale(L + kk)):4.1f}")
        print(f"     L{L:<3} " + ' | '.join(cells))
    print(f"   siege rows follow the Lancer row: base 1.0 x = {boss_hull(1):.0f}, pylon 0.125 x = {0.125 * boss_hull(1):.0f} at L1, both x HullScale(L)")
    print(f"   a home raid wave (3 pinners + 1 standoff, 229.7 row hull) at ANY level: {229.7 / (0.7 * par(1)['Dc']):.1f} s of par fire to clear")
    print("   within-level spread of the par TTK (walls and drops land mid-level):")
    for L in (1, 2, 3, 4, 5, 6, 8, 10, 20, 40):
        p = par(L); print(f"     L{L:<3} " + ' '.join(f"{kill_time(p['boss'], x[2], x[3]):5.1f}" for x in p['rows'])
                          + "  | TTD L " + ' '.join(f"{ttd(x[0], boss_sheet('lancer') * dmg_scale(L)):4.1f}" for x in p['rows']))

def class_section():
    print("\n== 1b · EVERY CLASS against the par boss: TTK s / TTD s (Lancer sheet), each flying its own category's drops, no chips")
    cols = (1, 2, 3, 4, 6, 8, 10, 20, 30, 40)
    print("   class        realistic " + ' '.join(f"{'L' + str(L):>9}" for L in cols))
    for c in CLASSES:
        row = []
        for L in cols:
            p = par(L); S = boss_sheet('lancer') * dmg_scale(L)
            xs = [pilot(c, L, k) for k in range(4)]
            tk = sum(kill_time(p['boss'], x[2], x[3]) for x in xs) / 4; hh = sum(x[0] for x in xs) / 4
            row.append(f"{tk:4.0f}/{ttd(hh, S):<4.0f}")
        print(f"   {c:<11}  {full_dps(c):5.1f}    " + ' '.join(row))
    rs = sorted(full_dps(c) for c in CLASSES)
    print(f"   fleet median realistic {(rs[5] + rs[6]) / 2:.1f}; the reference's {full_dps(REF):.1f}. A median-DPS class kills in {60 * full_dps(REF) / ((rs[5] + rs[6]) / 2):.1f} s at L20+.")

def chips_section():
    print("\n== 1c · THE SAME PILOT WITH EVERY OPEN CHIP SLOT FILLED (Combat, Combat, Combat, Armour, Armour, Armour in slot order;"
          " a Basic chip until a Combat chip drops; no chip ladder)")
    print("   L  | chips open | TTK par -> chipped | x DPS | TTD par -> chipped (Lancer) | x hull || with a chip ladder at g = 0.65L: TTK")
    for L in (1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 35, 40, 60):
        p = par(L); S = boss_sheet('lancer') * dmg_scale(L)
        xs = [pilot(REF, L, k, chips=True) for k in range(4)]
        tk0 = sum(kill_time(p['boss'], x[2], x[3]) for x in p['rows']) / 4
        tk1 = sum(kill_time(p['boss'], x[2], x[3]) for x in xs) / 4
        h1 = sum(x[0] for x in xs) / 4
        # chip ladder variant: chips lifted by G(g) as well
        lad = []
        for k in range(4):
            gr = dict(sim(REF)[(L, k)]); Gg = G(SHARE * min(40, L))
            gr = dict(gr, C=[c * Gg for c in gr['C']], A=[a * Gg for a in gr['A']])
            lad.append(pilot(REF, L, k, chips=True, gear=gr))
        tk2 = sum(kill_time(p['boss'], x[2], x[3]) for x in lad) / 4
        print(f"   {L:<3}| {opened(WALL_CHIP, FIGHTS[L][0])}-{opened(WALL_CHIP, FIGHTS[L][-1])}        | {tk0:4.1f} -> {tk1:4.1f}       | x{tk0 / tk1:4.2f} | "
              f"{ttd(p['H'], S):4.1f} -> {ttd(h1, S):4.1f}                | x{h1 / p['H']:4.2f} || {tk2:4.1f}")

# ================================================================ SECTION 2: RAIDS (boss-fight adds), re-run
HEAVY_DPS, LIGHT_ROW = 2.58, RV.ROW                     # twin laser, 1.29 a barrel (ruling)
REACT, EFF, RESPAWN, MISSILE, MISSILE_EVERY, LAND_PINNED = 2.0, 0.7, 30.0, 35.0, 12.0, 0.9
BEAM_FIRST, BEAM_EVERY, BEAM_WINDUP, BEAM_WAIT, ESCAPE = 6.0, 30.0, 6.0, 5.0, 0.6
PAIR = RV.PAIR
def Q(L): return Q_STEP ** (max(1, L) - 1)
def travel(slot, nl, heavy):
    lk, hk = PAIR[slot % 3]
    if heavy: return RV.travel(slot, nl)[0]
    c, b, t, r, *_ = RV.ROW[lk]; commit = c * b * t + r                   # a lights-only squad (Rusty L1-5)
    return RV.FORM_FOR + (commit - RV.hold(lk) - RV.EXTENT) / RV.burn(lk)
def strip_time(hull_left, dp): return REACT + hull_left / (EFF * dp)
def windup(L, pin_hull, dp): return max(BEAM_WINDUP / Q(L), strip_time(pin_hull, dp) + ESCAPE)

def fight(L, dt=0.05, ignore=False, adds_on=True, hp=None):
    """Solo par pilot vs the level's boss and its adds. Everything scales by LEVEL: add hull x craft_scale(L), add and
    boss damage x dmg_scale(L). Pilot DPS = par. Returns totals and per-second rates."""
    who = 'lancer' if rusty(L) else 'drake'
    p = par(L); dp_boss = sum(x[2] for x in p['rows']) / 4; r = sum(x[3] for x in p['rows']) / 4; dp = p['Dc']
    sh, sd = craft_scale(L), dmg_scale(L); k = anchors()[who]
    rows = LANCER if who == 'lancer' else DRAKE
    nonbeam_sheet = sum((s if sup else s * k) for n, (s, l, sup) in rows.items() if n != 'beam') * sd
    nonbeam_real = sum((s if sup else s * k) * l for n, (s, l, sup) in rows.items() if n != 'beam') * sd
    B0 = boss_hull(L, who); B = B0
    kinds = squad_kinds(L) if adds_on else []
    n = len(kinds)
    slots = [dict(k=i, st='wait', due=RESPAWN * i, first=True, h=kinds[i][0], nl=kinds[i][1]) for i in range(n)]
    t = dmg = pinned_t = add_dmg = boss_dmg = 0.0; missiles = []; beams = []; landed = 0; beam_dmg = 0.0
    arm = BEAM_FIRST; phase = 'idle'; tw = 0.0; burn_at = 0.0; wind = 0.0; last_pin = -99.0; pins_before = 0; t_charge = 0.0
    while B > 0 and t < 900:
        for sl in slots:
            if sl['st'] == 'wait':
                due = t >= sl['due'] or (sl['first'] and sl['k'] > 0 and B / B0 <= 1 - sl['k'] / n)
                if due:
                    lk, hk = PAIR[sl['k'] % 3]
                    sl.update(st='travel', T=travel(sl['k'], sl['nl'], sl['h']), lk=lk, hk=hk,
                              L_hp=[RV.ROW[lk][4] * sh] * sl['nl'], H_hp=RV.ROW[hk][4] * sh if sl['h'] else 0.0)
            elif sl['st'] == 'travel':
                sl['T'] -= dt
                if sl['T'] <= 0: sl.update(st='fight', eng=0.0, cd=0.0)
        eng = [sl for sl in slots if sl['st'] == 'fight']
        pin_hull = sum(x for sl in eng for x in sl['L_hp'] if x > 0)
        pins = sum(1 for sl in eng for x in sl['L_hp'] if x > 0)
        pinned = pin_hull > 0
        if pinned: pinned_t += dt; last_pin = t
        bd = (nonbeam_sheet if pinned else nonbeam_real) * dt; dmg += bd; boss_dmg += bd
        if who == 'lancer':                                             # THE BEAM, explicit
            if phase == 'idle' and t >= arm: phase = 'armed'; tw = t
            if phase == 'armed' and (pinned or t >= tw + BEAM_WAIT):
                burn_at = t + windup(L, pin_hull, dp); phase = 'winding'; t_charge = t; pins_before = pins
            elif phase == 'winding' and pins > pins_before:
                # THE ESCAPE FLOOR, live: a web that lands during the wind-up holds the burn until the par pilot
                # could strip every pinner now on it (2 s react + hull / (0.7 x par DPS)) plus 0.6 s to leave the line.
                burn_at = max(burn_at, t + strip_time(pin_hull, dp) + ESCAPE)
            if phase == 'winding': pins_before = pins
            if phase == 'winding' and t >= burn_at:
                full = last_pin >= burn_at - ESCAPE - dt / 2            # still held 0.6 s before the first tick
                hit = (250 if full else 250 * 0.25) * sd; dmg += hit; boss_dmg += hit; beam_dmg += hit
                landed += 1 if full else 0
                beams.append(('LAND' if full else 'dodged', round(burn_at - t_charge, 2)))
                phase = 'idle'; arm = max(tw + BEAM_EVERY, burn_at + 3.0)
        for sl in eng:
            sl['eng'] += dt
            a = sum(RV.ROW[sl['lk']][5] for x in sl['L_hp'] if x > 0) * sd * dt + (HEAVY_DPS * sd * dt if sl['H_hp'] > 0 else 0)
            dmg += a; add_dmg += a
            if sl['H_hp'] > 0 and RV.ROW[sl['hk']][6]:
                sl['cd'] -= dt
                if sl['cd'] <= 0 and pinned: sl['cd'] = MISSILE_EVERY; missiles.append(t + 10.0)
        for m in [m for m in missiles if m <= t]:
            missiles.remove(m); a = MISSILE * (LAND_PINNED if pinned else 0.15); dmg += a; add_dmg += a
        tgt = None
        if not ignore:                                                  # strip the web first, then heavies
            ready = [sl for sl in eng if sl['eng'] >= REACT]
            tgt = next((sl for sl in ready if any(x > 0 for x in sl['L_hp'])), None) or next((sl for sl in ready if sl['H_hp'] > 0), None)
        if tgt:
            hit = EFF * dp * dt                                         # overkill carries to the next body
            for j in range(len(tgt['L_hp'])):
                if hit <= 0: break
                d = min(hit, tgt['L_hp'][j]); tgt['L_hp'][j] -= d; hit -= d
            if hit > 0 and all(x <= 0 for x in tgt['L_hp']): tgt['H_hp'] = max(0.0, tgt['H_hp'] - hit)
            if all(x <= 0 for x in tgt['L_hp']) and tgt['H_hp'] <= 0: tgt.update(st='wait', due=t + RESPAWN, first=False)
        else:
            B -= (dp_boss + r * B0) * dt
        t += dt
    return dict(T=t, dps=dmg / t, add=add_dmg / t, boss=boss_dmg / t, pinned=pinned_t / t, landed=landed, beams=beams, beam_dmg=beam_dmg)

def alone(L):
    who = 'lancer' if rusty(L) else 'drake'
    f = fight(L, adds_on=False); return f
def raids_section():
    D1 = par(1)['Dc']
    print("\n== 2 · RAIDS: boss-fight adds re-run (heavy 2.58, heavies never web, Rusty escorts = squad wave 1 from L1, any web"
          " starts the beam, escape floor 0.6 s). Scaling: add hull x craft_scale(L), all damage x DamageScale(L)")
    print("   escape floor: windup = max(6 s / 1.01^(L-1), 2 s react + (pinner hull on you) / (0.7 x par DPS) + 0.6 s)."
          f" With raiders taking a level, the strip is the same at every level (par L1 DPS vs craft {D1:.1f}):")
    for name, hull in (('2 webifiers (Rusty L1-8)', 50), ('3 webifiers (squad 1 from L15)', 75), ('3 talons (squad 2)', 54),
                       ('3 pods (squad 3, L39)', 180), ('all 9 lights at once (L39 worst)', 309)):
        s = strip_time(hull, D1); print(f"     {name:<34} strip {s:4.2f} s -> floor {s + ESCAPE:4.2f} s")
    print("   L  | boss  | squads (H+lights)        | 6/Q(L) | squad-1 floor | windup vs squad 1 | margin || in the fight: wind-ups flown (s), beams landed on par")
    for L in (1, 3, 5, 7, 9, 11, 15, 17, 21, 25, 27, 29, 31, 33, 35, 37, 39, 41, 51):
        kinds = squad_kinds(L); n1 = kinds[0][1] if kinds else 0
        fl = strip_time(25 * n1, D1) + ESCAPE if n1 else 0.0
        wu = max(BEAM_WINDUP / Q(L), fl); f = fight(L)
        print(f"   {L:<3}| {'RUSTY' if rusty(L) else 'DRAKE'} | {' '.join(f'[{('H' if h else '')}+{nl}]' for h, nl in kinds):<24} | {BEAM_WINDUP / Q(L):5.2f}  | {fl:5.2f}         | {wu:5.2f}             | {wu - (fl - ESCAPE) if n1 else float('nan'):4.2f}   || "
              + ' '.join(f"{b[1]:.2f}" for b in f['beams']) + f"  landed {f['landed']}")
    print("\n   | L | adds | x boss | pinned | fight +% | adds' own DPS / DamageScale | full beams on par / fired | EXP +% (first fill) |")
    BANDS = [(1, 5), (6, 8), (9, 11), (12, 14), (15, 17), (18, 20), (21, 23), (24, 26), (27, 29), (30, 32), (33, 35), (36, 38), (39, 40)]
    res = {L: fight(L) for L in range(1, 41)}; base = {L: alone(L) for L in range(1, 41)}
    for a, b in BANDS:
        for who in ('lancer', 'drake'):
            Ls = [L for L in range(a, b + 1) if (who == 'lancer') == rusty(L)]
            if not Ls: continue
            x = sum(res[L]['dps'] / base[L]['dps'] for L in Ls) / len(Ls)
            pin = sum(res[L]['pinned'] for L in Ls) / len(Ls); fp = sum(res[L]['T'] / base[L]['T'] - 1 for L in Ls) / len(Ls)
            ad = sum(res[L]['add'] / dmg_scale(L) for L in Ls) / len(Ls)
            lan = sum(res[L]['landed'] for L in Ls); fired = sum(len([b for b in res[L]['beams'] if b != 'free']) for L in Ls)
            kinds = squad_kinds(Ls[0]); L0 = Ls[0]; pl = FIGHTS[L0][1]
            roster = sum((EXP_ADD['heavy'] if h else 0) + EXP_ADD['light'] * nl for h, nl in kinds)
            ex = roster * L0 / pl / (KILL * L0 / pl + DONE)
            print(f"   | {a}-{b} {'RUSTY' if who == 'lancer' else 'DRAKE'} | {' '.join(f'[{('H' if h else '')}+{nl}]' for h, nl in kinds) or '-'} | x{x:.2f} | {100 * pin:.0f}% | {100 * fp:+.0f}% | {ad:.2f} | {lan}/{fired} | +{100 * ex:.0f}% |")
    print("\n   TIME TO DIE, realistic (dodging), boss alone -> with adds; par gear on each class's base hull")
    for L in (1, 6, 9, 18, 20, 30, 39, 40):
        r = res[L]; b = base[L]; row = []
        for c in ('ECHO', 'WARDEN', 'DESTROYER', 'BATTLESHIP'):
            H = sum(pilot(c, L, k)[0] for k in range(4)) / 4
            f = lambda d: 'never' if d - REGEN * H <= 0 else f"{H / (d - REGEN * H):.0f}"
            row.append(f"{c[:3]} {H:4.0f}: {f(b['dps'])} -> {f(r['dps'])}")
        print(f"   L{L:<2} {'RUSTY' if rusty(L) else 'DRAKE'} (fight {r['T']:.0f} s, alone {b['T']:.0f} s) | " + ' | '.join(row))
    print("\n   IGNORE THE ADDS (worst case):")
    for L in (1, 9, 20, 39):
        ig = fight(L, ignore=True); print(f"     L{L}: x{ig['dps'] / base[L]['dps']:.2f}, pinned {100 * ig['pinned']:.0f}%, beams landed {ig['landed']}")
    print("\n   KEEPING PACE (raiders take a LEVEL): par seconds to kill one add, and one add's DPS as % of par hull / s")
    print("   L  | webifier | gunship | lancerkin || webifier dmg %/s | gunship laser %/s || old multiplier 1.025^(L-1) vs craft_scale")
    for L in (1, 5, 10, 20, 30, 40, 60):
        dp = par(L)['Dc']; H = par(L)['H']; sh, sd = craft_scale(L), dmg_scale(L)
        print(f"   {L:<3}| {25 * sh / (EFF * dp):6.2f}   | {100 * sh / (EFF * dp):6.2f}  | {150 * sh / (EFF * dp):6.2f}    || {100 * 1.0 * sd / H:6.3f}           | {100 * HEAVY_DPS * sd / H:6.3f}            || {1.025 ** (L - 1):5.2f} vs {sh:5.2f}")
    return res

# ================================================================ SECTION 3: ITEMS
def items_section():
    print("\n== 3 · ITEMS: the law (+10% compounding, 10 tiers), drops, the catalogue checks, and L40")
    print("   t | P(t)  | headline (T1 +25%) | reach/area (T1 18%) | top/accel (T1 22%) | chip (T1 8%) | +n on base 4/6/7 | price | scrap | first drops at L | main at L")
    for t in range(1, 11):
        lo = 4 * (t - 1) + 1
        first = max(1, lo - 4)
        print(f"   {t:<2}| {P(t):5.3f} | +{100 * UP1 * P(t):5.1f}%            | +{18 * P(t):4.1f}%              | +{22 * P(t):4.1f}%             | +{100 * CHIP1 * P(t):4.1f}%       | "
              f"+{count_up(t, 4)}/+{count_up(t, 6)}/+{count_up(t, 7)}         | fixed | {scrap(t):5.0f} | {first:<16} | {lo}-{lo + 3 if t < 10 else '40+'}")
    print(f"   with salvage: F = P(t) x (1 + {STEP} g); T10 at g26 = x{P(10) * G(26):.2f}, at g40 = x{P(10) * G(40):.2f} of the T1 headline")
    # the catalogue checks
    print("\n   catalogue checks:")
    ok = True
    for cat, lines in CAT.items():
        slots = {}
        for l in lines: slots.setdefault(l[2], []).append(l[1])
        leans = [(l[2], l[4]) for l in lines]
        dup = len(set(leans)) != len(leans)
        refs = {l[3] for l in lines if l[3] and l[6] is None and 'REF' in l[7]}
        per = {c: len(wearable(c)) for c in CLASSES if CLASSES[c][0] == cat}
        good = 8 <= len(lines) <= 12 and not dup and refs >= {'W', 'U', 'Sh', 'Fr'} and min(per.values()) >= 8
        ok &= good
        print(f"     {cat:<9} {len(lines)} lines | slots " + ', '.join(f"{s} {len(v)}" for s, v in slots.items())
              + f" | REF axes {sorted(refs)} | wearable per class {per} | duplicate lean: {dup} -> {'OK' if good else 'FAIL'}")
    print(f"     chips: {len(CHIPS)} generic lines ({sum(c[2] == 'combat' for c in CHIPS)} combat, {sum(c[2] == 'utility' for c in CHIPS)} utility) -> {'ALL OK' if ok else 'FAIL'}")
    # drop tier by level
    print("\n   drop tier by boss level (base = 1 + floor((L-1)/4); 20% one lower / 70% on / 10% one higher), and what Par wears (E[P] -> tier)")
    for L in (1, 2, 3, 4, 5, 8, 9, 12, 13, 16, 17, 20, 24, 28, 32, 36, 37, 40):
        gr = sim(REF)[(L, 0)]
        print(f"     L{L:<3} base T{base_tier(L):<2} | Par owns a hull part {100 * gr['own']:3.0f}%, mean tier worn T{gr['tier']:4.1f} | E[P] W {gr['W']:.2f} U {gr['U']:.2f} Sh {gr['Sh']:.2f} Fr {gr['Fr']:.2f}")
    # affordability of the 65% salvage share, per scrap law
    print("\n   salvage: can a rewards-only pilot keep 4 ladders at 65% of the gate? (% of gate reached; fleet income + scrap by tier)")
    inc = pm.run('bounty', hub=0)
    for kk in (1.10, 1.20, 1.25, 1.28):
        row = []
        for L in (5, 10, 20, 30, 40):
            fleet = inc[L]['cum'] - inc[L]['cs']
            sc = sum(4 * pm.crates(l) * sum(p * scrap(tt, kk) for p, tt in ((0.2, max(1, base_tier(l) - 1)), (0.7, base_tier(l)), (0.1, min(10, base_tier(l) + 1)))) for l in range(1, L + 1))
            a = cv.affordable(fleet + sc, 4, LADDER[0], LADDER[1], L)
            row.append(f"L{L} {100 * a / min(40, L):3.0f}%")
        print(f"     scrap 100 x {kk}^(t-1): " + '  '.join(row))
    # L40 builds
    print("\n   L40, median class (DESTROYER, capital lines), against the L40 Lancer (hull", f"{boss_hull(40):.0f}", "): TTK s / TTD s (Lancer; Drake)")
    L = 40; p = par(L); S_L = boss_sheet('lancer') * dmg_scale(L); S_D = boss_sheet('drake') * dmg_scale(L)
    T10 = {'W': P(10), 'U': P(10), 'Sh': P(10), 'Fr': P(10), 'C': [P(10)] * 3, 'A': [P(10)] * 3}
    NOHULL = dict(T10, Sh=0.0, Fr=0.0)
    def show(tag, **kw):
        xs = [pilot(REF, L, k, **kw) for k in range(4)]
        tk = sum(kill_time(p['boss'], x[2], x[3]) for x in xs) / 4; hh = sum(x[0] for x in xs) / 4
        print(f"     {tag:<58} hull {hh:6.0f} | TTK {tk:5.1f} | TTD {ttd(hh, S_L):5.1f}; {ttd(hh, S_D):5.1f}")
        return tk, hh
    show('Par (REF lines at worn tier, g26, no chips)')
    show('Par + 6 chips (3 Combat + 3 Armour)', chips=True)
    show('Par, no salvage (g0)', g=0)
    show('Par, no hull lines (Sh and Fr on non-hull lines)', hull_lines=False)
    show('stock (no parts, no chips)', gear={'W': 0, 'U': 0, 'Sh': 0, 'Fr': 0, 'C': [0] * 3, 'A': [0] * 3})
    show('T10 REF lines, g26, no chips', gear=T10)
    show('T10 REF lines, g40 (100% of the gate), no chips', gear=T10, g=40)
    show('CEILING: T10, g40, 3 Combat + 3 Armour (the best no-trade build)', gear=T10, g=40, chips=True)
    show('FASTEST: T10, g40, W+U damage, no hull lines, 3 Combat', gear=NOHULL, g=40, chips=True, chip_mode=('C', 'C', 'C', '-', '-', '-'))
    show('RATE: T10, g40, Tempo Core in U (cooldown), 3 Combat + 3 Armour', gear=T10, g=40, chips=True, u_kind='rate')
    show('RATE, no chips: T10, g26, Tempo Core in U', gear=T10, u_kind='rate')
    print("\n   multiplier leans (T1 +20%) against damage leans (T1 +25%), on one slot, median class: DPS ratio rate/damage")
    for L, t, g in ((1, 1, 0), (10, 3, 6.5), (20, 5, 13), (30, 8, 19.5), (40, 10, 26), (40, 10, 40)):
        G1 = {'W': P(t), 'U': P(t), 'Sh': P(t), 'Fr': P(t), 'C': [P(t)] * 3, 'A': [P(t)] * 3}
        out = []
        for ch in (False, True):
            for slot in ('w', 'u'):
                kw = {slot + '_kind': 'rate'}
                d0 = sum(pilot('SNIPER' if slot == 'w' else 'BATTLESHIP', L, k, chips=ch, g=g, gear=G1)[1] for k in range(4))
                d1 = sum(pilot('SNIPER' if slot == 'w' else 'BATTLESHIP', L, k, chips=ch, g=g, gear=G1, **kw)[1] for k in range(4))
                out.append(f"{'W (Sniper)' if slot == 'w' else 'U (Battleship)'}{' +chips' if ch else ''} x{d1 / d0:.2f}")
        print(f"     L{L:<2} T{t:<2} g{g:<4}: " + ' | '.join(out))
    # the ceiling for EVERY class: T10, g40, 3 Combat + 3 Armour, and its category's multiplier line where it has one
    print("\n   every class at L40: its own par TTK -> its ceiling (T10, 100% of the gate, 3 Combat + 3 Armour; the category's"
          " multiplier line in W or U where one exists)")
    RATE_W = {'heavy'}; RATE_U = {'capital'}
    for c in CLASSES:
        cat = CLASSES[c][0]
        base_ = [pilot(c, L, k) for k in range(4)]
        best = [pilot(c, L, k, gear=T10, g=40, chips=True, w_kind='rate' if cat in RATE_W else 'dmg',
                      u_kind='rate' if cat in RATE_U else 'dmg') for k in range(4)]
        t0 = sum(kill_time(p['boss'], x[2], x[3]) for x in base_) / 4; t1 = sum(kill_time(p['boss'], x[2], x[3]) for x in best) / 4
        h0 = sum(x[0] for x in base_) / 4; h1 = sum(x[0] for x in best) / 4
        print(f"     {c:<11} {cat:<9} TTK {t0:5.1f} -> {t1:5.1f} (x{t0 / t1:.2f})   TTD {ttd(h0, S_L):4.1f} -> {ttd(h1, S_L):4.1f}")
    # composition of the par's L40 hull and damage
    gr = {k2: sum(sim(REF)[(L, k)][k2] for k in range(4)) / 4 for k2 in ('W', 'U', 'Sh', 'Fr')}
    w, h = cv.split_points(FIGHTS[L][0] - 1); B = CLASSES[REF][1]; g = SHARE * 40
    base_, pts = B, 5 * h; drop = (B + pts) * UP1 * (gr['Sh'] + gr['Fr']); lev = (B + pts) * UP1 * (gr['Sh'] + gr['Fr']) * STEP * g
    tot = base_ + pts + drop + lev
    print(f"\n   Par's L40 hull {tot:.0f}: base {100 * base_ / tot:.0f}% · points {100 * pts / tot:.0f}% · parts (tier) {100 * drop / tot:.0f}% · salvage (levels) {100 * lev / tot:.0f}%"
          f"  -> gear {100 * (drop + lev) / tot:.0f}% of hull")
    dm = 1 + 0.03 * w; dgear = UP1 * gr['W']; dlev = dgear * STEP * g; dtot = dm + dgear + dlev
    print(f"   Par's L40 damage (weapon share): base 1 · points {0.03 * w:.2f} · parts {dgear:.2f} · salvage {dlev:.2f}  -> gear {100 * (dgear + dlev) / dtot:.0f}% of damage")
    for st in (0.03, 0.035, 0.05):
        l2 = (B + pts) * UP1 * (gr['Sh'] + gr['Fr']) * st * g; t2 = base_ + pts + drop + l2
        print(f"     step {st}: salvage {100 * l2 / t2:.0f}% of the L40 hull, gear {100 * (drop + l2) / t2:.0f}%")

def main():
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if what in ('all', 'curve'): curve_section()
    if what in ('all', 'classes'): class_section()
    if what in ('all', 'chips'): chips_section()
    if what in ('all', 'raids'): raids_section()
    if what in ('all', 'items'): items_section()

if __name__ == '__main__':
    main()
