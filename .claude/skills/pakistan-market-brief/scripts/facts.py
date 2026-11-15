"""Facts for the market brief, gathered without the app's database, for the cloud routine that writes the brief when
the PC is off. Mirrors what the app computes (DailyBriefService): price changes, long-term trend, what followed
similar trends (HistoricalOutlook), trading volume, the stronger-than-cash check, and the week's headlines.
Standard library only. Usage: python3 scripts/facts.py > facts.json   (from the skill folder)
"""
import json
import re
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta, timezone

API = "https://beta-restapi.sarmaaya.pk/api"

# The same instruments and sources as the app (InstrumentSeedData, TrackedStocks)
FUNDS = {
    "MIF": ("Meezan Islamic Fund", "698bac51-81aa-4e0c-ad94-93d630e1e2a4"),
    "KMIF": ("KSE Meezan Index Fund", "a0aac2a4-0ec4-4718-b2e5-4580f5cd8ddd"),
    "MBF": ("Meezan Balanced Fund", "f6bcc825-99fe-47f8-a09c-805f9e46697c"),
    "MGF": ("Meezan Gold Fund", "7e59207d-798c-43ab-bc76-4a44d2f90a64"),
    "MCF": ("Meezan Cash Fund (what cash earns)", "a0674272-8a64-4fb8-b708-4e60a1e7f135"),
}
STOCKS = {
    "GHNI": "Ghandhara Industries", "SYS": "Systems Limited", "SAZEW": "Sazgar Engineering Works",
    "SRVI": "Service Industries", "PIOC": "Pioneer Cement", "JVDC": "Javedan Corporation", "MARI": "Mari Energies",
    "FATIMA": "Fatima Fertilizer", "MEBL": "Meezan Bank", "DCR": "Dolmen City REIT",
}
# Indices need no payout data; the app also uses MIF, whose 2005-2013 payouts are estimated in its database only
OUTLOOK = ["KSE100", "KMI30", "MGF", "USDPKR"]
HORIZONS = [("1 month", 21), ("6 months", 126), ("1 year", 252)]
NEWS_QUERIES = [
    '"KSE-100" OR "Pakistan Stock Exchange" OR PSX Pakistan',
    "Pakistan inflation OR \"policy rate\" OR \"State Bank\"",
    "Pakistan IMF OR budget OR reserves",
    "rupee dollar interbank",
    "gold price Pakistan tola",
    "gold price ounce OR bullion",
    "Meezan OR \"mutual funds\" Pakistan",
] + [f'"{name}"' for name in STOCKS.values()]

RESEARCH_FINDINGS = [
    "Technical buy/sell signals, the golden cross and news mood were tested on 2005-2026 prices with results written down in advance: none beat simply holding Meezan Islamic Fund; news describes moves after they happen rather than predicting them.",
    "Trading volume was tested the same way: it made the noisy technical signal less bad but no volume rule beat holding.",
    "The one rule that passed both its test periods is 'stronger than cash': hold stocks while their last 12 months beat the Meezan Cash Fund, otherwise wait in cash, checked monthly. It kept similar returns while cutting the worst fall from about -60% to about -25%. It was tested on stocks, not gold.",
    "Mixing stocks, gold and cash (e.g. 60/20/20) gave up a little return for much smaller falls than any buy/sell rule, 2020-2026.",
]


def get(path):
    request = urllib.request.Request(API + path, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(request, timeout=90) as response:
        return json.load(response)["response"]


def series(rows, field="price"):
    """[(date, value)] sorted, positive values only, one per day."""
    by_day = {}
    for r in rows:
        value = r.get(field)
        if value and value > 0:
            by_day[r["date"][:10]] = float(value)
    return sorted(by_day.items())


def remove_spikes(points):
    """One-day reversals are data errors (PriceDataCleaner)."""
    if len(points) < 3:
        return points
    out = [points[0]]
    for i in range(1, len(points) - 1):
        prev, cur, nxt = out[-1][1], points[i][1], points[i + 1][1]
        jump, back = cur / prev - 1, nxt / cur - 1
        if not (abs(jump) > 0.15 and abs(back) > 0.15 and (jump > 0) != (back > 0) and abs(nxt / prev - 1) < 0.05):
            out.append(points[i])
    out.append(points[-1])
    return out


def adjust_splits(points):
    """Company shares: rescale across splits and bonus issues (StockSplitAdjuster)."""
    points = remove_spikes(points)
    def steady(window, price):
        values = sorted(v for _, v in window)
        return bool(values) and abs(price / values[len(values) // 2] - 1) <= 0.10
    factor, out = 1.0, [None] * len(points)
    for i in range(len(points) - 1, -1, -1):
        out[i] = (points[i][0], points[i][1] * factor)
        if i == 0:
            break
        (d0, p0), (d1, p1) = points[i - 1], points[i]
        gap = (datetime.fromisoformat(d1) - datetime.fromisoformat(d0)).days
        ratio = p1 / p0
        if (ratio < 0.85 or ratio > 1.18) and p0 >= 20 and gap <= 7 \
                and steady(points[max(0, i - 5):i], p0) and steady(points[i:i + 5], p1):
            factor *= ratio
    return out


def reinvest_payouts(points, payouts):
    """Funds: treat each cash payout as reinvested (PayoutAdjuster), so returns are comparable."""
    out = list(points)
    for date, amount in sorted(payouts):
        index = next((i for i, (d, _) in enumerate(out) if d >= date), None)
        if index is None or index == 0:
            continue
        before = out[index - 1][1]
        factor = (before - amount) / before if before > amount else 1
        out = [(d, v * factor) if i < index else (d, v) for i, (d, v) in enumerate(out)]
    return out


def on_or_before(points, day):
    value = None
    for d, v in points:
        if d > day:
            break
        value = v
    return value


def change(points, days):
    if not points:
        return None
    last_day = datetime.fromisoformat(points[-1][0])
    then = on_or_before(points, (last_day - timedelta(days=days)).date().isoformat())
    return round((points[-1][1] / then - 1) * 100, 2) if then else None


def sma(values, end, length):
    return None if end + 1 < length else sum(values[end - length + 1:end + 1]) / length


def trend_states(points):
    """Up, Up but weakening, Down, Down but recovering (golden cross + TrendPhase), per day."""
    values = [v for _, v in points]
    states = []
    for i, price in enumerate(values):
        fast, slow = sma(values, i, 50), sma(values, i, 200)
        if fast is None or slow is None:
            states.append(None)
            continue
        direction = "Up" if fast >= slow else "Down"
        if direction == "Down" and price > fast and price > slow:
            states.append("Down but recovering")
        elif direction == "Up" and price < fast and price < slow:
            states.append("Up but weakening")
        else:
            states.append(direction)
    return states


def stats(changes):
    if not changes:
        return {"days": 0}
    s = sorted(changes)
    at = lambda q: s[round(q * (len(s) - 1))]
    return {"days": len(s), "shareHigherPercent": round(100 * sum(c > 0 for c in s) / len(s)),
            "medianPercent": round(at(0.5), 1), "low10Percent": round(at(0.1), 1), "high90Percent": round(at(0.9), 1)}


def outlook(points):
    """What followed earlier days in today's trend state (HistoricalOutlook)."""
    states = trend_states(points)
    today = states[-1] if states else None
    if today is None:
        return None
    values = [v for _, v in points]
    spells = sum(1 for i, s in enumerate(states) if s == today and (i == 0 or states[i - 1] != today))
    result = {"state": today, "historyFrom": points[0][0], "historyTo": points[-1][0], "spells": spells, "horizons": []}
    for label, h in HORIZONS:
        same, every = [], []
        for i in range(len(values) - h):
            if states[i] is None:
                continue
            c = (values[i + h] / values[i] - 1) * 100
            every.append(c)
            if states[i] == today:
                same.append(c)
        result["horizons"].append({"label": label, "tradingDays": h, "sameState": stats(same), "allDays": stats(every)})
    return result


def summary(symbol, name, kind, points):
    states = trend_states(points)
    values = [v for _, v in points]
    window = values[-252:]
    peak, worst = 0, 0
    for v in window:
        peak = max(peak, v)
        worst = min(worst, v / peak - 1)
    return {
        "symbol": symbol, "name": name, "kind": kind, "lastDate": points[-1][0], "lastPrice": round(points[-1][1], 4),
        "changePercent": {"day": change(points, 1) if len(points) < 2 else round((values[-1] / values[-2] - 1) * 100, 2),
                          "week": change(points, 7), "month": change(points, 30), "threeMonths": change(points, 91),
                          "year": change(points, 365)},
        "high52W": round(max(window), 2), "low52W": round(min(window), 2), "worstFall1YPercent": round(worst * 100, 1),
        "trend": states[-1],
    }


# The app's filter for pages that aren't news (RssNewsFetcher.Excluded)
EXCLUDED = re.compile(
    r"Phillips 66|\$PSX|NYSE|Live Chart|Index Today|Price Today|Constituents|Historical Data|News & Updates|Market Roundup$|"
    r"Live Gold Price|Live Price|Rates on |^\w+ (Technical Analysis|Competitors|Company Announcements|Stock Discussion|"
    r"Financial Results|Share Price|Stock Price|Dividend History|Company Profile)\b", re.IGNORECASE)


def headlines():
    seen, items = set(), []
    since = datetime.now(timezone.utc) - timedelta(days=7)
    for query in NEWS_QUERIES:
        url = "https://news.google.com/rss/search?q=" + urllib.parse.quote(query + " when:7d") + "&hl=en-PK&gl=PK&ceid=PK:en"
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30) as r:
                root = ET.fromstring(r.read())
        except Exception as error:  # a failed search leaves out that topic, not the brief
            print(f"news search failed: {query}: {error}", file=sys.stderr)
            continue
        for item in list(root.iter("item"))[:12]:
            title = (item.findtext("title") or "").strip()
            key = re.sub(r"\W+", "", title.lower())
            if not title or key in seen or EXCLUDED.search(title):
                continue
            seen.add(key)
            published = item.findtext("pubDate") or ""
            items.append({"date": published[5:16], "title": title, "search": query})
    return items[:60]


def main():
    instruments, histories = [], {}

    for symbol, name in [("KSE100", "KSE-100 Index"), ("KMI30", "KMI-30 Index (Shariah)")]:
        points = remove_spikes(series(get(f"/indices/price-history/{symbol}?days=10000")))
        histories[symbol] = points
        instruments.append(summary(symbol, name, "Stock index", points))
    for symbol, (name, fund_id) in FUNDS.items():
        payouts = [(p["payoutDate"][:10], float(p["payoutPerUnit"])) for p in get(f"/mutual-funds/payouts/{fund_id}")
                   if p.get("payoutPerUnit") and p.get("payoutDate")]
        points = reinvest_payouts(remove_spikes(series(get(f"/mutual-funds/nav-history/{fund_id}?days=10000"), "nav")), payouts)
        histories[symbol] = points
        instruments.append(summary(symbol, name, "Cash fund" if symbol == "MCF" else "Gold fund" if symbol == "MGF" else "Stock fund", points))
    gold = remove_spikes(series(get("/commodities/price/gold?days=2500")))
    instruments.append(summary("GOLDPK", "Gold in Pakistan (rupees per tola)", "Gold price", gold))
    dollar = remove_spikes(series(get("/forex/price-history/USDPKR?days=10000")))
    histories["USDPKR"] = dollar
    instruments.append(summary("USDPKR", "US dollar in rupees (up = weaker rupee)", "Exchange rate", dollar))
    for symbol, name in STOCKS.items():
        points = adjust_splits(series(get(f"/stocks/price-history/{symbol}?days=10000")))
        instruments.append(summary(symbol, name, "Company share (Shariah-compliant)", points))

    # Trading volume: last month's daily average against the last five months'
    volume = {}
    for symbol in ["KSE100", "KMI30"]:
        shares = [v for _, v in series(get(f"/indices/volume-history/{symbol}?days=200"), "volume")]
        if len(shares) >= 100:
            recent, usual = sum(shares[-20:]) / 20, sum(shares[-100:]) / 100
            volume[symbol] = {"average20": round(recent), "average100": round(usual), "recentVersusUsualPercent": round(recent / usual * 100, 1)}

    # Stronger than cash: MIF's 12 months against the cash fund's, as at the first trading day of this month
    cash, mif = histories["MCF"], histories["MIF"]
    month_start = min(d for d, _ in mif if d[:7] == mif[-1][0][:7])
    year_ago = (datetime.fromisoformat(month_start) - timedelta(days=365)).date().isoformat()
    mif_12m = on_or_before(mif, month_start) / on_or_before(mif, year_ago) - 1
    cash_12m = on_or_before(cash, month_start) / on_or_before(cash, year_ago) - 1
    stronger = {"checkedOn": month_start, "mif12MPercent": round(mif_12m * 100, 2), "cash12MPercent": round(cash_12m * 100, 2),
                "aheadOfCash": mif_12m > cash_12m}

    facts = {
        "writtenOn": datetime.now(timezone.utc).date().isoformat(),
        "notes": [
            "Prices are daily closes from Sarmaaya. Fund prices include payouts reinvested; index prices leave out dividends; company share prices are adjusted for splits and bonus issues.",
            "Percent figures are already percentages (5.2 means +5.2%).",
            "GOLDPK is rupees per tola, USDPKR rupees per dollar (up = weaker rupee). MCF is what cash earns.",
            "Gathered in the cloud without the reader's app; news mood is not included, read the headlines instead.",
        ],
        "instruments": instruments,
        "tradingVolume": volume,
        "strongerThanCash": stronger,
        "whatFollowedSimilarTrends": {s: outlook(histories[s]) for s in OUTLOOK if s in histories},
        "headlines": headlines(),
        "researchFindings": RESEARCH_FINDINGS,
    }
    json.dump(facts, sys.stdout, indent=1)


if __name__ == "__main__":
    main()
