using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE RECYCLER — the hold, in the base's own window, with a queue beside it.
//
// A part in the hold is worth salvage and nothing else: it cannot be sold, and until this there
// was no way to be rid of one at all, so a pilot's hold filled with commons it would never fit
// again. Click a part to QUEUE it; the recycler chews through the queue at its own pace and pays
// the salvage into your base, which is the same salvage that levels the parts you kept.
//
// A LOCK is the only protection: a locked part cannot be queued, and the lock is on the part ID
// (Character.GearLocked), because that is how the hold counts them -- there is no "this copy" for
// a lock to belong to. Lock the Salvo Rack III you fly and every Salvo Rack III you own is safe.
//
// A KIT PART IS NEVER QUEUED. A hull's own hardware is not loot, cannot be dropped, and scrapping
// one would leave a slot that nothing in the game can fill again.
// ─────────────────────────────────────────────────────────────────────────────
public partial class RecyclerPanel : PanelContainer
{
    public Hub Hub;
    private VBoxContainer _hold, _queue;
    private Label _rate;
    public const float W = 560f;

    public override void _Ready()
    {
        Name = "RecyclerPanel";
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(12));
        AnchorRight = AnchorBottom = 1f;
        OffsetLeft = -W - 16; OffsetTop = 90; OffsetRight = -16; OffsetBottom = -90;
        MouseFilter = MouseFilterEnum.Stop;

        var col = Ui.VBox(8, "Body"); AddChild(col);
        col.AddChild(Ui.Lbl("RECYCLER", Ui.Head));
        _rate = Ui.Lbl("", Ui.Small, Ui.Dim); col.AddChild(_rate);
        col.AddChild(Ui.Heading("Queued"));
        _queue = Ui.VBox(4, "Queue"); col.AddChild(_queue);
        col.AddChild(Ui.Heading("Hold"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.CustomMinimumSize = new Vector2(W - 24, 0);
        col.AddChild(scroll);
        _hold = Ui.VBox(4, "Hold"); scroll.AddChild(_hold);
        Rebuild();
    }

    public override void _Process(double delta)
    {
        // the queue drains on the yard's clock, so the panel only has to keep up with it
        if (Hub?.Yard is { } y && y.ScrapPending != _shown) { _shown = y.ScrapPending; Rebuild(); }
    }
    private int _shown = -1;

    private void Rebuild()
    {
        Ui.Clear(_hold); Ui.Clear(_queue);
        var yard = Hub?.Yard;
        if (yard == null) return;
        _shown = yard.ScrapPending;
        Ui.SetText(_rate, $"One part every {Economy.ScrapEvery:0} s, paid in salvage. A locked part is never queued.");

        foreach (var g in yard.ScrapQueue.GroupBy(id => id))
        {
            var it = Equipment.ById(g.Key);
            var row = Ui.HBox(8, "Q_" + g.Key);
            row.AddChild(Ui.Lbl($"{it?.Name ?? g.Key}{(g.Count() > 1 ? $"  x{g.Count()}" : "")}", Ui.Body, Ui.RarityColor(it?.Rarity ?? Rarity.Common)));
            var back = Ui.Btn("TAKE BACK", () => { yard.UnqueueScrap(g.Key); Rebuild(); }, "TakeBack");
            back.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; row.AddChild(back);
            _queue.AddChild(Ui.CardWrap(row));
        }
        if (yard.ScrapQueue.Count == 0) _queue.AddChild(Ui.Lbl("nothing queued", Ui.Small, Ui.Dim));

        foreach (var kv in Character.GearHold.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, System.StringComparer.Ordinal))
        {
            var it = Equipment.ById(kv.Key);
            if (it == null || it.Kit) continue;                      // a hull's own hardware is not loot
            bool locked = Character.GearLocked.Contains(kv.Key);
            int lv = Equipment.LevelOf(kv.Key);
            var row = Ui.HBox(8, "Hold_" + kv.Key);
            var text = Ui.VBox(1); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.AddChild(Ui.Lbl($"{it.Name}{(kv.Value > 1 ? $"  x{kv.Value}" : "")}{(lv > 0 ? $"   +{lv * Equipment.LevelStep * 100:0}%" : "")}",
                                 Ui.Body, Ui.RarityColor(it.Rarity)));
            text.AddChild(Ui.Lbl($"{it.Slot.ToString().ToUpperInvariant()}  ·  {Economy.ScrapValue(it.Rarity):0} salvage", Ui.Small, Ui.Dim));
            row.AddChild(text);
            var lockBtn = Ui.Btn(locked ? "LOCKED" : "LOCK", () => { Character.ToggleGearLock(kv.Key); Rebuild(); }, "Lock");
            row.AddChild(lockBtn);
            var scrap = Ui.Btn("QUEUE", () => { yard.QueueScrap(kv.Key); Rebuild(); }, "Queue");
            scrap.Disabled = locked;
            row.AddChild(scrap);
            _hold.AddChild(Ui.CardWrap(row));
        }
        if (_hold.GetChildCount() == 0) _hold.AddChild(Ui.Lbl("the hold is empty", Ui.Small, Ui.Dim));
    }
}
