using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// HINTS -- the tutorial, as the owner asked for it: never in the way. The first time a pilot meets
// a system, a small card in a corner says how it works in a line or two and fades after 8 s. It
// takes no input and blocks nothing; the Esc menu turns hints off; and each character remembers
// the hints it has been shown (Character.HintsSeen), so a veteran pilot sees none and a new one
// sees each once.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hints : CanvasLayer
{
    // Every hint there is: its id (what a character remembers), a title and a line or two.
    public static readonly Dictionary<string, (string Title, string Body)> All = new()
    {
        ["flight"]      = ("FLYING", "W ahead, S astern, A / D the rudder. A capital ship turns on a radius; almost stopped, it pivots slowly. Every key is listed along the bottom of the screen."),
        ["target"]      = ("TARGETING", "Tab takes the nearest enemy; left-click one, or click it on the radar. Esc lets it go."),
        ["abilities"]   = ("ABILITIES", "The bar along the bottom shows each ability and its key. K lists every number your ship flies and fights with, and changes any ability's key."),
        ["base"]        = ("THE BASE", "B, or left-click the station. Miners, salvagers and the hauler earn while you fly; spend the credits here. REFIT changes your ship."),
        ["tio"]         = ("THE TIO", "Left-click the building for boss missions. When every pilot is READY the portal opens and the party goes through together."),
        ["equipment"]   = ("EQUIPMENT", "I. Bosses drop parts that lean hard one way; your hold keeps what you are not flying. Fit them here; each class keeps its own."),
        ["loot"]        = ("LOOT", "Crates only you can see. Fly over one to take it; any you leave behind come home with you."),
        ["multiplayer"] = ("MULTIPLAYER", "HOST THIS WORLD, then COPY ADDRESS for your friends. To join a friend, type their address, then JOIN."),
        ["raid"]        = ("RAIDERS", "They come for your base after a failed mission, and for your hauler on an escort. The base's guns help; miners, salvagers and the hauler cannot fight back."),
        ["hauler"]      = ("THE HAULER", "DISPATCH sends it alone: past the portal it may be lost (EVASION, on the HAULER tab, lowers the risk). ESCORT flies it round the four outposts for five times the pay; keep the raiders off it."),
        ["pilot"]       = ("A LEVEL UP", "L spends your points on rudder, hull, engines and weapons. Bosses give EXP; the first clear of each level gives more."),
        ["warp"]        = ("WARP", "V charges for 3 s, then jumps toward your target or waypoint if the bow is on it, else straight ahead. 1200 u at most, and 30 s to recharge."),
        ["stasis"]      = ("STASIS", "Your ship is held in stasis, not lost. Fly the escape pod clear; when the ship is ready, F re-boards it."),
        ["boss"]        = ("THE BOSS", "Red shapes are its attacks, drawn before they land: get out of them. Beat it for EXP, a bounty and parts."),
    };

    // how near a pilot has to come to a thing to have "met" it
    public const float TargetMeet = 500, BaseMeet = 600, TioMeet = 400, WarpMeet = 1500;
    private const double UpFor = 8.0, FadeFor = 1.0;               // seconds in full, then fading
    private const float CardW = 360, Gutter = 10, Above = 120, Pad = 8;
    public Hub Hub;
    private readonly Queue<string> _queue = new();
    private string _showing;
    private double _age;
    private PanelContainer _card;
    private Label _title, _body;

    public override void _Ready()
    {
        Name = "Hints";
        Layer = 3;                       // over the HUD and the session panel; under K, the creator and the Esc menu
        _card = new PanelContainer { Name = "HintCard", Visible = false };
        Ui.Panelise(_card, (int)Pad);
        // the bottom-right corner, clear of the ability bar, growing up and left from there
        _card.AnchorLeft = _card.AnchorRight = _card.AnchorTop = _card.AnchorBottom = 1f;
        _card.OffsetRight = -Gutter; _card.OffsetLeft = -Gutter - CardW; _card.OffsetTop = _card.OffsetBottom = -Above;
        _card.GrowHorizontal = _card.GrowVertical = Control.GrowDirection.Begin;
        var col = Ui.VBox(4);
        _title = Ui.Lbl("", Ui.Small, Ui.Accent);
        _body = Ui.Lbl("", Ui.Body, Ui.Text);
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(CardW - 2 * (Pad + 4), 0);
        col.AddChild(_title); col.AddChild(_body); _card.AddChild(col);
        AddChild(_card);
        // IT NEVER TAKES INPUT: every part of it lets clicks through and never holds the keyboard
        foreach (var c in new Control[] { _card, col, _title, _body }) { c.MouseFilter = Control.MouseFilterEnum.Ignore; c.FocusMode = Control.FocusModeEnum.None; }
    }

    // Not yet seen by this pilot, not already up or waiting, and hints are on.
    public bool Wants(string id) => !Character.HintsOff && !Character.HintsSeen.Contains(id) && _showing != id && !_queue.Contains(id);

    // A pilot has just met a system: its hint, if it is still wanted, joins the queue.
    public void Meet(string id)
    {
        if (!All.ContainsKey(id)) { GD.PushError($"no hint called {id}"); return; }
        if (Wants(id)) _queue.Enqueue(id);
    }

    public override void _Process(double delta)
    {
        if (Character.HintsOff) { _queue.Clear(); _showing = null; _card.Visible = false; return; }
        if (Hub == null || Hub.EscMenuOpen || Hub.CreatorOpen || Hub.StatsOpen) return;      // the clock holds while a window covers the game
        if (_showing == null)
        {
            if (_queue.Count == 0) return;
            Start(_queue.Dequeue());
        }
        _age += delta;
        _card.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp((UpFor + FadeFor - _age) / FadeFor, 0, 1));
        if (_age >= UpFor + FadeFor) { _card.Visible = false; _showing = null; }
    }

    // Up, and remembered: a pilot is shown each hint once, whatever happens next.
    private void Start(string id)
    {
        var (title, body) = All[id];
        _title.Text = title; _body.Text = body;
        _showing = id; _age = 0;
        _card.Modulate = Colors.White; _card.Visible = true;
        Character.HintsSeen.Add(id);
        Character.SaveSoon();
    }
}
