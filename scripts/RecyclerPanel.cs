using Godot;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE RECYCLER — the EQUIPMENT BASE's second window: the hold, with a queue beside it.
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
//
// QUEUE IS A SERVICE OF THE EQUIPMENT BASE (Landmarks.Serves). It works only over the base and
// 10 s clear of combat; anywhere else it is greyed, with the reason under the rate line. LOCK and
// TAKE BACK spend nothing and work wherever the window is open.
// ─────────────────────────────────────────────────────────────────────────────
public partial class RecyclerPanel : PanelContainer
{
    public Hub Hub;
    private VBoxContainer _hold, _queue;
    private Label _rate, _gate;
    private bool _served;                  // what the equipment base's gate said at the last Rebuild
    public const float W = 560f;
    private const float ListH = 640f;      // the queue and the hold, scrolling together: the window ends above the hull bar

    public override void _Ready()
    {
        Name = "RecyclerPanel";
        // IN THE SIDE SPOT, where EQUIPMENT opens: the two are the equipment base's windows and hand
        // the spot to each other. Sized by what it holds, like every side window.
        Position = new Vector2(360, 92);
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(12));
        MouseFilter = MouseFilterEnum.Stop;

        var col = Ui.VBox(8, "Body"); AddChild(col);
        var head = Ui.HBox(8);
        var heading = Ui.Lbl("RECYCLER", Ui.Head); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill; head.AddChild(heading);
        head.AddChild(Ui.Btn("EQUIPMENT", () => Hub.ToggleEquipment(), "Equipment"));
        col.AddChild(head);
        _rate = Ui.Lbl("", Ui.Small, Ui.Dim); col.AddChild(_rate);
        // WHY QUEUE IS GREY, when it is: off the equipment base, or in combat (Landmarks.Serves)
        _gate = Ui.Lbl("", Ui.Small, Ui.Warn); _gate.Name = "Gate"; col.AddChild(_gate);
        // THE QUEUE AND THE HOLD SCROLL TOGETHER, at a fixed height, so neither can push the other
        // off the bottom of the window.
        var scroll = new ScrollContainer { Name = "Scroll", CustomMinimumSize = new Vector2(W - 24, ListH),
                                           HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        var lists = Ui.VBox(8); lists.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(lists);
        lists.AddChild(Ui.Heading("Queued"));
        _queue = Ui.VBox(4, "Queue"); lists.AddChild(_queue);
        lists.AddChild(Ui.Heading("Hold"));
        _hold = Ui.VBox(4, "Hold"); lists.AddChild(_hold);
        Rebuild();
    }

    public override void _Process(double delta)
    {
        // The queue drains on the yard's clock, so the panel only has to keep up with it -- and with
        // the gate, which opens and shuts as the pilot flies on and off the base and in and out of a fight.
        var gate = Landmarks.Serves(Hub?.MyShip, Service.Scrap);
        Ui.SetText(_gate, gate.Ok ? "" : "Scrapping is shut: " + gate.Why);
        if (Hub?.Yard is { } y && (y.ScrapPending != _shown || gate.Ok != _served)) { _shown = y.ScrapPending; Rebuild(); }
    }
    private int _shown = -1;

    private void Rebuild()
    {
        Ui.Clear(_hold); Ui.Clear(_queue);
        var yard = Hub?.Yard;
        if (yard == null) return;
        _shown = yard.ScrapPending;
        var gate = Landmarks.Serves(Hub.MyShip, Service.Scrap);
        _served = gate.Ok;
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
            var row = Ui.HBox(8, "Hold_" + kv.Key);
            var text = Ui.VBox(1); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.AddChild(Ui.Lbl($"{it.Name}{(kv.Value > 1 ? $"  x{kv.Value}" : "")}",
                                 Ui.Body, Ui.RarityColor(it.Rarity)));
            text.AddChild(Ui.Lbl($"{it.Slot.ToString().ToUpperInvariant()}  ·  {Economy.ScrapValue(it.Rarity):0} salvage", Ui.Small, Ui.Dim));
            row.AddChild(text);
            var lockBtn = Ui.Btn(locked ? "LOCKED" : "LOCK", () => { Character.ToggleGearLock(kv.Key); Rebuild(); }, "Lock");
            row.AddChild(lockBtn);
            var scrap = Ui.Btn("QUEUE", () => { yard.QueueScrap(kv.Key); Rebuild(); }, "Queue");
            scrap.Disabled = locked || !gate.Ok;
            if (!gate.Ok) scrap.TooltipText = gate.Why;
            row.AddChild(scrap);
            _hold.AddChild(Ui.CardWrap(row));
        }
        if (_hold.GetChildCount() == 0) _hold.AddChild(Ui.Lbl("the hold is empty", Ui.Small, Ui.Dim));
    }
}
