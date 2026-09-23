using Godot;
using System.Linq;

// EQUIPMENT (I): the parts on the pilot's CURRENT ship -- five core slots and five chips -- beside
// the HOLD: every part this pilot owns and has not fitted, for any class (bosses drop them; see
// Loot). FIT puts a part from the hold on the ship and the part it replaces goes into the hold;
// UNEQUIP takes a chip off into the hold. A core slot is never empty. Each class keeps its own
// loadout; the hold is the pilot's. Only this pilot sees any of it: gear is per pilot, like the
// loot it comes from.
public partial class EquipmentWindow : PanelContainer
{
    public Hub Hub;
    private VBoxContainer _ship, _hold;
    // Both columns scroll, at a height that ends the window above the hull bar (930 px down a
    // 1080 screen): a ship's ten parts, each with what it does, run to about 970 px on their own.
    private const float ShipW = 500, HoldW = 400, ColH = 700;

    public override void _Ready()
    {
        Name = "EquipmentWindow";
        Position = new Vector2(360, 92);
        Ui.Panelise(this);
        var col = Ui.VBox(10); AddChild(col);
        var title = Ui.VBox(2);
        title.AddChild(Ui.Lbl("EQUIPMENT", Ui.Title, Ui.Accent));
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
        col.AddChild(Ui.Lbl("I or Esc closes.", Ui.Small, Ui.Dim));
        Rebuild();
    }

    private void Rebuild()
    {
        Ui.Clear(_ship); Ui.Clear(_hold);
        var cls = Character.Class; var l = Character.LoadoutFor(cls);
        _ship.AddChild(Ui.Heading($"{Classes.NameOf(cls)}  ·  core parts"));
        for (int k = 0; k < Equipment.CoreSlots; k++)
            _ship.AddChild(Ui.CardWrap(PartRow($"Slot_{Equipment.Core[k]}", Equipment.Core[k].ToString().ToUpperInvariant(), l[k], cls, null)));
        _ship.AddChild(Ui.Heading("Chips"));
        for (int k = 0; k < Equipment.ChipSlots; k++)
        {
            int slot = Equipment.CoreSlots + k;
            var off = string.IsNullOrEmpty(l[slot]) ? null
                    : Ui.Btn("UNEQUIP", () => { Character.Stow(l[slot]); l[slot] = ""; Changed(); }, "Unequip");
            _ship.AddChild(Ui.CardWrap(PartRow($"Chip_{k}", $"CHIP {k + 1}", l[slot], cls, off)));
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
                int free = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, string.IsNullOrEmpty);
                fit = it.Slot == GearSlot.Chip
                    ? Ui.Btn("EQUIP", () => FitChip(id), "Equip")
                    : Ui.Btn("FIT", () => FitCore(id), "Fit");
                fit.Disabled = it.Slot == GearSlot.Chip && free < 0;
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
        var item = Ui.Lbl(it?.Name ?? "(empty)", Ui.Body, it != null ? Ui.RarityColor(it.Rarity) : Ui.Dim);
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
    private void FitCore(string id)
    {
        var it = Equipment.ById(id); var cls = Character.Class; var l = Character.LoadoutFor(cls);
        int k = System.Array.IndexOf(Equipment.Core, it?.Slot ?? GearSlot.Chip);
        if (k < 0 || !Equipment.Fits(it, Equipment.Core[k], cls) || !Character.Unstow(id)) return;
        Character.Stow(l[k]); l[k] = id; Changed();
    }

    // A chip from the hold into the first empty chip slot.
    private void FitChip(string id)
    {
        var l = Character.LoadoutFor(Character.Class);
        int free = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, string.IsNullOrEmpty);
        if (free < 0 || Equipment.ById(id)?.Slot != GearSlot.Chip || !Character.Unstow(id)) return;
        l[free] = id; Changed();
    }

    // saved at once; the ship refits, and the host is told (it applies equipment too)
    private void Changed() { Character.Save(); Hub.PilotChanged(); CallDeferred(nameof(Rebuild)); }
}
