// THE BUILD'S IDENTITY. One number, bumped by one for each release that goes out as a zip.
//
// A character file records the version it was written by. When that does not match, the select
// screen greys the character out rather than loading it: a save written before a mechanic existed
// has no sensible value for it, and guessing one quietly is how a character ends up half in the
// old world and half in the new. The message says so, and says the door is not closed.
//
// WHEN TO BUMP IT: whenever a release changes what a character file MEANS -- a new persisted
// field, a changed unit, a renamed id, a rebalanced cost that invalidates what was bought. Not
// for a visual change, a fix, or anything a save cannot notice.
public static class Game
{
    public const int Version = 1;

    // Shown when a character cannot be loaded because it predates this build. Kept here, beside
    // the number that causes it, rather than in the screen that happens to draw it.
    public const string IncompatibleNote =
        "May be possible to import in the future, for now, a new character is required";
}
