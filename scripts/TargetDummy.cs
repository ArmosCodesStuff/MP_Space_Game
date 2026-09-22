using Godot;

// A training hulk in the hub. It counts as hostile so every weapon can shoot it, and never
// dies; the ARMED one (number 3) fires missiles back inside its ring, and the two PRACTICE
// FIGHTERS are light-fighter targets for point defence. Its readout is the DPS meter: damage taken is
// counted in one-second windows and the window total is shown once a second, with
// a 10-second average beside it so bursty weapons (a salvo, a torpedo run) read
// fairly. After 5 s without a hit, the next hit starts a fresh count.
//
// Host-owned like all combat: the host counts, and sends the readout to guests.
public partial class TargetDummy : Node2D, IHittable, ITagged
{
    // An ARMED dummy fights back: every 5 s it fires a guided missile at the nearest
    // player ship within 150 u, for 50 damage. Host-owned like all combat.
    public bool Armed;
    public const float ArmedRange = 150f, ArmedInterval = 5f;
    private const double ArmedDamage = 50;
    private double _fireCd;
    // Several dummies, each with its own id (1000, 1001, ...) and label number,
    // so target switching can be tested. Ids are assigned by the Hub in order,
    // identically on every peer.
    private const int FirstNetId = NetIds.Dummy;
    public int Number = 1;
    public int NetId => FirstNetId + Number - 1;
    // A PRACTICE FIGHTER is a dummy shaped like a light raider: small craft, so point
    // defence (which shoots only missiles and light fighters) has something to train on.
    public bool Fighter;
    public Tag Tags => Tag.Dummy | (Fighter ? Tag.Light : Tag.None);
    public float HitRadius => Fighter ? Raider.LightLength * 0.4f : 46f;
    // A child sprite, not a texture held in a static and drawn by hand: that one lived for the
    // whole process, past this dummy and every hub after it.
    private Sprite2D _fighter;
    public override void _Ready()
    {
        if (!Fighter) return;
        _fighter = Sprites.Fit("res://enemy_light_fighter.png", Raider.LightLength);
        AddChild(_fighter);
    }
    public bool Alive => true;
    Vector2 IHittable.Position => GlobalPosition;

    public double LastSecond, Average10, Total;
    private double _window, _clock;
    private readonly double[] _history = new double[10];
    private int _histN, _histI;
    private double _hitFlash;

    // Set by the Hub so the readout reaches guests.
    public System.Action<double, double, double> Published;

    // The meter resets itself: the first hit after this long without one starts a
    // fresh count, so each test starts from zero with no key to press.
    public const double IdleReset = 5.0;
    private double _idle = IdleReset;

    public void TakeDamage(double d)
    {
        if (!Net.Sim) return;
        if (_idle >= IdleReset) ResetMeter();
        _idle = 0;
        _window += d; Total += d; _hitFlash = 0.08;
    }

    public void ResetMeter()
    {
        LastSecond = Average10 = Total = _window = 0; _histN = _histI = 0; _clock = 0;
        _hullWatch.Tick(this, 0, taken: false);    // a meter restarted, not a repair: the next hit shows in full
    }

    private HullWatch _hullWatch;                  // what it takes, shown where it lands (its running total, as a falling "hull")
    public override void _Process(double delta)
    {
        _hullWatch.Tick(this, -Total, taken: false);
        _hitFlash = Mathf.Max(0, _hitFlash - delta);
        if (_fighter != null) _fighter.Modulate = _hitFlash > 0 ? new Color(1f, 0.8f, 0.7f) : Colors.White;
        if (Armed && Net.Sim)
        {
            _fireCd = System.Math.Max(0, _fireCd - delta);
            if (_fireCd <= 0)
            {
                var best = Targeting.Nearest(Combat.Players, GlobalPosition, Targeting.Attackable, ArmedRange);
                if (best != null)
                {
                    var dir = (best.Position - GlobalPosition).Normalized();
                    Combat.LaunchTorpedo(GlobalPosition + dir * (HitRadius + 8f), dir, 170f, 400f, ArmedDamage,
                                         best.NetId, 2.5f, heavy: false, hostile: true);
                    _fireCd = ArmedInterval;
                }
            }
        }
        _idle += delta;
        if (Net.Sim)
        {
            _clock += delta;
            while (_clock >= 1.0)
            {
                _clock -= 1.0;
                LastSecond = _window; _window = 0;
                _history[_histI] = LastSecond; _histI = (_histI + 1) % _history.Length;
                if (_histN < _history.Length) _histN++;
                double sum = 0; for (int i = 0; i < _histN; i++) sum += _history[i];
                Average10 = sum / _histN;
                Published?.Invoke(LastSecond, Average10, Total);
            }
        }
        QueueRedraw();
    }

    // a guest's copy of the host's meter, once a second: its first figures are where this peer starts
    // counting, and a meter the host restarted (its sums restart together) counts from zero
    private bool _readSeen;
    public void SetReadout(double last, double avg, double total)
    {
        if (!_readSeen) _hullWatch = default;
        else if (total < Total || (total > 0 && total == last)) _hullWatch.Tick(this, 0, taken: false);
        _readSeen = true;
        LastSecond = last; Average10 = avg; Total = total;
    }

    public override void _Draw()
    {
        if (Armed)   // its reach, faintly: stay outside this ring
            DrawArc(Vector2.Zero, ArmedRange, 0, Mathf.Tau, 64, new Color(1f, 0.35f, 0.3f, 0.22f), 1.5f);
        var f0 = ThemeDB.FallbackFont;
        if (Fighter)
        {   // a light raider's shape, 34 u (the child sprite), flashing when hit
            float h = Raider.LightLength;
            Txt.Centre(this, f0, new Vector2(0, -h / 2 - 30), "PRACTICE FIGHTER", 13, new Color(1f, 0.7f, 0.6f, 0.9f));
            Txt.Centre(this, f0, new Vector2(0, -h / 2 - 12), $"{LastSecond:0.00} DPS", 17, Colors.White);
            Txt.Centre(this, f0, new Vector2(0, h / 2 + 18), $"total {Total:0}", 12, new Color(1, 1, 1, 0.7f));
            return;
        }
        // a drawn hulk: an armoured octagon with a bullseye, tinted hostile red
        var hull = new Color(0.45f, 0.18f, 0.16f);
        var pts = new Vector2[8];
        for (int i = 0; i < 8; i++) pts[i] = Vector2.Right.Rotated(Mathf.Tau * (i + 0.5f) / 8f) * HitRadius;
        DrawColoredPolygon(pts, _hitFlash > 0 ? new Color(1f, 0.75f, 0.6f) : hull);
        for (int i = 0; i < 8; i++) DrawLine(pts[i], pts[(i + 1) % 8], new Color(0.9f, 0.4f, 0.3f), 2f);
        DrawArc(Vector2.Zero, 24f, 0, Mathf.Tau, 24, new Color(1f, 0.6f, 0.45f, 0.8f), 2f);
        DrawCircle(Vector2.Zero, 7f, new Color(1f, 0.6f, 0.45f));

        Txt.Centre(this, f0, new Vector2(0, -HitRadius - 34), $"TARGET DUMMY {Number}", 14, new Color(1f, 0.7f, 0.6f, 0.9f));
        Txt.Centre(this, f0, new Vector2(0, -HitRadius - 14), $"{LastSecond:0.00} DPS", 20, Colors.White);
        Txt.Centre(this, f0, new Vector2(0, HitRadius + 22), $"10s avg {Average10:0.00}   total {Total:0}", 13, new Color(1, 1, 1, 0.7f));
    }
}
