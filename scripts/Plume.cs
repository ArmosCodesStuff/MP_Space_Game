using Godot;
using System;

// A small engine plume out of the stern, in three layers: a soft outer glow, the flame, a hot core.
// Its size follows the ship's length. BURNING (the hull pushes ahead) it is the flickering POINT, its
// reach following the throttle; otherwise it is a steady HALF-DISC of glow on the nozzle, as wide as
// the bell and never a point (the owner, 2026-09-25). Player craft pass their accent colour; utility
// ships a light yellow no player setting changes. (Missiles are not engines here: they keep their
// smoke trail.) The SIDE JETS (DrawJets) are the same flame, a few units long, out of the hull's edge.
public static class Plume
{
    public static readonly Color Utility = new(1f, 0.93f, 0.55f);
    // the flame's width as a share of the size it is drawn at: a bell `b` wide is drawn at b / Width
    public const float Width = 0.07f;
    // the idle half-disc's rim: this many points from one side of the bell, round aft, to the other
    public const int CapPoints = 9;

    // at: the nozzle, in the caller's local frame; back: unit vector out of the stern.
    // burn: the engine pushes ahead -- only then is it the point, and only then does it flicker.
    public static void Draw(CanvasItem ci, Vector2 at, Vector2 back, float shipLength, Color col, float throttle, bool burn)
    {
        float flick = burn ? 0.85f + 0.15f * Mathf.Sin(Time.GetTicksMsec() / 37f + at.X) : 1f;
        ci.DrawColoredPolygon(Outline(at, back, shipLength, throttle, burn, flick), new Color(col.R, col.G, col.B, 0.22f));
        ci.DrawColoredPolygon(Outline(at, back, shipLength, throttle, burn, flick, 1f, 1f), new Color(col.R, col.G, col.B, 0.55f));
        ci.DrawColoredPolygon(Outline(at, back, shipLength, throttle, burn, flick, 0.55f, 0.5f), new Color(1f, 1f, 1f, 0.7f));
    }

    // ONE LAYER'S OUTLINE (the outer glow by default; the flame and the core are the same at their own
    // shares). Burning: a point, `reach` x the flame's length aft of the nozzle, `width` x the bell
    // across it. Idle: a half-disc on the nozzle, that same width across, bulging aft by its radius.
    public static Vector2[] Outline(Vector2 at, Vector2 back, float shipLength, float throttle, bool burn,
                                    float flick = 1f, float reach = 1.25f, float width = 1.5f)
    {
        float w = Mathf.Max(1.2f, shipLength * Width * 0.5f) * width;
        var side = new Vector2(-back.Y, back.X);
        if (!burn)
        {
            var cap = new Vector2[CapPoints];
            for (int i = 0; i < CapPoints; i++)
            {
                float a = Mathf.Pi * i / (CapPoints - 1);
                cap[i] = at + (side * Mathf.Cos(a) + back * Mathf.Sin(a)) * w;
            }
            return cap;
        }
        float len = shipLength * 0.16f * (0.35f + 0.65f * Mathf.Clamp(throttle, 0f, 1f)) * flick * reach;
        return new[] { at + side * w, at + back * len, at - side * w };
    }

    // THE SIDE JETS, in ClassArt.SideJets order (Ships.cs): x to starboard, so the port pair fire to -x
    public const int BowPort = 0, BowStarboard = 1, SternPort = 2, SternStarboard = 3;

    // Which jets fire, a bit per jet in that order, for a turn and a slide (+1 to starboard, -1 to port,
    // 0 none). Each fires outboard and so pushes its end of the hull the other way: a turn to starboard
    // swings the bow to starboard (the bow's PORT jet) and the stern to port (the stern's STARBOARD jet);
    // a slide to starboard is both port jets.
    public static int Lit(int yaw, int slide)
    {
        int m = 0;
        if (yaw > 0) m |= 1 << BowPort | 1 << SternStarboard;
        else if (yaw < 0) m |= 1 << BowStarboard | 1 << SternPort;
        if (slide > 0) m |= 1 << BowPort | 1 << SternPort;
        else if (slide < 0) m |= 1 << BowStarboard | 1 << SternStarboard;
        return m;
    }

    // the outer flame of a side jet, world units: a few, whatever the hull
    public static float JetLength(float shipLength) => Mathf.Clamp(shipLength * 0.02f, 3f, 6f);

    // every lit jet: a burning flame out of its spot on the hull's edge, outboard. A flame drawn at size s
    // reaches 0.16 x 1.25 x s at full throttle, so a jet JetLength long is drawn at 5 x that.
    public static void DrawJets(CanvasItem ci, Vector2[] at, int lit, float shipLength, Color col)
    {
        float size = JetLength(shipLength) * 5f;
        for (int i = 0; i < at.Length; i++)
            if ((lit & 1 << i) != 0) Draw(ci, at[i], i % 2 == 0 ? Vector2.Left : Vector2.Right, size, col, 1f, true);
    }
}

// WHAT A HULL'S ENGINES ARE DOING, read off its motion alone, so every peer reads the same off the same
// replicated state and no input crosses the wire: the owner samples its hull every frame, every other peer
// at each report. The PUSH on an axis is what moved the speed beyond what the water did by itself,
// dv/dt + damping x v (v at the span's midpoint): ahead the drag (water_drag), across the keel's grip
// (keel). Coasting, the water explains the whole change and the push is ~0; held at top speed the engine
// still pushes against the drag. A push over Share of the sheet's own thrust lights it; a turn faster than
// YawDead lights the pair of side jets that makes it (Plume.Lit).
public struct EngineWatch
{
    public const float Share = 0.25f, YawDead = 0.05f, MinSpan = 0.03f, Stale = 0.5f;
    public bool Burn;              // the main engine pushes ahead: the plume's point
    public int Yaw, Slide;         // the turn and the slide it makes: +1 to starboard, -1 to port, 0 none
    private Vector2 _v;
    private float _rot, _span;
    private bool _has;

    public readonly int Lit => Plume.Lit(Yaw, Slide);

    // time since the last sample; a hull unheard for Stale seconds reads idle
    public void Tick(float dt)
    {
        _span += dt;
        if (_span > Stale) { Burn = false; Yaw = 0; Slide = 0; }
    }

    public void Watch(Vector2 v, float rot, ShipStats st)
    {
        if (_has && _span < MinSpan) return;       // two reports in one frame: read over a real span
        if (_has)
        {
            Vector2 f0 = Vector2.Up.Rotated(_rot), f1 = Vector2.Up.Rotated(rot);
            float ahead = Push(_v.Dot(f0), v.Dot(f1), (float)st["water_drag"], _span);
            float across = Push(_v.Dot(new Vector2(-f0.Y, f0.X)), v.Dot(new Vector2(-f1.Y, f1.X)), (float)st["keel"], _span);
            float yaw = Mathf.AngleDifference(_rot, rot) / _span;
            double slideAt = Share * st["strafe_thrust"];
            Burn = ahead > Share * st["thrust"];
            Slide = slideAt > 0 && Math.Abs(across) > slideAt ? Math.Sign(across) : 0;
            Yaw = Mathf.Abs(yaw) > YawDead ? Math.Sign(yaw) : 0;
        }
        _v = v; _rot = rot; _span = 0f; _has = true;
    }

    // the push that took a speed `from` to `to` over `span` s against a damping (per second)
    public static float Push(float from, float to, float damping, float span) => (to - from) / span + damping * (from + to) * 0.5f;
}
