using Godot;

// ART, SIZED TO THE WORLD. Every hull is its art scaled so the drawing measures `length` world
// units nose to tail -- the one rule the fleet, the raiders, the boss, the pod, the wing, the TIO
// and the title screen's foes all follow, and each of them used to write out for itself.
public static class Sprites
{
    public static Sprite2D Fit(string path, float length)
    {
        var tex = Assets.Load<Texture2D>(path);
        return new Sprite2D { Texture = tex, Scale = Vector2.One * (length / tex.GetHeight()) };
    }
}
