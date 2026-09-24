using Godot;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE TOUR — the soft tutorial a pilot is walked through on its FIRST character, as the owner
// asked: a card at a time, each waiting for CONTINUE, with SKIP TUTORIAL in the top right.
//
// IT IS THE SAME TEXT THE HINTS ARE. `Hints.All` already says what every system is, in a line or
// two, and a second copy of that text is a second copy to keep true -- so a tour is an ORDER of
// hint ids (Hints.Tour) and nothing else. A card met in play still fades on its own clock; a card
// on the tour waits to be dismissed, and dismissing it marks the hint SEEN, so the same pilot is
// never told twice.
//
// SKIP ends it at once and marks every hint on the tour as seen: a pilot who says they know the
// game is not asked again, on this character or any other, because the mark is on the file.
//
// It takes the mouse (a button must be clickable) but never the keyboard, and it is not up while a
// window covers the game.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Tour : CanvasLayer
{
    public Hub Hub;
    private const float W = 420, Pad = 10;
    private int _at = -1;
    private PanelContainer _card;
    private Label _title, _body, _step;
    private Button _next, _skip;

    public bool Running => _at >= 0 && _at < Hints.Tour.Length;

    public override void _Ready()
    {
        Name = "Tour";
        Layer = 4;                                  // over the hint cards, under the creator and the Esc menu
        _card = new PanelContainer { Name = "TourCard" };
        Ui.Panelise(_card, (int)Pad);
        _card.AnchorLeft = _card.AnchorRight = 0.5f; _card.AnchorTop = _card.AnchorBottom = 1f;
        _card.OffsetLeft = -W / 2; _card.OffsetRight = W / 2;
        _card.OffsetTop = _card.OffsetBottom = -210;
        _card.GrowVertical = Control.GrowDirection.Begin;

        var col = Ui.VBox(6);
        _step = Ui.Lbl("", Ui.Small, Ui.Dim);
        _title = Ui.Lbl("", Ui.Head, Ui.Accent);
        _body = Ui.Lbl("", Ui.Body, Ui.Text);
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.CustomMinimumSize = new Vector2(W - 2 * (Pad + 4), 0);
        col.AddChild(_step); col.AddChild(_title); col.AddChild(_body);
        var row = Ui.HBox(8);
        _next = Ui.Btn("CONTINUE", Next, "Continue");
        row.AddChild(_next);
        col.AddChild(row);
        _card.AddChild(col);
        AddChild(_card);

        // SKIP sits in the top right of the screen, away from the card, as asked.
        _skip = Ui.Btn("SKIP TUTORIAL", Skip, "Skip");
        Ui.Style(_skip);                            // parented to the layer, so it inherits nothing
        _skip.AnchorLeft = _skip.AnchorRight = 1f;
        _skip.OffsetLeft = -190; _skip.OffsetRight = -14; _skip.OffsetTop = 14; _skip.OffsetBottom = 46;
        AddChild(_skip);
        Visible = false;
    }

    // A pilot that has seen nothing at all is a pilot on its first character.
    public void StartIfNew()
    {
        if (Character.HintsOff || Character.TourDone || Character.HintsSeen.Count > 0) return;
        _at = 0; Visible = true; Show(Hints.Tour[0]);
    }

    private void Next()
    {
        if (!Running) return;
        Character.HintsSeen.Add(Hints.Tour[_at]);          // told once, never again
        _at++;
        if (!Running) { Finish(); return; }
        Show(Hints.Tour[_at]);
    }

    private void Skip()
    {
        foreach (var id in Hints.Tour) Character.HintsSeen.Add(id);
        _at = Hints.Tour.Length;
        Finish();
    }

    private void Finish()
    {
        Character.TourDone = true;
        Character.Save();
        Visible = false;
        QueueFree();
    }

    private void Show(string id)
    {
        var (title, body) = Hints.All[id];
        Ui.SetText(_step, $"{_at + 1} of {Hints.Tour.Length}");
        Ui.SetText(_title, title);
        Ui.SetText(_body, body);
        Ui.SetText(_next, _at == Hints.Tour.Length - 1 ? "FLY" : "CONTINUE");
    }

    public override void _Process(double _)
    {
        if (!Running) return;
        // not over a window that has covered the game: the tour waits rather than talking over it
        Visible = Hub == null || !(Hub.EscMenuOpen || Hub.CreatorOpen || Hub.StatsOpen);
    }
}
