using Godot;

// The escape pod a pilot flies while their ship is in stasis. Small (60% of a
// salvager), quick, and non-interactable: it is in no target list, so nothing can
// select, shoot or collide with it. Its owner flies it (WASD); everyone else sees
// the position the owner reports.
public partial class EscapePod : Node2D
{
    public const float Length = 24f, Speed = 95f, Accel = 260f;
    public bool Local;                  // true on the owner's machine
    public Color Engine = Colors.White; // the owner's accent: it is a player craft with an engine
    public Vector2 Velocity;
    private Vector2 _net; private bool _hasNet;
    // ITS ENGINE BURNS (Plume's point) while its pilot holds a key; elsewhere, while its reports move it.
    // Otherwise the plume is the idle half-disc.
    public bool Burning { get; private set; }

    public override void _Ready()
    {
        AddChild(Sprites.Fit("res://escape_pod.png", Length));
        ZIndex = 5;
    }

    public void SetNet(Vector2 p, float rot)
    {
        Burning = _hasNet && p.DistanceSquaredTo(_net) > 0.25f;
        _net = p; _hasNet = true; Rotation = rot;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Local)
        {
            if (_hasNet) Position = Position.Lerp(_net, Mathf.Clamp(12f * dt, 0f, 1f));
            return;
        }
        var dir = Vector2.Zero;
        if (!Hub.ControlsLocked)
        {
            if (Input.IsKeyPressed(Key.W)) dir.Y -= 1;
            if (Input.IsKeyPressed(Key.S)) dir.Y += 1;
            if (Input.IsKeyPressed(Key.A)) dir.X -= 1;
            if (Input.IsKeyPressed(Key.D)) dir.X += 1;
        }
        var want = dir == Vector2.Zero ? Vector2.Zero : dir.Normalized() * Speed;
        Burning = dir != Vector2.Zero;
        Velocity = Velocity.MoveToward(want, Accel * dt);
        Position += Velocity * dt;
        QueueRedraw();
        if (Velocity.LengthSquared() > 25f)
            Rotation = Mathf.LerpAngle(Rotation, Aim.Along(Velocity), Mathf.Clamp(8f * dt, 0f, 1f));
    }

    public override void _Draw() =>
        Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, Length, Engine, Velocity.Length() / Speed, Burning);
}
