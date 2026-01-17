📊 Market Data Analytics & Technical Analysis Pipeline
Overview

This project is a market data analytics and technical analysis system designed to automatically fetch, clean, analyze, and visualize financial time-series data across multiple asset classes.

It supports:

Stock indices

Mutual fund NAVs

Forex (USD/PKR)

Gold (international & local)

The system is analysis-only — it does not execute trades.
Its purpose is trend monitoring, investment research, and market intelligence.

🎯 What This System Does

At a high level, the system:

Fetches historical & incremental market data

Stores data in local Excel files

Creates timestamped backups before overwriting

Cleans and standardizes time-series data

Resamples data by Daily / Weekly / Monthly / Quarterly

Filters data by timeframe (Week, Month, Quarter, Year, All)

Computes technical indicators

Generates publication-ready charts

Produces a cross-instrument summary table

Exports all outputs as images

🧩 System Classification

Type:

Market Data ETL & Technical Analysis System

Also known as:

Market Intelligence System

Financial Time-Series Analytics Pipeline

Technical Analysis & Visualization Engine

📈 Supported Instruments
Category	Examples
Stock Indices	KSE-100, KMI-30
Mutual Funds	MIF, KMIF, MBF, MGF
Forex	USD/PKR
Commodities	Gold (International & Pakistan)

Adding a new instrument requires only one configuration entry.

⚙️ Architecture Overview
Data Source
   ↓
Fetch / Scrape
   ↓
Clean & Validate
   ↓
Store (Excel)
   ↓
Resample & Filter
   ↓
Indicators
   ↓
Charts & Summary
   ↓
Exports

🧠 Indicators & Analytics
Built-in Indicators

SMA-20 (Simple Moving Average)

Bollinger Bands (±2σ)

Interval-Aware Moving Averages
Interval	Moving Averages
Weekly	4-week MA
Monthly	6-month, 12-month MA
🕒 Timeframes Supported
Timeframe	Description
Day	Last 24 hours
Week	Last 7 days
Month	Last 1 month
Quarter	Last 3 months
Half-Year	Last 6 months
Year	Last 12 months
YTD	Year-to-date
All	Full history
📊 Visualization Features

Each instrument chart includes:

Price series

SMA-20

Bollinger Bands

Interval-specific MAs

Last value markers with labels

“Last 3 Prices” info box

Clean, print-ready styling

📁 Output Structure
D:/PriceData/
├── KSE100.xlsx
├── MIF.xlsx
├── USDPKR.xlsx
├── backup/
│   └── 2026-01-17/
│       └── KSE100_20260117_153012.xlsx
└── Exports/
    └── 2026-01-17/
        ├── KSE100-DAILY-QUARTER-2026-01-17.png
        ├── GOLDPK-DAILY-QUARTER-2026-01-17.png
        └── SUMMARY-DAILY-QUARTER-2026-01-17.png

🚀 How to Run
1️⃣ Install dependencies
pip install pandas matplotlib requests beautifulsoup4 openpyxl

2️⃣ Configure instruments

Edit INSTRUMENT_CONFIG to:

Add/remove instruments

Change data sources

Update file paths

3️⃣ Run the script
python main.py

🔧 Configuration Example
interval = "Daily"
timeframe = "Quarter"

# Other examples:
# interval = "Weekly", timeframe = "Year"
# interval = "Monthly", timeframe = "All"

🛡️ Data Safety

Automatic pre-overwrite backups

Duplicate date protection

Invalid data filtering

Date range validation

🚫 What This System Is NOT

❌ Trading bot

❌ Order execution engine

❌ Portfolio optimizer

❌ High-frequency system

It is intentionally decision-support focused.

🔮 Future Extensions (Optional)

This system is designed to evolve into:

Signal generation (trend breaks, volatility squeeze)

Backtesting engine

Relative strength comparisons

Automated reports (PDF / WhatsApp)

Portfolio-level analytics

📌 Ideal Use Cases

Long-term investing research

Mutual fund NAV trend tracking

Gold vs FX vs index comparison

Weekly/monthly market reviews

Personal market intelligence dashboard

👤 Author

Lead Software Engineer & Market Analytics Enthusiast
Focused on clean data, clarity, and long-term decision support.