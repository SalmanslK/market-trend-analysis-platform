# Market Trend Analysis Platform

Tracks Pakistani market instruments for ordinary investors: KSE-100 and KMI-30, Meezan funds (MIF, KMIF, MBF, MGF;
cash/income funds MCF, MRAF, MIIF, MMP-I), gold (GOLDPK, GOLDINT) and USD/PKR. It shows prices, rule-based signals,
news mood and honest backtests in plain language. No AI at runtime: everything is rules over closing prices and headlines.

`SETUP_CHECKLIST.md` is the detailed reference for setup, every feature and all research results. Read the relevant
section before changing a feature, and update it when behaviour changes.

## Layout

- `backend/` .NET 10 clean architecture: `Domain` (entities) → `Application` (services, DTOs, signal rules) →
  `Infrastructure` (EF Core/PostgreSQL, data fetchers, `Updates/` shared by Worker and API) → `Api` and `Worker`;
  `Tests` (xUnit). EF migrations apply automatically when the API starts.
- `frontend/` React 18 + TypeScript + Vite + Recharts. `src/services/` holds API clients, types and the plain-language
  text (`plainLanguage.ts`, `answers.ts`, `glossary.ts`); `src/pages/` one file per page.
- `scripts/` start/stop/backup (PowerShell), `research/` (file-based C# scripts run with `dotnet run x.cs`, need the
  `MA_CONN` env var), `verify/` (independent Python checks of the calculations).

## Commands

```bash
dotnet test backend/MarketAnalytics.sln                  # backend tests
cd frontend && npm run lint && npm test && npm run build # what CI runs for the website
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-app.ps1   # DB + API :5000 + Worker + site :3000
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/stop-app.ps1
```

- Set `MARKET_ANALYTICS_NO_BROWSER=1` to start without opening a browser. Call the `.ps1` files directly: the `.cmd`
  wrappers pause and hang automated runs.
- **Stop the app before building the backend**: the running API/Worker lock their DLLs.
- PostgreSQL is a portable install in `%LOCALAPPDATA%\Programs\postgresql-17\pgsql` (not a service). The connection
  string lives only in the Api's user secrets (`dotnet user-secrets list` in `backend/MarketAnalytics.Api`); never
  print or commit the password.
- New migration: `dotnet ef migrations add Name -p MarketAnalytics.Infrastructure -s MarketAnalytics.Api` (from
  `backend/`). Hand-written data migrations need `[DbContext]` and `[Migration("yyyyMMddHHmmss_Name")]` attributes;
  keep migration ids in time order.
- In Git Bash, prefix commands that take "/path" arguments with `MSYS_NO_PATHCONV=1`.

## Rules that matter here

- **Shared code paths.** Prices are always read through `PriceHistory.LoadDailyAsync` / `PriceDataCleaner.RemoveSpikes`
  + `PayoutAdjuster.Adjust` (spikes removed, fund payouts reinvested). Signals come from `SignalRules`, `SignalHistory`
  and `IndicatorSeries`, so the live app, backtests, alerts and research all agree. Don't re-implement them.
- **No look-ahead.** A decision made at a day's close acts from the next day; forward moves start at the next close.
- **Forward record is append-only in time.** `SignalRecorder` may update today's reading until the next price arrives
  but must never write a reading for an older date.
- **Low-risk funds** (`Instrument.LowRiskKind`: "Cash fund"/"Income fund") get no signals or backtests.
- **News is optional** (`Features:News` in Api `appsettings.json`); the website hides news when it is off.

## Research discipline (important to the user)

Future prediction matters, so results must be honest, not overfit:
- Write the hypothesis and the pass mark in the script header and **commit it before running**.
- Develop on an early period, keep a later period held back and look at it **once**, only for ideas that pass.
- Label any check added after seeing results as such; it can't change a verdict. Report failures plainly.
- Still-unused held-back periods: news after 2025-03-15; policy-rate test 2016 on. Findings so far are in
  `SETUP_CHECKLIST.md` (only "stronger than cash" has passed; news describes the past but doesn't predict).

## Writing for users

- UI text is for non-experts: plain words, the answer first, then detail. Every number or term gets an ⓘ tip via
  `glossary.ts`. Signals describe the past; never present them as predictions or advice.
- Pages must work at 390 px wide with no sideways scroll, and keep AA contrast.
- Code style: match the surrounding code; comments explain why, sparingly. Plain-language helpers get Vitest tests.

## Working with the user

- Explain changes and results in plain words, with effort estimates for proposed work.
- Branch from `main`, open PRs to `main`, end commits with the co-author line. Don't merge PRs unless asked, and don't
  delete a base branch before the PR stacked on it is retargeted.
