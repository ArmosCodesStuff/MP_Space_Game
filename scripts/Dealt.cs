// ─────────────────────────────────────────────────────────────────────────────
// THE HOSTILE DAMAGE DOOR (F4 + F18) — every blow a player's side deals to a hostile goes through
// here: the damage lands (IHittable.TakeDamage), then whoever dealt it is credited
// (ITurretHost.NoteDealt), by a weapon id, so anything that listens for "I just hit something" --
// the echo, a future ability's AbilityDef.OnDealt -- has ONE place to hear it, instead of a copy of
// the credit line at every gun. A hostile hitting a PLAYER (PlayerShip.Incoming / Hit) is NOT this
// door: that one is unchanged.
//
// WEAPON IDS name what dealt the blow, for PlayerShip.DealtBy and AbilityDef.OnDealt. Where the
// blow already carries a row of its own, the row's OWN id is used (Shots.Of(kind).Id): a main gun's
// shell is "shell", a hunter or a bomber's torpedo "torpedo", the destroyer's burst "missile", the
// siege base's cruise-missile launcher "cruise". Everything else names its own id here.
// ─────────────────────────────────────────────────────────────────────────────
public static class Dealt
{
    public const string Pd = "pd", Turret = "turret", Rail = "rail", Emp = "emp", Echo = "echo",
        Fighter = "fighter", Outpost = "outpost", Base = "base";

    // EVERY BLOW THROUGH THE DOOR, whoever dealt it -- a blow with no ship behind it (the base's
    // own laser, a shot whose shooter has gone) included, which no NoteDealt hears. A listener
    // subscribes and unsubscribes in its own lifetime.
    public static event System.Action<IHittable, double, string> Landed;

    // `by` is null for a blow no ship dealt: it still lands through here, and Landed hears it.
    // The pilot behind the blow weighs it first (PlayerShip.Outgoing: its gear's conditions), and
    // hears the kill it made (PlayerShip.NoteKill).
    public static void Deal(IHittable target, double d, ITurretHost by, string weapon)
    {
        var pilot = by?.Credit;
        if (pilot != null) d = pilot.Outgoing(target, d, weapon);
        bool was = target.Alive;
        target.TakeDamage(d);
        by?.NoteDealt(d, target, weapon);
        if (pilot != null && was && !target.Alive) pilot.NoteKill();
        Landed?.Invoke(target, d, weapon);
    }
}
