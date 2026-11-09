"""Checks the news signal: lists each instrument's direct stories of the last 7 days with the app's score, recomputes
the simple share of positive and negative stories, and compares the mood with what prices actually did that week."""
import json
import sys
import urllib.request
from datetime import datetime, timedelta, timezone

API = "http://localhost:5000/api"


def get(path):
    with urllib.request.urlopen(API + path, timeout=120) as r:
        return json.load(r)


moods = {m["symbol"]: m for m in get("/news/sentiment?days=7")}
analytics = {a["symbol"]: a for a in get("/analytics")}
since = datetime.now(timezone.utc) - timedelta(days=7)

for symbol in sys.argv[1:]:
    articles = get(f"/news?symbol={symbol}&days=8&limit=500")
    direct = []
    for a in articles:
        published = datetime.fromisoformat(a["publishedAt"].replace("Z", "+00:00"))
        mine = next((i for i in a["instruments"] if i["symbol"] == symbol), None)
        if mine and mine["isDirect"] and published >= since:
            direct.append((published, mine["sentimentScore"], a["title"]))
    pos = sum(1 for _, s, _ in direct if s >= 20)
    neg = sum(1 for _, s, _ in direct if s <= -20)
    mean = sum(s for _, s, _ in direct) / len(direct) if direct else 0
    m, a = moods[symbol], analytics[symbol]
    print(f"\n=== {symbol}: {len(direct)} direct stories in 7 days ({pos} positive, {neg} negative, {len(direct) - pos - neg} neutral); "
          f"plain average {mean:+.0f}; app mood {m['score']:+d} from {m['articles']} stories, {m['confidence']}% sure; news signal {a['newsSignal']['action']} {a['newsSignal']['score']:+d}")
    print(f"    price: 1 week {a['change1W']:+.2f}%, 1 month {a['change1M']:+.2f}%")
    # The strongest-scored stories each way, to read by hand
    for published, score, title in sorted(direct, key=lambda x: -abs(x[1]))[:14]:
        print(f"    {published:%m-%d} {score:+4d}  {title[:110]}")
