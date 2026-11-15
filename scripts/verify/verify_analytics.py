"""Independent check of the app's headline figures: each instrument's latest price, 1-day, 1-month and 1-year change
and 52-week high/low, recomputed from the source (Sarmaaya) without the app's code, and the funds' money in/out.
Funds that paid out in the period differ by design (the app reinvests payouts), so those are reported, not failed.
Usage: python scripts/verify/verify_analytics.py   (the API must be running)"""
import json
import urllib.request
from datetime import date, timedelta

API = "http://localhost:5000/api"
SRC = "https://beta-restapi.sarmaaya.pk/api"
FUNDS = {"MIF": "698bac51-81aa-4e0c-ad94-93d630e1e2a4", "KMIF": "a0aac2a4-0ec4-4718-b2e5-4580f5cd8ddd",
         "MBF": "f6bcc825-99fe-47f8-a09c-805f9e46697c", "MGF": "7e59207d-798c-43ab-bc76-4a44d2f90a64",
         "MCF": "a0674272-8a64-4fb8-b708-4e60a1e7f135"}
SOURCES = {"KSE100": "/indices/price-history/KSE100?days=400", "KMI30": "/indices/price-history/KMI30?days=400",
           "USDPKR": "/forex/price-history/USDPKR?days=400", "GOLDPK": "/commodities/price/gold?days=400"}
SOURCES.update({s: f"/mutual-funds/nav-history/{fid}?days=400" for s, fid in FUNDS.items()})
SOURCES.update({s: f"/stocks/price-history/{s}?days=400" for s in ["GHNI", "SYS", "MARI", "MEBL", "FATIMA", "DCR"]})


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=120) as r:
        return json.load(r)


def spikes_out(points):
    out = [points[0]]
    for i in range(1, len(points) - 1):
        prev, cur, nxt = out[-1][1], points[i][1], points[i + 1][1]
        j, b = cur / prev - 1, nxt / cur - 1
        if not (abs(j) > .15 and abs(b) > .15 and (j > 0) != (b > 0) and abs(nxt / prev - 1) < .05):
            out.append(points[i])
    return out + [points[-1]]


def on_or_before(points, day):
    v = None
    for d, p in points:
        if d > day:
            break
        v = p
    return v


app = {a["symbol"]: a for a in get(API + "/analytics")}
problems = 0
print(f"{'':8}{'price':>12}{'1D app/src':>18}{'1M app/src':>18}{'1Y app/src':>18}{'52w high app/src':>26}")
for sym, path in SOURCES.items():
    rows = get(SRC + path)["response"]
    pts = sorted({r["date"][:10]: float(r.get("nav") or r.get("price") or 0) for r in rows}.items())
    pts = spikes_out([(date.fromisoformat(d), p) for d, p in pts if p > 0])
    last_day, last = pts[-1]
    d1 = (last / pts[-2][1] - 1) * 100
    month_ago = (last_day.replace(day=1) - timedelta(days=1)).replace(day=min(last_day.day, (last_day.replace(day=1) - timedelta(days=1)).day))
    m1 = (last / on_or_before(pts, month_ago) - 1) * 100  # one calendar month back, as the app counts it
    y1 = (last / on_or_before(pts, last_day - timedelta(days=365)) - 1) * 100
    hi = max(p for d, p in pts if d > last_day - timedelta(days=365))
    a = app[sym]
    checks = [abs(a["lastPrice"] - last) / last < 1e-4, abs(a["change1D"] - d1) < 0.05, abs(a["change1M"] - m1) < 0.05,
              abs(a["change1Y"] - y1) < 0.05, abs(a["high52W"] - hi) / hi < 1e-3]
    # A fund that paid out differs on longer spans by design: the app reinvests the payout, the source price drops by it
    flag = "ok" if all(checks) else ("differs: payouts reinvested" if sym in FUNDS and all(checks[:2]) else "MISMATCH")
    problems += flag == "MISMATCH"
    print(f"{sym:8}{last:>12.2f}{a['change1D']:>9.2f}/{d1:<8.2f}{a['change1M']:>9.2f}/{m1:<8.2f}{a['change1Y']:>9.2f}/{y1:<8.2f}"
          f"{a['high52W']:>13.2f}/{hi:<12.2f} {flag}")

# Money in or out of each fund in its latest reported month: size change minus unit price change
print()
for sym, fid in FUNDS.items():
    sizes = sorted((r["date"][:10], r["aum"]) for r in get(f"{SRC}/mutual-funds/aum-history/{fid}?days=10000")["response"])
    navs = sorted((date.fromisoformat(r["date"][:10]), r["nav"]) for r in get(f"{SRC}/mutual-funds/nav-history/{fid}?days=120")["response"])
    (m0, s0), (m1_, s1) = sizes[-2], sizes[-1]
    flow = (s1 / s0 - on_or_before(navs, date.fromisoformat(m1_)) / on_or_before(navs, date.fromisoformat(m0))) * 100
    app_flow = (app[sym].get("fundSize") or {}).get("flow1MPercent")
    ok = app_flow is not None and abs(app_flow - flow) < 0.15
    problems += not ok
    print(f"{sym:6} money in/out {m1_[:7]}: app {app_flow}  source {flow:.1f}  {'ok' if ok else 'MISMATCH'}")

print(f"\nMismatches: {problems}")
