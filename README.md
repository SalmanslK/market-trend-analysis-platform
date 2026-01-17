# 🌐 Market Data Analytics Web Platform

## Overview

This project is a **web-based Market Data Analytics & Technical Analysis Platform** that provides
real-time and historical trend analysis for multiple financial instruments.

It is a modernization of an existing Python-based analytics pipeline, now re-architected into a:

- **Backend API** using **ASP.NET Core (.NET)**
- **Frontend UI** using **React**
- **Persistent storage** using a relational database
- **Chart-based visualization** for market intelligence

The system focuses on **analysis, visualization, and decision support** — not automated trading.

---

## 🎯 Core Capabilities

- Multi-asset market data ingestion
- Incremental historical data updates
- Time-series cleaning & normalization
- Interval-based resampling
- Timeframe filtering (Week, Month, Quarter, Year, All)
- Technical indicators:
  - SMA-20
  - Bollinger Bands
  - Interval-aware moving averages
- Interactive charts
- Cross-instrument summary views
- Secure & extensible API-first architecture

---

## 🧩 System Classification

**Type:**  
Market Data ETL & Technical Analysis Web Platform

**Also known as:**
- Market Intelligence System
- Financial Time-Series Analytics Platform
- Technical Analysis Visualization System

---

## 🏗️ Architecture

Frontend (React)
↓ REST API
Backend (ASP.NET Core)
↓
Data Fetchers / Scrapers
↓
Analytics Engine
↓
Database (Time-Series Data)

yaml
Copy code

---

## 🖥️ Frontend (React)

### Responsibilities
- Instrument selection
- Interval & timeframe controls
- Chart rendering
- Summary dashboards
- User-friendly visual analytics

### Tech Stack
- React
- TypeScript
- Charting (Recharts / Chart.js / ECharts)
- Axios
- Tailwind / MUI (optional)

---

## ⚙️ Backend (ASP.NET Core)

### Responsibilities
- Market data ingestion (API + scraping)
- Data cleaning & validation
- Resampling & indicator computation
- REST API endpoints
- Background update jobs
- Data persistence

### Tech Stack
- ASP.NET Core Web API
- Entity Framework Core
- Background Services / Hangfire
- HttpClient
- PostgreSQL / SQL Server

---

## 🗄️ Data Storage

- Time-series price/NAV data
- Instrument metadata
- Update logs
- Computed indicator values

---

## 📈 Indicators Supported

| Indicator | Description |
|--------|-------------|
| SMA-20 | Simple Moving Average |
| Bollinger Bands | ±2 Standard Deviations |
| MA-4W | Weekly trend |
| MA-6M | Monthly trend |
| MA-12M | Long-term trend |

---

## ⏱️ Intervals & Timeframes

### Intervals
- Daily
- Weekly
- Monthly
- Quarterly

### Timeframes
- Day
- Week
- Month
- Quarter
- Half-Year
- Year
- YTD
- All

---

## 🔐 Non-Goals

- ❌ No order execution
- ❌ No trading automation
- ❌ No portfolio optimization (yet)

---

## 🚀 Roadmap

- Phase 1: API + charts parity with Python system
- Phase 2: User dashboards & saved views
- Phase 3: Alerts & signals
- Phase 4: Backtesting engine
- Phase 5: Portfolio analytics

---

## 🧠 Ideal Use Cases

- Long-term investing research
- Mutual fund NAV tracking
- Gold vs FX vs index comparison
- Weekly/monthly market reviews
- Personal market intelligence dashboard

---

## 👤 Author

Lead Software Engineer  
Focused on **clean data pipelines, analytics, and long-term decision support**
