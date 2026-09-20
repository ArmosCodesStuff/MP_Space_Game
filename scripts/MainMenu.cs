using Godot;
using System;
using System.Collections.Generic;

// MAIN MENU. A still diorama -- a capital ship with infinite hull holding off pirates among a few
// asteroids inside a nebula -- under the title, with Launch / volume / quit. Entirely
// self-contained: it does not touch the game classes, so it can never be broken by a game change.
public partial class MainMenu : Node2D
{
    // Pirates make ATTACK RUNS: they fly in, shoot, and carry straight on past, then turn and come
    // back.
    private struct Foe { public Vector2 P, V; public float Hp, Rot; public bool Alive; public double Respawn;
                         public bool Passing; public double Gun; }
    private struct Shot { public Vector2 A, B; public double T; public bool Hostile; }
    private struct Torp { public Vector2 P, To; public float Speed; }
    // four turret mounts in the ship's local frame (it sits upright, so local == world here)
    private static readonly Vector2[] Mounts = { new(-16, -14), new(16, -14), new(-13, 12), new(13, 12) };
    private readonly List<Foe> _foes = new();
    private readonly List<Shot> _shots = new();
    private Sprite2D _cap = null!; private readonly List<Sprite2D> _rocks = new();
    private Texture2D _enemyTex = null!;
    private readonly List<Torp> _torps = new();
    private double _fire, _t, _torpCd = 3.0; private readonly Random _rng = new(3);
    private const double FireInterval = 0.18;     // the capital's guns: brisk, so the scene reads as a fight
    private const double FoeGunInterval = 1.15;   // pirates shoot back (cosmetic: the hull is invulnerable here)
    private const double TorpInterval = 5.5;      // and a torpedo every few seconds, with a wider burst
    private Label _info = null!;

    public override void _Ready()
    {
        Ui.Install(GetTree());                                  // the game-wide look
        Settings.Load(); Settings.ApplyVolume();
        // Reaching the menu ends whatever trip was in progress. Hub.Sector is static and is only
        // ever set by Hub.GoTo, so quitting to the menu FROM THE ARENA used to leave it there:
        // the next session skipped BuildWorld entirely and dropped the pilot into an arena with
        // no base, no economy and a boss. Resetting here covers every route back, not just the
        // Esc menu's quit button.
        Hub.Sector = Hub.SectorKind.Home;
        Yard.EndSession();      // and the trip snapshot / parked base, for the same reason
        // Hub sets this on the way in and nothing cleared it on the way out, so quitting from the
        // arena left the combat track playing over the menu until a new Hub was built.
        Music.CombatZone = false;
        if (Music.I != null) Music.I.Target = Music.Mood.Ambient;
        var vs = GetViewport().GetVisibleRect().Size;
        var centre = vs * 0.5f + new Vector2(0, 60);
        // nebula backdrop: a few large tinted patches
        var neb = GD.Load<Texture2D>("res://nebula.png");
        Color[] tints = { new(0.45f, 0.30f, 0.75f), new(0.25f, 0.45f, 0.80f), new(0.85f, 0.55f, 0.30f), new(0.30f, 0.75f, 0.85f) };
        for (int i = 0; i < 22; i++)
        {
            var sp = new Sprite2D { Texture = neb, ZIndex = -50 };
            sp.Position = centre + new Vector2((float)(_rng.NextDouble() - 0.5) * vs.X * 1.2f, (float)(_rng.NextDouble() - 0.5) * vs.Y * 1.2f);
            float sc = 0.8f + (float)Math.Pow(_rng.NextDouble(), 1.8) * 3.4f; sp.Scale = new Vector2(sc, sc); sp.Rotation = (float)(_rng.NextDouble() * Math.PI * 2);
            var t = tints[i % tints.Length]; sp.Modulate = new Color(t.R, t.G, t.B, sc > 2.6f ? 0.13f : 0.24f);
            AddChild(sp);
        }
        // asteroids
        var rockTex = GD.Load<Texture2D>("res://asteroid_1.png");
        for (int i = 0; i < 6; i++)
        {
            var r = new Sprite2D { Texture = rockTex, ZIndex = -5 };
            float a = (float)(_rng.NextDouble() * Math.PI * 2), d = 260f + (float)_rng.NextDouble() * 260f;
            r.Position = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            float sc = 0.7f + (float)_rng.NextDouble() * 0.9f; r.Scale = new Vector2(sc, sc); r.Rotation = (float)(_rng.NextDouble() * 6.28);
            AddChild(r); _rocks.Add(r);
        }
        // the capital, holding station
        _cap = new Sprite2D { Texture = GD.Load<Texture2D>("res://capital_ship.png"), Position = centre, Scale = new Vector2(0.6f, 0.6f), ZIndex = 5 };
        AddChild(_cap);
        _enemyTex = GD.Load<Texture2D>("res://enemy_light_tier_2.png");
        for (int i = 0; i < 5; i++) _foes.Add(NewFoeReal(centre, vs));

        // ── UI ──
        var ui = new CanvasLayer { Layer = 10 }; AddChild(ui);
        var box = new VBoxContainer(); box.SetAnchorsPreset(Control.LayoutPreset.FullRect); box.AddThemeConstantOverride("separation", 10); ui.AddChild(box);
        var title = new Label { Text = "WARSHIPS", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 64); title.AddThemeColorOverride("font_color", new Color(0.85f, 0.93f, 1f));
        
        var spacer = new Control { CustomMinimumSize = new Vector2(0, vs.Y * 0.36f) }; box.AddChild(spacer);
        // the title sits with the menu panel, centred together, not pinned to the top edge
        var centre2 = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddChild(centre2);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 24); centre2.AddChild(stack);
        stack.AddChild(title);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) }; col.AddThemeConstantOverride("separation", 8); stack.AddChild(Ui.Wrap(col, 16));
        // Warships has no save file yet -- only characters and settings -- so the
        // Continue / New Game pair carried over from Space Fleet Idle could never enable
        // Continue. One entry point until there is world state worth continuing.
        var launch = Big("PLAY"); launch.Pressed += () => GetTree().ChangeSceneToFile("res://CharacterSelect.tscn"); col.AddChild(launch);
        col.AddChild(new Label { Text = "Volume", HorizontalAlignment = HorizontalAlignment.Center });
        var vol = new HBoxContainer(); vol.AddThemeConstantOverride("separation", 4); col.AddChild(vol);
        for (int i = 0; i < Settings.VolumeSteps.Length; i++) { int idx = i; var b = new Button { Text = $"{Settings.VolumeSteps[i] * 100:F0}%", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; b.Pressed += () => { Settings.VolumeIdx = idx; Settings.ApplyVolume(); Settings.Save(); _info.Text = $"Volume {Settings.Volume * 100:F0}%"; }; vol.AddChild(b); }
        var quit = Big("QUIT"); quit.Pressed += () => QuitSoon(); col.AddChild(quit);
        _info = new Label { HorizontalAlignment = HorizontalAlignment.Center, Text = $"Volume {Settings.Volume * 100:F0}%" }; _info.AddThemeFontSizeOverride("font_size", 12); col.AddChild(_info);
        var ver = new Label { Text = "Warships  early build", HorizontalAlignment = HorizontalAlignment.Center }; ver.AddThemeFontSizeOverride("font_size", 11); ver.AddThemeColorOverride("font_color", new Color(0.5f, 0.55f, 0.65f)); col.AddChild(ver);
    }
    private static Button Big(string text) { var b = new Button { Text = text }; b.AddThemeFontSizeOverride("font_size", 20); b.CustomMinimumSize = new Vector2(0, 44); return b; }

    // The offset has to be PERPENDICULAR to the approach, not just some point on a ring: a straight
    // line through a ring point can still clip the hull, and aiming in a box around the centre sent
    // runs clean through it.
    private Vector2 RunAim(Vector2 centre, Vector2 from)
    {
        var toward = (centre - from).Normalized();
        if (toward == Vector2.Zero) toward = Vector2.Right;
        var perp = new Vector2(-toward.Y, toward.X);
        float off = (130f + (float)_rng.NextDouble() * 130f) * (_rng.NextDouble() < 0.5 ? -1f : 1f);
        return centre + perp * off;
    }
    private Foe NewFoeReal(Vector2 centre, Vector2 vs)
    {
        float a = (float)(_rng.NextDouble() * Math.PI * 2);
        var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (vs.Length() * 0.6f);
        var aim = RunAim(centre, p);
        return new Foe { P = p, V = (aim - p).Normalized() * (95f + (float)_rng.NextDouble() * 70f),
                         Hp = 3, Alive = true, Gun = _rng.NextDouble() * FoeGunInterval };
    }
    public override void _Process(double delta)
    {
        _t += delta; _fire -= delta;
        var centre = _cap.Position;
        _cap.Rotation = Mathf.Sin((float)_t * 0.3f) * 0.05f;
        foreach (var r in _rocks) r.Rotation += (float)delta * 0.08f;
        for (int i = 0; i < _foes.Count; i++)
        {
            var f = _foes[i];
            if (!f.Alive) { f.Respawn -= delta; if (f.Respawn <= 0) f = NewFoeReal(centre, GetViewport().GetVisibleRect().Size); _foes[i] = f; continue; }
            float d = f.P.DistanceTo(centre);
            f.P += f.V * (float)delta;
            if (!f.Passing && d < 250f) f.Passing = true;                 // committed: fly the run through
            else if (f.Passing && d > 680f)
            {   // clear of the ship: wheel around and line up another pass
                var aim = RunAim(centre, f.P);
                f.V = (aim - f.P).Normalized() * f.V.Length(); f.Passing = false;
            }
            // pirates shoot back on the way in. Cosmetic only: the capital has no hull here.
            f.Gun -= delta;
            if (f.Gun <= 0 && d < 560f)
            {
                f.Gun = FoeGunInterval * (0.7 + _rng.NextDouble() * 0.6);
                _shots.Add(new Shot { A = f.P, B = centre + new Vector2((float)(_rng.NextDouble() - 0.5) * 34f,
                                                                        (float)(_rng.NextDouble() - 0.5) * 34f),
                                      T = 0.13, Hostile = true });
            }
            f.Rot = f.V.Angle() + Mathf.Pi / 2f;
            _foes[i] = f;
        }
        if (_fire <= 0)
        {
            _fire = FireInterval;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < _foes.Count; i++) if (_foes[i].Alive) { float d = _foes[i].P.DistanceTo(centre); if (d < bd && d < 520f) { bd = d; best = i; } }
            if (best >= 0)
            {
                var f = _foes[best];
                f.Hp -= 1;
                // fire from the mount facing the target, not the hull centre
                Vector2 mount = Mounts[0]; float md = float.MaxValue;
                foreach (var mo in Mounts) { float dd = (centre + mo).DistanceTo(f.P); if (dd < md) { md = dd; mount = mo; } }
                _shots.Add(new Shot { A = centre + mount, B = f.P, T = 0.15 });
                if (f.Hp <= 0) { f.Alive = false; f.Respawn = 1.5 + _rng.NextDouble() * 2; _shots.Add(new Shot { A = f.P, B = f.P, T = 0.4 }); }
                _foes[best] = f;
            }
        }
        // TORPEDO: one every few seconds at the furthest live pirate, bursting wider than a shell.
        _torpCd -= delta;
        if (_torpCd <= 0)
        {
            int far = -1; float fd = 0f;
            for (int i = 0; i < _foes.Count; i++)
                if (_foes[i].Alive) { float d2 = _foes[i].P.DistanceTo(centre); if (d2 > fd && d2 < 640f) { fd = d2; far = i; } }
            if (far >= 0)
            {
                _torpCd = TorpInterval;
                _torps.Add(new Torp { P = centre + new Vector2(0, -26), To = _foes[far].P, Speed = 190f });
            }
            else _torpCd = 0.6;
        }
        for (int i = _torps.Count - 1; i >= 0; i--)
        {
            var tp = _torps[i];
            float step = tp.Speed * (float)delta;
            if (tp.P.DistanceTo(tp.To) <= step)
            {
                _shots.Add(new Shot { A = tp.To, B = tp.To, T = 0.75 });     // the wide burst
                _torps.RemoveAt(i);
                for (int k = 0; k < _foes.Count; k++)                        // anything close enough goes with it
                {
                    var fk = _foes[k];
                    if (!fk.Alive || fk.P.DistanceTo(tp.To) > 78f) continue;
                    fk.Hp = 0; fk.Alive = false; fk.Respawn = 1.5 + _rng.NextDouble() * 2; _foes[k] = fk;
                }
                continue;
            }
            tp.P = tp.P.MoveToward(tp.To, step); _torps[i] = tp;
        }
        for (int i = _shots.Count - 1; i >= 0; i--) { var s = _shots[i]; s.T -= delta; if (s.T <= 0) _shots.RemoveAt(i); else _shots[i] = s; }
        QueueRedraw();
    }
    public override void _Draw()
    {
        foreach (var f in _foes) if (f.Alive) DrawTexture(_enemyTex, f.P, f.Rot, 0.45f);
        foreach (var mo in Mounts) DrawCircle(_cap.Position + mo, 2.6f, new Color(0.55f, 0.75f, 1f, 0.85f));
        foreach (var tp in _torps)
        {
            DrawCircle(tp.P, 3.4f, new Color(1f, 0.85f, 0.55f));
            DrawLine(tp.P, tp.P - (tp.To - tp.P).Normalized() * 13f, new Color(1f, 0.6f, 0.25f, 0.75f), 2.4f);
        }
        foreach (var s in _shots)
        {
            if (s.A == s.B)
            {   // a burst. The torpedo's lasts longer, so it also draws wider -- that is the tell.
                bool big = s.T > 0.42;
                float r = big ? 88f * (float)(1.0 - s.T / 0.75) : 22f * (float)(s.T / 0.4);
                float a = big ? (float)(s.T / 0.75) * 0.9f : (float)s.T * 2f;
                DrawCircle(s.A, r, new Color(1f, big ? 0.72f : 0.6f, 0.3f, a));
                if (big) DrawArc(s.A, r * 1.18f, 0, Mathf.Tau, 40, new Color(1f, 0.85f, 0.5f, a * 0.8f), 2.2f);
            }
            else DrawLine(s.A, s.B, s.Hostile ? new Color(1f, 0.55f, 0.45f, (float)(s.T / 0.13) * 0.85f)
                                              : new Color(0.6f, 0.9f, 1f, (float)(s.T / 0.15) * 0.9f), 2.5f);
        }
    }
    private void DrawTexture(Texture2D tex, Vector2 at, float rot, float scale)
    {
        DrawSetTransform(at, rot, new Vector2(scale, scale));
        DrawTexture(tex, -tex.GetSize() * 0.5f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    // silence the music first, then quit on the next moment (see Music.Silence)
    private void QuitSoon()
    {
        Music.I?.Silence();
        GetTree().CreateTimer(0.15).Timeout += () => { OS.DelayMsec(50); GetTree().Quit(); };
    }
}
