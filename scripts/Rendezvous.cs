using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
//   * the ROWS (Paths), the ways in: a pasted code, or a typed address. Each claims the text it can carry;
//     the host OPENS each row on its desk, the guest STARTS a join on one;
//   * the address row's LISTENER (the host's TCP port) and DIALER (the guest's knock), and the paste
//     row's CLIPBOARD PICKUP (the host's game taking a friend's reply off the clipboard);
//   * the PENDING table, one entry per invite not yet connected, and the two DESKS a session implements
//     so the rows can reach it (IHostDesk, IGuestDesk: Net, and the harness's own desk).
// It replaced the public-address reveal and copy: a friend is sent an invite, not an address.
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
        void Open(IHostDesk desk);          // the host takes joins by this row, on its desk
        void Close();                       // ...and stops: a join in flight is refused `closed`
        Task<Record> Start(string text, IGuestDesk desk);   // the guest joins by this text: the host's record back
        void Poll();                        // once a frame on the main thread: what the row watches by itself
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
        // The friend pastes the invite; the desk answers it (the reply the friend then sends back).
        public async Task<Record> Start(string text, IGuestDesk desk)
        {
            var invite = Find(text, Kind.Invite) ?? throw new FormatException("the text holds no whole invite code");
            await desk.Answer(invite);
            return invite;
        }
        // THE CLIPBOARD PICKUP (§3.4, §15 Q1): while an invite is pending, the clipboard is read at most
        // every PickupMs, and a reply for an entry still Waiting is handed to the desk, once per distinct
        // text -- so the host's part is to copy the friend's message, in combat, a menu, or alt-tabbed.
        // Anything else on the clipboard (the host's own invite, a stale reply, words) is left alone.
        private IHostDesk _desk;
        private ulong _nextAt;
        private string _seen;
        public void Open(IHostDesk desk) { _desk = desk; _nextAt = 0; _seen = null; }
        public void Close() => _desk = null;
        public void Poll()
        {
            if (_desk == null || _desk.Pending.Count == 0) return;
            ulong now = Time.GetTicksMsec();
            if (now < _nextAt) return;
            _nextAt = now + PickupMs;
            string text = Clipboard() ?? "";
            if (text == _seen) return;
            _seen = text;
            if (Find(text, Kind.Reply) is { } reply && _desk.Pending.Find(reply.Id) is { Stage: Stage.Waiting })
                _desk.Replied(reply);
        }
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
        public void Open(IHostDesk desk) { Close(); _listening = new Listener(desk); }
        public void Close() { _listening?.Close(); _listening = null; }
        public Task<Record> Start(string text, IGuestDesk desk) => Dial(text, desk);
        public void Poll() { }              // the listener hands each knock to the main thread itself
    }
    public const int WireMax = 8192;                // bytes: the listener's read cap for one record
    public static readonly IRendezvousPath Paste = new PasteRow(), Address = new AddressRow();
    public static readonly IRendezvousPath[] Paths = { Paste, Address };
    public static IRendezvousPath PathFor(string text) => Paths.FirstOrDefault(p => p.Claims(text));
    public const int PickupMs = 500;                // the clipboard pickup reads at most twice a second

    // ── the guest's mark (§3.3 B) ────────────────────────────────────────────
    // A 32-bit value drawn once per game process, never 0: a knock carries it, so the listener tells a
    // guest's newer knock from a new guest's. A property over a mutable field, NEVER a readonly static:
    // Net.Fingerprint folds those into Net.Protocol, and a value drawn per process there would make every
    // process a different build (Game.Build's trap).
    public static uint Guest => _guest ??= (uint)System.Random.Shared.NextInt64(1, 1L << 32);
    private static uint? _guest;

    // ── the pending table (§3.1) ─────────────────────────────────────────────
    // ONE ENTRY PER INVITE NOT YET CONNECTED, whichever row carried it. Waiting: the invite is out and no
    // reply has been taken. Linking: a reply was taken and the connection has Link.LinkMs to come up.
    // Net holds one; a connected entry leaves it, its connection now the session's.
    public enum Stage { Waiting, Linking }
    public sealed class Entry
    {
        public int Id;                              // the invite's id, which is also the friend's peer id
        public string Row = "";                     // the row that carried the invite, by its Id
        public uint Guest;                          // the knock's mark (the address row); 0 on the paste row
        public Stage Stage;
        public Link.Gather Conn;                    // the connection being made for it (none on the harness's own desk)
        public string Name = "";                    // who it is for: the knock's or the reply's name, a dropped pilot's; "" until known
        public ulong Made;                          // when it was made, in ms since start (an unanswered invite runs out: Link.InviteLifeS)
        public ulong Until;                         // Linking: when the connection has had its Link.LinkMs
        public string Code = "";                    // the paste row's invite as text: what COPY puts on the clipboard
    }
    public sealed class Pending
    {
        private readonly List<Entry> _all = new();
        public int Count => _all.Count;
        public IReadOnlyList<Entry> All => _all;           // in the order they were made
        public void Add(Entry e) { Remove(e.Id); _all.Add(e); }
        public bool Remove(int id) => _all.RemoveAll(e => e.Id == id) > 0;
        public Entry Find(int id) => _all.Find(e => e.Id == id);
        // the entries a knock with this mark made, as a copy (0 is no mark: none)
        public List<Entry> OfGuest(uint guest) => guest == 0 ? new() : _all.FindAll(e => e.Guest == guest);
    }

    // ── the desks: what a row asks of the session. Every call is on the main thread ──
    public interface IHostDesk
    {
        Pending Pending { get; }
        bool Full { get; }                          // no room for another friend: a knock is refused `full`
        Task<Record> Invite(Record knock);          // an invite for this knock, gathered with no STUN row, its entry added Waiting
        void Hang(int id);                          // this pending entry is over: hung up (Link.Hang) and gone
        void Replied(Record reply);                 // a reply for a pending invite: take it (§3.3 A6)
        void Refused(Record knock, Why why);        // a knock was refused: the host's line naming who knocked
    }
    public interface IGuestDesk
    {
        Record Knock();                             // this player's knock: the build, the name, Guest
        Task<Record> Answer(Record invite);         // the reply to an invite (§3.3 A guest 2-4)
    }

    // ── the address row: the listener and the dialer (§3.3 B) ────────────────
    public const int ReadMs = 3000;                 // every read and write on a listener connection, at either end
    public const int KnocksPerMinute = 20;          // from one address; the next within the minute is refused `rate`
    // The listener's ports: `from`, the nine after it, then one the OS picks. From 0: the OS's alone.
    public static int[] ListenPorts(int from) => from == 0 ? new[] { 0 } : Enumerable.Range(from, 10).Append(0).ToArray();
    // Where the listener starts. The harness sets 0, so its listener never takes a port a real session or
    // another run would. Not readonly: which port this PC gets is not part of what two builds agree on.
    public static int ListenFrom = Net.DefaultPort;
    // The port the listener serves, 0 while it is closed (the host panel's "Same network: {lan}:{port}").
    public static int ListenPort => _listening?.Port ?? 0;
    private static Listener _listening;

    // THE LISTENER: a dual-stack TCP port (IPv4 and IPv6 in one socket, v1 §4.3). Its threads only move
    // bytes: each knock goes to the main thread for the checks and the desk's invite, the answer comes
    // back to the connection by a TaskCompletionSource, and a reply read on it goes to the main thread
    // for the desk. AN ADDRESS-ROW INVITE LIVES ON ITS CONNECTION: nobody else holds it, so a connection
    // that ends without its reply hangs its entry up.
    private sealed class Listener
    {
        private readonly IHostDesk _desk;
        private readonly TcpListener _tcp;
        public readonly int Port;
        private volatile bool _closed;
        private readonly List<TaskCompletionSource<Record>> _inFlight = new();   // knocks on the main thread, not yet answered
        private readonly Dictionary<IPAddress, Queue<ulong>> _knocks = new();     // the main thread's: each address's last minute

        public Listener(IHostDesk desk)
        {
            _desk = desk;
            foreach (int port in ListenPorts(ListenFrom))
            {
                try { _tcp = Bound(port); }
                catch (SocketException) when (port != 0) { continue; }     // taken, or reserved by the OS
                Port = ((IPEndPoint)_tcp.LocalEndpoint).Port;
                break;
            }
            _ = Task.Run(Accept);
        }
        private static TcpListener Bound(int port)
        {
            var l = Socket.OSSupportsIPv6 ? new TcpListener(IPAddress.IPv6Any, port) : new TcpListener(IPAddress.Any, port);
            if (Socket.OSSupportsIPv6) l.Server.DualMode = true;
            try { l.Start(); } catch (SocketException) { l.Stop(); throw; }
            return l;
        }
        private async Task Accept()
        {
            while (!_closed)
            {
                TcpClient client;
                try { client = await _tcp.AcceptTcpClientAsync(); }
                catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException) { return; }
                _ = Task.Run(() => Serve(client));
            }
        }
        private async Task Serve(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                Record knock;
                IPAddress from;
                try
                {
                    knock = await ReadRecord(stream);
                    from = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                }
                catch (Exception e) when (Wire(e)) { return; }
                if (knock.Kind != Kind.Knock) return;
                if (from.IsIPv4MappedToIPv6) from = from.MapToIPv4();
                var answer = new TaskCompletionSource<Record>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_inFlight) _inFlight.Add(answer);
                OnMain(() => _ = Knocked(knock, from, answer));
                await Task.WhenAny(answer.Task, Task.Delay(ReadMs));
                answer.TrySetResult(null);                  // too late: an invite the main thread makes after this is hung up there
                lock (_inFlight) _inFlight.Remove(answer);
                var said = answer.Task.Result;
                if (said == null) return;
                bool taken = false;
                try
                {
                    await WriteRecord(stream, said);
                    if (said.Kind == Kind.Invite && await ReadRecord(stream) is { Kind: Kind.Reply } reply && reply.Id == said.Id)
                    {
                        taken = true;
                        OnMain(() => _desk.Replied(reply));
                    }
                }
                catch (Exception e) when (Wire(e)) { }
                if (said.Kind == Kind.Invite && !taken) OnMain(() => _desk.Hang(said.Id));
            }
        }
        // On the main thread: the checks, then the connection's answer. An invite whose connection has
        // already been answered (the listener closed, or its 3 s ran out) is hung up.
        private async Task Knocked(Record knock, IPAddress from, TaskCompletionSource<Record> answer)
        {
            Record said;
            try { said = await Weigh(knock, from); }
            catch (Exception e) { GD.PushError($"the listener's desk threw on a knock: {e}"); said = null; }
            if (!answer.TrySetResult(said) && said is { Kind: Kind.Invite }) _desk.Hang(said.Id);
        }
        // THE CHECKS, IN ORDER (§3.3 B host 2): the build, the rate, one pending entry per guest (a newer
        // knock supersedes the older entry, which is hung up), the room; then the desk's invite. Every
        // knock counts toward its address's rate, refused or not.
        private async Task<Record> Weigh(Record knock, IPAddress from)
        {
            if (_closed) return Refusal(Why.Closed);
            if (!_knocks.TryGetValue(from, out var seen)) _knocks[from] = seen = new Queue<ulong>();
            ulong now = Time.GetTicksMsec();
            while (seen.Count > 0 && seen.Peek() + 60000 <= now) seen.Dequeue();
            seen.Enqueue(now);
            Why? why = knock.Proto != Net.Protocol ? Why.Build : seen.Count > KnocksPerMinute ? Why.Rate : null;
            if (why == null)
            {
                foreach (var old in _desk.Pending.OfGuest(knock.Guest)) _desk.Hang(old.Id);
                if (_desk.Full) why = Why.Full;
            }
            if (why is { } w) { _desk.Refused(knock, w); return Refusal(w); }
            return await _desk.Invite(knock);
        }
        // On the main thread. A knock still waiting on the main thread is refused `closed`.
        public void Close()
        {
            _closed = true;
            _tcp.Stop();
            lock (_inFlight) foreach (var a in _inFlight) a.TrySetResult(Refusal(Why.Closed));
        }
    }
    // A refusal from this host: its build, so the guest's text can name both.
    private static Record Refusal(Why why) => new() { Kind = Kind.Refuse, Why = why, Proto = Net.Protocol, Build = Game.Build };
    // Work for the main thread, where the desks live; none once the game is shutting down.
    private static void OnMain(Action act)
    {
        if (!Game.ShuttingDown) Callable.From(act).CallDeferred();
    }

    // THE DIALER (§3.3 B guest 1-3): a knock to the host's listener; an invite is answered on the same
    // connection, a refusal handed back as it is. The name is looked up as JOIN always has (DialAddress:
    // IPv4, else IPv6, never link-local). Returns the host's record.
    private static async Task<Record> Dial(string text, IGuestDesk desk)
    {
        if (Net.ParseAddress(text) is not var (host, port)) throw new FormatException($"\"{text}\" is not an address");
        string ip = IPAddress.TryParse(host, out _) ? host : Net.DialAddress(await Dns.GetHostAddressesAsync(host));
        if (ip == "") throw new SocketException((int)SocketError.HostNotFound);
        var at = IPAddress.Parse(ip);
        using var tcp = new TcpClient(at.AddressFamily);
        await tcp.ConnectAsync(at, port);
        var stream = tcp.GetStream();
        await WriteRecord(stream, desk.Knock());
        var got = await ReadRecord(stream);
        if (got.Kind == Kind.Invite) await WriteRecord(stream, await desk.Answer(got));
        else if (got.Kind != Kind.Refuse) throw new FormatException($"the host answered a knock with a {got.Kind}");
        return got;
    }

    // A RECORD ON A CONNECTION: its length in 2 bytes, big-endian, then Pack's bytes; at most WireMax,
    // and ReadMs to arrive or leave.
    private static async Task WriteRecord(Stream s, Record r)
    {
        var body = Pack(r);
        if (body.Length > WireMax) throw new FormatException($"a record of {body.Length} bytes, over the {WireMax} a connection carries");
        var framed = new byte[2 + body.Length];
        framed[0] = (byte)(body.Length >> 8); framed[1] = (byte)body.Length;
        body.CopyTo(framed, 2);
        using var cut = new CancellationTokenSource(ReadMs);
        await s.WriteAsync(framed, cut.Token);
    }
    private static async Task<Record> ReadRecord(Stream s)
    {
        using var cut = new CancellationTokenSource(ReadMs);
        var head = new byte[2];
        await s.ReadExactlyAsync(head, cut.Token);
        int n = head[0] << 8 | head[1];
        if (n == 0 || n > WireMax) throw new FormatException($"a record of {n} bytes, over the {WireMax} a connection carries");
        var body = new byte[n];
        await s.ReadExactlyAsync(body, cut.Token);
        return Unpack(body);
    }
    // What a connection's end throws: a timeout, a closed or reset socket, a damaged record.
    private static bool Wire(Exception e) => e is IOException or SocketException or OperationCanceledException or FormatException or ObjectDisposedException;

    // THE CLIPBOARD, AS A SEAM: what the game reads when it looks for a copied code (§15 Q1: the host's
    // game takes a friend's reply off the clipboard). Never readonly, so it stays out of the build's
    // fingerprint, and the harness swaps it: a headless clipboard is never relied on.
    // Only where the display server has one: a headless run's clipboard read is an engine ERROR.
    public static Func<string> Clipboard = () => DisplayServer.HasFeature(DisplayServer.Feature.Clipboard) ? DisplayServer.ClipboardGet() : "";
    // ...and what the game writes there (an invite, a reply), swapped the same way.
    public static Action<string> Copy = text => { if (DisplayServer.HasFeature(DisplayServer.Feature.Clipboard)) DisplayServer.ClipboardSet(text); };
}
