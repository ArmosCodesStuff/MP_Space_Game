# An internet between two processes on one machine: a UDP relay that delays, jitters and drops.
#
#     python wan.py <listen port> <host port> <one-way ms> <jitter ms> <loss %> <seconds>
#
# A guest joins 127.0.0.1:<listen port> instead of the host's own port, and every datagram each
# way waits <one-way ms> +/- <jitter ms> and is lost with probability <loss %>. Jitter reorders
# packets, as a real path does. Each guest gets its own upstream socket, so the host sees a
# different address per guest -- the way a NAT presents them.
#
# The drops come from a seeded generator, so the same packet sequence loses the same packets.
# Exits by itself after <seconds>, printing what it carried.
import heapq, random, select, socket, sys, time

listen_port, host_port = int(sys.argv[1]), int(sys.argv[2])
delay, jitter, loss, life = float(sys.argv[3]) / 1000, float(sys.argv[4]) / 1000, float(sys.argv[5]) / 100, float(sys.argv[6])

rng = random.Random(listen_port)
front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
front.bind(('127.0.0.1', listen_port))
upstream = {}      # guest address -> the socket that speaks for it to the host
guest_of = {}      # that socket -> the guest address
queue, seq = [], 0
sent = dropped = 0
end = time.monotonic() + life


def later(sock, data, to):
    global seq, dropped
    if rng.random() < loss:
        dropped += 1
        return
    seq += 1
    heapq.heappush(queue, (time.monotonic() + max(0.0, delay + rng.uniform(-jitter, jitter)), seq, sock, data, to))


while time.monotonic() < end:
    wait = min(0.05, max(0.0, queue[0][0] - time.monotonic())) if queue else 0.05
    ready, _, _ = select.select([front] + list(guest_of), [], [], wait)
    for s in ready:
        try:
            data, addr = s.recvfrom(65535)
        except OSError:            # Windows reports an ICMP "port unreachable" as an error on the socket
            continue
        if s is front:
            if addr not in upstream:
                u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                u.bind(('127.0.0.1', 0))
                upstream[addr], guest_of[u] = u, addr
            later(upstream[addr], data, ('127.0.0.1', host_port))
        else:
            later(front, data, guest_of[s])
    now = time.monotonic()
    while queue and queue[0][0] <= now:
        _, _, sock, data, to = heapq.heappop(queue)
        try:
            sock.sendto(data, to)
            sent += 1
        except OSError:
            pass

print(f"wan {listen_port}->{host_port}: {sent} delivered, {dropped} lost, {len(upstream)} guests", flush=True)
