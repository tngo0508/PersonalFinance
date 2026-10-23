import json, sys, os, time, urllib.request
S = sys.argv[1]
UA = "PersonalFinance research contact@example.com"
top = {}
ciks = set()
from statistics import median
peryear = {}
for y in range(2011, 2026):
    best = {}
    for f in (f"pf{y}q1", f"pf{y}", f"pf{y}q3", f"pf{y}q4"):
        try: rows = json.load(open(f"{S}/raw/{f}.json"))["data"]
        except Exception: continue
        for r in rows:
            if r["cik"] not in best or r["end"] > best[r["cik"]]["end"]: best[r["cik"]] = r
    peryear[y] = best
assets = {}
for y in range(2010, 2026):
    for q in (2, 4):
        try: rows = json.load(open(f"{S}/raw/assets{y}q{q}.json"))["data"]
        except Exception: continue
        for r in rows: assets.setdefault(r["cik"], {})[y * 10 + q] = r["val"]
def assets_near(c, y):
    a = assets.get(c, {})
    for k in (y * 10 + 2, (y - 1) * 10 + 4, y * 10 + 4, (y - 1) * 10 + 2):
        if a.get(k): return a[k]
    return None
hist = {}
for y, best in peryear.items():
    for c, r in best.items(): hist.setdefault(c, []).append(r["val"])
dropped = []
for y in range(2011, 2026):
    vals = []
    for c, r in peryear[y].items():
        prior = [peryear[z][c]["val"] for z in range(y - 1, 2010, -1) if c in peryear[z]][:1]
        after = [peryear[z][c]["val"] for z in range(y + 1, 2026) if c in peryear[z]][:1]
        nb = prior + after
        if (c == 1209821 or r["entityName"].upper().startswith(("WGL HOLDINGS", "GREAT SOUTHERN", "MISTER CAR WASH", "ENLINK", "READY CAPITAL"))) and r["val"] > 1e11:
            dropped.append((y, r["entityName"], r["val"])); continue  # manual exclusion list (prototype)
        a = assets_near(c, y)
        if a and r["val"] > 200 * a:
            dropped.append((y, r["entityName"], r["val"])); continue
        if r["val"] > 6e12 or (nb and r["val"] > 30 * max(nb)) or (not nb and r["val"] > 1e12):
            dropped.append((y, r["entityName"], r["val"])); continue
        vals.append([c, r["val"]])
    vals.sort(key=lambda x: -x[1])
    top[y] = vals[:500]
    ciks.update(c for c, _ in top[y])
print("dropped outliers", len(dropped), dropped[:8])
print("union ciks", len(ciks))
os.makedirs(f"{S}/raw/sub", exist_ok=True)
meta = {}
for i, c in enumerate(sorted(ciks)):
    p = f"{S}/raw/sub/{c}.json"
    if not os.path.exists(p):
        req = urllib.request.Request(f"https://data.sec.gov/submissions/CIK{c:010d}.json", headers={"User-Agent": UA})
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                d = json.load(resp)
        except Exception as e:
            print("fail", c, e); continue
        d = {k: d.get(k) for k in ("name", "sic", "sicDescription", "ownerOrg", "tickers")}
        json.dump(d, open(p, "w"))
        time.sleep(0.12)
    meta[c] = json.load(open(p))
json.dump({"top": top, "meta": meta}, open(f"{S}/raw/sec_merged.json", "w"))
from collections import Counter
print(Counter(m.get("ownerOrg") for m in meta.values()).most_common(15))
