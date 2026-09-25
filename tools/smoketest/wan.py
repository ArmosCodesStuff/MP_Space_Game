# THE BOX: the network between players, for the smoke test (docs/plans/network_webrtc.md section 10.1).
# One stdlib process; run.ps1 starts it for every run and stops it after. It serves:
#   * a STUN responder on 127.0.41.1:3478 -- Binding requests only, answered with XOR-MAPPED-ADDRESS --
#     so the STUN walk is exact on one machine and the sealing check has a candidate to race for;
#   * two silent UDP ports, 127.0.0.1:19482 and :19483: as STUN rows, a network that answers neither
#     provider (bound, so the OS sends no "port unreachable" that would end a gather early);
#   * a silent TCP port, 127.0.0.1:19481: it accepts and never answers;
#   * the PAIR PROXY: POST /box/pair makes two UDP ports, A and B, on 127.0.42.1, and answers
#     {"n": n, "a": A, "b": B}; POST /box/pair/<n>?host=ip:port&guest=ip:port&seed=s gives it the real
#     endpoints. A datagram arriving at A leaves from B to the host; one arriving at B leaves from A to
#     the guest. To each side the other is one fixed address, like a NAT with fixed mappings. Every
#     datagram waits the path's delay +/- jitter and is lost at its rate, drawn from the pair's seed;
#   * POST /box/blackhole?s=<seconds>: every proxied datagram is dropped for that long;
#   * GET /box/stats: what the box has carried, as JSON; POST /box/quit ends it, printing that;
#   * --relay <listen>:<host>: the plain UDP relay the ENet -Wan run still joins through (a guest joins
#     127.0.0.1:<listen>); it goes when R2 moves -Wan onto the pair proxy.
# Exits by itself after --life seconds, printing what it carried.
#
#     python wan.py --http 19480 --path 90,25,2 --life 1300 [--relay 28115:27115 ...]
import argparse, heapq, json, random, select, socket, struct, threading, time, zlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

ap = argparse.ArgumentParser()
ap.add_argument('--http', type=int, default=19480)
ap.add_argument('--path', default='0,0,0')              # one-way ms, jitter ms, loss %
ap.add_argument('--life', type=float, default=1300)
ap.add_argument('--relay', action='append', default=[])
args = ap.parse_args()
delay, jitter, loss = (float(x) for x in args.path.split(','))
delay, jitter, loss = delay / 1000, jitter / 1000, loss / 100

lock = threading.RLock()                                  # the main loop holds it while a handler takes it again
queue, seq = [], 0                                        # (due, seq, socket, data, to)
stats = {'stun': {'bound': False, 'answered': 0}, 'silent_udp': {}, 'silent_tcp': {'bound': False, 'accepted': 0},
         'pairs': {}, 'blackholed': 0}
blackhole_until = 0.0
readers = {}                                              # socket -> what to do with a datagram (or a connection)


def udp(host, port):
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind((host, port))
    if hasattr(socket, 'SIO_UDP_CONNRESET'):              # Windows: an ICMP "port unreachable" is not an error on the socket
        s.ioctl(socket.SIO_UDP_CONNRESET, False)
    return s


def later(sock, data, to, rng):
    """Queues one datagram with the path's delay; False when the path loses it."""
    global seq
    if rng.random() < loss:
        return False
    seq += 1
    heapq.heappush(queue, (time.monotonic() + max(0.0, delay + rng.uniform(-jitter, jitter)), seq, sock, data, to))
    return True


# ---- the STUN responder (RFC 5389: a Binding response with XOR-MAPPED-ADDRESS and FINGERPRINT) ----
COOKIE = 0x2112A442


def stun(sock, data, frm):
    if len(data) < 20:
        return
    kind, length, cookie = struct.unpack('!HHI', data[:8])
    if kind != 0x0001 or cookie != COOKIE:
        return
    ip = socket.inet_aton(frm[0])
    xaddr = struct.pack('!BBH', 0, 1, frm[1] ^ (COOKIE >> 16)) + bytes(a ^ b for a, b in zip(ip, struct.pack('!I', COOKIE)))
    attrs = struct.pack('!HH', 0x0020, len(xaddr)) + xaddr
    head = struct.pack('!HHI', 0x0101, len(attrs) + 8, COOKIE) + data[8:20]
    crc = (zlib.crc32(head + attrs) ^ 0x5354554E) & 0xFFFFFFFF
    try:
        sock.sendto(head + attrs + struct.pack('!HHI', 0x8028, 4, crc), frm)
        with lock:
            stats['stun']['answered'] += 1
    except OSError:
        pass


try:
    readers[udp('127.0.41.1', 3478)] = ('stun', None)
    stats['stun']['bound'] = True
except OSError as e:
    print(f'BOX: the STUN responder could not bind 127.0.41.1:3478 ({e})', flush=True)
for port in (19482, 19483):
    try:
        readers[udp('127.0.0.1', port)] = ('silent', port)
        stats['silent_udp'][str(port)] = 0
    except OSError as e:
        print(f'BOX: the silent port 127.0.0.1:{port} could not bind ({e})', flush=True)
held = []                                                 # the silent TCP port's connections, kept open and never answered
try:
    tcp = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    tcp.bind(('127.0.0.1', 19481))
    tcp.listen(16)
    readers[tcp] = ('tcp', None)
    stats['silent_tcp']['bound'] = True
except OSError as e:
    print(f'BOX: the silent TCP port 127.0.0.1:19481 could not bind ({e})', flush=True)


# ---- the pair proxy ----
pairs = {}                                                # n -> {'a', 'b', 'host', 'guest', 'rng'}


def new_pair():
    with lock:
        n = len(pairs) + 1
        a, b = udp('127.0.42.1', 0), udp('127.0.42.1', 0)
        pairs[n] = {'a': a, 'b': b, 'host': None, 'guest': None, 'rng': random.Random(n)}
        readers[a] = ('pair', (n, 'a'))
        readers[b] = ('pair', (n, 'b'))
        stats['pairs'][str(n)] = {'a': a.getsockname()[1], 'b': b.getsockname()[1], 'to_host': 0, 'to_guest': 0, 'lost': 0, 'unregistered': 0}
        return {'n': n, 'a': a.getsockname()[1], 'b': b.getsockname()[1]}


def endpoint(text):
    host, _, port = text.rpartition(':')
    return (host, int(port))


def proxied(n, side, data):
    p, st = pairs[n], stats['pairs'][str(n)]
    to_host = side == 'a'                                 # arriving at A is the guest's: it leaves from B for the host
    to = p['host'] if to_host else p['guest']
    if to is None:
        st['unregistered'] += 1
        return
    if time.monotonic() < blackhole_until:
        stats['blackholed'] += 1
        return
    if later(p['b'] if to_host else p['a'], data, to, p['rng']):
        st['to_host' if to_host else 'to_guest'] += 1
    else:
        st['lost'] += 1


# ---- the ENet relays (-Wan, until R2) ----
relays = []
for spec in args.relay:
    listen, host = (int(x) for x in spec.split(':'))
    front = udp('127.0.0.1', listen)
    r = {'front': front, 'host': host, 'upstream': {}, 'guest_of': {}, 'rng': random.Random(listen), 'sent': 0, 'lost': 0}
    relays.append(r)
    readers[front] = ('front', r)


def relayed(r, sock, data, frm):
    if sock is r['front']:
        if frm not in r['upstream']:
            u = udp('127.0.0.1', 0)
            r['upstream'][frm], r['guest_of'][u] = u, frm
            with lock:
                readers[u] = ('back', r)
        ok = later(r['upstream'][frm], data, ('127.0.0.1', r['host']), r['rng'])
    else:
        ok = later(r['front'], data, r['guest_of'][sock], r['rng'])
    r['sent' if ok else 'lost'] += 1


# ---- the control port ----
class Control(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def reply(self, body, code=200):
        raw = json.dumps(body).encode()
        self.send_response(code)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        if urlparse(self.path).path == '/box/stats':
            with lock:
                self.reply(stats)
        else:
            self.reply({'error': 'no such page'}, 404)

    def do_POST(self):
        global blackhole_until, end
        u = urlparse(self.path)
        q = {k: v[0] for k, v in parse_qs(u.query).items()}
        parts = [p for p in u.path.split('/') if p]
        try:
            if parts == ['box', 'pair']:
                self.reply(new_pair())
            elif len(parts) == 3 and parts[:2] == ['box', 'pair']:
                n = int(parts[2])
                with lock:
                    p = pairs[n]
                    p['host'], p['guest'] = endpoint(q['host']), endpoint(q['guest'])
                    p['rng'] = random.Random(int(q.get('seed', n)))
                self.reply({'n': n, 'host': q['host'], 'guest': q['guest']})
            elif parts == ['box', 'quit']:                  # run.ps1's way to stop it with its stats printed
                end = 0
                self.reply({'quit': True})
            elif parts == ['box', 'blackhole']:
                with lock:
                    blackhole_until = time.monotonic() + float(q['s'])
                self.reply({'blackhole': float(q['s'])})
            else:
                self.reply({'error': 'no such page'}, 404)
        except (KeyError, ValueError) as e:
            self.reply({'error': str(e)}, 400)


control = ThreadingHTTPServer(('127.0.0.1', args.http), Control)
threading.Thread(target=control.serve_forever, daemon=True).start()
print(f'BOX: up, control on 127.0.0.1:{args.http}, path {args.path}', flush=True)

end = time.monotonic() + args.life
while time.monotonic() < end:
    with lock:
        socks = list(readers)
    wait = min(0.05, max(0.0, queue[0][0] - time.monotonic())) if queue else 0.05
    if not socks:                                         # select() on Windows refuses an empty list
        time.sleep(wait)
        continue
    ready, _, _ = select.select(socks, [], [], wait)
    for s in ready:
        what, arg = readers[s]
        if what == 'tcp':
            try:
                c, _ = s.accept()
                held.append(c)
                stats['silent_tcp']['accepted'] += 1
            except OSError:
                pass
            continue
        try:
            data, frm = s.recvfrom(65535)
        except OSError:
            continue
        with lock:
            if what == 'stun':
                stun(s, data, frm)
            elif what == 'silent':
                stats['silent_udp'][str(arg)] += 1
            elif what == 'pair':
                proxied(arg[0], arg[1], data)
            else:
                relayed(arg, s, data, frm)
    now = time.monotonic()
    with lock:
        while queue and queue[0][0] <= now:
            _, _, sock, data, to = heapq.heappop(queue)
            try:
                sock.sendto(data, to)
            except OSError:
                pass

control.shutdown()
for r in relays:
    print(f"wan {r['front'].getsockname()[1]}->{r['host']}: {r['sent']} delivered, {r['lost']} lost, {len(r['upstream'])} guests", flush=True)
print('BOX: ' + json.dumps(stats), flush=True)
