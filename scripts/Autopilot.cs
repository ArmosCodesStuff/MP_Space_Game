using Godot;

// AUTOPILOT -- flies a ship to a point for its pilot (READY for a bounty flies you to
// the mission portal). Two variants, because the controls differ:
//   CAPITAL ships steer like naval vessels: rudder and throttle, no strafing, a
//     turning radius. The autopilot turns the bow onto the target, throttles down
//     as it closes, and stops inside the given radius.
//   FIGHTER-class ships point and thrust directly. (No player fighter class exists
//     yet; this variant is ready for the first one, and is tested on its own.)
// Both return the same inputs the keys would give, so the ship's own physics apply.
public static class Autopilot
{
    // -> (throttle -1..1, rudder -1..1) for PlayerShip.Steer
    public static (float throttle, float rudder) Capital(Vector2 pos, float rotation, Vector2 vel, float maxSpeed, Vector2 target, float stopRadius)
    {
        var to = target - pos; float dist = to.Length();
        if (dist <= stopRadius) return (vel.Length() > 4f ? -0.5f : 0f, 0f);         // there: take the way off
        var bow = Vector2.Up.Rotated(rotation);
        float off = bow.AngleTo(to);                                                   // + means the target is to starboard
        float rudder = Mathf.Clamp(off / 0.35f, -1f, 1f);
        // full ahead when roughly aligned, easing as it closes so it can stop in the radius
        float align = Mathf.Clamp(1f - Mathf.Abs(off) / 1.2f, 0.15f, 1f);
        float brake = Mathf.Clamp((dist - stopRadius) / (maxSpeed * 3f), 0.2f, 1f);
        float throttle = align * brake;
        if (vel.Length() > maxSpeed * brake + 5f) throttle = -0.3f;                   // too fast for the distance left
        return (throttle, rudder);
    }

    // -> (turn -1..1, thrust 0..1): point at the target and burn, braking to arrive
    public static (float turn, float thrust) Fighter(Vector2 pos, float heading, Vector2 vel, Vector2 target, float maxSpeed, float stopRadius)
    {
        var to = target - pos; float dist = to.Length();
        float off = Mathf.AngleDifference(heading, to.Angle());
        float turn = Mathf.Clamp(off / 0.25f, -1f, 1f);
        if (dist <= stopRadius) return (turn, 0f);
        float want = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * maxSpeed * dist));
        float thrust = Mathf.Abs(off) < 0.6f && vel.Length() < want ? 1f : 0f;
        return (turn, thrust);
    }
}
