# Setup Checklist - Market Analytics Platform

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.x | All projects target `net10.0` |
| Node.js | 20+ | Tested with 24.x |
| PostgreSQL | 16+ | Tested with 17.7 (portable binaries, see below) |
| EF Core tools | any | `dotnet tool install --global dotnet-ef` |

## 1. Database password (user secrets)

The Api and Worker share one user-secrets store, so set the connection string once:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=MarketAnalytics;Username=postgres;Password=<your-password>" --project backend/MarketAnalytics.Api
```

## 2. PostgreSQL

### Option A: portable binaries (no admin rights, no Windows service)

1. Download the Windows x64 binaries zip from
   https://www.enterprisedb.com/download-postgresql-binaries (e.g. `postgresql-17.7-1-windows-x64-binaries.zip`)
   and extract it to `%LOCALAPPDATA%\Programs\postgresql-17` (it contains a `pgsql` folder).
2. Initialise a cluster once, using the same password as the user secret
   (put the password alone in a temporary file, then delete it):
   ```
   set PG=%LOCALAPPDATA%\Programs\postgresql-17\pgsql
   "%PG%\bin\initdb" -D "%PG%\data" -U postgres -A scram-sha-256 --pwfile=pw.txt -E UTF8 --locale=C
   ```
3. Start the server (needed after every reboot):
   ```
   "%PG%\bin\pg_ctl" -D "%PG%\data" -l "%PG%\postgres.log" start
   ```
   Stop it with `"%PG%\bin\pg_ctl" -D "%PG%\data" stop`. Check it with `"%PG%\bin\pg_isready" -h localhost`.
4. Create the database once:
   ```
   "%PG%\bin\psql" -h localhost -U postgres -c "CREATE DATABASE \"MarketAnalytics\";"
   ```

### Option B: regular installer

Install PostgreSQL from https://www.postgresql.org/download/windows/ (requires admin), set the `postgres`
password to match the user secret, and create the `MarketAnalytics` database.

## 3. Database schema

From the `backend` folder:

```
dotnet ef database update --project MarketAnalytics.Infrastructure --startup-project MarketAnalytics.Api
```

This creates the tables and seeds the 9 instruments.

## 4. Load market data (Worker)

```
cd backend/MarketAnalytics.Worker
dotnet run
```

The Worker fetches data for every instrument on startup, then again every 24 hours while it keeps running.
The first run loads about a year of history: from sarmaaya.pk for KSE100, KMI30, KMIF, MIF, MBF, MGF, USDPKR
and GOLDPK, and from the free, keyless [fawazahmed0 currency-api](https://github.com/fawazahmed0/exchange-api)
daily snapshots for **GOLDINT (XAU/USD)**. (Investing.com, the original GOLDINT source, blocks automated
requests with HTTP 403.)

### Optional: import older history from CSV

The APIs only serve about a year. To load several years more (e.g. from the psx-trading-analysis project's
`data/prices` folder, with files named `<SYMBOL>.csv` and a `date,price` header):

```
cd backend/MarketAnalytics.Worker
dotnet run -- --import-csv D:\repos\psx-trading-analysis\data\prices
```

The import only adds dates that aren't already stored, so prices from the live sources always win, and it
exits when done. It is safe to run again.

## 5. Run the API and frontend

```
cd backend/MarketAnalytics.Api
dotnet run --urls http://localhost:5000
```

```
cd frontend
npm install
npm run dev
```

Open http://localhost:3000. The Vite dev server proxies `/api` to the API on port 5000.

## Start and stop everything (after setup)

Double-click **`start-app.cmd`** in the repo root. It starts PostgreSQL, builds the backend, starts the API, Worker
and website in the background, and opens http://localhost:3000. It keeps running after you close the window, and
anything already running is left alone. **`stop-app.cmd`** stops it all. Logs are in the `logs` folder.

It doesn't start automatically after a reboot, and .NET updates may close the API and Worker; run `start-app.cmd` again.

## Tests

```
cd backend
dotnet test
```

Covers the indicator maths (Bollinger, EMA, RSI, MACD, SMA, weekly resampling) and the analytics service
(returns, drawdown, volatility, spike filter, signal scoring).

## Pages

- **Dashboard (Market Overview)**: a plain-language market summary; price, 1D/1M/YTD/1Y change, RSI, 52-week range,
  long-term trend and 7-day news mood for every instrument; sortable columns
- **News**: all collected news with sentiment, filterable by instrument, topic and mood
- **Guide**: plain-language explanation of every number, chart and signal (also available as ⓘ tooltips)
- **Instrument view**: price chart with SMA-20, Bollinger Bands and interval-specific moving averages
  (MA-4W for weekly, MA-6M / MA-12M for monthly). Indicators are computed on the full history before the
  timeframe is applied, so they cover the whole window; each line's latest value is shown in the legend.
  Also shows an "In plain words" summary, the long-term trend, what each indicator shows, performance and risk stats with a
  plain reading of each, RSI / MACD panels, the news mood, the biggest moves with the news around them, and the
  latest news.
- **Comparison**: % change of several instruments on one chart, plus a performance table
- **Track Record**: how acting on the trend label and the old Buy/Sell score would have done against simply holding
- **Settings**: default interval/timeframe (saved in the browser), suggested presets, and data source
  update status

## Market news

The Worker also collects news every hour (`NewsUpdateBackgroundService`) from:

- **Publisher RSS feeds**: Dawn Business, Business Recorder (latest news and markets), Express Tribune Business.
  Articles are tagged with topics (Pakistan stocks, Mutual funds, Interest rates & inflation, Rupee & dollar,
  Economy & IMF, Gold, Oil prices) by keyword rules in `NewsTopicClassifier`; Pakistan-specific topics require a
  Pakistan mention, so foreign stories ("European stocks fall") are not tagged.
- **Per-instrument Google News RSS searches** (`RssNewsFetcher.InstrumentSearches`), last 30 days. A result is
  linked to the instrument only if its title is about it (e.g. "PSX" is also Phillips 66's US ticker), and
  evergreen price pages ("Live Chart", "Constituents") are dropped.

The same story from several sources is stored once (matched on title). Feeds only carry recent items, so the
archive builds up from the first run (which backfills about 30 days).

**Direct vs background**: an article is *direct* for an instrument when its search found it or its main topic is
about it (gold news for gold, rupee news for USD/PKR). Wider topics (interest rates, IMF, oil) are shown as
*background* and are not counted in that instrument's news mood.

**Sentiment** (`NewsSentimentAnalyzer`) is read from the wording: a financial word list (gains, surges, record
high vs sheds, slump, panic selling), reversal phrases ("snaps four-day rally" is negative), title words counting
double. Score -100 to +100; confidence grows with the number of matching words and how much they agree. The
matched words are returned so readers can see why. For USD/PKR the score is flipped when the rupee is the subject
("Rupee gains" means USD/PKR falls). The per-instrument news mood is the confidence-weighted average over 7 days.
A word list can misread headlines such as "PSX ends rally on oil surge"; an LLM would be more accurate but needs a
paid API key.

**Biggest moves**: for each of an instrument's largest daily moves in the last 30 days, the API returns the news
published from the day before to the next trading day (so weekend reports of a Friday move are included).

## Long-term trend label

The headline label on the Overview and Instrument pages is the long-term trend, by the golden cross rule: **Up**
while the 50-period average is above the 200-period average, otherwise **Down** (`LongTermTrendDto`, computed in
`MarketAnalyticsService`). It describes the trend so far and is not a recommendation. The app used to show a
Buy/Hold/Sell score; it was replaced after the backtest below showed that acting on it did worse than holding.

The five indicators remain on the Instrument page as detail, and scoring stays in `SignalRules` so the backtest can
keep reporting the old score:

| Indicator | Points up | Points down |
| --- | --- | --- |
| Price vs 200-day average | price above 200-period SMA | below |
| Price vs 50-day average | price above 50-period SMA | below |
| Momentum | MACD above its signal line | below |
| RSI (14) | below 30 (oversold) | above 70 (overbought) |
| Bollinger Bands | below the lower band | above the upper band |

One-day price spikes that immediately reverse (data errors in the sources) are filtered out when prices are read.
Mutual fund NAVs drop when dividends are paid out and are not adjusted for this, so fund returns around payout dates
understate performance.

## Track record (backtest)

`BacktestService` replays the signal over stored daily prices, using the same rules as the live signal
(`SignalRules`). Strategy: invested after a positive score, in cash after a negative one, unchanged on zero.

- No look-ahead: indicators use only prices up to their own day, and trades happen at the next day's close.
- Costs: each trade pays `costPercent` (default 0.5%, adjustable on the page). Holding pays one purchase.
- Testing starts once all five rules can be evaluated (200 periods), and needs at least 250 further days.
- Verdict: **Helped** (at least the return of holding), **LowerRisk** (less return, but worst fall at least a third
  smaller and more return per unit of fall), otherwise **DidNotHelp**. Under 3 years is flagged as too short to judge.
- Limits: prices exclude dividends, cash earns nothing in the main result (a 10% a year variant is also shown), and
  the history is mostly a rising market.

Result on the data as of Nov 2026, at 0.5% per trade: no instrument beat holding, GOLDPK lowered risk at a lower
return, and the other 8 did worse. With no costs, 4 of 9 came out slightly ahead, so frequent trading is a large part
of the shortfall. The rules were deliberately not tuned to improve this, since that would only fit them to the past.

Two slower rules, fixed before seeing their results, are tested the same way: **golden cross** (invested while the
50-period average is above the 200-period) and **monthly trend check** (on the first trading day of each month,
invested if the price is above its 200-period average). At 0.5% per trade both beat the current signal on 8 of 9
instruments by trading far less, but mostly matched or trailed holding (KSE-100: golden cross +26.8% a year against
+26.6% holding and +13.4% for the signal). They make few trades, so this is weak evidence. Golden cross became the app's trend label; it was
picked after seeing this test, so its figures may flatter it.

## Nice to have

- **Dividend-adjusted fund returns**: needs payout data for the Meezan funds
- **Alerts**: notify when an instrument's signal changes

- **Error handling**: global exception middleware and more detailed error responses
- **Validation**: FluentValidation for API request models
- **Testing**: unit tests for indicator/resampling services, API integration tests, frontend component tests
- **Logging**: structured logging (Serilog)
