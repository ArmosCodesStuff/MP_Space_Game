using Godot;

// EQUIPMENT (I): the parts on the pilot's CURRENT ship -- five core slots and five chips --
// and the chips taken off it. Chips can come off and go back on; the core parts stay put
// until there is something to swap them for (the inventory, later). Each class keeps its own.
public partial class EquipmentWindow : PanelContainer
{
    public Hub Hub;
    private VBoxContainer _col;
    private static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Uncommon => Ui.Good, Rarity.Rare => Ui.Accent,
        Rarity.Epic => new Color(0.75f, 0.45f, 1f), Rarity.Legendary => Ui.Warn,
        _ => Ui.Text,
    };

    public override void _Ready()
    {
        Name = "EquipmentWindow";
        Position = new Vector2(360, 92);
        Ui.Panelise(this);
        _col = new VBoxContainer(); _col.AddThemeConstantOverride("separation", 10); AddChild(_col);
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var c in _col.GetChildren()) { _col.RemoveChild(c); c.QueueFree(); }
        var cls = Character.Class; var l = Character.LoadoutFor(cls); var spares = Character.SparesFor(cls);
        var title = new VBoxContainer(); title.AddThemeConstantOverride("separation", 2);
        title.AddChild(Ui.Lbl($"EQUIPMENT  ·  {Classes.NameOf(cls)}", Ui.Title, Ui.Accent));
        title.AddChild(Ui.Lbl("Parts change no looks, only numbers. Each class keeps its own gear.", Ui.Small, Ui.Dim));
        _col.AddChild(title);
        _col.AddChild(Ui.Heading("Core parts"));
        for (int k = 0; k < Equipment.CoreSlots; k++)
        {
            var it = Equipment.ById(l[k]);
            var row = new HBoxContainer { Name = $"Slot_{Equipment.Core[k]}" }; row.AddThemeConstantOverride("separation", 12);
            var slotName = Ui.Lbl(Equipment.Core[k].ToString().ToUpperInvariant(), Ui.Small, Ui.Dim);
            slotName.CustomMinimumSize = new Vector2(90, 0); slotName.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(slotName);
            var item = Ui.Lbl(it?.Name ?? "—", Ui.Body, it != null ? RarityColor(it.Rarity) : Ui.Dim);
            item.Name = "Item"; item.CustomMinimumSize = new Vector2(210, 0); item.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(item);
            var blurb = Ui.Lbl(it?.Blurb ?? "", Ui.Small, Ui.Dim);
            blurb.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(blurb);
            _col.AddChild(Ui.CardWrap(row));
        }
        _col.AddChild(Ui.Heading("Chips"));
        for (int k = 0; k < Equipment.ChipSlots; k++)
        {
            int slot = Equipment.CoreSlots + k; var it = Equipment.ById(l[slot]);
            var row = new HBoxContainer { Name = $"Chip_{k}" }; row.AddThemeConstantOverride("separation", 12);
            var chip = Ui.Lbl(it != null ? $"{it.Name}   ({it.Blurb})" : "(empty)", Ui.Body,
                              it != null ? RarityColor(it.Rarity) : Ui.Dim with { A = 0.7f });
            chip.CustomMinimumSize = new Vector2(360, 0); chip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            chip.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(chip);
            if (it != null)
            {
                var off = new Button { Name = "Unequip", Text = "UNEQUIP", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                off.Pressed += () => { spares.Add(l[slot]); l[slot] = ""; Changed(); };
                row.AddChild(off);
            }
            _col.AddChild(Ui.CardWrap(row));
        }
        _col.AddChild(Ui.Heading(spares.Count > 0 ? "Spare chips" : "Spare chips  (none)"));
        for (int j = 0; j < spares.Count; j++)
        {
            int idx = j; var it = Equipment.ById(spares[j]);
            var row = new HBoxContainer { Name = $"Spare_{j}" }; row.AddThemeConstantOverride("separation", 12);
            var sp = Ui.Lbl($"{it?.Name}   ({it?.Blurb})", Ui.Body, RarityColor(it?.Rarity ?? Rarity.Common));
            sp.CustomMinimumSize = new Vector2(360, 0); sp.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            sp.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(sp);
            int free = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, s => string.IsNullOrEmpty(s));
            var on = new Button { Name = "Equip", Text = "EQUIP", FocusMode = FocusModeEnum.None, Disabled = free < 0, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            on.Pressed += () => { int f = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, s => string.IsNullOrEmpty(s));
                                  if (f < 0) return; l[f] = spares[idx]; spares.RemoveAt(idx); Changed(); };
            row.AddChild(on); _col.AddChild(Ui.CardWrap(row));
        }
        _col.AddChild(Ui.Lbl("I or Esc closes.", Ui.Small, Ui.Dim));
    }

    // saved at once; the ship refits, and the host is told (it applies equipment too)
    private void Changed() { Character.Save(); Hub.PilotChanged(); CallDeferred(nameof(Rebuild)); }
}
