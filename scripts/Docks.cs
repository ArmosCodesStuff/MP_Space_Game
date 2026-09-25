using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// DOCKS — a place on a station's hull a craft pulls up to, nose in, and sits a while. ONE ROW PER
// DOCK, measured off its station's art, and nothing about where a craft docks anywhere else.
//
// WHY A FILE, AND WHY "DOCK". The mechanism is A FACE ON A STATION THAT A CRAFT HOLDS OFF, NOSE
// IN: where it stops, which way it points, how close it comes. Its first use was the base's five
// service arms, and they were the Yard's own class and table (Yard.Arm, Yard.Arms, with the
// berth worked out in Yard.UnloadSpot) -- so when the lanes' couriers came they could not reach a
// pad at all, and turned round in open space on the line between the base and the outpost
// (Lanes.BaseDock / Lanes.Dock / Lanes.Run), short of both. The arms are rows here now, the
// outposts' four clamps beside them, and a miner and a courier find their spot by the SAME
// arithmetic (Dock.Berth), by the SAME "nearest pad" rule (Docks.Nearest), and swing onto it at
// the SAME rate (Docks.NoseIn).
//
// WHICH STATION CARRIES WHICH DOCKS is its place's row (Landmark.Docks, Landmarks.cs), and a
// station's docks are reached through that row, by its id: Docks.On(Landmarks.ById("base")).
// The rows are grouped here by the art they are measured on -- BaseArms on base_station.png,
// OutpostClamps on outpost.png -- and every place built from that art names the group, so four
// outposts are four places carrying one group of four, not sixteen rows.
//
// A ROW MUST FILL IN:
//   Id        what it is called (a check names it)
//   Pad       its centre, from the station's centre, in world units: pixels from the art's
//             centre times the scale the station is drawn at
//   Open      the outward normal of the face craft load through: they hold on this side, nose in
//   Reach     the pad's centre to that face
//   Face      that face's length: what the Yard's hologram bars span, and how far along it a
//             second craft can sit (Dock.Abreast)
// ...in the group of the art it is measured on. A station with new art is a new group here and
// its Landmark row naming it (Docks = ...), and nothing else.
// BASEARMS ARE IN THE ORDER THE WIRE SENDS AN ARM'S INDEX (Yard.NetState): APPEND ONLY.
//
// WHO HOLDS ONE is not a dock's business. The Yard reserves the base's arms for its miners and
// salvagers, one at a time with a queue for the rest (Yard.Arm.Occupant), because an unload is a
// delivery. A courier delivers nothing and holds nothing, so it takes the nearest dock whoever is
// in it. A craft that holds the dock berths on the face's centre line; one that holds none berths
// at its outboard end (Dock.Abreast), so two craft on one pad read as two and not one on the other.
//
// HOW A CRAFT GETS THERE is its own: a miner steers (Gatherer.FlyTo) because the host flies it and
// a guest follows it; a courier is on rails off its own clock (Courier.Place) because nothing is
// read off where it is. Both end on Dock.Berth and swing through Docks.NoseIn.
//
// Hub.DockAt is NOT a dock: it is where the 200 u hauler HOLDS off an outpost on an escort, a hull
// far larger than any clamp on it.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class DockDef
{
    public string Id;            // reached by id or row, never by type
    public Vector2 Pad;          // pad centre, from the station's centre
    public Vector2 Open;         // the open face's outward normal: craft load through this side
    public float Reach;          // pad centre to its open face
    public float Face;           // length of the open face
}

// A DOCK IN THE WORLD: a row, on a station standing at At. What a craft flies to.
public readonly struct Dock
{
    public readonly DockDef Row;
    public readonly Vector2 At;                  // where its station stands
    public Dock(DockDef row, Vector2 at) { Row = row; At = at; }

    public Vector2 Pad => At + Row.Pad;
    // the rotation that points a nose-up hull in through the open face
    public float Heading => Aim.Along(-Row.Open);
    // ALONG THE FACE, AWAY FROM THE STATION: the side of the centre line that is outside. (A pad
    // on the station's own axis takes the face's positive side.)
    public Vector2 Outboard { get { var s = new Vector2(-Row.Open.Y, Row.Open.X); return s.Dot(Row.Pad) < 0f ? -s : s; } }
    // how far outboard along the face a craft `halfWidth` wide can sit and still be on it
    public float Abreast(float halfWidth) => Mathf.Max(0f, Row.Face * 0.5f - halfWidth);
    // WHERE A CRAFT HOLDS: `length` long, its nose Docks.Gap off the open face, on the open side,
    // `abreast` u outboard of the centre line (0: on it, for the craft that holds the dock)
    public Vector2 Berth(float length, float abreast = 0f) =>
        Pad + Row.Open * (Row.Reach + length * 0.5f + Docks.Gap) + Outboard * abreast;
}

public static class Docks
{
    public const float Gap = 4f;         // nose to face, at rest
    public const float Swing = 6f;       // how briskly a craft turns onto its heading: the lerp weight a second

    public static readonly DockDef[] BaseArms =
    {
        // THE BASE'S FIVE SERVICE ARMS: base_station.png, pixels from its centre x 0.6 (Hub.BaseScale).
        // Measured off each pad's rotated outline: its centre, and its north-facing edge -- the
        // outward normal (Open), the distance to it (Reach) and its length (Face). The four diagonal
        // pads are turned 30 degrees, so their north faces tilt with them; every arm, the top one
        // included, loads from its north face.
        new() { Id = "arm_n",  Pad = new(0f, -195.9f),     Open = new(0f, -1f),          Reach = 22.2f, Face = 54.6f },
        new() { Id = "arm_nw", Pad = new(-169.5f, -97.9f), Open = new(0.501f, -0.865f),  Reach = 28.1f, Face = 45.6f },
        new() { Id = "arm_ne", Pad = new(169.5f, -97.9f),  Open = new(-0.501f, -0.865f), Reach = 28.1f, Face = 45.6f },
        new() { Id = "arm_sw", Pad = new(-169.5f, 97.9f),  Open = new(-0.501f, -0.865f), Reach = 28.1f, Face = 45.6f },
        new() { Id = "arm_se", Pad = new(169.5f, 97.9f),   Open = new(0.501f, -0.865f),  Reach = 28.1f, Face = 45.6f },
    };

    public static readonly DockDef[] OutpostClamps =
    {
        // AN OUTPOST'S FOUR CLAMPS: outpost.png (150 x 150 px, drawn 170 u tall: 1.133 u a pixel,
        // never rotated), the forked clamps on its corners. Each is a 20 x 13 px block on a joint to
        // the hull, its prongs flaring up and down at its outer end: the pad is the block's centre
        // (38 px either side of the art's centre, 50.5 px above it and 49 px below), the open face is
        // the outer end (9.5 px out: 10.8 u) and the face is the prongs' span (22 px: 24.9 u).
        new() { Id = "clamp_nw", Pad = new(-43.1f, -57.2f), Open = new(-1f, 0f), Reach = 10.8f, Face = 24.9f },
        new() { Id = "clamp_ne", Pad = new(43.1f, -57.2f),  Open = new(1f, 0f),  Reach = 10.8f, Face = 24.9f },
        new() { Id = "clamp_sw", Pad = new(-43.1f, 55.5f),  Open = new(-1f, 0f), Reach = 10.8f, Face = 24.9f },
        new() { Id = "clamp_se", Pad = new(43.1f, 55.5f),   Open = new(1f, 0f),  Reach = 10.8f, Face = 24.9f },
    };

    // EVERY DOCK ON A PLACE, where it stands, in its group's order (none, for a place with none).
    public static Dock[] On(Landmark place) =>
        (place?.Docks ?? Array.Empty<DockDef>()).Select(d => new Dock(d, place.At)).ToArray();

    // THE NEAREST PAD to a point, among those `free` allows (every one, if it is not given): its
    // index, or -1 when none is. A miner asks among the base's arms nobody holds (Yard.RequestArm);
    // a courier among every dock on the station at each end of its lane (Lanes.EndDocks).
    public static int Nearest(IReadOnlyList<Dock> docks, Vector2 from, Func<int, bool> free = null)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < docks.Count; i++)
        {
            if (free != null && !free(i)) continue;
            float d = from.DistanceSquaredTo(docks[i].Pad);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // THE ONE NOSE-IN: a craft on a dock swings to point in through its open face, at the same
    // rate whoever it is.
    public static void NoseIn(Node2D craft, Dock dock, float dt) =>
        craft.Rotation = Mathf.LerpAngle(craft.Rotation, dock.Heading, Mathf.Clamp(Swing * dt, 0f, 1f));
}
