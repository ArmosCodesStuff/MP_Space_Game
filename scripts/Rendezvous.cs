using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

// HOW TWO MACHINES SWAP SESSION DESCRIPTIONS (docs/plans/network_webrtc.md §4, §7). WebRTC needs each
// side to hand the other its session description -- ICE credentials, the DTLS fingerprint, the
// addresses it can be reached on -- and there is no server to carry them: the players carry them, as
// codes. This file owns every way they do:
//   * the four RECORDS (invite, reply, knock, refuse) and their one codec, Pack/Unpack, every record
//     ending in a check;
//   * the SDP TEMPLATE (Sdp): the plugin's bundle is 17 fixed lines of which five values vary, so a code
//     carries those five and the candidates, and the far side rebuilds the rest byte for byte;
//   * the TEXT a player pastes: WSI (invite) or WSR (reply), then Crockford base32, read forgivingly;
//   * the FIT rule that keeps a code inside its row's budget;
//   * the ROWS (Paths), the ways in: a pasted code, or a typed address. Each claims the text it can carry.
// It replaces the public-address reveal and copy (Net's Describe, Reach and PublicIpService, deleted in
// R2): a friend is sent an invite, not an address.
//
// A NEW WAY TO CARRY CODES IS A ROW of Paths (an IRendezvousPath); a new record is a Kind. The template,
// the format version and the alphabet are readonly or const, so they are part of the build's
// fingerprint: two builds whose codecs differ refuse each other before any network step.
public static class Rendezvous
{
    // ── the records (§4.1) ───────────────────────────────────────────────────
    public const int Format = 1;                    // the low nibble of every record's first byte
    public const int NameMax = 16, BuildMax = 24;   // bytes of UTF-8, cut at a character boundary
    public enum Kind { Invite = 1, Reply = 2, Knock = 3, Refuse = 4 }
    public enum Why { Full, Build, Rate, Closed }
    // A candidate's type: the codec's two bits (bit 2 is the family). Component 1 and UDP are implied,
    // because libjuice gathers nothing else.
    public enum Via { Host, Srflx, Prflx, Relay }
    public sealed record Candidate(Via Type, int Foundation, uint Priority, int Port, IPAddress Address);

    // One record of any kind. Each kind carries the fields its row of §4.1 names; the rest stay unset.
    public sealed class Record
    {
        public Kind Kind;
        public int Setup;                           // invite, reply: the SDP's a=setup (0 actpass, 1 active, 2 passive)
        public bool NoStun;                         // invite, reply: no STUN row answered, so this PC's own addresses only
        public int Proto;                           // invite, knock, refuse: the build's fingerprint
        public int Id;                              // invite, reply: the invite's id, which is also the friend's peer id
        public uint Guest;                          // knock: drawn once per game process
        public string Build = "", Name = "";        // Build: invite, knock, refuse. Name: invite, reply, knock
        public string Ufrag = "", Pwd = "";         // invite, reply: the ICE credentials
        public byte[] Fingerprint = new byte[32];   // invite, reply: the sha-256 of the DTLS certificate
        public List<Candidate> Candidates = new();  // invite, reply
        public Why Why;                             // refuse
    }

    // FORMAT VERSION 1, big-endian (§4.1): byte 0 is the kind (high nibble) and the format (low); then
    // the kind's fields; then the CHECK, the first 4 bytes of SHA-256 over everything before it.
    public static byte[] Pack(Record r)
    {
        var b = new List<byte> { (byte)((int)r.Kind << 4 | Format) };
        switch (r.Kind)
        {
            case Kind.Invite: b.Add(Flags(r)); Put32(b, r.Proto); Put32(b, r.Id); PutText(b, r.Build, BuildMax); PutText(b, r.Name, NameMax); PutSession(b, r); break;
            case Kind.Reply: b.Add(Flags(r)); Put32(b, r.Id); PutText(b, r.Name, NameMax); PutSession(b, r); break;
            case Kind.Knock: Put32(b, r.Proto); Put32(b, unchecked((int)r.Guest)); PutText(b, r.Build, BuildMax); PutText(b, r.Name, NameMax); break;
            case Kind.Refuse: b.Add((byte)r.Why); Put32(b, r.Proto); PutText(b, r.Build, BuildMax); break;
            default: throw new FormatException($"there is no record kind {(int)r.Kind}");
        }
        b.AddRange(SHA256.HashData(b.ToArray())[..4]);
        return b.ToArray();
    }

    // A whole record from its bytes (the listener's form); every byte must belong to it.
    public static Record Unpack(byte[] data)
    {
        int at = 0;
        var r = Read(() => at < data.Length ? data[at++] : throw new FormatException("the record is cut off"));
        if (at != data.Length) throw new FormatException($"{data.Length - at} bytes follow the record's end");
        return r;
    }

    // ONE RECORD, READ AS ITS FIELDS NEED BYTES: every field is fixed-length or length-prefixed, so a
    // record says its own length as it is read. That is what lets a pasted code sit inside a message:
    // nothing after its last character is ever read (§4.3).
    private static Record Read(Func<byte> source)
    {
        var seen = new List<byte>();
        byte Next() { byte x = source(); seen.Add(x); return x; }
        int Get32() => Next() << 24 | Next() << 16 | Next() << 8 | Next();
        string GetText(int max)
        {
            int n = Next();
            if (n > max) throw new FormatException($"a text field of {n} bytes, over its {max}");
            var bytes = new byte[n];
            for (int i = 0; i < n; i++) bytes[i] = Next();
            return Encoding.UTF8.GetString(bytes);
        }
        void GetSession(Record r)
        {
            r.Ufrag = GetCred(Next); r.Pwd = GetCred(Next);
            for (int i = 0; i < 32; i++) r.Fingerprint[i] = Next();
            int count = Next();
            for (int i = 0; i < count; i++)
            {
                int t = Next();
                if ((t & ~7) != 0) throw new FormatException($"a candidate type byte {t:x2}");
                int foundation = Next();
                uint priority = unchecked((uint)Get32());
                int port = Next() << 8 | Next();
                var addr = new byte[(t & 4) != 0 ? 16 : 4];
                for (int j = 0; j < addr.Length; j++) addr[j] = Next();
                r.Candidates.Add(new Candidate((Via)(t & 3), foundation, priority, port, new IPAddress(addr)));
            }
        }
        void GetFlags(Record r)
        {
            int f = Next();
            if ((f & ~7) != 0 || (f & 3) == 3) throw new FormatException($"flags {f:x2}");
            r.Setup = f & 3; r.NoStun = (f & 4) != 0;
        }

        int head = Next();
        var r = new Record { Kind = (Kind)(head >> 4) };
        if ((head & 15) != Format) throw new FormatException($"record format {head & 15}; this build reads {Format}");
        switch (r.Kind)
        {
            case Kind.Invite: GetFlags(r); r.Proto = Get32(); r.Id = Get32(); r.Build = GetText(BuildMax); r.Name = GetText(NameMax); GetSession(r); break;
            case Kind.Reply: GetFlags(r); r.Id = Get32(); r.Name = GetText(NameMax); GetSession(r); break;
            case Kind.Knock: r.Proto = Get32(); r.Guest = unchecked((uint)Get32()); r.Build = GetText(BuildMax); r.Name = GetText(NameMax); break;
            case Kind.Refuse:
                int why = Next();
                if (!Enum.IsDefined(typeof(Why), why)) throw new FormatException($"a refusal of kind {why}");
                r.Why = (Why)why; r.Proto = Get32(); r.Build = GetText(BuildMax);
                break;
            default: throw new FormatException($"there is no record kind {head >> 4}");
        }
        byte[] want = SHA256.HashData(seen.ToArray());
        for (int i = 0; i < 4; i++)
            if (source() != want[i]) throw new FormatException("the check does not match: the code is damaged or cut off");
        return r;
    }

    private static byte Flags(Record r)
    {
        if (r.Setup is < 0 or > 2) throw new FormatException($"a=setup {r.Setup}");
        return (byte)(r.Setup | (r.NoStun ? 4 : 0));
    }
    private static void Put32(List<byte> b, int v) { b.Add((byte)(v >> 24)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 8)); b.Add((byte)v); }
    private static void PutText(List<byte> b, string s, int max)
    {
        var bytes = Encoding.UTF8.GetBytes(Clip(s, max));
        b.Add((byte)bytes.Length); b.AddRange(bytes);
    }
    // A name or a build id as a code carries it: at most `max` bytes of UTF-8, never half a character.
    private static string Clip(string s, int max)
    {
        var kept = new StringBuilder();
        int used = 0;
        foreach (var rune in (s ?? "").EnumerateRunes())
        {
            if (used + rune.Utf8SequenceLength > max) break;
            used += rune.Utf8SequenceLength; kept.Append(rune.ToString());
        }
        return kept.ToString();
    }
    private static void PutSession(List<byte> b, Record r)
    {
        PutCred(b, r.Ufrag); PutCred(b, r.Pwd);
        if (r.Fingerprint is not { Length: 32 }) throw new FormatException("a fingerprint that is not the 32 bytes of a sha-256");
        b.AddRange(r.Fingerprint);
        if (r.Candidates.Count > 255) throw new FormatException($"{r.Candidates.Count} candidates");
        b.Add((byte)r.Candidates.Count);
        foreach (var c in r.Candidates)
        {
            bool v6 = c.Address.AddressFamily == AddressFamily.InterNetworkV6;
            if (!v6 && c.Address.AddressFamily != AddressFamily.InterNetwork) throw new FormatException($"the address {c.Address}");
            if (c.Foundation is < 0 or > 255) throw new FormatException($"a candidate foundation of {c.Foundation}: a code carries 0-255");
            b.Add((byte)((int)c.Type | (v6 ? 4 : 0))); b.Add((byte)c.Foundation);
            Put32(b, unchecked((int)c.Priority)); b.Add((byte)(c.Port >> 8)); b.Add((byte)c.Port);
            b.AddRange(c.Address.GetAddressBytes());
        }
    }

    // ICE CREDENTIALS, 6 bits a character over libjuice's base64 alphabet (4 + 22 characters in 22
    // bytes, §4.1). A character outside it is refused, by name.
    private const string Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private static void PutCred(List<byte> b, string s)
    {
        CheckCred(s);
        b.Add((byte)s.Length);
        int acc = 0, bits = 0;
        foreach (char c in s)
        {
            acc = acc << 6 | Base64.IndexOf(c); bits += 6;
            if (bits >= 8) { bits -= 8; b.Add((byte)(acc >> bits)); acc &= (1 << bits) - 1; }
        }
        if (bits > 0) b.Add((byte)(acc << (8 - bits)));
    }
    private static void CheckCred(string s)
    {
        if (s.Length is 0 or > 255) throw new FormatException($"an ICE credential of {s.Length} characters");
        foreach (char c in s)
            if (Base64.IndexOf(c) < 0) throw new FormatException($"the ICE credential \"{s}\" holds '{c}', which is not in libjuice's base64 alphabet");
    }
    private static string GetCred(Func<byte> next)
    {
        int n = next();
        var s = new StringBuilder(n);
        int acc = 0, bits = 0;
        while (s.Length < n)
        {
            if (bits < 6) { acc = acc << 8 | next(); bits += 8; }
            bits -= 6; s.Append(Base64[(acc >> bits) & 63]); acc &= (1 << bits) - 1;
        }
        return s.ToString();
    }

    // ── the SDP template (§4.2) ──────────────────────────────────────────────
    public static class Sdp
    {
        // THE PLUGIN'S 17 LINES (webrtc-native 1.2.1; SPIKE runs\stun offer_2 and answer_2). Five values
        // vary per connection, in braces; everything else is a constant of this plugin version. A plugin
        // upgrade that changes a line fails the solo run's byte-for-byte check on its first run.
        public static readonly string[] Template =
        {
            "v=0", "o=rtc {o} 0 IN IP4 127.0.0.1", "s=-", "t=0 0", "a=group:BUNDLE 0", "a=msid-semantic:WMS *",
            "a=ice-options:ice2,trickle", "a=fingerprint:sha-256 {fingerprint}", "m=application 9 UDP/DTLS/SCTP webrtc-datachannel",
            "c=IN IP4 0.0.0.0", "a=mid:0", "a=sendrecv", "a=sctp-port:5000", "a=max-message-size:262144",
            "a=setup:{setup}", "a=ice-ufrag:{ufrag}", "a=ice-pwd:{pwd}",
        };
        public static readonly string[] Setups = { "actpass", "active", "passive" };
        private const string Eol = "\r\n";

        // SENDING: the plugin's own SDP and candidate lines, reduced to a record of the given kind. Each
        // line must match its template line; a line the template does not know is refused, and the
        // refusal names it -- so an upgrade that adds a line the far end needs fails here, loudly.
        public static Record Strip(Kind kind, string sdp, IEnumerable<string> candidates)
        {
            var r = new Record { Kind = kind };
            var lines = (sdp ?? "").Split(Eol);
            int n = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
            for (int i = 0; i < Math.Max(n, Template.Length); i++)
            {
                if (i >= Template.Length) throw new FormatException($"the SDP line \"{lines[i]}\" is not in the plugin's template (it has {Template.Length} lines)");
                if (i >= n) throw new FormatException($"the SDP ends before the template's line \"{Template[i]}\"");
                string line = lines[i], t = Template[i];
                int open = t.IndexOf('{');
                if (open < 0) { if (line != t) throw Unknown(line, i); continue; }
                int close = t.IndexOf('}');
                string head = t[..open], tail = t[(close + 1)..];
                if (line.StartsWith("a=fingerprint:", StringComparison.Ordinal) && !line.StartsWith(head, StringComparison.Ordinal))
                    throw new FormatException($"the SDP line \"{line}\" is not a sha-256 fingerprint, the only kind a code carries");
                if (line.Length < head.Length + tail.Length || !line.StartsWith(head, StringComparison.Ordinal) || !line.EndsWith(tail, StringComparison.Ordinal))
                    throw Unknown(line, i);
                string v = line[head.Length..^tail.Length];
                switch (t[(open + 1)..close])
                {
                    case "o": if (v.Length == 0 || !v.All(char.IsAsciiDigit)) throw Unknown(line, i); break;
                    case "fingerprint": r.Fingerprint = Hex(line, v); break;
                    case "setup": r.Setup = Array.IndexOf(Setups, v); if (r.Setup < 0) throw Unknown(line, i); break;
                    case "ufrag": CheckCred(v); r.Ufrag = v; break;
                    case "pwd": CheckCred(v); r.Pwd = v; break;
                }
            }
            r.Candidates = (candidates ?? Enumerable.Empty<string>()).Select(Parse).ToList();
            return r;
        }
        private static FormatException Unknown(string line, int i) =>
            new($"the SDP line \"{line}\" is not in the plugin's template (line {i + 1} is \"{Template[i]}\")");
        private static byte[] Hex(string line, string v)
        {
            var parts = v.Split(':');
            if (parts.Length != 32 || parts.Any(p => p.Length != 2 || !p.All(char.IsAsciiHexDigit)))
                throw new FormatException($"the SDP line \"{line}\" does not hold the 32 bytes of a sha-256");
            return parts.Select(p => byte.Parse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        }

        // RECEIVING: the template with the carried values, a fresh o= id, a=setup from the flags.
        public static string Build(Record r)
        {
            if (r.Setup is < 0 or > 2) throw new FormatException($"a=setup {r.Setup}");
            string o = Random.Shared.NextInt64(1, 1L << 32).ToString(CultureInfo.InvariantCulture);
            string fp = string.Join(":", r.Fingerprint.Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
            var sb = new StringBuilder();
            foreach (var t in Template)
                sb.Append(t.Replace("{o}", o).Replace("{fingerprint}", fp).Replace("{setup}", Setups[r.Setup])
                           .Replace("{ufrag}", r.Ufrag).Replace("{pwd}", r.Pwd)).Append(Eol);
            return sb.ToString();
        }

        // A CANDIDATE LINE exactly as libjuice writes it (SPIKE): the related address is always hidden,
        // "raddr 0.0.0.0 rport 0" for IPv4. What libjuice writes for an IPv6 one is unread (the spike's
        // network has none): "::" is RFC 8839's, and ICE ignores the related address either way.
        public static string Line(Candidate c) => string.Create(CultureInfo.InvariantCulture,
            $"candidate:{c.Foundation} 1 UDP {c.Priority} {c.Address} {c.Port} typ {TypeName(c.Type)}")
            + (c.Type == Via.Host ? "" : c.Address.AddressFamily == AddressFamily.InterNetworkV6 ? " raddr :: rport 0" : " raddr 0.0.0.0 rport 0");

        // One of the plugin's candidate lines, as a code carries it. Refused, by name: anything but
        // component 1 over UDP, a foundation that is not a number or is over 255, a field a code drops.
        public static Candidate Parse(string line)
        {
            var p = (line ?? "").Split(' ');
            FormatException Bad(string why) => new($"the candidate \"{line}\" {why}");
            if (p.Length < 8 || !p[0].StartsWith("candidate:", StringComparison.Ordinal) || p[1] != "1"
                || !p[2].Equals("UDP", StringComparison.OrdinalIgnoreCase) || p[6] != "typ")
                throw Bad("is not a component-1 UDP candidate as libjuice writes one");
            string f = p[0]["candidate:".Length..];
            if (f.Length == 0 || !f.All(char.IsAsciiDigit)) throw Bad("has a foundation that is not a number");
            if (f.Length > 3 || int.Parse(f, CultureInfo.InvariantCulture) > 255) throw Bad("has a foundation over 255, which a code cannot carry");
            if (!uint.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint priority)) throw Bad("has no priority");
            if (!IPAddress.TryParse(p[4], out var address)
                || address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)) throw Bad("has no IP address");
            if (!int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535) throw Bad("has no port");
            var type = Enum.GetValues<Via>().Cast<Via?>().FirstOrDefault(v => TypeName(v.Value) == p[7]) ?? throw Bad("has a type a code does not know");
            bool tail = type == Via.Host ? p.Length == 8 : p.Length == 12 && p[8] == "raddr" && p[10] == "rport";
            if (!tail) throw Bad("carries fields a code does not");
            return new Candidate(type, int.Parse(f, CultureInfo.InvariantCulture), priority, port, address);
        }
        private static string TypeName(Via v) => v switch
        {
            Via.Host => "host", Via.Srflx => "srflx", Via.Prflx => "prflx", Via.Relay => "relay",
            _ => throw new FormatException($"a candidate type {(int)v}"),
        };
    }

    // ── the text (§4.3) ──────────────────────────────────────────────────────
    // CROCKFORD BASE32: no character Discord eats (_ *), none a double-click stops at (- + /), one case,
    // and I, L and O read as 1, 1 and 0, so a hand-typed or re-cased code still decodes.
    public const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int PasteBudget = 400;            // characters: a code and a sentence in one Discord message
    private static string Prefix(Kind k) => k switch
    {
        Kind.Invite => "WSI", Kind.Reply => "WSR",
        _ => throw new FormatException($"a {k} is never pasted"),
    };
    public static string Encode(Record r)
    {
        var s = new StringBuilder(Prefix(r.Kind));
        int acc = 0, bits = 0;
        foreach (byte x in Pack(r))
        {
            acc = acc << 8 | x; bits += 8;
            while (bits >= 5) { bits -= 5; s.Append(Crockford[(acc >> bits) & 31]); }
            acc &= (1 << bits) - 1;
        }
        if (bits > 0) s.Append(Crockford[(acc << (5 - bits)) & 31]);
        return s.ToString();
    }
    private static int Digit(char c) => char.ToUpperInvariant(c) switch
    {
        'I' or 'L' => 1, 'O' => 0, char u => Crockford.IndexOf(u),
    };

    // THE FORGIVING READER (§4.3). Looks for WSI or WSR anywhere in the text, in any case, and tries each
    // place in turn. From there it takes code characters, skipping spaces, line breaks and hyphens, only
    // as many as the record needs, and the check decides. So a whole copied Discord message still reads:
    // the name and time before it, words after it, a "wsr" inside a name before the code. null when no
    // place holds a whole, undamaged code (of kind `want`, when given).
    public static Record Find(string text, Kind? want = null)
    {
        text ??= "";
        for (int i = 0; i + 3 <= text.Length; i++)
        {
            Kind k;
            if (string.Compare(text, i, "WSI", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) k = Kind.Invite;
            else if (string.Compare(text, i, "WSR", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) k = Kind.Reply;
            else continue;
            if (want != null && want != k) continue;
            try { var r = Read(Base32From(text, i + 3)); if (r.Kind == k) return r; }
            catch (FormatException) { }
        }
        return null;
    }
    private static Func<byte> Base32From(string text, int at)
    {
        int acc = 0, bits = 0;
        return () =>
        {
            while (bits < 8)
            {
                if (at >= text.Length) throw new FormatException("the code is cut off");
                char c = text[at++];
                if (c is ' ' or '\r' or '\n' or '-') continue;
                int d = Digit(c);
                if (d < 0) throw new FormatException($"'{c}' is not a code character");
                acc = acc << 5 | d; bits += 5;
            }
            bits -= 8;
            byte x = (byte)(acc >> bits);
            acc &= (1 << bits) - 1;
            return x;
        };
    }
    // TEXT THAT IS A CODE, whole or not: a prefix and more code characters than any host name's label
    // can hold (63). A damaged code is still a code: never an address to look up.
    public static bool HoldsCode(string text)
    {
        text ??= "";
        for (int i = 0; i + 3 <= text.Length; i++)
        {
            if (string.Compare(text, i, "WSI", 0, 3, StringComparison.OrdinalIgnoreCase) != 0
                && string.Compare(text, i, "WSR", 0, 3, StringComparison.OrdinalIgnoreCase) != 0) continue;
            int run = 0;
            for (int j = i + 3; j < text.Length && run < 64; j++)
            {
                if (text[j] is ' ' or '\r' or '\n' or '-') continue;
                if (Digit(text[j]) < 0) break;
                run++;
            }
            if (run >= 64) return true;
        }
        return false;
    }

    // ── the fit rule (§4.3) ──────────────────────────────────────────────────
    // First, a global IPv6 address on a /64 the code already carries is dropped, fit or not: a temporary
    // and a stable address on one /64 are one path. Then, while the code is over its row's budget, the
    // lowest-ranked candidate goes: the rest first, then global IPv6, then this PC's LAN address, then an
    // overlay's (Radmin, Tailscale, ZeroTier, Hamachi), server-reflexive last; within a rank, the later
    // one, since the plugin lists its best first. Returns how many were dropped, for the report.
    public static int Fit(Record r, IRendezvousPath row, ICollection<IPAddress> overlays, IPAddress lan)
    {
        int before = r.Candidates.Count;
        var kept = new List<Candidate>();
        foreach (var c in r.Candidates)
            if (!(Global6(c.Address) && kept.Any(k => Global6(k.Address) && k.Address.GetAddressBytes()[..8].SequenceEqual(c.Address.GetAddressBytes()[..8]))))
                kept.Add(c);
        r.Candidates = kept;
        int Rank(Candidate c) => c.Type != Via.Host ? 4 : overlays.Contains(c.Address) ? 3 : c.Address.Equals(lan) ? 2 : Global6(c.Address) ? 1 : 0;
        while (r.Candidates.Count > 0 && row.Measure(r) > row.Budget)
        {
            int worst = 0;
            for (int i = 1; i < r.Candidates.Count; i++)
                if (Rank(r.Candidates[i]) <= Rank(r.Candidates[worst])) worst = i;
            r.Candidates.RemoveAt(worst);
        }
        return before - r.Candidates.Count;
    }
    private static bool Global6(IPAddress a) => a.AddressFamily == AddressFamily.InterNetworkV6 && (a.GetAddressBytes()[0] & 0xE0) == 0x20;

    // ── the rows (§3.3, §7) ──────────────────────────────────────────────────
    // A WAY IN. The JOIN box asks each row in turn whether it claims the text; the first that does
    // carries the join. A new way to carry codes is a new row.
    public interface IRendezvousPath
    {
        string Id { get; }                  // the row's name, for the report
        bool Claims(string text);           // is this text for this row?
        bool Auto { get; }                  // can it signal again by itself after a drop: retries and RECONNECT (§3.9)
        bool Stun { get; }                  // does it walk the STUN rows when it gathers (§5.2)
        int Budget { get; }                 // what one code may take on this row, in Measure's unit
        int Measure(Record r);              // what this record takes on this row
    }
    // BY INVITE: a code the players paste to each other (Discord, a text message). The host makes the
    // first code, so it is the one row that crosses the internet, and the one that walks the STUN rows.
    private sealed class PasteRow : IRendezvousPath
    {
        public string Id => "paste";
        public bool Claims(string text) => Find(text) != null;
        public bool Auto => false;
        public bool Stun => true;
        public int Budget => PasteBudget;
        public int Measure(Record r) => Encode(r).Length;
    }
    // BY TYPED ADDRESS: a LAN, Radmin VPN or Tailscale. The same invite and reply travel over a TCP
    // connection to the host's listener, with no pasting; host candidates are the path, so no STUN.
    private sealed class AddressRow : IRendezvousPath
    {
        public string Id => "address";
        public bool Claims(string text) => !HoldsCode(text) && Net.ParseAddress(text) != null;
        public bool Auto => true;
        public bool Stun => false;
        public int Budget => WireMax;
        public int Measure(Record r) => Pack(r).Length;
    }
    public const int WireMax = 8192;                // bytes: the listener's read cap for one record
    public static readonly IRendezvousPath Paste = new PasteRow(), Address = new AddressRow();
    public static readonly IRendezvousPath[] Paths = { Paste, Address };
    public static IRendezvousPath PathFor(string text) => Paths.FirstOrDefault(p => p.Claims(text));

    // THE CLIPBOARD, AS A SEAM: what the game reads when it looks for a copied code (§15 Q1: the host's
    // game takes a friend's reply off the clipboard). Never readonly, so it stays out of the build's
    // fingerprint, and the harness swaps it: a headless clipboard is never relied on.
    public static Func<string> Clipboard = DisplayServer.ClipboardGet;
}
