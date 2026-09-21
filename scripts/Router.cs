using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// ─────────────────────────────────────────────────────────────────────────────
// THE ROUTER, and the network around this PC: everything hosting needs to know so that friends
// anywhere can reach it, whatever sits in between.
//
// Open() tries, in order, until one of them opens the port:
//   1. UPnP, asked directly (plain .NET): the protocol nearly every home router speaks.
//   2. NAT-PMP, then PCP, at the network's gateway: Apple's routers, and many that have no UPnP.
//   3. Godot's own UPnP -- a second implementation, for the router the first somehow missed.
// Then, if the router that opened it is itself behind another router (its internet side is a
// private address -- your own router behind the provider's modem), it asks THAT one too, by
// UPnP and by NAT-PMP/PCP, to forward to our router. If that one is silent, the report says
// exactly which address has to be forwarded there, by hand, once.
//
// Why Godot's Upnp is no longer the main path, each found on a real network: it REFUSES a router
// whose internet side is private, so it reported "no router answered" about one that answered
// perfectly well; it cannot ask the router in front; and it is an engine object used from a
// background thread -- one still running when the engine shut down crashed the process. Only the
// last resort touches the engine; everything else is safe on any thread.
//
// Also here: which adapter is the real LAN (not WSL's, Hyper-V's or a VPN's), the virtual
// networks friends can share (Tailscale, ZeroTier, Radmin VPN, Hamachi), and this PC's IPv6
// address -- the way in for a friend when carrier-grade NAT closes every IPv4 door.
// ─────────────────────────────────────────────────────────────────────────────
public static class Router
{
    // ── the test's network ───────────────────────────────────────────────────
    // When set, nothing here touches the real network: UPnP searches and NAT-PMP/PCP go to
    // loopback on these ports (the smoke test's fake routers, or nobody), Godot's UPnP is skipped
    // (it can only search the real network), and this PC reports no virtual network and no IPv6.
    public static (int ssdp, int pmp)? Fake;

    // ── one router that opened the port, and how to close it again ──────────────────────────────
    public sealed class Hop
    {
        public string Via, At;                                  // "UPnP" / "NAT-PMP" / "PCP" / "UPnP (Godot)", and where
        public int ExtPort;                                     // the port it opened (NAT-PMP/PCP may choose another)
        internal Action Close;
        public Hop(string via, string at, int extPort, Action close) { Via = via; At = at; ExtPort = extPort; Close = close; }
    }

    public sealed class Report
    {
        public readonly List<Hop> Opened = new();
        public string Ext = "";      // the outermost internet-side address a router we opened told us
        public string Why = "";      // when nothing opened: what went wrong
        public string Front = "";    // opened, but behind a silent router: our router's internet side, to forward to
        public int ExtPort;          // the port friends dial
    }

    public static Report Open(int port, Nic lan)
    {
        var r = new Report { ExtPort = port };
        var upnp = Find(lan.Ip).FirstOrDefault();
        string refused = "";
        if (upnp != null)
        {
            // Its internet side first, whether or not it then opens the port: a router that refuses
            // still says whether it is behind another one, or behind carrier-grade NAT -- which
            // decides what the player is told to do by hand.
            r.Ext = ExternalAddress(upnp);
            refused = Map(upnp, port, lan.Ip);
            if (refused.Length == 0) r.Opened.Add(UpnpHop(upnp, port));
        }
        if (r.Opened.Count == 0 && (Fake != null || lan.Gateway.Length > 0))
        {
            var h = PortProtocol(Fake != null ? "127.0.0.1" : lan.Gateway, lan.Ip, port, out var ext);
            if (h != null) { r.Opened.Add(h); r.Ext = ext; r.ExtPort = h.ExtPort; }
        }
        if (r.Opened.Count == 0 && Fake == null)
        {
            var h = GodotUpnp(lan.Ip, port, out var ext);
            if (h != null) { r.Opened.Add(h); r.Ext = ext; }
        }
        if (r.Opened.Count == 0)
        {
            r.Why = upnp != null ? $"the router refused ({refused})" : "no router answered";
            return r;
        }
        // BEHIND A SECOND ROUTER. Ours says its internet side is a private address, so the port
        // is open only as far as the router in front -- typically the provider's modem. Ask that
        // one directly (a network-wide search does not cross a router), to forward to OUR router's
        // internet side. Carrier-grade NAT is excluded: the router in front of that belongs to the
        // provider, and it is nobody's to ask.
        if (IsShared(r.Ext) && !IsCarrierGrade(r.Ext))
        {
            string inner = r.Ext;
            foreach (var at in FrontCandidates(inner))
            {
                var f = Find(inner, at, passes: 2).FirstOrDefault();
                if (f != null && Map(f, r.ExtPort, inner).Length == 0)
                {
                    r.Opened.Add(UpnpHop(f, r.ExtPort)); r.Ext = ExternalAddress(f);
                    break;
                }
                var h = PortProtocol(at, inner, r.ExtPort, out var ext);
                if (h != null) { r.Opened.Add(h); r.Ext = ext; r.ExtPort = h.ExtPort; break; }
            }
            if (r.Ext == inner) r.Front = inner;
        }
        return r;
    }

    public static void Close(IEnumerable<Hop> hops)
    {
        foreach (var h in hops.Reverse())
            try { h.Close(); } catch (Exception) { /* a router that is gone cannot be asked */ }
    }

    // The addresses the router in front of ours most likely has, given our router's internet
    // side: the first and last host of its /24 -- what nearly every home router hands out.
    public static IEnumerable<string> FrontCandidates(string routerExt)
    {
        var p = routerExt.Split('.');
        if (p.Length != 4) yield break;
        foreach (var last in new[] { "1", "254" })
            if (p[3] != last) yield return $"{p[0]}.{p[1]}.{p[2]}.{last}";
    }

    // ── addresses ────────────────────────────────────────────────────────────
    public static bool IsIpv4(string s)
    {
        var p = s.Split('.');
        return p.Length == 4 && p.All(x => int.TryParse(x, out int v) && v >= 0 && v <= 255);
    }
    private static (int a, int b)? Head(string ip)
    {
        var p = ip.Split('.');
        return p.Length == 4 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b) ? (a, b) : null;
    }
    // 100.64.0.0/10: carrier-grade NAT on a router's internet side; Tailscale's addresses on a PC.
    public static bool IsCarrierGrade(string ip) => Head(ip) is (int a, int b) && a == 100 && b >= 64 && b <= 127;
    // Private, carrier-grade and loopback addresses: none can be reached from the internet.
    public static bool IsShared(string ip) => Head(ip) is (int a, int b)
        && (a == 10 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168) || IsCarrierGrade(ip) || a == 127);

    // ── this PC's adapters ───────────────────────────────────────────────────
    public sealed record Nic(string Ip, string Gateway);

    // THE REAL LAN, not the first private address the OS lists. A PC with WSL, Hyper-V, VirtualBox
    // or a VPN has several, and the wrong one sends the router search where no router listens, and
    // tells the player to forward the port to an address their router has never heard of. The one
    // that leads to the internet is up, has a gateway, and is not a virtual adapter.
    public static Nic Lan()
    {
        Nic best = null; int rank = -1;
        foreach (var n in Adapters())
        {
            var p = n.GetIPProperties();
            var ip = p.UnicastAddresses.Select(a => a.Address)
                      .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."));
            if (ip == null) continue;
            var gw = p.GatewayAddresses.Select(g => g.Address)
                      .FirstOrDefault(g => g.AddressFamily == AddressFamily.InterNetwork && !g.Equals(IPAddress.Any));
            int r = (gw != null ? 2 : 0) + (IsVirtual(n) ? 0 : 1);
            if (r > rank) { rank = r; best = new Nic(ip.ToString(), gw?.ToString() ?? ""); }
        }
        return best ?? new Nic("127.0.0.1", "");
    }

    // Virtual networks friends can join from anywhere, routers or no routers: on one of these,
    // a friend on the same network joins this PC's address on it.
    private static readonly (string match, string name)[] OverlayKinds =
        { ("Tailscale", "Tailscale"), ("ZeroTier", "ZeroTier"), ("Radmin", "Radmin VPN"), ("Hamachi", "Hamachi") };
    public static List<(string name, string ip)> Overlays()
    {
        var found = new List<(string, string)>();
        if (Fake != null) return found;
        foreach (var n in Adapters(tunnels: true))
        {
            string what = n.Description + " " + n.Name;
            var kind = OverlayKinds.FirstOrDefault(k => what.Contains(k.match, StringComparison.OrdinalIgnoreCase));
            var ip = n.GetIPProperties().UnicastAddresses.Select(a => a.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ip == null) continue;
            // By NAME only: an address in Tailscale's range is not proof -- a PC tethered to a phone,
            // or on a modem in pass-through, has one too, and would send friends to a network
            // that does not exist.
            if (kind.name != null) found.Add((kind.name, ip.ToString()));
        }
        return found;
    }

    // A global IPv6 address (2000::/3), preferring a stable one to a temporary privacy address.
    // With IPv6 there is no NAT: behind carrier-grade NAT -- no IPv4 way in at all -- this is the
    // one address a friend who also has IPv6 can still reach, if the router's firewall lets them.
    public static string GlobalIpv6()
    {
        if (Fake != null) return "";
        string any = "";
        foreach (var n in Adapters())
            foreach (var a in n.GetIPProperties().UnicastAddresses)
            {
                if (a.Address.AddressFamily != AddressFamily.InterNetworkV6 || (a.Address.GetAddressBytes()[0] & 0xE0) != 0x20) continue;
                // Only Windows says which addresses are temporary; elsewhere the first one serves.
                bool temporary = OperatingSystem.IsWindows() && a.SuffixOrigin == SuffixOrigin.Random && a.AddressPreferredLifetime < 86400 * 2;
                if (!temporary) return a.Address.ToString();
                if (any.Length == 0) any = a.Address.ToString();
            }
        return any;
    }

    private static IEnumerable<NetworkInterface> Adapters(bool tunnels = false)
    {
        NetworkInterface[] all;
        try { all = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { yield break; }
        foreach (var n in all)
            if (n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && (tunnels || n.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
                yield return n;
    }
    private static bool IsVirtual(NetworkInterface n) =>
        Regex.IsMatch(n.Description + " " + n.Name, "Hyper-V|vEthernet|VirtualBox|VMware|WSL|Virtual|TAP-|Tunnel|WireGuard|ZeroTier|Tailscale|Radmin|Hamachi|VPN",
                      RegexOptions.IgnoreCase);

    // ── UPnP ─────────────────────────────────────────────────────────────────
    // A router's port-mapping service: where to send the SOAP calls, and which service it is.
    public sealed record Gateway(string Control, string Service);

    private static readonly string[] Targets =
    {
        "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
        "urn:schemas-upnp-org:service:WANIPConnection:1", "urn:schemas-upnp-org:service:WANIPConnection:2",
        "urn:schemas-upnp-org:service:WANPPPConnection:1",
    };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    // Every router that answers a search. The network-wide search leaves from the LAN adapter
    // (the default route may be a virtual adapter the router never hears); `at` sends it to one
    // address instead -- how the router in front of ours is asked. Passes: a search is one
    // unacknowledged datagram, and one lost frame on Wi-Fi used to mean "no UPnP" for the whole
    // session. Each pass waits just over a second, the longest the search asks devices to wait.
    private static List<Gateway> Find(string lanIp, string at = null, int passes = 3)
    {
        var found = new List<Gateway>();
        var tried = new HashSet<string>();
        var to = new IPEndPoint(IPAddress.Parse(at ?? (Fake != null ? "127.0.0.1" : "239.255.255.250")), Fake?.ssdp ?? 1900);
        using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        // Only the network-wide search is pinned to the LAN adapter: a search sent to one address
        // goes wherever the routing table says, and Windows refuses outright to send from a LAN
        // address to a loopback one.
        bool everyone = at == null && Fake == null;
        s.Bind(new IPEndPoint(everyone && IPAddress.TryParse(lanIp, out var local) ? local : IPAddress.Any, 0));
        s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        var buf = new byte[4096];
        for (int pass = 0; pass < passes && found.Count == 0; pass++)
        {
            foreach (var st in Targets)
                s.SendTo(Encoding.ASCII.GetBytes($"M-SEARCH * HTTP/1.1\r\nHOST: {to}\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: {st}\r\n\r\n"), to);
            var until = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < until)
            {
                s.ReceiveTimeout = Math.Max(1, (int)(until - DateTime.UtcNow).TotalMilliseconds);
                int n;
                try { n = s.Receive(buf); }
                catch (SocketException) { continue; }      // the timeout, or Windows reporting an ICMP "unreachable"
                var loc = Regex.Match(Encoding.ASCII.GetString(buf, 0, n), @"(?im)^LOCATION:\s*(\S+)");
                if (!loc.Success || !tried.Add(loc.Groups[1].Value)) continue;
                var g = Describe(loc.Groups[1].Value);
                if (g != null && found.All(x => x.Control != g.Control)) found.Add(g);
            }
        }
        return found;
    }

    // A device's description -> its WANIPConnection (or WANPPPConnection) control URL, or null.
    private static Gateway Describe(string location)
    {
        string xml;
        try { xml = Http.GetStringAsync(location).GetAwaiter().GetResult(); }
        catch (Exception) { return null; }
        var baseUrl = Regex.Match(xml, @"<URLBase>\s*([^<\s]+)\s*</URLBase>").Groups[1].Value;
        foreach (Match svc in Regex.Matches(xml, @"<service>(.*?)</service>", RegexOptions.Singleline))
        {
            string type = Regex.Match(svc.Value, @"<serviceType>\s*([^<\s]+)\s*</serviceType>").Groups[1].Value;
            string ctl = Regex.Match(svc.Value, @"<controlURL>\s*([^<\s]+)\s*</controlURL>").Groups[1].Value;
            if (ctl.Length == 0 || !(type.Contains(":WANIPConnection:") || type.Contains(":WANPPPConnection:"))) continue;
            return new Gateway(new Uri(new Uri(baseUrl.Length > 0 ? baseUrl : location), ctl).ToString(), type);
        }
        return null;
    }

    private static Hop UpnpHop(Gateway g, int port) => new("UPnP", new Uri(g.Control).Host, port, () => Unmap(g, port));

    // What the router says its internet side is: "" if it will not say.
    private static string ExternalAddress(Gateway g) =>
        Soap(g, "GetExternalIPAddress", "", out var reply) ? Regex.Match(reply, @"<NewExternalIPAddress>\s*([^<\s]*)\s*<").Groups[1].Value : "";

    // Forward UDP `port` on the router to `client`:`port`. "" when done, otherwise what the router
    // said. A permanent lease is what we want -- nothing has to renew it -- but plenty of routers
    // only take a bounded one, and a mapping left by a session that was killed rather than closed
    // looks like a conflict: so the permanent lease, then two hours, then our old one cleared and
    // the permanent lease once more.
    private static string Map(Gateway g, int port, string client)
    {
        string Add(int lease) => Soap(g, "AddPortMapping",
            $"<NewRemoteHost></NewRemoteHost><NewExternalPort>{port}</NewExternalPort><NewProtocol>UDP</NewProtocol>"
          + $"<NewInternalPort>{port}</NewInternalPort><NewInternalClient>{client}</NewInternalClient><NewEnabled>1</NewEnabled>"
          + $"<NewPortMappingDescription>Warships</NewPortMappingDescription><NewLeaseDuration>{lease}</NewLeaseDuration>", out var why) ? "" : why;
        if (Add(0) == "" || Add(7200) == "") return "";
        Unmap(g, port);
        return Add(0);
    }

    private static void Unmap(Gateway g, int port) =>
        Soap(g, "DeletePortMapping", $"<NewRemoteHost></NewRemoteHost><NewExternalPort>{port}</NewExternalPort><NewProtocol>UDP</NewProtocol>", out _);

    // One SOAP call. On failure `reply` is the router's own reason ("ConflictInMappingEntry"), or
    // what went wrong on the way to it.
    private static bool Soap(Gateway g, string action, string args, out string reply)
    {
        var body = "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                 + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>"
                 + $"<u:{action} xmlns:u=\"{g.Service}\">{args}</u:{action}></s:Body></s:Envelope>";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, g.Control) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
            req.Headers.TryAddWithoutValidation("SOAPAction", $"\"{g.Service}#{action}\"");
            using var res = Http.Send(req);
            reply = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (res.IsSuccessStatusCode) return true;
            var d = Regex.Match(reply, @"<errorDescription>\s*([^<]*?)\s*<").Groups[1].Value;
            reply = d.Length > 0 ? d : $"HTTP {(int)res.StatusCode}";
            return false;
        }
        catch (Exception e) { reply = e is TaskCanceledException ? "no reply" : e.Message; return false; }
    }

    // ── NAT-PMP and PCP ──────────────────────────────────────────────────────
    // The other two port-mapping protocols: NAT-PMP (RFC 6886) on Apple's routers and many others,
    // and PCP (RFC 6887), its successor. Both are one datagram each way to the gateway's port 5351,
    // and a PCP-only gateway answers a NAT-PMP request with "unsupported version" -- the cue to
    // speak PCP instead. `client` is the address the gateway sees the request come from: this PC
    // for our own router, our router's internet side for the one in front of it.
    //
    // Both lease rather than map for good: a day, which outlasts any session, and which means a
    // mapping from a crashed game expires on its own.
    private const uint Lease = 86400;

    private static Hop PortProtocol(string gateway, string client, int port, out string ext)
    {
        ext = "";
        int pmpPort = Fake?.pmp ?? 5351;
        // NAT-PMP: ask the external address (which also says whether it speaks NAT-PMP at all)...
        var a = Ask(gateway, pmpPort, client, new byte[] { 0, 0 }, 12);
        if (a == null) return null;
        if (a[0] == 0 && (a[2] << 8 | a[3]) != 1)
        {
            if ((a[2] << 8 | a[3]) == 0) ext = $"{a[8]}.{a[9]}.{a[10]}.{a[11]}";
            var m = Ask(gateway, pmpPort, client, PmpMap(port, port, Lease), 16);
            if (m == null || (m[2] << 8 | m[3]) != 0) return null;
            return new Hop("NAT-PMP", gateway, m[10] << 8 | m[11], () => Ask(gateway, pmpPort, client, PmpMap(port, 0, 0), 16));
        }
        // ...or PCP: MAP with a nonce, which the delete has to repeat.
        var nonce = new byte[12];
        Random.Shared.NextBytes(nonce);
        var r = Ask(gateway, pmpPort, client, PcpMap(client, nonce, port, Lease), 60);
        if (r == null || r[0] != 2 || r[3] != 0) return null;
        ext = $"{r[56]}.{r[57]}.{r[58]}.{r[59]}";
        return new Hop("PCP", gateway, r[42] << 8 | r[43], () => Ask(gateway, pmpPort, client, PcpMap(client, nonce, port, 0), 60));
    }

    private static byte[] PmpMap(int inner, int outer, uint life) => new byte[]
    {
        0, 1, 0, 0, (byte)(inner >> 8), (byte)inner, (byte)(outer >> 8), (byte)outer,
        (byte)(life >> 24), (byte)(life >> 16), (byte)(life >> 8), (byte)life,
    };

    private static byte[] PcpMap(string client, byte[] nonce, int port, uint life)
    {
        var b = new byte[60];
        b[0] = 2; b[1] = 1;                                                   // version 2, MAP request
        b[4] = (byte)(life >> 24); b[5] = (byte)(life >> 16); b[6] = (byte)(life >> 8); b[7] = (byte)life;
        b[18] = 0xff; b[19] = 0xff;                                           // client address, IPv4-mapped
        IPAddress.Parse(client).GetAddressBytes().CopyTo(b, 20);
        nonce.CopyTo(b, 24);
        b[36] = 17;                                                           // UDP
        b[40] = (byte)(port >> 8); b[41] = (byte)port;                        // internal port
        b[42] = (byte)(port >> 8); b[43] = (byte)port;                        // the external port we would like
        b[54] = 0xff; b[55] = 0xff;                                           // any external address (::ffff:0.0.0.0)
        return b;
    }

    // One request, retried at 250 / 500 / 1000 ms (RFC 6886's schedule, cut short). Sent from this
    // PC's LAN address when the request names it, so the gateway sees the address the request says.
    private static byte[] Ask(string gateway, int port, string client, byte[] req, int want)
    {
        using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var from = IPAddress.Any;
        if (Fake == null && IPAddress.TryParse(client, out var c) && Lan().Ip == client) from = c;
        s.Bind(new IPEndPoint(from, 0));
        var to = new IPEndPoint(IPAddress.Parse(gateway), port);
        var buf = new byte[1100];
        for (int wait = 250; wait <= 1000; wait *= 2)
        {
            s.SendTo(req, to);
            s.ReceiveTimeout = wait;
            try { int n = s.Receive(buf); if (n >= want) return buf[..n]; }
            catch (SocketException) { /* timed out, or nothing listening */ }
        }
        return null;
    }

    // ── Godot's own UPnP: the last resort ────────────────────────────────────
    // A second, independent implementation (miniupnpc), for a router the search above somehow
    // missed. It only accepts a router whose internet side is public, so it never needs the
    // second-router step. The one path here that touches the engine: see Game.Quit.
    private static Hop GodotUpnp(string lanIp, int port, out string ext)
    {
        ext = "";
        var u = new Godot.Upnp();
        if (lanIp.Length > 0) u.DiscoverMulticastIf = lanIp;
        if (u.Discover(2000, 2, "") != (int)Godot.Upnp.UpnpResult.Success) return null;
        var gw = u.GetGateway();
        if (gw == null || !gw.IsValidGateway() || u.AddPortMapping(port, port, "Warships", "UDP", 0) != (int)Godot.Upnp.UpnpResult.Success) return null;
        ext = u.QueryExternalAddress();
        return new Hop("UPnP (Godot)", gw.IgdOurAddr, port, () => u.DeletePortMapping(port, "UDP"));
    }
}
