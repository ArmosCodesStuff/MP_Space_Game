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
        Rarity.Uncommon => new Color(0.45f, 0.9f, 0.45f), Rarity.Rare => new Color(0.4f, 0.65f, 1f),
        Rarity.Epic => new Color(0.75f, 0.45f, 1f), Rarity.Legendary => new Color(1f, 0.65f, 0.25f),
        _ => new Color(0.84f, 0.86f, 0.9f),
    };

    public override void _Ready()
    {
        Name = "EquipmentWindow";
        Position = new Vector2(360, 92);
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(10, 1f));
        _col = new VBoxContainer(); _col.AddThemeConstantOverride("separation", 5); AddChild(_col);
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var c in _col.GetChildren()) { _col.RemoveChild(c); c.QueueFree(); }
        var cls = Character.Class; var l = Character.LoadoutFor(cls); var spares = Character.SparesFor(cls);
        var head = new Label { Text = $"EQUIPMENT  ·  {cls.ToString().ToUpper()}" }; head.AddThemeFontSizeOverride("font_size", 20); _col.AddChild(head);
        _col.AddChild(new Label { Text = "Parts change no looks, only numbers. Each class keeps its own gear.", Modulate = new Color(1, 1, 1, 0.6f) });
        for (int k = 0; k < Equipment.CoreSlots; k++)
        {
            var it = Equipment.ById(l[k]);
            var row = new HBoxContainer { Name = $"Slot_{Equipment.Core[k]}" }; row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Label { Text = Equipment.Core[k].ToString().ToUpper(), CustomMinimumSize = new Vector2(90, 0), Modulate = new Color(0.55f, 0.8f, 1f) });
            row.AddChild(new Label { Name = "Item", Text = it?.Name ?? "—", CustomMinimumSize = new Vector2(210, 0), Modulate = it != null ? RarityColor(it.Rarity) : Colors.Gray });
            row.AddChild(new Label { Text = it?.Blurb ?? "", Modulate = new Color(1, 1, 1, 0.55f) });
            _col.AddChild(row);
        }
        _col.AddChild(new Label { Text = "CHIPS", Modulate = new Color(0.55f, 0.8f, 1f) });
        for (int k = 0; k < Equipment.ChipSlots; k++)
        {
            int slot = Equipment.CoreSlots + k; var it = Equipment.ById(l[slot]);
            var row = new HBoxContainer { Name = $"Chip_{k}" }; row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Label { Text = it != null ? $"{it.Name}   ({it.Blurb})" : "(empty)", CustomMinimumSize = new Vector2(360, 0),
                                     Modulate = it != null ? RarityColor(it.Rarity) : new Color(1, 1, 1, 0.4f) });
            if (it != null)
            {
                var off = new Button { Name = "Unequip", Text = "UNEQUIP", FocusMode = FocusModeEnum.None };
                off.Pressed += () => { spares.Add(l[slot]); l[slot] = ""; Changed(); };
                row.AddChild(off);
            }
            _col.AddChild(row);
        }
        _col.AddChild(new Label { Text = spares.Count > 0 ? "SPARE CHIPS" : "SPARE CHIPS  (none)", Modulate = new Color(0.55f, 0.8f, 1f) });
        for (int j = 0; j < spares.Count; j++)
        {
            int idx = j; var it = Equipment.ById(spares[j]);
            var row = new HBoxContainer { Name = $"Spare_{j}" }; row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Label { Text = $"{it?.Name}   ({it?.Blurb})", CustomMinimumSize = new Vector2(360, 0), Modulate = RarityColor(it?.Rarity ?? Rarity.Common) });
            int free = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, s => string.IsNullOrEmpty(s));
            var on = new Button { Name = "Equip", Text = "EQUIP", FocusMode = FocusModeEnum.None, Disabled = free < 0 };
            on.Pressed += () => { int f = System.Array.FindIndex(l, Equipment.CoreSlots, Equipment.ChipSlots, s => string.IsNullOrEmpty(s));
                                  if (f < 0) return; l[f] = spares[idx]; spares.RemoveAt(idx); Changed(); };
            row.AddChild(on); _col.AddChild(row);
        }
        _col.AddChild(new Label { Text = "I or Esc closes.", Modulate = new Color(1, 1, 1, 0.45f) });
    }

    // saved at once; the ship refits, and the host is told (it applies equipment too)
    private void Changed() { Character.Save(); Hub.PilotChanged(); CallDeferred(nameof(Rebuild)); }
}
