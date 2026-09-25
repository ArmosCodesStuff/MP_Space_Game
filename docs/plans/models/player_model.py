# Reference-pilot model. Every constant below is read from the repo (file noted) unless marked ASSUME.
import math
S = lambda L: 1.025 ** (max(1, L) - 1)                 # Missions.S
KILL, FIRST, DONE, EXPLV = 300, 250, 100, 1000         # Missions / Progression
BOUNTY = 2000                                          # Missions.BountyBase, solo
def crates(L): return 2 + (L >= 6) + (L >= 11)         # Loot.CratesFor
def scrap_avg(L):                                      # Economy.ScrapValue x Loot.RollRarity
    if L >= 11: return .55*100 + .30*250 + .15*600
    if L >= 6:  return .70*100 + .30*250
    return 100.0
LC, LG, LSTEP, LMAX = 100, 1.25, 0.05, 40              # Equipment
def lvl_cost(n): return round(LC * LG ** n)            # cost of level n -> n+1
def ladder(n): return sum(lvl_cost(k) for k in range(n))
AWAY = 1/20                                            # Yard.AwayShare
# salvager: GathererDef defaults Hold 100, Speed 110, Rate 2; Economy.UnloadRate 50
# one-way trip per salvager index, from Hub.WreckPos(-1840,60), Yard.WreckSpread/WreckEdge (+140)
def trip(i):
    spread=[0,.35,-.35,.7,-.7][i]; edge=[96,96,101,95,343][i]+140
    wx,wy=-1840,60; dx,dy=-wx,-wy; d=math.hypot(dx,dy); ux,uy=dx/d,dy/d
    c,s=math.cos(spread),math.sin(spread); rx,ry=ux*c-uy*s, ux*s+uy*c
    px,py=wx+rx*edge, wy+ry*edge
    return math.hypot(px,py)
TRIPS=[trip(i) for i in range(5)]
def fleet(n, h, r, v):
    H=100*(1+.1*h); R=2*(1+.1*r); V=110*(1+.1*v)
    return sum(H/(H/R + 2*TRIPS[i]/V + H/50) for i in range(n))
COUNT_COST=[400,800,1600,3200]                          # Economy.Rows count: 400 x2^n, max 4
def pct_cost(n): return round(100*1.25**n)              # hold/speed/rate rows


def run(kind, hub=60.0, share=0.5, carry=False, common_at=None, hullparts=('sh','fr'), rare_epic=None):
    """kind: 'bounty' (4 kills a level) or 'siege' (2 sieges a level). hub: ASSUME seconds in the hub per mission.
    share: ASSUME fraction of bounty credits spent on salvager rows. carry: levels kept across rarity (a proposal)."""
    common_at = common_at or (3 if kind=='bounty' else 5)
    kills = 4 if kind=='bounty' else 2
    xmul = 1 if kind=='bounty' else 2                  # MissionKind.Exp
    away = 100.0 if kind=='bounty' else 300.0          # ASSUME seconds in the arena per mission
    pl, exp = 1, 0; cr=0.0; n=1; h=r=v=0; bank=0.0; cum=0.0; cf=0.0; cs=0.0; cfa=0.0
    rare_at, epic_at = rare_epic or ((12, 21) if kind=='bounty' else (17, 32))
    slots = tuple(hullparts)+('wp',)
    owned={s:{} for s in slots}
    out={}
    for L in range(1,41):
        for k in range(kills):
            ok = L >= pl*0.5                            # Missions.WorthExp
            ke = round(KILL*L/pl) if ok else 0
            e = round(xmul*(ke + (FIRST if k==0 else 0) + DONE)) if ok else 0
            exp += e
            while exp >= EXPLV: exp -= EXPLV; pl += 1
        cr += share*kills*BOUNTY*S(L)
        while True:
            opts=[]; base=fleet(n,h,r,v)
            if n<5: opts.append(((fleet(n+1,h,r,v)-base)/COUNT_COST[n-1],'n',COUNT_COST[n-1]))
            opts.append(((fleet(n,h+1,r,v)-base)/pct_cost(h),'h',pct_cost(h)))
            opts.append(((fleet(n,h,r+1,v)-base)/pct_cost(r),'r',pct_cost(r)))
            opts.append(((fleet(n,h,r,v+1)-base)/pct_cost(v),'v',pct_cost(v)))
            opts=[o for o in opts if o[2]<=cr]
            if not opts: break
            b=max(opts); cr-=b[2]
            if b[1]=='n': n+=1
            elif b[1]=='h': h+=1
            elif b[1]=='r': r+=1
            else: v+=1
        F=fleet(n,h,r,v)
        fa=F*kills*away*AWAY                            # while in the arena: Yard.AwayShare
        fl=F*kills*hub + fa
        sc=kills*crates(L)*scrap_avg(L)
        bank+=fl+sc; cum+=fl+sc; cf+=fl; cs+=sc; cfa+=fa
        for s in owned:
            if L==common_at: owned[s][1]=0
            if L==rare_at: owned[s][2]=max(owned[s].values()) if carry else 0
            if L==epic_at: owned[s][3]=max(owned[s].values()) if carry else 0
        while True:
            c=[]
            for s,d in owned.items():
                if not d: continue
                t=max(d); lv=d[t]
                if lv<LMAX: c.append((lvl_cost(lv),s,t))
            c.sort()
            if not c or c[0][0]>bank: break
            cost,s,t=c[0]; bank-=cost; owned[s][t]+=1
            if carry:
                for kk in owned[s]: owned[s][kk]=owned[s][t]
        scl={1:1.0,2:1.5,3:2.0}
        worn={}
        for s,d in owned.items():
            if not d: worn[s]=(0.0,0); continue
            b=max(d.items(), key=lambda kv: scl[kv[0]]*(1+LSTEP*kv[1])); worn[s]=(scl[b[0]],b[1])
        out[L]=dict(pl=pl,pts=pl-1,F=F,fl=fl,fa=fa,sc=sc,cum=cum,cf=cf,cfa=cfa,cs=cs,worn=worn,bank=bank,rows=(n,h,r,v))
    return out

LINE = {'sh':0.40, 'fr':0.30}                          # Bulwark Array, Braced Frame: hull share at Common

def points_split(P, B, N, fp, rapid):
    # greedy on log(hull)+log(dmg); Cooling/Gunnery excluded (sign bug); Weapons assumed to cover every damage stat
    w=hp=0
    def val(w,hp):
        dsh=0.05*N+0.03*w
        dm = fp*(1+dsh-(0.30 if rapid[0]>0 else 0))*(1+rapid[0]) + (1-fp)*(1+dsh)
        return math.log(B+5*hp)+math.log(dm)
    left=P
    while True:
        cw, ch = w+1, hp+1
        g=[]
        if cw<=left: g.append(((val(w+1,hp)-val(w,hp))/cw,'w'))
        if ch<=left: g.append(((val(w,hp+1)-val(w,hp))/ch,'h'))
        if not g: break
        b=max(g)
        if b[1]=='w': left-=cw; w+=1
        else: left-=ch; hp+=1
    return w,hp


def mult(o, B, N, fp=0.6):
    """Waterfall on the BARE sheet (kit parts, no chips, no points): [bare, +chips, +points, +base gear, +gear levels]."""
    wp=o['worn']['wp']
    g0=sum(LINE[s]*o['worn'][s][0] for s in LINE if s in o['worn'])
    g =sum(LINE[s]*o['worn'][s][0]*(1+LSTEP*o['worn'][s][1]) for s in LINE if s in o['worn'])
    rap0=0.60*wp[0]; rap=rap0*(1+LSTEP*wp[1]); has=wp[0]>0
    w,hp=points_split(o['pts'],B,N,fp,(rap,))
    ch=0.05*N
    D=lambda dsh,r,has: fp*(1+dsh-(0.30 if has else 0))*(1+r) + (1-fp)*(1+dsh)
    Hh=lambda php,gg: (B+5*php)/B*(1+ch+gg)
    hull=[1.0, Hh(0,0), Hh(hp,0), Hh(hp,g0), Hh(hp,g)]
    dmg=[1.0, D(ch,0,False), D(ch+0.03*w,0,False), D(ch+0.03*w,rap0,has), D(ch+0.03*w,rap,has)]
    return hull,dmg,w,hp
