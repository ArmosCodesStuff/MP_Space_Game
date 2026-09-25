using Godot;
using System.Linq;

// EQUIPMENT (I): the parts on the pilot's CURRENT ship -- five core slots and six chip slots, each
// chip slot opened by the pilot's highest level (Unlocks) -- beside the HOLD: every part this pilot
// owns and has not fitted, for any class (bosses drop them; see Loot). FIT puts a part from the
// hold on the ship and the part it replaces goes into the hold; EQUIP puts a chip in the first open
// empty slot (Equipment.ChipFit, which also says why not); UNEQUIP takes a chip off into the hold.
// A core slot is never empty. Each class keeps its own
// loadout; the hold is the pilot's. Only this pilot sees any of it: gear is per pilot, like the
// loot it comes from.
// It is the EQUIPMENT BASE's window. A click on the base opens it, and RECYCLER on its title line
// hands the spot to the recycler. It opens anywhere, to look and to fit. What it will NOT do
// anywhere is level a part: that is a service of the base (Landmarks.Serves), available over it
// and 10 s clear of combat.
public partial class EquipmentWindow : PanelContainer
{
    public Hub Hub;
    private VBoxContainer _ship, _hold;
    private Label _gate;                 // why the SALVAGE buttons are grey, when they are (on the foot line)
    private bool _served;                // what the equipment base's gate said at the last Rebuild
    // Both columns scroll, at a height that ends the window above the hull bar (930 px down a
    // 1080 screen): a ship's eleven parts, each with what it does, run past 970 px on their own.
    private const float ShipW = 500, HoldW = 400, ColH = 700;

    public override void _Ready()
    {
        Name = "EquipmentWindow";
        Position = new Vector2(360, 92);
        Ui.Panelise(this);
        var col = Ui.VBox(10); AddChild(col);
        var title = Ui.VBox(2);
        // THE TITLE LINE carries the way to the RECYCLER. Both windows belong to the equipment base
        // and share the side spot; a click on the base opens this one. Away from home there is no
        // yard and nothing to recycle, so there the button is not shown.
        var head = Ui.HBox(8);
        var heading = Ui.Lbl("EQUIPMENT", Ui.Title, Ui.Accent); heading.SizeFlagsHorizontal = SizeFlags.ExpandFill; head.AddChild(heading);
        var toRecycler = Ui.Btn("RECYCLER", () => Hub.OpenSide(() => new RecyclerPanel { Hub = Hub }), "Recycler");
        toRecycler.SizeFlagsVertical = SizeFlags.ShrinkCenter; toRecycler.Visible = Hub?.Yard != null; head.AddChild(toRecycler);
        title.AddChild(head);
        title.AddChild(Ui.Lbl("Bosses drop parts that lean hard one way. Each class keeps its own; the hold is yours.", Ui.Small, Ui.Dim));
        col.AddChild(title);
        var cols = Ui.HBox(16); col.AddChild(cols);
        var shipScroll = new ScrollContainer { Name = "ShipScroll", CustomMinimumSize = new Vector2(ShipW + 14, ColH),
                                               HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _ship = Ui.VBox(8, "Ship"); _ship.CustomMinimumSize = new Vector2(ShipW, 0);
        shipScroll.AddChild(_ship); cols.AddChild(shipScroll);
        var scroll = new ScrollContainer { Name = "HoldScroll", CustomMinimumSize = new Vector2(HoldW, ColH),
                                           HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _hold = Ui.VBox(6, "Hold"); _hold.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_hold); cols.AddChild(scroll);
        // THE FOOT LINE says WHY the SALVAGE buttons are grey, when they are. It shares the line
        // that was already there: the window ends about 30 px above the hull bar (930 px down), and a
        // line of its own would take most of that.
        var foot = Ui.HBox(16);
        foot.AddChild(Ui.Lbl("I or Esc closes.", Ui.Small, Ui.Dim));
        _gate = Ui.Lbl("", Ui.Small, Ui.Warn); _gate.Name = "Gate";
        _gate.SizeFlagsHorizontal = SizeFlags.ExpandFill; _gate.HorizontalAlignment = HorizontalAlignment.Right;
        foot.AddChild(_gate);
        col.AddChild(foot);
        Rebuild();
    }

    // THE EQUIPMENT BASE'S GATE, kept live. This window opens anywhere to look, and its SALVAGE
    // buttons wake the moment the pilot is over the base and 10 s clear of combat
    // (Landmarks.Serves). The reason is on the foot line -- except where there is no yard (the
    // arena), which has no SALVAGE buttons for it to explain. The rows are rebuilt only when the
    // answer changes.
    public override void _Process(double delta)
    {
        var gate = Landmarks.Serves(Hub?.MyShip, Service.LevelGear);
        Ui.SetText(_gate, gate.Ok || Hub?.Yard == null ? "" : "Levelling is shut: " + gate.Why);
        if (gate.Ok != _served) Rebuild();
    }

    private void Rebuild()
    {
        Ui.Clear(_ship); Ui.Clear(_hold);
        _served = Landmarks.Serves(Hub?.MyShip, Service.LevelGear).Ok;
        var cls = Character.Class; var l = Character.LoadoutFor(cls);
        _ship.AddChild(Ui.Heading($"{Classes.NameOf(cls)}  ·  core parts"));
        for (int k = 0; k < Equipment.CoreSlots; k++)
            _ship.AddChild(Ui.CardWrap(PartRow($"Slot_{Equipment.Core[k]}", Equipment.Core[k].ToString().ToUpperInvariant(), l[k], cls, Upgrade(l[k]))));
        _ship.AddChild(Ui.Heading("Chips"));
        int open = Unlocks.Count(Opens.ChipSlot, Character.Peak);
        for (int k = 0; k < Equipment.ChipSlots; k++)
        {
            int slot = Equipment.CoreSlots + k;
            // A SLOT THE PILOT'S LEVEL HAS NOT OPENED says the level that opens it, and has no button.
            if (k >= open)
            {
                _ship.AddChild(Ui.CardWrap(PartRow($"Chip_{k}", $"CHIP {k + 1}  ·  {Unlocks.Locked(Unlocks.At(Opens.ChipSlot, k + 1))}", l[slot], cls, null)));
                continue;
            }
            var off = string.IsNullOrEmpty(l[slot]) ? null
                    : Ui.Btn("UNEQUIP", () => { Character.Stow(l[slot]); l[slot] = ""; Changed(); }, "Unequip");
            _ship.AddChild(Ui.CardWrap(PartRow($"Chip_{k}", $"CHIP {k + 1}", l[slot], cls, off ?? Upgrade(l[slot]))));
        }

        // THE HOLD: this class's parts first, in slot order, rarest first; then other classes' parts,
        // named but not fittable here.
        int total = Character.GearHold.Values.Sum();
        _hold.AddChild(Ui.Heading(total > 0 ? $"Hold  ·  {total} part{(total == 1 ? "" : "s")}" : "Hold  ·  empty"));
        var owned = Character.GearHold.Where(kv => kv.Value > 0).Select(kv => (it: Equipment.ById(kv.Key), n: kv.Value))
                             .Where(x => x.it != null)
                             .OrderBy(x => !Equipment.Fits(x.it, x.it.Slot, cls))
                             .ThenBy(x => x.it.Slot).ThenByDescending(x => x.it.Rarity).ThenBy(x => x.it.Name).ToList();
        if (owned.Count == 0) _hold.AddChild(Ui.Lbl("Parts a boss drops for you land here.", Ui.Small, Ui.Dim));
        foreach (var (it, n) in owned)
        {
            string id = it.Id;
            Button fit = null;
            if (Equipment.Fits(it, it.Slot, cls))
            {
                // A CHIP WITH NOWHERE TO GO greys its EQUIP and says why: no open empty slot (and the
                // level that opens the next), or three of its kind already on.
                var (free, why) = it.Slot == GearSlot.Chip ? Equipment.ChipFit(l, Character.Peak, it) : (0, null);
                fit = it.Slot == GearSlot.Chip
                    ? Ui.Btn("EQUIP", () => FitChip(id), "Equip")
                    : Ui.Btn("FIT", () => FitCore(id), "Fit");
                fit.Disabled = free < 0;
                if (why != null) fit.TooltipText = why;
            }
            string what = !Equipment.Fits(it, it.Slot, cls) ? $"{it.Slot.ToString().ToUpperInvariant()}  ·  needs {Equipment.NeedsSaid(it)}"
                                                              : it.Slot.ToString().ToUpperInvariant();
            _hold.AddChild(Ui.CardWrap(PartRow($"Hold_{id}", what + (n > 1 ? $"  ·  x{n}" : ""), id, cls, fit, HoldW - 40)));
        }
    }

    // One part: its slot, its name in its rarity's colour, and what it does on this class, in words.
    private static HBoxContainer PartRow(string name, string slot, string id, ShipClass cls, Button action, float width = ShipW - 40)
    {
        var it = Equipment.ById(id);
        var row = Ui.HBox(10, name);
        var text = Ui.VBox(1); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // The slot line wraps like the effects line below it: it carries what a part asks of a
        // hull, which is a sentence ("WEAPON · needs Main guns"), not a word.
        var head = Ui.Lbl(slot, Ui.Small, Ui.Dim);
        head.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        head.CustomMinimumSize = new Vector2(width - (action != null ? 110 : 0), 0);
        text.AddChild(head);
        // A LEVELLED PART SAYS SO beside its name: what it has been lifted to, in the units the
        // salvage bought (+5% a level to what the part is FOR).
        int lv = it != null ? Equipment.LevelOf(it.Id) : 0;
        var item = Ui.Lbl((it?.Name ?? "(empty)") + (lv > 0 ? $"   +{lv * Equipment.LevelStep * 100:0}%" : ""),
                          Ui.Body, it != null ? Ui.RarityColor(it.Rarity) : Ui.Dim);
        item.Name = "Item"; text.AddChild(item);
        if (it != null)
        {
            var effects = Equipment.Describe(it, cls).ToList();
            var what = Ui.Lbl(effects.Count > 0 ? string.Join("   ", effects) : it.Blurb, Ui.Small, Ui.Dim);
            what.Name = "Effects"; what.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            what.CustomMinimumSize = new Vector2(width - (action != null ? 110 : 0), 0);
            text.AddChild(what);
        }
        row.AddChild(text);
        if (action != null) { action.SizeFlagsVertical = SizeFlags.ShrinkCenter; action.CustomMinimumSize = new Vector2(96, 0); row.AddChild(action); }
        return row;
    }

    // A core part from the hold onto its slot; the part it replaces goes into the hold.
    // LEVEL THIS PART, in salvage from your own base: what the next one costs, or nothing at all
    // for an empty slot, a part at the ceiling, or a base with no salvage in it. A level lifts what
    // the part is FOR by 5% and leaves what it takes exactly as printed.
    private Button Upgrade(string id)
    {
        var it = Equipment.ById(id);
        if (it == null || Hub.I?.Yard is not { } yard) return null;
        double cost = Equipment.NextLevelCost(id);
        if (cost < 0) { var top = Ui.Btn($"+{Equipment.MaxLevel * Equipment.LevelStep * 100:0}%", () => { }, "Upgrade"); top.Disabled = true; return top; }
        // ...and only over the EQUIPMENT BASE, 10 s clear of combat. The price stays on the button,
        // greyed, so a pilot can see what a level will cost before flying there; the foot line says why.
        var gate = Landmarks.Serves(Hub?.MyShip, Service.LevelGear);
        var b = Ui.Btn($"{cost:0} SALVAGE", () => { if (yard.BuyGearLevel(id)) Changed(); }, "Upgrade");
        b.Disabled = yard.OwnStock("salvage") < cost || !gate.Ok;
        if (!gate.Ok) b.TooltipText = gate.Why;
        return b;
    }

    private void FitCore(string id)
    {
        var it = Equipment.ById(id); var cls = Character.Class; var l = Character.LoadoutFor(cls);
        int k = System.Array.IndexOf(Equipment.Core, it?.Slot ?? GearSlot.Chip);
        if (k < 0 || !Equipment.Fits(it, Equipment.Core[k], cls) || !Character.Unstow(id)) return;
        Character.Stow(l[k]); l[k] = id; Changed();
    }

    // A chip from the hold into the first open empty chip slot (Equipment.ChipFit).
    private void FitChip(string id)
    {
        var l = Character.LoadoutFor(Character.Class);
        int free = Equipment.ChipFit(l, Character.Peak, Equipment.ById(id)).slot;
        if (free < 0 || !Character.Unstow(id)) return;
        l[free] = id; Changed();
    }

    // saved at once; the ship refits, and the host is told (it applies equipment too)
    private void Changed() { Character.Save(); Hub.PilotChanged(); CallDeferred(nameof(Rebuild)); }
}
