using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// ─────────────────────────────────────────────────────────────────────────────
// THIS PC'S ADDRESSES: the LAN one and each virtual network's (docs/plans/network_webrtc.md §7). The
// host panel labels them ("Same network: {lan}:{port}", "On Radmin VPN: {ip}:{port}"), and the fit rule
// ranks an invite's candidates by them (Rendezvous.Fit). It replaced Router.cs: WebRTC's ICE finds its
// own way through a router, so the game no longer asks one to open a port (UPnP, NAT-PMP and PCP are
// gone, and with them the public-address lookup). A new virtual network is a row of OverlayKinds.
// ─────────────────────────────────────────────────────────────────────────────
public static class Adapters
{
    // THE REAL LAN, not the first private address the OS lists. A PC with WSL, Hyper-V, VirtualBox
    // or a VPN has several, and the wrong one names an address the friend's PC has never heard of.
    // The one that leads to the internet is up, has a gateway, and is not a virtual adapter.
    // "127.0.0.1" when there is none.
    public static string Lan()
    {
        string best = null; int rank = -1;
        foreach (var n in Up())
        {
            var p = n.GetIPProperties();
            var ip = p.UnicastAddresses.Select(a => a.Address)
                      .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254."));
            if (ip == null) continue;
            bool gateway = p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            int r = (gateway ? 2 : 0) + (IsVirtual(n) ? 0 : 1);
            if (r > rank) { rank = r; best = ip.ToString(); }
        }
        return best ?? "127.0.0.1";
    }

    // Virtual networks friends can join from anywhere, routers or no routers: on one of these, a
    // friend on the same network types this PC's address on it.
    private static readonly (string match, string name)[] OverlayKinds =
        { ("Tailscale", "Tailscale"), ("ZeroTier", "ZeroTier"), ("Radmin", "Radmin VPN"), ("Hamachi", "Hamachi") };
    public static List<(string name, string ip)> Overlays()
    {
        var found = new List<(string, string)>();
        foreach (var n in Up(tunnels: true))
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

    private static IEnumerable<NetworkInterface> Up(bool tunnels = false)
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
}
