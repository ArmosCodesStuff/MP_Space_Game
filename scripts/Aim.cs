using Godot;

// POINTING AND ARRIVING, once. A hull's nose is its local -Y, so "face that" is
// `(to - from).Angle() + PI/2` -- which was written out in twenty places, each free to get the
// sign wrong. Arrive-steering (ease off so you stop ON the point, never past it) is the same
// square root in five movers. Neither is a behaviour: they are the arithmetic behaviours are
// made of, and a new ship class should never have to rediscover either.
public static class Aim
{
    // the rotation of a nose-up hull pointing along `dir`
    public static float Along(Vector2 dir) => dir.Angle() + Mathf.Pi / 2f;
    // the rotation that points a nose-up hull at `to`
    public static float Face(Vector2 from, Vector2 to) => Along(to - from);
    // the world point `len` ahead of a node's centre, along its nose
    public static Vector2 Nose(Node2D n, float len) => n.Position + Vector2.Up.Rotated(n.Rotation) * len;
}

public static class Motion
{
    // The fastest speed to be going `dist` from a point and still stop exactly on it, at `accel`,
    // never above `top`.
    public static float Arrive(float top, float dist, float accel) =>
        Mathf.Min(top, Mathf.Sqrt(2f * accel * Mathf.Max(0f, dist)));
    // That speed, reached from `now` at no more than the engine's acceleration this frame.
    public static float ArriveSpeed(float now, float dist, float top, float accel, float dt) =>
        Mathf.MoveToward(now, Arrive(top, dist, accel), accel * dt);
}
