"""Independent re-calculation of the technical signal, written without the app's code, compared with the app's API."""
import json
import math
import urllib.request

API = "http://localhost:5000/api"
SYMBOLS = ["KSE100", "KMI30", "KMIF", "MIF", "MBF", "GOLDINT", "GOLDPK", "MGF", "USDPKR"]


def get(path):
    with urllib.request.urlopen(API + path, timeout=120) as r:
        return json.load(r)


def sma(values, n):
    out = [None] * len(values)
    for i in range(n - 1, len(values)):
        out[i] = sum(values[i - n + 1:i + 1]) / n
    return out


def ema(values, n):
    # pandas ewm(span=n, adjust=False): seeded with the first value
    k = 2 / (n + 1)
    out = [values[0]]
    for v in values[1:]:
        out.append(v * k + out[-1] * (1 - k))
    return out


def rsi_wilder(values, n=14):
    out = [None] * len(values)
    gains = [max(values[i] - values[i - 1], 0) for i in range(1, len(values))]
    losses = [max(values[i - 1] - values[i], 0) for i in range(1, len(values))]
    if len(gains) < n:
        return out
    avg_gain = sum(gains[:n]) / n
    avg_loss = sum(losses[:n]) / n
    def value(g, l):
        return 100.0 if l == 0 else 100 - 100 / (1 + g / l)
    out[n] = value(avg_gain, avg_loss)
    for i in range(n, len(gains)):
        avg_gain = (avg_gain * (n - 1) + gains[i]) / n
        avg_loss = (avg_loss * (n - 1) + losses[i]) / n
        out[i + 1] = value(avg_gain, avg_loss)
    return out


def bollinger(values, n=20, k=2):
    mid = sma(values, n)
    upper, lower = [None] * len(values), [None] * len(values)
    for i in range(n - 1, len(values)):
        window = values[i - n + 1:i + 1]
        sd = math.sqrt(sum((v - mid[i]) ** 2 for v in window) / (n - 1))
        upper[i], lower[i] = mid[i] + k * sd, mid[i] - k * sd
    return upper, lower


def votes(price, s200, s50, hist, rsi, pct_b):
    v = (1 if price >= s200 else -1) + (1 if price >= s50 else -1) + (1 if hist >= 0 else -1)
    v += 1 if rsi < 30 else -1 if rsi > 70 else 0
    v += 1 if pct_b < 0 else -1 if pct_b > 1 else 0
    return v


def action(score):
    return "StrongBuy" if score >= 60 else "Buy" if score >= 20 else "StrongSell" if score <= -60 else "Sell" if score <= -20 else "Hold"


analytics = {a["symbol"]: a for a in get("/analytics")}
problems = 0
print(f"{'':8} {'SMA200':>12} {'SMA50':>12} {'RSI':>12} {'MACD hist':>16} {'%B':>12} {'votes':>9} {'steady':>11} {'trend':>9}")
for symbol in SYMBOLS:
    rows = get(f"/prices/{symbol}?interval=0&timeframe=7")
    prices = [float(r["price"]) for r in rows]
    s200, s50 = sma(prices, 200), sma(prices, 50)
    e12, e26 = ema(prices, 12), ema(prices, 26)
    macd = [a - b for a, b in zip(e12, e26)]
    signal = ema(macd, 9)
    hist = [m - s for m, s in zip(macd, signal)]
    rsi = rsi_wilder(prices)
    up, lo = bollinger(prices)
    pct_b = [None if up[i] is None or up[i] == lo[i] else (prices[i] - lo[i]) / (up[i] - lo[i]) for i in range(len(prices))]

    daily = []
    for i in range(len(prices)):
        if None in (s200[i], s50[i], rsi[i], up[i]):
            continue
        daily.append(votes(prices[i], s200[i], s50[i], hist[i], rsi[i], pct_b[i]))
    steady = sum(daily[-20:]) / len(daily[-20:]) * 20
    trend = "Up" if s50[-1] >= s200[-1] else "Down"

    a = analytics[symbol]
    mine = [s200[-1], s50[-1], rsi[-1], hist[-1], pct_b[-1], daily[-1], round(steady), trend]
    app = [a["sma200"], a["sma50"], a["rsi14"], a["macdHistogram"], a["percentB"], a["signal"]["score"], a["steadySignal"]["score"], a["longTermTrend"]["direction"]]
    cells = []
    for m, x in zip(mine, app):
        if isinstance(m, str):
            ok = m == x
            cells.append(f"{m}{'' if ok else ' != ' + str(x)}")
        else:
            ok = abs(m - x) <= max(1e-6, abs(m) * 1e-6) or (isinstance(m, int) and m == x)
            cells.append(f"{m:.4g}{'' if ok else ' != ' + format(x, '.4g')}")
        problems += 0 if ok else 1
    print(f"{symbol:8} " + " ".join(f"{c:>12}" for c in cells))
    print(f"{'':8} today's votes: price vs 200d {'+1' if prices[-1] >= s200[-1] else '-1'}, vs 50d {'+1' if prices[-1] >= s50[-1] else '-1'}, "
          f"MACD {'+1' if hist[-1] >= 0 else '-1'}, RSI {rsi[-1]:.0f}, %B {pct_b[-1]:.2f} -> {daily[-1]:+d}; last 20 days of votes: {daily[-20:]}; steady {steady:+.0f} = {action(steady)}")
print(f"\nMismatches with the app: {problems}")
