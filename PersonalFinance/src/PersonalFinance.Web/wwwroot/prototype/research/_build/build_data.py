import json, sys
from collections import Counter, defaultdict
S, OUT = sys.argv[1], sys.argv[2]

# ---- SEC: largest 500 filers by public float, per year ----
d = json.load(open(f"{S}/raw/sec_merged.json"))
meta = d["meta"]
by_sic, by_major = defaultdict(Counter), defaultdict(Counter)
for v in meta.values():
    if v.get("ownerOrg"):
        by_sic[v.get("sic")][v["ownerOrg"]] += 1
        by_major[(v.get("sic") or "")[:2]][v["ownerOrg"]] += 1
def office(v):
    if v.get("ownerOrg"): return v["ownerOrg"]
    for table, key in ((by_sic, v.get("sic")), (by_major, (v.get("sic") or "")[:2])):
        if table.get(key): return table[key].most_common(1)[0][0]
    return "00 Unassigned"
def clean_office(o):  # "06 Technology" -> "Technology"
    return o.split(" ", 1)[1] if o[:2].isdigit() else o
offices, industries, companies = [], [], []
oi, ii, ci = {}, {}, {}
for cik in sorted(meta, key=int):
    v = meta[cik]
    o = clean_office(office(v))
    if o not in oi: oi[o] = len(offices); offices.append(o)
    ind = (v.get("sicDescription") or "Unclassified").strip()
    key = (ind, oi[o])
    if key not in ii: ii[key] = len(industries); industries.append([ind, oi[o]])
    tick = (v.get("tickers") or [None])[0]
    ci[cik] = len(companies); companies.append([v.get("name") or f"CIK {cik}", tick, ii[key]])
values = {}
for y, rows in d["top"].items():
    values[y] = [[ci[str(c)], round(val / 1e6)] for c, val in rows]
sec = {"offices": offices, "industries": industries, "companies": companies, "values": values}

# ---- World Bank: market cap and GDP by country ----
codes = {a3: num for _, a3, num, _ in json.load(open(f"{S}/raw/codes.json"))}
countries = {c["id"]: c["name"] for c in json.load(open(f"{S}/raw/wb_countries.json"))[1] if c["region"]["id"] != "NA"}
def series(path):
    out = defaultdict(dict)
    for r in json.load(open(path))[1]:
        a3 = r["countryiso3code"]
        if r["value"] is None or a3 not in countries or a3 not in codes: continue
        out[int(r["date"])][codes[a3]] = r["value"]
    return out
mcap, gdp = series(f"{S}/raw/wb_mcap.json"), series(f"{S}/raw/wb_gdp.json")
world = {"names": {codes[a]: n for a, n in countries.items() if a in codes}, "years": {}}
for y in range(1990, 2026):
    world["years"][y] = {k: [round(v / 1e6), round(v / gdp[y][k] * 100, 1) if gdp[y].get(k) else None] for k, v in mcap[y].items()}
atlas = json.load(open(f"{S}/raw/countries-110m.json"))

with open(f"{OUT}/data-sec.js", "w", encoding="utf-8") as f:
    f.write("// PROTOTYPE data. SEC EDGAR frames dei:EntityPublicFloat (latest of CY{year}Q1I-Q4I per filer, outliers dropped: >200x total assets, >30x nearest other year, manual list), top 500 per year, $ millions; office = submissions ownerOrg (SIC fallback). Fetched 2026-10-23.\n")
    f.write("window.RP_SEC = " + json.dumps(sec, separators=(",", ":")) + ";\n")
with open(f"{OUT}/data-world.js", "w", encoding="utf-8") as f:
    f.write("// PROTOTYPE data. World Bank WDI CM.MKT.LCAP.CD ($ millions) and % of GDP (NY.GDP.MKTP.CD), CC BY 4.0; world-atlas@2.0.2 countries-110m (ISC, Natural Earth). Fetched 2026-10-23.\n")
    f.write("window.RP_WORLD = " + json.dumps(world, separators=(",", ":")) + ";\n")
    f.write("window.RP_ATLAS = " + json.dumps(atlas, separators=(",", ":")) + ";\n")
print(len(offices), offices, len(industries), len(companies))
print({y: len(v) for y, v in world["years"].items() if y in (1990, 2000, 2025)})
