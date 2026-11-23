#!/usr/bin/env python3
"""Tick-time breakdown of a dotnet-trace speedscope file (1.7, sampled-thread-time profile).

  speedscope_ticks.py trace.speedscope.json REGEX...      inclusive time of matching frames, % of DoSingleTick
  speedscope_ticks.py trace.speedscope.json --callees FRAME_SUFFIX
"""
import collections, json, re, sys

d = json.load(open(sys.argv[1]))
frames = [f['name'] for f in d['shared']['frames']]

def walk(prof, on_close):
    stack = []
    for e in prof.get('events', []):
        if e['type'] == 'O':
            stack.append((e['frame'], e['at']))
            continue
        while stack:
            f, t0 = stack.pop()
            on_close(f, t0, e['at'], stack)
            if f == e['frame']:
                break

for prof in d['profiles']:
    if sys.argv[2] == '--callees':
        target, ch = sys.argv[3], collections.Counter()
        def close(f, t0, t1, stack):
            if stack and frames[stack[-1][0]].endswith(target): ch[frames[f]] += t1 - t0
        walk(prof, close)
        for k, v in ch.most_common(15): print(f'{v:9.1f}ms  {k[:160]}')
        continue
    incl, depth, start = collections.Counter(), collections.Counter(), {}
    stack = []
    for e in prof.get('events', []):  # recursion-safe inclusive time
        f, t = e['frame'], e['at']
        if e['type'] == 'O':
            stack.append(f)
            if depth[f] == 0: start[f] = t
            depth[f] += 1
        else:
            while stack:
                g = stack.pop(); depth[g] -= 1
                if depth[g] == 0: incl[frames[g]] += t - start[g]
                if g == f: break
    tick = sum(v for k, v in incl.items() if k.endswith('TickManager.DoSingleTick()'))
    if not tick: continue
    print(f"== {prof['name']}: span {prof['endValue'] - prof['startValue']:.0f} ms, DoSingleTick {tick:.0f} ms")
    for p in sys.argv[2:]:
        r = re.compile(p)
        for k, v in sorted(((k, v) for k, v in incl.items() if r.search(k)), key=lambda x: -x[1])[:15]:
            print(f'  {v / tick * 100:6.2f}%  {v:8.1f}ms  {k[:160]}')
