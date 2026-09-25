# Kits v3.1 power table, curve anchor and helm. Copied from power_v3.py and edited for the owner's rulings:
#   * BASE CLASSES, NO CHIPS: the curve's reference pilot carries none; kill and time-to-die are re-anchored on it.
#   * Freighter F = Time on target again, as HITSCAN lines (no 1.2 s flight); Unmask is deleted.
#   * Supercarrier engages anything, missiles included (ranked as a sentry ranks: missiles first).
#   * Grapnel's torn chunk is a real hit: 1% of the anchor's TOTAL hull + 10, once per cast-off.
#   * Warp is capital-only; the other nine get the V boost (+50% top and thrust, +50% strafe, 3 s, cooldown 15 s).
#     The Dart prices its guns from top speed, so the boost reaches its Pepperbox and rod (the generic path).
#   * Supers stay 250 (burn, 5 ticks after fix B) and 250 (rock) at L1.
# Method as v2/v3: "sheet" = max uptime, "realistic" = the uptime a pilot holds. Read-only model.
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__)); UP = os.path.dirname(HERE)
sys.path.insert(0, UP)
import curve as cv                     # PL table, points split, rarity ramp, items rows (read-only import)

# ---------------------------------------------------------------- boss incoming (per unit of scale)
LAN = {"<=340": 8.0, "340-900": 6.7, "900-1100": 3.4, "1100-1920": 3.4, ">1920": 1.9}   # realistic, today's boss
DRA = {"<=340": 3.9, "340-900": 3.9, "900-1100": 3.9, "1100-1920": 2.7, ">1920": 2.7}
LAN_SHEET, DRA_SHEET = 17.05, 13.23             # boss_model 1.3 (beam at 4 ticks)
LAN_SUPER_SHEET_OLD, LAN_SUPER_SHEET = 200 / 30, 250 / 30   # fix B: the burn is 5 ticks = 250
DRA_SUPER_SHEET = 250 / 30                      # the rock
LAN_SUPER_REAL_OLD, LAN_SUPER_REAL = 1.67, 1.67 * 250 / 200
DRA_SUPER_REAL = 2.08
REGEN = 0.005
def mix(tab, parts): return sum(tab[b] * w for b, w in parts.items())

# ---------------------------------------------------------------- SNIPER (v3, unchanged): v1 Anchor + Overcharge
full, rec = 1.6, 0.2
duty_anc = 8.0 / (8.0 + 12.0 + 0.3)
def rail(dmg, hold, rate): return dmg / (hold / rate + rec)
sn_un, sn_an = rail(45 * 2.2, 2.4, 1.0), rail(45 * 2.2, 2.4, 2.5)
sn_sheet = (1 - duty_anc) * sn_un + duty_anc * sn_an; sn_real = sn_sheet * 0.90
sn_anchor_share = duty_anc * (sn_an - sn_un)            # what the Anchor adds on top of the railgun (walled)

# ---------------------------------------------------------------- CARRIER: Supercarrier, anything incl. missiles
fighter_dps = 3 * 3.5 / 0.35
sc_duty = 20.0 / (20.0 + 30.0)
# a lone Lancer throws 3 trident seekers every 15 s; a seeker spends ~4 s inside the 600 u ring and one patrol
# craft peels off for one ~1.2 s pass: 3 x 1.2 / (3 craft x 15 s) = 8% of the patrol's time
divert = 3 * 1.2 / (3 * 15)
sc_sheet = fighter_dps * sc_duty * (1 - divert)
sc_real = fighter_dps * sc_duty * 0.85 * 0.6 * (1 - divert)
cv_sheet = 56.1 - 4.0 + sc_sheet
cv_real = 50 - 4.0 * 50 / 56.1 + sc_real
# hull it saves: seekers at the carrier inside the ring while the mode is up, half of which would land
sc_saved = 3 * 15 / 15 * sc_duty * 0.5

# ---------------------------------------------------------------- FREIGHTER: Time on target, hitscan
tot_dmg, tot_cd, tot_guns, sentry_reach = 40.0, 16.0, 3, 0.85
tot_sheet = (1 + tot_guns) * tot_dmg / tot_cd
tot_real = (tot_dmg + tot_guns * tot_dmg * sentry_reach) / tot_cd * 0.9       # paint up 90%; no flight to dodge
fr_sheet = 65.0
fr_real = 53.5 - 7.0 + tot_real + 30 * (0.85 - 0.80)                       # v2 TOT out (7 real); thrown sentries

# ---------------------------------------------------------------- DART: Ramjet (v3) + the V boost in its pricing
rj_duty = 8.0 / 20.0
rj_mean_sheet = (0.25 * 5 + 0.5 * 3) / 8          # ramp 0 -> 0.5 in 5 s, hold 3 s
rj_mean_real = 0.22
vb_duty_sheet, vb_use = 3.0 / 15.0, 0.8           # the boost: 3 s of every 15; a pilot spends 20% of them on flight
vb_duty_real = vb_duty_sheet * vb_use
def pep_bonus(pb, ram): return pb * 0.5 + (1 - pb) * rj_duty * ram        # the Pepperbox caps at +50% of top
def rod(b): top = 260 * (1 + b) + 100; return 180 * min(2.0, max(1.0, top / 260))
pep3_sheet, pep3_real = 45 * rj_duty * rj_mean_sheet, 45 * rj_duty * rj_mean_real * 0.87
rod3_sheet, rod3_real = (rod(0.5) - rod(0)) * 0.6 / 12, (rod(0.3) - rod(0)) * 0.6 / 12 * 0.87
pep_sheet, pep_real = 45 * pep_bonus(vb_duty_sheet, rj_mean_sheet), 45 * pep_bonus(vb_duty_real, rj_mean_real) * 0.87
rod_price_sheet = 0.8 * rod(1.0) + 0.2 * (0.6 * rod(0.5) + 0.4 * rod(0))          # 12 s rods, 15 s boost: 80% boosted
rod_price_real = 0.5 * (0.6 * rod(0.8) + 0.4 * rod(0.5)) + 0.5 * (0.6 * rod(0.3) + 0.4 * rod(0))
rod_sheet, rod_real = (rod_price_sheet - rod(0)) / 12, (rod_price_real - rod(0)) / 12 * 0.87
da_sheet, da_real = 70.1 + pep_sheet + rod_sheet, 61 + pep_real + rod_real
da3_sheet, da3_real = 70.1 + pep3_sheet + rod3_sheet, 61 + pep3_real + rod3_real
slip_share = 0.16 + 0.12                                                     # time at >= 325 u/s: sprint + boost

# ---------------------------------------------------------------- the classes (hull, top, tier, sheet, real)
# learn: the three walled abilities IN LEARN ORDER with their sheet DPS vs a lone boss. The weapon, its R action,
# passives (PD, Slipstream, Backstab, Overcharge) and the drive (V) are never walled.
def dd_rip_sheet(boss_hull): return (0.01 * boss_hull + 10) / (5.0 + 16.0)   # one rip per swing + 16 s cooldown
C = {
 'BATTLESHIP': dict(tier='capital',  hull=500, top=88,  sheet=52.3, real=46.0, learn=[('F Broadside', 27.3), ('Q Brace', 0), ('E CIWS', 0)]),
 'CARRIER':    dict(tier='capital',  hull=425, top=99,  sheet=cv_sheet, real=cv_real, learn=[('F Bomber strike', 17.45), ('E Warp gunships', 9.6), ('Q Supercarrier', sc_sheet)],
                    lm={"340-900": 0.24, "1100-1920": 0.76}),
 'DESTROYER':  dict(tier='capital',  hull=395, top=117, sheet=61.7, real=55.0, learn=[('F Long Lance', 16.7), ('Q Suppressing fire', 0), ('E Grapnel', 1.0)],
                    lm={"<=340": 0.24, "340-900": 0.76}),
 'FREIGHTER':  dict(tier='freighter', hull=450, top=120, sheet=fr_sheet, real=fr_real, learn=[('F Time on target', tot_sheet), ('Q Bubble', 0), ('E Redeploy', 0)],
                    lm={"340-900": 1}),
 'TENDER':     dict(tier='freighter', hull=380, top=120, sheet=48.4, real=46.0, learn=[('F Overdrive', 6.6), ('Q Repair field', 0), ('E Resupply', 1.8)]),
 'BASTION':    dict(tier='freighter', hull=420, top=120, sheet=55.0, real=47.0, learn=[('F Bunker buster', 30.0), ('Q Shockwave', 0), ('E Gravity well', 0)]),
 'WARRIOR':    dict(tier='heavy', hull=300, top=190, sheet=74.7, real=50.0, learn=[('E Lunge', 5.7), ('Q Whirlwind', 0), ('F Prism stance', 4.0)]),
 'SNIPER':     dict(tier='heavy', hull=240, top=190, sheet=sn_sheet, real=sn_real, learn=[('F Anchor', sn_anchor_share), ('Q Tether mine', 0), ('E Flares', 0)],
                    lm={">1920": 1}),
 'WARDEN':     dict(tier='heavy', hull=270, top=190, sheet=53.0, real=49.0, learn=[('F Hunters', 19.3), ('Q Taunt', 0), ('E Flak curtain', 0)],
                    lm={"340-900": 1}, guard=1 - 0.33 * 6 / 20),
 'DART':       dict(tier='light', hull=200, top=260, sheet=da_sheet, real=da_real, learn=[('F Rod from God', 25.1), ('Q Ramjet', pep3_sheet + rod3_sheet), ('E Slingshot', 0)],
                    lm={"340-900": 1}, guard=1 - 0.3 * slip_share),
 'ECHO':       dict(tier='light', hull=180, top=260, sheet=77.4, real=72.0, learn=[('F Reverb', 11.4), ('Q Rewind', 0), ('E EMP', 0)]),
 'WRAITH':     dict(tier='light', hull=220, top=260, sheet=77.1, real=64.0, learn=[('F Veil', 5.5), ('Q Venom', 6.25), ('E Shadow step', 0)]),
}
V2LOST = {"BATTLESHIP": (0.11, 0.18), "TENDER": (0.82, 0.34), "BASTION": (0.19, 0.27), "WARRIOR": (1.30, 0.48),
          "WRAITH": (1.48, 0.60), "ECHO": (1.34, 0.70)}                        # hull lost vs today's boss over kill @ 3000
V3 = {"CARRIER": 52.6, "DESTROYER": 55.0, "FREIGHTER": 56.1, "DART": 66.8}

def fp(c):                                   # the weapon's share of the sheet (never walled)
    d = C[c]; return (d['sheet'] - sum(s for _, s in d['learn'])) / d['sheet']
def frac(c, na):                             # share of full damage with `na` abilities open
    d = C[c]; return fp(c) + sum(s for _, s in d['learn'][:na]) / d['sheet']

# ---------------------------------------------------------------- the walls and the pace (level_gates.md, owner-approved)
WALLS = [(1, 'A', 1), (2, 'C', 1), (3, 'A', 2), (4, 'C', 2), (6, 'A', 3), (8, 'C', 3), (10, 'C', 4), (12, 'C', 5), (14, 'C', 6)]
def opened(kind, PL): return sum(1 for (lv, k, n) in WALLS if k == kind and lv <= PL)
def fights(top=80, kills=4):
    pl, exp, out = 1, 0, {}
    for L in range(1, top + 1):
        out[L] = []
        for k in range(kills):
            out[L].append(pl)
            ok = L >= pl * 0.5
            ke = round(cv.KILL * L / pl) if ok else 0
            exp += (ke + (cv.FIRST if k == 0 else 0) + cv.DONE) if ok else 0
            while exp >= cv.EXPLV: exp -= cv.EXPLV; pl += 1
    return out
FIGHTS = fights()
def fL(c, L): return sum(frac(c, opened('A', p)) for p in FIGHTS[L]) / len(FIGHTS[L])
def median(xs): s = sorted(xs); n = len(s); return (s[n // 2 - 1] + s[n // 2]) / 2 if n % 2 == 0 else s[n // 2]
def Mw(L): return median([C[c]['real'] * fL(c, L) for c in C])          # the fleet's median walled realistic DPS

# ---------------------------------------------------------------- Par with NO chips (N = 0), today's / capped items
REF_B = 395                                  # the reference hull: the DESTROYER (the approved curve's reference)
def par_growth(L, items):
    it = cv.ITEMS[items]; g = cv.PAR['share'] * cv.cap(L); r = cv.rho(L); p = cv.parts(L); G = 1 + cv.STEP * g
    w, h = cv.split_points(cv.PL[min(max(1, L), max(cv.PL))] - 1)
    dmg = (1 + 0.03 * w - p * it['down']) * (1 + p * it['rate'] * r * G)
    hull = (REF_B + 5 * h) * (1 + p * it['sh'] * r * G)
    return dmg, hull
def scales(L, items='today'):
    d1, h1 = par_growth(1, items); d, h = par_growth(L, items)
    return (Mw(L) * d) / (Mw(1) * d1), h / h1        # HullScale, DamageScale

# ---------------------------------------------------------------- the anchor
w1 = cv.split_points(cv.PL[1] - 1)[0]
A = 60 * Mw(1) * (1 + 0.03 * w1)                     # Lancer L1 hull
AD = A * 700 / 760                                   # Drake keeps today's ratio
# the DD's torn chunk depends on the boss's hull; one pass is enough (it moves the median by < 0.1)
C['DESTROYER']['sheet'] += dd_rip_sheet(A)
C['DESTROYER']['real'] += dd_rip_sheet(A) * 0.8
C['DESTROYER']['learn'][2] = ('E Grapnel', 1.0 + dd_rip_sheet(A))
A = 60 * Mw(1) * (1 + 0.03 * w1); AD = A * 700 / 760
Mfull = median([C[c]['real'] for c in C])

# damage anchor k on the NON-SUPER move rows so the reference (395 hull, no chips) holds its time to die
def k_for(ttd, sheet, sup, H=REF_B): return (H / ttd + REGEN * H - sup) / (sheet - sup)
LAN_SHEET_B = LAN_SHEET - LAN_SUPER_SHEET_OLD + LAN_SUPER_SHEET
kL, kD = k_for(30.7, LAN_SHEET_B, LAN_SUPER_SHEET), k_for(41.5, DRA_SHEET, DRA_SUPER_SHEET)
K = 0.64
def lan_sheet(k): return (LAN_SHEET_B - LAN_SUPER_SHEET) * k + LAN_SUPER_SHEET
def dra_sheet(k): return (DRA_SHEET - DRA_SUPER_SHEET) * k + DRA_SUPER_SHEET
def ttd(H, sheet): return H / max(1e-9, sheet - REGEN * H)

def main():
    print("== v3.1 pieces")
    print(f"  SNIPER (v3): anchor duty {duty_anc:.3f}; sheet {sn_sheet:.1f} real {sn_real:.1f}; the Anchor's walled share {sn_anchor_share:.1f} sheet")
    print(f"  CARRIER: patrol {fighter_dps:.0f} DPS firing, duty {sc_duty:.2f}, {100*divert:.0f}% of it on seekers -> +{sc_sheet:.1f} sheet +{sc_real:.2f} real;"
          f" sheet {cv_sheet:.1f} real {cv_real:.1f} (v3 52.6); seekers it stops ~{sc_saved:.2f} DPS sheet before k")
    print(f"  FREIGHTER: Time on target hitscan {tot_sheet:.1f} sheet / {tot_real:.2f} real (v2 flight 7.0); sheet {fr_sheet:.1f} real {fr_real:.1f}")
    print(f"  DART: pepper v3 +{pep3_sheet:.1f}/{pep3_real:.1f} -> v3.1 +{pep_sheet:.1f}/{pep_real:.1f}; rod v3 +{rod3_sheet:.1f}/{rod3_real:.1f} -> v3.1 +{rod_sheet:.1f}/{rod_real:.1f}"
          f" (mean price {rod_price_sheet:.1f} / {rod_price_real:.1f}); sheet {da_sheet:.1f} real {da_real:.1f} (v3 {da3_real:.1f})")
    print(f"  DESTROYER: torn chunk at the L1 Lancer ({A:.0f}) = {0.01*A+10:.1f} a cast-off -> +{dd_rip_sheet(A):.2f} sheet, +{0.8*dd_rip_sheet(A):.2f} real;"
          f" sheet {C['DESTROYER']['sheet']:.1f} real {C['DESTROYER']['real']:.1f}")

    print("\n== the anchor (no chips; 1 Weapons point at L1; ability walls on the level's four fights)")
    print(f"  L1 fights at PL {FIGHTS[1]}; median walled realistic {Mw(1):.2f}, median full {Mfull:.2f}")
    print(f"  Lancer L1 = 60 x {Mw(1):.2f} x {1+0.03*w1:.2f} = {A:.0f}   Drake = {AD:.0f}   (approved curve, 3 kit chips: 3820 / 3520)")
    print(f"  non-super damage anchor for TTD 30.7 s (Lancer) = {kL:.3f}, 41.5 s (Drake) = {kD:.3f}; one k = {K}")
    print(f"  Lancer sheet at k: {lan_sheet(K):.2f} (burn 250 kept), Drake {dra_sheet(K):.2f} (rock 250 kept)")
    print(f"  reference TTD at k={K}: Lancer {ttd(REF_B, lan_sheet(K)):.1f} s, Drake {ttd(REF_B, dra_sheet(K)):.1f} s;"
          f"  with no anchor (k=1): {ttd(REF_B, lan_sheet(1)):.1f} / {ttd(REF_B, dra_sheet(1)):.1f} s")
    print("  moves at k: Lancer guns 3.6 -> {:.2f}, trident seeker 15 -> {:.1f}, wave 45 -> {:.1f}, ram 40 -> {:.1f}; Drake gun 6 -> {:.2f}, scrap 18.75 -> {:.1f}".format(
          3.6*K, 15*K, 45*K, 40*K, 6*K, 18.75*K))

    print("\n== THE TABLE: base classes, NO chips, L1")
    print("| class | tier | hull | top | sheet | realistic (v3) | kill L1 (walled) | kill from L6 | TTD Lancer / Drake (stops dodging) | hull lost Lancer / Drake (realistic, own kill) |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    rows = []
    for c, d in C.items():
        re = d['real']; H = d['hull']
        kill1 = 60 * Mw(1) / (re * fL(c, 1)); kill6 = 60 * Mfull / re
        tl, td = ttd(H, lan_sheet(K)), ttd(H, dra_sheet(K))
        if 'lm' in d:
            g = d.get('guard', 1.0)
            il, idr = mix(LAN, d['lm']), mix(DRA, d['lm'])
        else:
            l0, d0 = V2LOST[c]; T0 = 3000 / re
            il, idr = l0 * H / T0 + REGEN * H, d0 * H / T0 + REGEN * H; g = 1.0
        il = (max(0, il - LAN_SUPER_REAL_OLD) * K + LAN_SUPER_REAL) * g
        idr = (max(0, idr - DRA_SUPER_REAL) * K + DRA_SUPER_REAL) * g
        ll = max(0, (il - REGEN * H) * kill6 / H); dl = max(0, (idr - REGEN * H) * kill6 / H)
        rows.append((re, c, d, kill1, kill6, tl, td, ll, dl))
    rows.sort(key=lambda r: r[0])
    for re, c, d, kill1, kill6, tl, td, ll, dl in rows:
        was = f" ({V3[c]})" if c in V3 else ""
        print(f"| {c} | {d['tier']} | {d['hull']} | {d['top']} | {d['sheet']:.1f} | **{re:.1f}**{was} | {kill1:.0f} s | {kill6:.0f} s | {tl:.0f} / {td:.0f} s | {ll:.2f} / {dl:.2f} |")
    rs = [r[0] for r in rows]
    lights = [r[0] for r in rows if r[2]['tier'] == 'light']; non = [r[0] for r in rows if r[2]['tier'] != 'light']
    print(f"median {median(rs):.1f} mean {sum(rs)/12:.1f} spread {rs[0]:.1f}-{rs[-1]:.1f} = x{rs[-1]/rs[0]:.2f}; lowest light {min(lights):.1f}, highest non-light {max(non):.1f}")

    print("\n== walls: share of full damage at L1 (A1 only) and kill by level (Lancer), every class")
    cols = (1, 2, 3, 4, 5, 6)
    print("| class | weapon share | A1 / A2 / A3 shares | " + " | ".join(f"L{L}" for L in cols) + " |")
    print("|---|---|---|" + "---|" * len(cols))
    for c, d in C.items():
        sh = [s / d['sheet'] for _, s in d['learn']]
        ks = [60 * Mw(L) / (d['real'] * fL(c, L)) for L in cols]
        print(f"| {c} | {fp(c):.2f} | " + " / ".join(f"{x:.2f}" for x in sh) + " | " + " | ".join(f"{x:.0f}" for x in ks) + " |")

    for items in ('today', 'capped'):
        print(f"\n== PAR, no chips, {items} items (HullScale from the fleet's median walled DPS; DamageScale from the DD's hull)")
        print("| L | PL in | HullScale | DamageScale | Lancer hull | Drake hull | approved curve (3 kit chips) H / D |")
        print("|---|---|---|---|---|---|---|")
        for L in list(range(1, 11)) + [15, 20, 25, 30, 40, 60, 80]:
            hs, ds = scales(L, items); oh, od = cv.par_scales(L, items)
            print(f"| {L} | {FIGHTS[L][0]}-{FIGHTS[L][-1]} | {hs:.3f} | {ds:.3f} | {A*hs:.0f} | {AD*hs:.0f} | {oh:.3f} / {od:.3f} |")

    print("\n== CHIPS ARE UPSIDE: the median pilot at par, plus every open chip slot filled (3 Combat first, then Armour)")
    print("   (i) today's chip rows (+8% up, -5% drawback) at the par's rarity AND levelled on the slot at the par's 65%")
    print("   (ii) today's rows at the par's rarity, chip slots NOT levelled")
    print("   (iii) the proposed budget: no levels on chip slots; a chip's up = 10% at T10, compounding 10% a tier; no drawback")
    print("| L | open chip slots | tier | kill: (i) / (ii) / (iii) | TTD Lancer: (i) / (ii) / (iii) |")
    print("|---|---|---|---|---|")
    it = cv.ITEMS['today']
    for L in (1, 2, 4, 8, 10, 12, 14, 20, 30, 40):
        PLend = FIGHTS[L][-1]; n = opened('C', PLend); c_n, a_n = min(3, n), max(0, n - 3)
        tier = 1 if L < 5 else min(10, 2 + (L - 5) // 4)
        g = cv.PAR['share'] * cv.cap(L); r = cv.rho(L); p = cv.parts(L); G = 1 + cv.STEP * g
        w, h = cv.split_points(cv.PL[L] - 1)
        P_d, P_h = 0.03 * w - p * it['down'], p * it['sh'] * r * G
        base_d = (1 + P_d) * (1 + p * it['rate'] * r * G); base_h = (REF_B + 5 * h) * (1 + P_h)
        out_k, out_t = [], []
        for up_d, up_h, down in ((0.08 * r * G, 0.08 * r * G, 0.05), (0.08 * r, 0.08 * r, 0.05), (0.10 / 1.1 ** (10 - tier),) * 2 + (0.0,)):
            dm = (1 + P_d + c_n * up_d - a_n * down) * (1 + p * it['rate'] * r * G)
            hl = (REF_B + 5 * h) * (1 + P_h + a_n * up_h - c_n * down)
            hs, ds = scales(L); sheet = lan_sheet(K) * ds
            out_k.append(60 * base_d / dm); out_t.append(ttd(hl, sheet))
        print(f"| {L} | {n} ({c_n} C + {a_n} A) | T{tier} | " + " / ".join(f"{x:.0f}" for x in out_k) + " s | " + " / ".join(f"{x:.0f}" for x in out_t) + " s |")
    print("   par: 60 s, and TTD {:.1f} s at every level by construction".format(ttd(REF_B, lan_sheet(K))))

    print("\n== HELM: capitals warp (hold V), the other nine boost (+50% top/thrust, +50% strafe, 3 s, cooldown 15 s)")
    def accel(T, k, top, lift=1.0, v0=0.0, dt=0.001):
        v, t, d = v0, 0.0, 0.0
        while v < top * lift - 1e-3 and t < 30:
            v = min(top * lift, v + (T * lift - k * v) * dt); d += v * dt; t += dt
        return t, d
    def warp_eff(top, T, hop, hold, cd, dis=0.0):
        t_acc, d_acc = accel(T, 0.35, top)
        cycle = hold + cd
        fly = max(0.0, cycle - dis - t_acc)
        return (top * fly + d_acc + hop) / cycle
    def boost_eff(top, T, dur=3.0, cd=15.0, lift=1.5, dt=0.001):
        v, d, t = top, 0.0, 0.0
        while t < cd:
            L = lift if t < dur else 1.0
            v = min(top * L, v + (T * L - 0.35 * v) * dt); d += v * dt; t += dt
        return d / cd
    print("| hull | top (today) | thrust | long haul today | long haul v3.1 |")
    print("|---|---|---|---|---|")
    for name, top0, top, T0, T in (("BATTLESHIP", 104, 88, 56, 47), ("CARRIER", 116.48, 99, 58.24, 49.5), ("DESTROYER", 130, 117, 70, 63)):
        today = warp_eff(top0, T0, 1200, 3.0, 30.0)
        safe = warp_eff(top, T, 2400, 1.0 + 2400 / 800, 20.0); over = warp_eff(top, T, 3300, 1.0 + 3300 / 800, 20.0, dis=6.0)
        print(f"| {name} | {top} ({top0}) | {T} ({T0}) | {today:.0f} u/s (warp 1200 / 30 s) | {safe:.0f} u/s (warp 2400 / 20 s); {over:.0f} with a full overshoot |")
    for name, top0, top, T0, T in (("FREIGHTERS (default 120)", 85, 120, 45, 63.5), ("FREIGHTERS (kept at 85)", 85, 85, 45, 45),
                                   ("HEAVIES", 190, 190, 130, 130), ("LIGHTS", 260, 260, 190, 190)):
        today = warp_eff(top0, T0, 1200, 3.0, 30.0)
        print(f"| {name} | {top} ({top0}) | {T} ({T0}) | {today:.0f} u/s (warp) | {boost_eff(top, T):.0f} u/s (boost; no warp) |")
    for name, top, T in (("FREIGHTER at 120", 120, 63.5), ("HEAVY", 190, 130), ("LIGHT", 260, 190)):
        t, _ = accel(T, 0.35, top, 1.5, v0=top)
        print(f"   {name}: the boost reaches its new top {top*1.5:.0f} u/s in {t:.2f} s")
    print(f"   DD swing at 320 u off the Lancer: {117/320:.3f} rad/s vs the Lancer's 0.30 x 1.01^(L-1): matched at L{1+math.log(117/320/0.30)/math.log(1.01):.0f}")

if __name__ == '__main__':
    main()
