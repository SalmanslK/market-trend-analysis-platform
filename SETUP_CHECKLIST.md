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
The first run loads about a year of history for the sarmaaya.pk instruments (KSE100, KMI30, KMIF, MIF, MBF,
MGF, USDPKR, GOLDPK).

**GOLDINT (XAU/USD)** comes from gold-api.com's free, keyless real-time endpoint. It returns only the current
spot price, so GOLDINT history grows by one point per day the Worker runs. (Investing.com, the original source,
blocks automated requests with HTTP 403.)

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

## Pages

- **Dashboard**: latest price, SMA-20 and Bollinger Bands for every instrument
- **Instrument view**: price chart with SMA-20, Bollinger Bands and interval-specific moving averages
  (MA-4W for weekly, MA-6M / MA-12M for monthly)
- **Comparison**: % change of several instruments on one chart, plus a performance table
- **Settings**: default interval/timeframe (saved in the browser) and data source update status

## Nice to have

- **Gold history backfill**: gold-api.com's history endpoint needs a free API key (10 requests/hour on the
  free plan). Adding it would let GOLDINT backfill a year of history instead of growing daily.
- **Error handling**: global exception middleware and more detailed error responses
- **Validation**: FluentValidation for API request models
- **Testing**: unit tests for indicator/resampling services, API integration tests, frontend component tests
- **Logging**: structured logging (Serilog)
