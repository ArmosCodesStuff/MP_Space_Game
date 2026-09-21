# Two fake home routers for the smoke test: they answer UPnP searches and port-mapping calls the
# way real ones do, so the whole plug-and-play path is driven without touching the real router.
#
#     python fakeigd.py <ssdp port> <http port> <nat-pmp port> <seconds>
#
# INNER, at 127.0.0.1, is the router on your network. FRONT, at 127.0.4.1, is the one in front of
# it when there are two -- the address Net guesses from INNER's internet side, 127.0.4.40. The
# test picks a scenario with GET http://127.0.0.1:<http>/scenario/<name>, and reads back every
# mapping the routers were asked to make or remove from /log (one line each).
#   single        INNER's internet side is public: 203.0.113.7
#   double        INNER's is 127.0.4.40; FRONT answers, maps, and its internet side is 203.0.113.7
#   double-quiet  as double, but FRONT never answers
#   refuse        INNER answers searches and refuses every mapping (ConflictInMappingEntry)
#   cgnat         INNER maps, and its internet side is carrier-grade: 100.72.1.5
#   vpn           INNER maps, its internet side is 198.51.100.9 -- but /ip (the internet) sees 203.0.113.7
#   pmp           no UPnP; INNER speaks NAT-PMP, and gives out a DIFFERENT outside port (asked + 1000)
#   pcp           no UPnP; INNER speaks PCP, answering NAT-PMP with "unsupported version" as PCP routers do
#   none          nobody answers
# /ip answers "what is my address" with 203.0.113.7, standing in for api.ipify.org.
import re, socket, sys, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

SSDP, HTTP, PMP, LIFE = int(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3]), float(sys.argv[4])
INNER, FRONT, PUBLIC, INNER_WAN = '127.0.0.1', '127.0.4.1', '203.0.113.7', '127.0.4.40'
state = {'scenario': 'none', 'log': []}
lock = threading.Lock()


UPNP = {'single', 'double', 'double-quiet', 'refuse', 'cgnat', 'vpn'}
INNER_EXT = {'single': PUBLIC, 'refuse': PUBLIC, 'cgnat': '100.72.1.5', 'vpn': '198.51.100.9'}


def answers(who):
    sc = state['scenario']
    return (who == INNER and sc in UPNP) or (who == FRONT and sc == 'double')


def log(line):
    with lock:
        state['log'].append(line)


def pmp(addr):
    # NAT-PMP (RFC 6886) and PCP (RFC 6887), on one port, as real gateways do
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind((addr, PMP))
    s.settimeout(0.5)
    while True:
        try:
            d, peer = s.recvfrom(1100)
        except OSError:
            continue
        sc, who = state['scenario'], ('inner' if addr == INNER else 'front')
        if who != 'inner' or sc not in ('pmp', 'pcp'):
            continue
        if d[0] == 0 and sc == 'pcp':                       # "unsupported version": speak PCP
            s.sendto(bytes([0, 128 + d[1], 0, 1]) + bytes(8), peer)
        elif d[0] == 0 and d[1] == 0:                      # NAT-PMP: the external address
            s.sendto(bytes([0, 128, 0, 0]) + (7).to_bytes(4, 'big') + bytes([203, 0, 113, 7]), peer)
        elif d[0] == 0 and d[1] == 1:                      # NAT-PMP: map (lifetime 0 = delete)
            inner, life = int.from_bytes(d[4:6], 'big'), int.from_bytes(d[8:12], 'big')
            log(f"{who} pmp {'add' if life else 'delete'} {inner}")
            ext = inner + 1000 if life else 0
            s.sendto(bytes([0, 129, 0, 0]) + (7).to_bytes(4, 'big') + d[4:6] + ext.to_bytes(2, 'big') + d[8:12], peer)
        elif d[0] == 2 and d[1] == 1 and sc == 'pcp':      # PCP MAP
            life, port = int.from_bytes(d[4:8], 'big'), int.from_bytes(d[40:42], 'big')
            log(f"{who} pcp {'add' if life else 'delete'} {port}")
            r = bytearray(60)
            r[0], r[1], r[3] = 2, 0x81, 0
            r[4:8], r[24:36], r[36], r[40:44] = d[4:8], d[24:36], 17, d[40:42] + d[40:42]
            r[54], r[55], r[56:60] = 0xff, 0xff, bytes([203, 0, 113, 7])
            s.sendto(bytes(r), peer)


def ssdp(addr):
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.bind((addr, SSDP))
    s.settimeout(0.5)
    while True:
        try:
            data, peer = s.recvfrom(4096)
        except OSError:
            continue
        st = re.search(rb'(?im)^ST:\s*(\S+)', data)
        if not st or not answers(addr):
            continue
        s.sendto(b'HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nST: ' + st.group(1) +
                 f'\r\nUSN: uuid:fake-{addr}\r\nLOCATION: http://{addr}:{HTTP}/desc.xml\r\nSERVER: FakeIGD\r\n\r\n'.encode(), peer)


DESC = ('<?xml version="1.0"?><root xmlns="urn:schemas-upnp-org:device-1-0"><device>'
        '<deviceType>urn:schemas-upnp-org:device:InternetGatewayDevice:1</deviceType><deviceList><device>'
        '<deviceType>urn:schemas-upnp-org:device:WANDevice:1</deviceType><deviceList><device>'
        '<deviceType>urn:schemas-upnp-org:device:WANConnectionDevice:1</deviceType><serviceList><service>'
        '<serviceType>urn:schemas-upnp-org:service:WANIPConnection:1</serviceType>'
        '<controlURL>/ctl/IPConn</controlURL></service></serviceList>'
        '</device></deviceList></device></deviceList></device></root>')


def handler_for(who):
    class H(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def reply(self, code, body, kind='text/xml'):
            b = body.encode()
            self.send_response(code)
            self.send_header('Content-Type', kind)
            self.send_header('Content-Length', str(len(b)))
            self.end_headers()
            self.wfile.write(b)

        def do_GET(self):
            if self.path == '/desc.xml':
                return self.reply(200, DESC)
            if self.path == '/ip':
                return self.reply(200, PUBLIC, 'text/plain')
            if self.path.startswith('/scenario/'):
                with lock:
                    state['scenario'], state['log'] = self.path.split('/')[-1], []
                return self.reply(200, 'ok', 'text/plain')
            if self.path == '/log':
                with lock:
                    return self.reply(200, '\n'.join(state['log']), 'text/plain')
            self.reply(404, 'no')

        def do_POST(self):
            body = self.rfile.read(int(self.headers.get('Content-Length', 0))).decode()
            action = self.headers.get('SOAPAction', '').strip('"').split('#')[-1]
            arg = lambda n: (re.search(f'<{n}>([^<]*)</{n}>', body) or [None, ''])[1]
            if action == 'GetExternalIPAddress':
                ext = PUBLIC if who == FRONT else INNER_EXT.get(state['scenario'], INNER_WAN)
                return self.reply(200, f'<s:Envelope><s:Body><u:r><NewExternalIPAddress>{ext}</NewExternalIPAddress></u:r></s:Body></s:Envelope>')
            name = 'inner' if who == INNER else 'front'
            if action == 'AddPortMapping':
                if state['scenario'] == 'refuse':
                    return self.reply(500, '<s:Envelope><s:Body><s:Fault><detail><UPnPError><errorCode>718</errorCode>'
                                           '<errorDescription>ConflictInMappingEntry</errorDescription></UPnPError></detail></s:Fault></s:Body></s:Envelope>')
                with lock:
                    state['log'].append(f"{name} add {arg('NewExternalPort')} {arg('NewInternalClient')}")
                return self.reply(200, '<s:Envelope><s:Body></s:Body></s:Envelope>')
            if action == 'DeletePortMapping':
                with lock:
                    state['log'].append(f"{name} delete {arg('NewExternalPort')}")
                return self.reply(200, '<s:Envelope><s:Body></s:Body></s:Envelope>')
            self.reply(500, '<errorDescription>InvalidAction</errorDescription>')
    return H


for addr in (INNER, FRONT):
    threading.Thread(target=ssdp, args=(addr,), daemon=True).start()
    threading.Thread(target=pmp, args=(addr,), daemon=True).start()
    threading.Thread(target=ThreadingHTTPServer((addr, HTTP), handler_for(addr)).serve_forever, daemon=True).start()
print(f'fake routers up: ssdp {SSDP}, http {HTTP}, nat-pmp {PMP}', flush=True)
time.sleep(LIFE)
