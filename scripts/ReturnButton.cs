using Godot;

// RETURN TO BASE -- in the arena, after a win only. Each pilot presses it when it has finished
// collecting its crates; the whole party warps home once every pilot present has (Hub: no clock,
// a held place does not block it). Hidden until the boss is dead, so it can never end a mission
// that is still on. On the arena HUD layer, so a click never falls through to the world.
public partial class ReturnButton : Button
{
    public Hub Hub;

    public override void _Ready()
    {
        Name = "ReturnButton";
        FocusMode = FocusModeEnum.None;
        AnchorLeft = AnchorRight = 0.5f;
        OffsetLeft = -120; OffsetRight = 120; OffsetTop = 176; OffsetBottom = 210;
        Ui.Style(this);                        // a CanvasLayer breaks theme inheritance -- see Ui
        Pressed += () => Hub?.SetMyReturn(!Hub.IReturned);
        Visible = false;
    }

    public override void _Process(double delta)
    {
        Visible = Hub != null && Hub.InArena && Hub.MissionWon;
        if (Visible) Ui.SetText(this, Hub.IReturned ? $"READY  ·  WAITING  {Hub.ReturnReady} / {Hub.ReturnTotal}" : "RETURN TO BASE");
    }
}
