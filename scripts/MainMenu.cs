using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// MAIN MENU. A live diorama under the title: the REAL battleship holding station among
// asteroids in a nebula, fighting off light fighters, heavies and a webifier, with Play,
// volume and quit below it.
//
// IT USES THE GAME CLASSES ON PURPOSE, and that is a reversal of what this file used to say.
// It was a self-contained pile of sprites and structs so that no gameplay change could break
// the menu -- but the cost was that the menu could show something the game does not do. The
// ship here is a real PlayerShip with Demo set: the same turrets, the same shells, the same
// broadside, the same warp. What you see on the title screen is the battleship, or it is a bug in
// the battleship. The menu is a small WORLD now, so it registers hostiles and wires the Combat
// hooks -- and drops all of it in _ExitTree, because every hook is a lambda holding this node.
public partial class MainMenu : Node2D
{
    // The display ship's own tuning. It is not a pilot's ship and it is not balanced against
    // anything: these are the numbers that make the scene read.
    private const double BroadsideEvery = 5.0;        // its broadside, back every five seconds
    private const double AoeEvery = 15.0, AoeWarn = 8.0;   // an area shot every 15 s, telegraphed for 8
    private const float AoeRadius = 260f, WarpHop = 500f;
    // It starts the jump with 4 s to go and the warp takes 3, so it lands ONE SECOND before the
    // shot arrives. Dodging by a whole second reads as a dodge; dodging by a frame reads as luck.
    // HOW LONG BEFORE THE SHOT IT STARTS TO MOVE: its own warm-up, plus the time its own rudder
    // needs for a half turn, because the jump goes along the keel and the turn IS the aim. It was
    // a flat 4 s, which was enough only while the turn was read as degrees and the hull barely
    // moved; at the rate a battleship really turns, a half turn is 2.9 s and 4 s left it clearing
    // the blast with 0.2 s to spare.
    private double DodgeAt => PlayerShip.WarpWarmup + Mathf.Pi / System.Math.Max(0.1, _cap.Stats["turn_rate"]);
    private const double GunRange = 620;
    // The diorama gets a camera, for the same reason the hub has one: the ships are drawn at the
    // size they really are, and at 1:1 a battleship is a smudge on a 2560-wide screen.
    //
    // IT IS THE HUB'S ZOOM, not a number of its own. A title screen showing the ship larger than
    // the game does is a promise the game does not keep -- the first thing you see after PLAY
    // would be the same hull, smaller. Reading Hub.DefaultZoom rather than copying its value is
    // what stops the two drifting apart the next time the game's zoom is tuned.
    private const float Zoom = Hub.DefaultZoom;

    private struct Shot { public Vector2 A, B; public double T; public bool Hostile; }
    private readonly List<Shot> _shots = new();
    private readonly List<MenuFoe> _foes = new();
    private readonly List<Sprite2D> _rocks = new();
    private PlayerShip _cap = null!;
    private Vector2 _centre;
    private float _ring;
    // The first area shot is deliberately LATE. The establishing view of a title screen should be
    // the ship on station trading fire, not an empty patch of space it warped out of.
    private double _t, _broadsideCd, _aoeCd = 16.0, _aoeLeft = -1;
    private Vector2 _aoeAt;
    private bool _dodged;
    private readonly Random _rng = new(3);
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
        Hub.EndSession();       // and which world each guest was in, and the places held for dropped pilots
        Missions.EndSession();  // and the level that was selected, which a guest took from its host
        // ...AND THE NETWORK SESSION. Every other session-scoped thing was reset here with the
        // argument that covering every route back matters more than covering the Esc menu's quit
        // button; the session itself was not, because the menu used to be sprites and structs and
        // had nothing to say on the wire. It builds a real PlayerShip now, with a multiplayer
        // authority and an RPC path no peer has, so a session surviving into the menu means the
        // title screen starts talking to a world that is not there.
        Net.I?.GoOffline();
        // Hub sets this on the way in and nothing cleared it on the way out, so quitting from the
        // arena left the combat track playing over the menu until a new Hub was built.
        Music.CombatZone = false;
        if (Music.I != null) Music.I.Target = Music.Mood.Ambient;

        // The hub clears these on its way out, but a menu reached at boot has never had a hub.
        Combat.Clear();

        var vs = GetViewport().GetVisibleRect().Size;
        // The diorama sits in the BAND BETWEEN the title and the menu panel, not in the middle of
        // the screen. Moving the title to the top freed the top third but left the capital ship
        // centred -- directly behind the panel, which hid the thing the scene is about. Three
        // bands down the screen: title, fight, menu.
        _centre = new Vector2(vs.X * 0.5f, vs.Y * 0.36f);
        // Where a KILLED foe comes back from: off the edge of the band, not off the edge of a
        // 2560-wide screen diagonal.
        _ring = vs.Y * 0.62f;
        // Put _centre on the screen where the band wants it: the camera shows the world around its
        // own position, so it sits BELOW the diorama by the offset the band needs, scaled by zoom.
        AddChild(new Camera2D { Position = _centre + new Vector2(0, (vs.Y * 0.5f - vs.Y * 0.36f) / Zoom),
                                Zoom = new Vector2(Zoom, Zoom), Enabled = true });
        // The hub sets these from its camera each frame; the overlays drawn in world space (a
        // foe's health bar) read them, and without this they keep whatever the last scene left.
        Txt.UiScale = 1f / Zoom;
        // nebula backdrop: a few large tinted patches
        var neb = GD.Load<Texture2D>("res://nebula.png");
        Color[] tints = { new(0.45f, 0.30f, 0.75f), new(0.25f, 0.45f, 0.80f), new(0.85f, 0.55f, 0.30f), new(0.30f, 0.75f, 0.85f) };
        for (int i = 0; i < 22; i++)
        {
            var sp = new Sprite2D { Texture = neb, ZIndex = -50 };
            sp.Position = _centre + new Vector2((float)(_rng.NextDouble() - 0.5) * vs.X * 1.2f, (float)(_rng.NextDouble() - 0.5) * vs.Y * 1.2f);
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
            r.Position = _centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            float sc = 0.7f + (float)_rng.NextDouble() * 0.9f; r.Scale = new Vector2(sc, sc); r.Rotation = (float)(_rng.NextDouble() * 6.28);
            AddChild(r); _rocks.Add(r);
        }

        // ── the capital: a REAL battleship, with nobody at the helm ──
        _cap = new PlayerShip { Demo = true, Name = PlayerShip.NodeName(Net.LocalId),
                                Pilot = "Warships", Main = Character.Defaults.Main, Accent = Character.Defaults.Accent };
        AddChild(_cap);
        // Init() is what BUILDS a ship: its sprite, its turrets, its stat sheet. Skip it and the
        // node still moves, warps and reports its position perfectly well while drawing NOTHING --
        // which is exactly what happened, and every mechanical check passed on an invisible ship.
        // A ship is not a ship until Init.
        _cap.Init(Net.LocalId, _centre);
        // Retuned AFTER Init, because Init rebuilds the sheet from the class and would discard it.
        _cap.Stats.SetBase("broadside_cooldown", BroadsideEvery);
        _cap.WarpHop = WarpHop;
        _cap.WarpEvery = AoeEvery - AoeWarn;      // ready again before the next area shot is called

        // Its turrets and shells all go through Combat, exactly as in the hub.
        Combat.OnFlash = (a, b, c, snd) => { _shots.Add(new Shot { A = a, B = b, T = 0.15 }); Sfx.Laser(a, b, snd); };
        Fx.On = r => AddChild(new FxNode { Id = r.Id, Position = r.At, To = r.To, Radius = r.Size, Time = r.Time,
                                           Hold = r.Hold, Since = r.Since, Anchor = r.Anchor, Cue = r.Cue, Strike = r.Strike });
        Combat.World = this;

        // three lights, two heavies and a webifier
        foreach (var kind in new[] { MenuFoeKind.Light, MenuFoeKind.Light, MenuFoeKind.Light,
                                     MenuFoeKind.Heavy, MenuFoeKind.Heavy, MenuFoeKind.Web })
        {
            var f = new MenuFoe(kind, _rng) { ZIndex = 4, Target = _cap };
            f.Died += at => _shots.Add(new Shot { A = at, B = at, T = 0.4 });
            AddChild(f);
            f.Deploy(_centre);
            Combat.Hostiles.Add(f);
            _foes.Add(f);
        }

        // ── UI ──
        var ui = new CanvasLayer { Layer = 10 }; AddChild(ui);
        var box = new VBoxContainer(); box.SetAnchorsPreset(Control.LayoutPreset.FullRect); box.AddThemeConstantOverride("separation", 10); ui.AddChild(box);
        var title = Ui.Lbl("WARSHIPS", Ui.Display, Ui.Text);
        title.HorizontalAlignment = HorizontalAlignment.Center;

        // THE TITLE BELONGS TO THE TOP OF THE SCREEN, NOT TO THE MENU. It used to be stacked with
        // the panel and the pair centred together, which put it straight across the capital ship
        // holding station in the middle of the diorama -- the word and the hull competed and both
        // lost. Pinned near the top edge it frames the scene instead of sitting in it, and the
        // panel is free to centre in what is left.
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, vs.Y * 0.06f) });
        box.AddChild(title);
        
        var centre2 = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddChild(centre2);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) }; col.AddThemeConstantOverride("separation", 10); centre2.AddChild(Ui.Wrap(col, 18));
        // Warships has no save file yet -- only characters and settings -- so the
        // Continue / New Game pair carried over from Space Fleet Idle could never enable
        // Continue. One entry point until there is world state worth continuing.
        col.AddChild(Ui.Btn("PLAY", () => GetTree().ChangeSceneToFile("res://CharacterSelect.tscn"), size: Ui.Head));
        col.AddChild(Ui.Heading("Volume"));
        var vol = Ui.HBox(4); col.AddChild(vol);
        for (int i = 0; i < Settings.VolumeSteps.Length; i++) { int idx = i; var b = new Button { Text = $"{Settings.VolumeSteps[i] * 100:F0}%", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; b.Pressed += () => { Settings.VolumeIdx = idx; Settings.ApplyVolume(); Settings.Save(); _info.Text = $"Volume {Settings.Volume * 100:F0}%"; }; vol.AddChild(b); }
        col.AddChild(Ui.Btn("QUIT", () => Game.Quit(), size: Ui.Head));
        _info = Ui.Lbl($"Volume {Settings.Volume * 100:F0}%", Ui.Small, Ui.Dim);
        _info.HorizontalAlignment = HorizontalAlignment.Center; col.AddChild(_info);
        var ver = Ui.Lbl("Warships  early build", Ui.Small, Ui.Dim with { A = 0.7f });
        ver.HorizontalAlignment = HorizontalAlignment.Center; col.AddChild(ver);
    }

    // ── what the smoke test looks at ──────────────────────────────────────────
    public PlayerShip DemoShip => _cap;
    public Vector2 Station => _centre;
    public IReadOnlyList<MenuFoe> Foes => _foes;
    public double AreaShotIn => _aoeLeft;                 // seconds to impact, or -1 between shots
    public float CameraZoom => Zoom;
    public void ForceAreaShot() { _aoeCd = 0; _aoeLeft = -1; }

    // Every hook above is a lambda holding THIS node, and Combat is static: one left behind is a
    // freed node the next world's shot calls into. The hub clears them on its way out for the
    // same reason.
    public override void _ExitTree() => Combat.Clear();

    public override void _Process(double delta)
    {
        _t += delta;
        foreach (var r in _rocks) r.Rotation += (float)delta * 0.08f;

        foreach (var f in _foes)
        {
            var shot = f.Tick(delta, _centre, _ring);
            if (shot is { } s) _shots.Add(new Shot { A = s.from, B = s.to, T = 0.13, Hostile = true });
        }

        DriveShip(delta);

        for (int i = _shots.Count - 1; i >= 0; i--) { var s = _shots[i]; s.T -= delta; if (s.T <= 0) _shots.RemoveAt(i); else _shots[i] = s; }
        QueueRedraw();
    }

    // Nobody is flying it, so this is the pilot: aim, shoot, dodge, and come home.
    private void DriveShip(double delta)
    {
        if (!IsInstanceValid(_cap)) return;
        var live = _foes.Where(f => f.Alive).ToList();
        var near = Combat.Nearest(live, _cap.Position, f => f.GlobalPosition);

        // ── the area shot, and the jump out of it ──
        if (_aoeLeft < 0)
        {
            _aoeCd -= delta;
            if (_aoeCd <= 0) { _aoeCd = AoeEvery; _aoeLeft = AoeWarn; _aoeAt = _cap.Position; _dodged = false;
                               Fx.Warn(new FxRaise { Id = Fx.WarnZone, At = _aoeAt, To = _aoeAt, Size = AoeRadius, Time = AoeWarn }); }
        }
        else
        {
            _aoeLeft -= delta;
            if (_aoeLeft <= 0) _aoeLeft = -1;
            // With DodgeAt to go it turns its bow AWAY from the impact and jumps: the warp goes
            // along the keel, so the turn is the aim. Both are the ship's own -- its turn rate,
            // its warm-up -- which is why the numbers here are a scene, not a cheat.
            else if (_aoeLeft <= DodgeAt && !_dodged)
            {
                var away = (_cap.Position - _aoeAt);
                if (away.LengthSquared() < 1f) away = Vector2.Right;
                _cap.AutopilotTo = null;
                TurnTowards(Aim.Along(away), delta);
                if (Mathf.Abs(Mathf.AngleDifference(_cap.Rotation, Aim.Along(away))) < 0.25f)
                    _dodged = _cap.StartWarp();
            }
        }

        // ── back to station, or face the fight ──
        bool dodging = _aoeLeft > 0 && _aoeLeft <= DodgeAt;
        if (!dodging && !_cap.Warping)
        {
            if (_cap.Position.DistanceTo(_centre) > 40f) _cap.AutopilotTo = _centre;
            else
            {
                _cap.AutopilotTo = null;
                if (near != null) TurnTowards(Aim.Face(_cap.Position, near.GlobalPosition), delta);
            }
        }

        // ── guns and the broadside ──
        _cap.AimPoint = near?.GlobalPosition ?? Aim.Nose(_cap, 400f);
        _cap.Trigger = near != null && near.GlobalPosition.DistanceTo(_cap.Position) < GunRange;

        _broadsideCd -= delta;
        if (_broadsideCd <= 0 && near != null && near.GlobalPosition.DistanceTo(_cap.Position) < GunRange)
        {
            _broadsideCd = 1.0;                     // retried every second; the cooldown decides
            _cap.UseAbility("broadside", 0);
        }
    }

    // The hull turns at its own rate and no faster. The autopilot does this for a real pilot;
    // holding station, there is nowhere to go, so the turn is all there is.
    //
    // turn_rate IS RADIANS A SECOND. The sheet says so (Stats.cs: "Rudder limit", rad/s) and
    // PlayerShip.Steer caps its yaw with the figure itself. This read it as DEGREES and multiplied
    // by pi/180, so the title battleship came round at 1.08 degrees a second where the ship it is
    // meant to BE turns 61.9: a sixtieth of its rate. It never came onto the foe it was shooting
    // at, and the warp dodge below -- which waits until the bow is within 0.25 rad of the heading
    // away from the shot -- almost never fired, which is the scene's whole set piece. There is no
    // degrees-per-second turn rate anywhere in this build, so the conversion is gone, not corrected.
    private void TurnTowards(float want, double delta)
    {
        float rate = (float)_cap.Stats["turn_rate"];
        if (rate <= 0f) rate = 0.4f;
        _cap.Rotation += Mathf.Clamp(Mathf.AngleDifference(_cap.Rotation, want), -rate * (float)delta, rate * (float)delta);
    }

    public override void _Draw()
    {
        foreach (var s in _shots)
        {
            if (s.A == s.B)
            {
                float r = 24f * (float)(s.T / 0.4);
                DrawCircle(s.A, r, new Color(1f, 0.6f, 0.3f, (float)s.T * 2f));
            }
            else DrawLine(s.A, s.B, s.Hostile ? new Color(1f, 0.55f, 0.45f, (float)(s.T / 0.13) * 0.85f)
                                              : new Color(0.6f, 0.9f, 1f, (float)(s.T / 0.15) * 0.9f), 2.5f);
        }
    }


}
