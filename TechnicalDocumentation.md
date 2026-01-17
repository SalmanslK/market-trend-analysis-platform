📦 Project Structure
market-analytics-platform/
├── backend/
│   ├── MarketAnalytics.Api/
│   ├── MarketAnalytics.Application/
│   ├── MarketAnalytics.Domain/
│   ├── MarketAnalytics.Infrastructure/
│   └── MarketAnalytics.Worker/
│
├── frontend/
│   ├── src/
│   ├── components/
│   ├── pages/
│   ├── services/
│   └── charts/
│
└── docs/

🧠 Backend Design (Clean Architecture)
Domain Layer

Instrument

PricePoint

IndicatorValue

Application Layer

Resampling logic

Indicator calculations

Timeframe filtering

DTOs

Infrastructure Layer

Database (EF Core)

External API clients

Scrapers

Repositories

API Layer

REST endpoints

Validation

Authentication (future)

🔌 Backend API Endpoints
Instruments
GET /api/instruments

Price Data
GET /api/prices/{instrument}
  ?interval=Daily
  &timeframe=Quarter

Summary
GET /api/summary

📊 Indicator Computation Strategy

Compute indicators on-demand for charts

Cache results for performance

Store raw data only (initially)

🔄 Background Jobs

Daily incremental data updates

Data validation

Error logging

🖼️ Frontend UI Pages
Page	Description
Dashboard	Overview & summary
Instrument View	Detailed chart
Comparison	Multi-instrument
Settings	Preferences
📉 Charting Requirements

Each chart must support:

Price

SMA-20

Bollinger Bands

Moving averages

Tooltip values

Zoom / pan (optional)

🛡️ Data Integrity Rules

No duplicate timestamps

Positive prices only

Valid date ranges

UTC normalization