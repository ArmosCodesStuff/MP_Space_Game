using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// RAIDERS -- enemy fighters. Host-simulated; guests draw them from the host's
// 10 Hz state. They are hostile: selectable, and every player weapon can hit them.
//
// EVERY RAIDER FLIES IN A SQUAD (Squads.cs), a squad of one included (doctrine "lone"). The squad
// picks the target, holds the formation, decides the commit and the post; this file flies one
// member of it and fires its guns:
//   IN FORMATION  it flies its slot off the squad's anchor, at up to CatchUp x its cruise.
//   COMMITTED     it flies to its post on the target -- a pinner AHEAD / LEFT / RIGHT and on round the
//                 bow (Front), a standoff astern (Rear) -- every member timed to arrive together, on
//                 the boost unless the squad burned inside Reboost; posted, it keeps station at no
//                 more than that speed (a warp never throws it across the map in one frame).
//   LATCHED       within 12 u of its post and within reach of the hull. A PINNER puts its row's Cc on
//                 what it holds (Pinned: a fifth of its speed, thrusting, unable to turn) and fires
//                 its 1 DPS laser (the raider damage "x"). A STANDOFF never holds anything: from
//                 astern it fires both barrels (2.58x the raider damage) whether or not the target is
//                 pinned, and its row's missile -- only at a PINNED target -- lands where the target
//                 WILL be when the row's flight ends (EnemyDef.MissileFlight): a red circle marks it.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Raider : Node2D, IHittable, ITagged, IStatused, ISquadMember, ITowable, ICalled
{
    public Hub Hub;
    // WHICH enemy this is: a row of Enemies.All. The index is what goes on the wire.
    public int Kind = Enemies.Webifier;
    public EnemyDef Def => Enemies.Of(Kind);
    public int NetId { get; set; }
    public double Hp;
    public bool Alive => Hp > 0;
    public bool Heavy => Def.Way == EnemyWay.Standoff;
    public Tag Tags => Def.Tag;
    // what is being done to it: a warrior's EMP holds it still (Statuses). Host-decided; a guest
    // sees it stop because the host stops sending it anywhere.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status st, double seconds, double share = double.NaN) { if (Net.Sim && StatusSet.Reaches(st, Tags)) _status.Apply(st, seconds, share); }
    public float Length => Def.Length;
    // A LEVEL, the boss's that was failed (fractional for an escort's threat): its hull x
    // Par.CraftScale, its guns x Par.DamageScale -- the curve a boss is on (Par.cs)
    public double Strength = 1;
    // The share of that hull it is built with: an escort's hunters come at half (Hub.HunterHull).
    // Not Strength, which scales its damage too. Set before it enters the tree (_Ready reads it).
    public double HullShare = 1;
    // Speed and turning, x: an escort's hunters fly a very little quicker the harder the escort
    // (Hub.ThreatAgility). The host flies every raider; guests follow where it says.
    public double Agility = 1;
    // HOST-ONLY BOOKKEEPING, set at the spawn as Agility is (Raids.Send): the level it was sent at,
    // and what shooting it down pays each pilot before the level rule (Missions.ExpFor) --
    // EnemyDef.Exp x WaveDef.Exp on a first fill, 0 on a refill and on everything else.
    public int Level = 1;
    public double Worth;
    // ...and the share of its level's damage it deals (WaveDef.DamageShare: a boss's adds take the
    // party's damage multiplier). Host-only: only the host strikes.
    public double DamageShare = 1;
    private HullWatch _hullWatch;
    public double MaxHull => Def.Hull * Par.CraftScale(Strength) * HullShare;
    public double HullLeft => MaxHull > 0 ? Hp / MaxHull : 1;
    // ONE VOLLEY, every barrel at once (F20), on its level's damage scale: what each laser Strike carries
    public double Volley => Def.Dps * Def.Barrels * Def.ShotEvery * Par.DamageScale(Strength) * DamageShare;
    public float HitRadius => Length * Def.HitShare;
    public Vector2? Facing => Vector2.Up.Rotated(Rotation);
    public bool Selectable => true;

    // The two the rest of the game names by hand -- the plain webifier and the plain gunship.
    // Every figure comes from their rows (Enemies.All), so there is one place a number lives.
    public static float LightLength => Enemies.Of(Enemies.Webifier).Length;      // twice a carrier fighter
    public static float HeavyLength => Enemies.Of(Enemies.Gunship).Length;
    public static double RaiderDps => Enemies.Of(Enemies.Webifier).Dps;          // x -- the game's damage unit
    public static double HeavyDps => Enemies.Of(Enemies.Gunship).Dps * Enemies.Of(Enemies.Gunship).Barrels;
    // THE MISSILE IS THE ROW'S: EnemyDef.MissileRange / MissileEvery / MissileDamage / MissileFlight /
    // BlastRadius, read through Def (F20: each row its own flight -- the Lancerkin's own point is
    // standing off further, so its flight need not match the gunship's).
    // HOW a predicted missile flies, telegraphs and lands is Missiles.cs, whosever it is: the
    // outposts throw the same one back (Lanes.cs), which is why none of it is in this file.
    public static float BlastRadius => Enemies.Of(Enemies.Gunship).BlastRadius;
    public static float PinRange => Enemies.Of(Enemies.Webifier).Reach;
    // THE WEBIFIER'S COMMIT RANGE: 1600 u (3 s at 500%, plus its 100 u reach)
    public static float BoostAt
    {
        get { var d = Enemies.Of(Enemies.Webifier); return d.Cruise * d.BoostMult * (float)d.BoostTime + d.Reach; }
    }

    // ── ITS SQUAD (host only; a guest's raiders have none) and what the squad reads of it ──
    public Squad Squad;
    public bool Pins => Def.Cc != null;
    public float Cruise => Def.Cruise * (float)Agility;
    public float Burn => Def.Cruise * Def.BoostMult * (float)Agility;
    public float CommitRange => Def.Cruise * Def.BoostMult * (float)Def.BoostTime + Def.Reach;
    public float Hold => Def.Hold;
    // WHAT IT IS AFTER is its squad's, and so is a hunter's quarry (set by the host at the spawn)
    public Node2D Target => Squad?.Target;
    public Node2D Quarry
    {
        get => Squad?.Quarry;
        set { if (Squad != null) Squad.Quarry = value; }
    }
    // A CALL is its squad's too (Squad.Call): host, a timed override that leaves Quarry alone
    public void Call(Node2D by, double seconds) { if (Net.Sim) Squad?.Call(by, seconds); }
    public Node2D CalledBy => Squad?.CalledBy;
    public TowState Towed { get; set; }            // F19 (Towing.cs): held off a bow or hurled from it; host
    public bool Latched { get; private set; }
    public bool Boosting => Net.Sim ? _boosting : (_netFlags & FlagBoost) != 0;
    public float Speed { get; private set; }
    private bool _boosting;
    private double _shot, _missileCd = 2.0;
    private Lead _lead;                                // what its target is doing (Missiles.cs)
    private Sprite2D _turret;
    private const float TurretPixels = 66f;            // turret_main.png is 66 px across: the ART's figure, not an enemy's
    private NetPose _net;
    private Vector2? _tether;                          // guests: where the web goes
    private Sprite2D _sprite;

    // ITS ROW'S NAME under the hull, for SquadSight.NameShow s: when its squad enters this peer's
    // view, and again as it commits -- with a web glyph when the row has a Cc. On every peer: it
    // reads the packet's bits and this peer's own screen. The range to a selected raider drops
    // below the name while it shows (LabelDrop).
    private double _nameT;
    private bool _inView, _wasLocking;
    public double NameLeft => _nameT;
    public float LabelDrop => _nameT > 0 ? 22f + 13f : 0f;
    private void TickName(double delta)
    {
        _nameT = System.Math.Max(0, _nameT - delta);
        bool inView = GetViewportRect().HasPoint(GetGlobalTransformWithCanvas().Origin), locking = Locking;
        if (inView && !_inView && Hub != null)
        {   // coming into view names its whole squad, the ones still off the screen with it
            foreach (var r in Hub.Raiders)
                if (ReferenceEquals(r, this) || (SquadId != 0 && r.SquadId == SquadId)) r._nameT = SquadSight.NameShow;
        }
        if (locking && !_wasLocking) _nameT = SquadSight.NameShow;
        _inView = inView; _wasLocking = locking;
    }

    public override void _Ready()
    {
        Hp = MaxHull;
        _sprite = Sprites.Fit(Def);
        AddChild(_sprite);
        if (Def.Turret)
        {   // the main turret on its spine behind the canopy, mounted where ITS OWN ROW says and
            // as wide as its row says -- both shares of its length, so a new hull states where its
            // gun sits instead of being scaled off the gunship's
            _turret = new Sprite2D { Texture = Assets.Load<Texture2D>("res://turret_main.png"),
                                     Position = new Vector2(0, Length * Def.TurretAft),
                                     Scale = Vector2.One * (Length * Def.TurretWidth / TurretPixels),
                                     Modulate = Def.Tint, ZIndex = 1 };
            AddChild(_turret);
        }
        ZIndex = 5;
        Combat.Hostiles.Add(this);
    }
    // (the blow that finishes it is shown as it goes: it leaves before its next frame)
    public override void _ExitTree()
    {
        _hullWatch.Tick(this, System.Math.Max(0, Hp), taken: false); Combat.Hostiles.Remove(this);
        Squad?.Leave(this);                       // its post and slot go with it (invariant A)
    }

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        Hp -= d;
        if (Hp > 0) return;
        // SHOT DOWN, and only here: a boss's death sweeps its adds out through RaiderDown with their
        // hull left, and a withdrawn hunter goes quietly -- neither is a kill, so neither pays
        if (Worth > 0) Hub.PayKill(Worth, Level);
        Hub.RaiderDown(this);
    }

    // ── what a raid can reach, and what a raider can go after ──
    // REACHABLE: an IRaidTarget there to be hit -- what a raid's burst lands on (Hub.RaiderTargets,
    // Missiles.Raid). UP: reachable AND SEEN -- what a raider may choose to come for. Stealth is the
    // whole difference between the two (Targeting.cs: it stops choosing, never hitting).
    public static bool Reachable(Node2D t) => GodotObject.IsInstanceValid(t) && t is IRaidTarget r && r.InReach;
    public static bool Up(Node2D t) => Reachable(t) && !Targeting.Hidden(t);
    // How far the target's hull reaches from its centre along `dir`: an ellipse with the
    // hull's half-length and half-width. Posts and reach are measured from the HULL, so a
    // raider holds station beside a long ship, never on top of its bow.
    public static float Extent(Node2D t, Vector2 dir)
    {
        var (len, wid) = t is IRaidTarget r ? r.Extent : (20f, 12f);
        var fwd = Vector2.Up.Rotated(t.Rotation); var side = new Vector2(-fwd.Y, fwd.X);
        float a = dir.Dot(fwd), b = dir.Dot(side);
        return 1f / Mathf.Sqrt(a * a / (len * len) + b * b / (wid * wid));
    }
    public static float Gap(Vector2 from, Node2D t) =>
        from.DistanceTo(t.Position) - Extent(t, (from - t.Position).Normalized());
    // EVERY LASER BLOW, light's or heavy's, goes out through the door (StatusSet.Out): a gun whose
    // statuses take it to nothing lands nothing, and starts no gap on the hull it would have hit
    void Strike(Node2D t, double d)
    {
        d = _status.Out(d, OutKind.Gun);
        if (d > 0 && !(t is IHittable h && Prism.Catch(h, BlowKind.Ray, Position, t.Position, d, $"raider:{NetId}")))   // a prism splits the ray (F11)
            (t as IRaidTarget)?.Hit(d, Position, $"raider:{NetId}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _hullWatch.Tick(this, System.Math.Max(0, Hp), taken: false);
        TickName(delta);
        if (!Net.Sim)
        {
            _net.Follow(this, dt);
            QueueRedraw();
            return;
        }
        if (!Alive) return;
        _status.Tick(delta);
        _boosting = false;
        if (Towed != null) { Speed = 0; Latched = false; if (!Towing.Step(this, this, delta)) Towed = null; QueueRedraw(); return; }   // towed: off its post
        if (_status.Has(Status.Disabled) || Squad == null) { Speed = 0; Latched = false; QueueRedraw(); return; }   // stunned: it sits there
        var from = Position;
        var t = Target;
        if (t != null) _lead.Watch(t.Position, delta);   // the one tracker every predicted missile reads (Missiles.Lead)
        if (!Squad.Posted(this))
        {   // IN FORMATION: its slot off the anchor, catching up at CatchUp x its cruise
            Latched = false;
            var slot = Squad.SlotOf(this);
            float d = Position.DistanceTo(slot);
            Position = Position.MoveToward(slot, Mathf.Min(Squad.Doctrine.CatchUp * Cruise, d * 6f + Squad.Moving) * dt);
            float face = t != null ? Aim.Face(Position, t.Position) : Squad.Moving > 0 ? Squad.Heading : Rotation;
            Rotation = Mathf.LerpAngle(Rotation, face, Mathf.Clamp(6f * (float)Agility * dt, 0f, 1f));
        }
        else
        {   // COMMITTED: to its post, every member timed to land together; posted, station kept at
            // no more than its top speed
            var post = Squad.PostOf(this);
            float top = Squad.Top(this), d = Position.DistanceTo(post);
            float step = Latched ? top : Mathf.Min(top, d / (float)System.Math.Max(Squad.Left, 1.0 / 60));
            Position = Position.MoveToward(post, step * dt);
            // THE LATCH GATES (F17 rows): a status may refuse a new latch, or drop the one it holds
            Latched = Position.DistanceTo(post) < 12f && Gap(Position, t) <= Def.Reach
                      && (Latched ? !_status.DropsLatch : !_status.BlocksLatch);
            Rotation = Mathf.LerpAngle(Rotation, Aim.Face(Position, t.Position), Mathf.Clamp(8f * (float)Agility * dt, 0f, 1f));
        }
        Speed = dt > 0 ? from.DistanceTo(Position) / dt : 0f;
        _boosting = Squad.Posted(this) && Squad.Burning && Speed > Cruise + 1f;   // the burn, not a catch-up
        if (t == null) { QueueRedraw(); return; }
        bool pinned = t is IStatused st && st.Statuses.Has(Status.Pinned);
        if (_turret != null && !_status.HoldsAim) _turret.GlobalRotation = Aim.Face(Position, t.Position);   // its one turret tracks the target, unless jammed
        if (Latched)
        {
            // WHAT A LATCH APPLIES (F20): the row's own Cc -- a standoff row names none, at any level
            if (Def.Cc is { } cc) (t as IRaidTarget)?.ApplyStatus(cc, 0.25);
            _shot -= delta;
            if (_shot <= 0 && (Pins || !Squad.Doctrine.LaserNeedsPin || pinned))
            {
                _shot = Def.ShotEvery;
                // ONE Strike per volley carries every barrel's damage (F20): two separate Strikes,
                // 0.52 s apart or not, would be eaten by the target's own hit gap and undercount.
                Strike(t, Volley);
                DrawVolley(t);
            }
        }
        _missileCd -= delta;
        // HELD while a status holds its throw (StatusSet.HoldsThrow): the clock keeps its zero, and
        // it throws the frame the status lapses. ONLY AT A PINNED TARGET (the doctrine's row), by
        // anyone's web: a free pilot is never lobbed at.
        if (Def.Missiles && (pinned || !Squad.Doctrine.MissileNeedsPin) && _missileCd <= 0 && !_status.HoldsThrow
            && Position.DistanceTo(t.Position) <= Def.MissileRange)
        {   // at where it WILL be: its velocity carried the whole flight forward -- from ITS row's
            // reach, on its row's cadence, for its row's damage
            _missileCd = Def.MissileEvery;
            // THE ROW'S DAMAGE, AND NOTHING ELSE. A raider's own hull and guns climb with the
            // level through Strength, but the MISSILE it throws is the same missile at level 40 as
            // at level 1 -- the owner's rule, and the reason a level-40 raid is survivable at all:
            // a blast that scaled with the level would be unavoidable rather than merely heavy.
            // (A boss's projectiles DO scale; that is Missions.Quicken, and it is a boss.)
            Hub.ThrowMissile(new MissileSpec { Side = Missiles.Raid, Damage = Def.MissileDamage,
                                              Blast = Def.BlastRadius, Flight = Def.MissileFlight },
                             Position, Missiles.Predict(t.Position, _lead.Velocity, Def.MissileFlight), NetId);
        }
        QueueRedraw();
    }

    // A volley, drawn: a light fires from its nose; a turret fires Barrels flashes from offsets
    // either side of its centre
    private void DrawVolley(Node2D t)
    {
        if (_turret == null) { Combat.Flash(Position, t.Position, Def.Beam); return; }
        var centre = ToGlobal(_turret.Position);
        if (Def.Barrels <= 1) { Combat.Flash(centre, t.Position, Def.Beam); return; }
        var side = Vector2.Right.Rotated(_turret.GlobalRotation) * (Length * Def.TurretWidth * 0.5f);
        Combat.Flash(centre + side, t.Position, Def.Beam);
        Combat.Flash(centre - side, t.Position, Def.Beam);
    }

    // WHERE A HUNT FORMS UP (Raids.Hunt): the map's edge nearest its quarry, as seen from the base
    public static Vector2 EdgeSpot(Vector2 target) =>
        Hub.BasePos + ((target - Hub.BasePos).LengthSquared() > 1f ? (target - Hub.BasePos).Normalized() : Vector2.Right) * Hub.RaidEdge;

    // WHAT A GUEST NEEDS TO DRAW IT, on the packet that already carries its position: the boost
    // (its plume), whether it LEADS its squad (link lines), whether it is COMMITTING (lock lines),
    // and its squad's id in bits 8-23 (grouping), and whether it is JAMMED (the EMP's mark: the status itself is host-only).
    // The bit values are literals the harness asserts.
    public const int FlagBoost = 1, FlagLead = 4, FlagLock = 8, FlagJam = 16, SquadShift = 8, SquadMask = 0xFFFF;
    public int NetFlags => !Net.Sim ? _netFlags
                         : (Boosting ? FlagBoost : 0)
                         | (Squad != null && Squad.Members.Count > 1 && ReferenceEquals(Squad.Leader, this) ? FlagLead : 0)
                         | (Squad != null && Squad.Posted(this) && !Latched ? FlagLock : 0)
                         | (_status.Has(Status.Jammed) ? FlagJam : 0)
                         | ((Squad?.Id ?? 0) & SquadMask) << SquadShift;
    public bool Leads => (NetFlags & FlagLead) != 0;
    public bool Locking => (NetFlags & FlagLock) != 0;
    public bool JamMarked => (NetFlags & FlagJam) != 0;
    public int SquadId => (NetFlags >> SquadShift) & SquadMask;
    private int _netFlags;
    public void SetNet(Vector2 p, float rot, double hp, Vector2? tether, int flags)
    {
        if (!_net.Has) _hullWatch = default;       // the host's first figure is where this peer starts counting
        _net.Set(p, rot); Hp = hp; _tether = tether; _netFlags = flags;
    }
    // WHERE ITS LINE GOES: what it holds, or -- committing -- what it is closing on
    public Vector2? TetherTo => Net.Sim ? ((Latched || Locking) && Up(Target) ? Target.Position : null) : _tether;

    public override void _Draw()
    {
        var inv = GlobalTransform.AffineInverse();
        if (TetherTo is { } to)
        {
            if (Locking)
            {   // COMMITTING: a dashed red lock line converging on the victim for the burn
                var end = inv * to; int dashes = Mathf.Clamp((int)(end.Length() / 24f), 1, 80);
                for (int i = 0; i < dashes; i += 2)
                    DrawLine(end * (i / (float)dashes), end * ((i + 1) / (float)dashes), new Color(1f, 0.25f, 0.2f, 0.7f), 2f);
            }
            else if (Pins) DrawLine(Vector2.Zero, inv * to, new Color(1f, 0.3f, 0.25f, 0.35f), 1.5f);   // the web; a heavy draws none
        }
        else if (!Leads && SquadId != 0 && SquadLead() is { } lead)
            DrawLine(Vector2.Zero, inv * lead.GlobalPosition, new Color(1f, 0.4f, 0.35f, 0.25f), 1f);   // in formation: a faint link to its lead
        // a flame out of every bell its art has (its row's Nozzles): the burn on the boost
        if (JamMarked)
        {   // JAMMED (the EMP): a broken blue ring and a crackle across the hull, on every peer
            var jam = new Color(0.6f, 0.85f, 1f, 0.8f); float jr = HitRadius + 6f;
            for (int i = 0; i < 6; i++) DrawArc(Vector2.Zero, jr, Mathf.Tau * i / 6f, Mathf.Tau * i / 6f + 0.6f, 5, jam, 1.5f);
            DrawPolyline(new[] { new Vector2(-jr * 0.6f, -2f), new Vector2(-jr * 0.2f, 3f), new Vector2(jr * 0.2f, -3f), new Vector2(jr * 0.6f, 2f) }, jam, 1.2f);
        }
        var flame = new Color(1f, 0.35f, 0.25f);
        if (Boosting) Def.DrawPlumes(this, Vector2.Zero, 1f, flame, 1f, true, 1.6f);
        else Def.DrawPlumes(this, Vector2.Zero, 1f, flame, 0.5f, Speed > 1f);
        if (_nameT > 0)
        {   // upright whatever its heading, fading over its last half second
            DrawSetTransform(Vector2.Zero, -GlobalRotation, Vector2.One);
            var font = ThemeDB.FallbackFont; int px = Txt.Size(13);
            var col = new Color(1f, 0.6f, 0.55f, 0.9f * (float)System.Math.Min(1.0, _nameT / 0.5));
            var at = new Vector2(0, HitRadius + 22f);
            Txt.Centre(this, font, at, Def.Name, px, col);
            if (Pins)
            {   // the web glyph: a small ring and its spokes, left of the name
                float w = font.GetStringSize(Def.Name, HorizontalAlignment.Left, -1, px).X;
                var g = at + new Vector2(-w * 0.5f - px * 0.7f, -px * 0.35f); float gr = px * 0.38f;
                DrawArc(g, gr, 0, Mathf.Tau, 12, col, 1f);
                for (int i = 0; i < 4; i++) DrawLine(g, g + Vector2.Right.Rotated(Mathf.Tau * i / 4f + 0.4f) * gr * 1.3f, col, 1f);
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
    // its squad's lead, as every peer sees it: the raider of the same squad id that says it leads
    public Raider SquadLead()
    {
        if (Hub == null) return null;
        int id = SquadId;
        foreach (var r in Hub.Raiders) if (r != this && r.SquadId == id && r.Leads) return r;
        return null;
    }
}
