using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// HINTS -- the tutorial, as the owner asked for it: never in the way. The first time a pilot meets
// a system, a small card in a corner says how it works in a line or two and fades after 8 s. It
// takes no input and blocks nothing; the Esc menu turns hints off; and each character remembers
// the hints it has been shown (Character.HintsSeen), so a veteran pilot sees none and a new one
// sees each once.
//
// A LEVEL WALL CROSSED (Unlocks) has a card too, written by the unlock table for the class being
// flown -- "LEVEL 3 · Q · SUPPRESSING FIRE" and that ability's blurb -- so it is no row here: its id
// is the table row's (Unlock.Hint), and Card finds either kind.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hints : CanvasLayer
{
    // Every hint there is: its id (what a character remembers), a title and a line or two.
    public static readonly Dictionary<string, (string Title, string Body)> All = new()
    {
        ["flight"]      = ("FLYING", "W ahead, S astern, A / D the rudder. A capital ship turns on a radius; almost stopped it pivots slowly. Every key is listed along the bottom."),
        ["target"]      = ("TARGETING", "Tab takes the nearest enemy; left-click one, or click it on the radar. Esc lets go."),
        ["abilities"]   = ("ABILITIES", "The bar along the bottom shows each ability and its key; a locked one shows the level that opens it. K lists every number your ship flies and fights with, and rebinds any key."),
        ["base"]        = ("THE BASE", "B, or left-click the station. Miners, salvagers and the hauler earn while you fly; spend the credits here. REFIT changes your ship."),
        ["tio"]         = ("THE TIO", "Left-click the building for boss missions. When every pilot is READY the portal opens and the party goes through."),
        ["equipment"]   = ("EQUIPMENT", "I. Bosses drop parts that lean hard one way; your hold keeps what you are not flying, and each class keeps its own. Level or scrap them on the EQUIPMENT BASE, right of the station, 10 s clear of combat."),
        ["loot"]        = ("LOOT", "Crates only you can see. Fly over one to take it; any you leave behind come home with you."),
        ["multiplayer"] = ("MULTIPLAYER", "HOST THIS WORLD, then INVITE A FRIEND and send them the code. To join, paste an invite into JOIN and send back the reply it copies."),
        ["raid"]        = ("RAIDERS", "They come for your base after a failed mission, and for your hauler on an escort. The base's guns help; nothing else you own can fight back."),
        ["hauler"]      = ("THE HAULER", "DISPATCH sends it alone: past the portal it may be lost (EVASION lowers the risk). ESCORT flies it round the four outposts for five times the pay; keep the raiders off it."),
        ["pilot"]       = ("A LEVEL UP", "L spends your points on rudder, hull, engines and weapons, and names what your next level opens. Bosses give EXP, and so do their adds the first time they come; a level's first clear gives more."),
        // THE DRIVES (Drives.cs): a hull meets its own drive's card, by the drive's id.
        ["warp"]        = ("WARP", "Hold V: after 1 s the range grows to a safe 2400 u (the faint ring). Release to jump along the bow, or short of the target or waypoint it points at. Past the ring you land disabled, 2 s per 300 u over. 20 s to recharge."),
        ["boost"]       = ("BOOST", "V: +50% top speed, thrust and strafe for 3 s, every 15 s."),
        ["strafe"]      = ("STRAFE", "Hold Shift and A / D slide the hull sideways; the nose and your guns stay where they are."),
        ["stasis"]      = ("STASIS", "Your ship is held in stasis, not lost. Fly the escape pod clear; F re-boards when the ship is ready."),
        ["boss"]        = ("THE BOSS", "Red shapes are its attacks, drawn before they land: get out of them. Its adds come in squads: a web holds you for its beam, so kill the webifiers. Beat it for EXP, a bounty and parts."),
    };

    // THE TOUR a first character is walked through, in this order (Tour.cs): the things a pilot
    // cannot work out by flying around, soonest first. It is an ORDER OF IDS and not a second copy
    // of the words -- the cards are the ones above, shown with a CONTINUE instead of a clock.
    // A hint not on this list is still met in play, the way every hint always was.
    public static readonly string[] Tour =
    {
        "flight", "target", "abilities", "base", "tio", "pilot", "equipment", "loot",
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

    // A card's words: a row above, or a wall's (Unlocks.Card) for the class being flown. Null for
    // an id neither knows.
    public static (string Title, string Body)? Card(string id) =>
        All.TryGetValue(id, out var row) ? row : Unlocks.Card(id, Character.Class);

    // A pilot has just met a system: its hint, if it is still wanted, joins the queue.
    public void Meet(string id)
    {
        if (Card(id) == null) { GD.PushError($"no hint called {id}"); return; }
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
        if (Card(id) is not { } card) return;          // a wall's card for a class no longer flown
        var (title, body) = card;
        _title.Text = title; _body.Text = body;
        _showing = id; _age = 0;
        _card.Modulate = Colors.White; _card.Visible = true;
        Character.HintsSeen.Add(id);
        Character.SaveSoon();
    }
}
