# Setup Checklist - Market Analytics Platform

## ✅ Completed

- Backend project structure (Clean Architecture)
- Domain layer (enums, entities, value objects)
- Application layer (services, DTOs, interfaces)
- Infrastructure layer (EF Core, repositories, data fetchers)
- API controllers (Instruments, Prices, Summary)
- Background worker service
- React frontend structure and components
- Dependency injection configuration

## 🔧 Critical Items Remaining

### 1. **Fix Project References** ⚠️ REQUIRED
   - **Issue**: Infrastructure repositories implement `IInstrumentRepository` and `IPricePointRepository` from Application layer, but Infrastructure doesn't reference Application
   - **Fix**: Add project reference from `MarketAnalytics.Infrastructure` to `MarketAnalytics.Application`
   - **File**: `backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj`

### 2. **Worker Configuration** ⚠️ REQUIRED
   - **Issue**: Worker `appsettings.json` missing connection string
   - **Fix**: Add `ConnectionStrings` section to match API appsettings
   - **File**: `backend/MarketAnalytics.Worker/appsettings.json`

### 3. **Database Migrations** ⚠️ REQUIRED
   - **Status**: EF Core configured but migrations not created
   - **Steps**:
     1. Install EF Core tools: `dotnet tool install --global dotnet-ef`
     2. Add migration: `dotnet ef migrations add InitialCreate --project backend/MarketAnalytics.Infrastructure --startup-project backend/MarketAnalytics.Api`
     3. Update database: `dotnet ef database update --project backend/MarketAnalytics.Infrastructure --startup-project backend/MarketAnalytics.Api`

### 4. **Database Setup** ⚠️ REQUIRED
   - Install PostgreSQL
   - Create database: `CREATE DATABASE MarketAnalytics;`
   - Update connection string in `appsettings.json` files if needed

### 5. **Frontend Dependencies** ⚠️ REQUIRED
   - Run: `cd frontend && npm install`
   - This installs React, Recharts, Axios, and other dependencies

## 📝 Nice to Have (Not Critical for Basic Functionality)

### 6. **Error Handling Enhancement**
   - Global exception middleware
   - More detailed error responses
   - Currently has basic try-catch in controllers

### 7. **Validation**
   - FluentValidation for API request models
   - Input validation attributes
   - Currently relies on basic parameter validation

### 8. **Testing**
   - Unit tests for services
   - Integration tests for API endpoints
   - Frontend component tests

### 9. **Logging Enhancement**
   - Structured logging (Serilog)
   - Log levels configuration
   - Currently uses built-in ILogger

### 10. **Documentation**
   - API Swagger documentation (partially configured)
   - Setup instructions in README
   - Architecture documentation

## 🚀 Quick Start Steps

1. **Fix project reference** (Item #1 above)
2. **Add connection string to Worker** (Item #2 above)
3. **Install PostgreSQL and create database**
4. **Run database migrations** (Item #3 above)
5. **Install frontend dependencies** (Item #5 above)
6. **Run API**: `cd backend/MarketAnalytics.Api && dotnet run`
7. **Run Worker**: `cd backend/MarketAnalytics.Worker && dotnet run` (optional, for background updates)
8. **Run Frontend**: `cd frontend && npm run dev`
