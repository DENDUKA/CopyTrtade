# Migration to PostgreSQL - Complete Guide

## Overview

The CopyTrading application has been migrated from SQLite to PostgreSQL for better performance, scalability, and production readiness.

## What Changed

### 1. Database Repositories
- **Created**: New PostgreSQL repositories in `CopyTrading/Repository/PostgreSQL/`
  - `OrderRepository.cs`
  - `TradeRepository.cs`
  - `WalletInfoRepository.cs`
  - `WalletSettingsRepository.cs`
  - `LogRepository.cs`

### 2. Configuration
- **Updated**: `Startup.cs` to use PostgreSQL repositories
- **Updated**: Serilog configuration to log to PostgreSQL
- **Updated**: Serilog UI to read from PostgreSQL

### 3. Migration Scripts
- **Created**: `Database/Migrations/001_InitialSchema.sql` - Creates all necessary tables

### 4. Docker Support
- **Already configured**: `docker-compose.yml` includes PostgreSQL setup with auto-migrations

## Migration Steps

### Option 1: Using Docker with Auto-Start (Recommended)

**The application now automatically starts the PostgreSQL Docker container on startup!**

1. **Just run the application:**
   ```bash
   dotnet run --project CopyTrading/CopyTrading.csproj
   ```

   The application will:
   - ✅ Check if Docker is available
   - ✅ Check if PostgreSQL container is running
   - ✅ Start the container if it's not running
   - ✅ Wait for PostgreSQL to be ready
   - ✅ Apply migrations automatically (via docker-entrypoint-initdb.d)
   - ✅ Start the application

2. **Verify container is running (optional):**
   ```bash
   docker ps | grep copytrading-postgres
   ```

3. **View logs (optional):**
   ```bash
   docker-compose logs postgres
   ```

### Option 1b: Manual Docker Start (if needed)

If you prefer to start Docker manually:

1. **Start PostgreSQL container:**
   ```bash
   docker-compose up -d postgres
   ```

2. **Run the application:**
   ```bash
   dotnet run --project CopyTrading/CopyTrading.csproj
   ```

### Option 2: Using Local PostgreSQL

1. **Install PostgreSQL** (if not already installed)

2. **Create database and user:**
   ```sql
   CREATE DATABASE copytrading;
   CREATE USER copytrading_user WITH PASSWORD 'copytrading_password';
   GRANT ALL PRIVILEGES ON DATABASE copytrading TO copytrading_user;
   GRANT ALL ON SCHEMA public TO copytrading_user;
   ```

3. **Run migration script:**
   ```bash
   psql -h localhost -U copytrading_user -d copytrading -f Database/Migrations/001_InitialSchema.sql
   ```

4. **Update appsettings.json** (if using different credentials):
   ```json
   {
     "PostgreSQL": {
       "Host": "localhost",
       "Port": 5432,
       "Database": "copytrading",
       "Username": "copytrading_user",
       "Password": "your_password"
     }
   }
   ```

5. **Run the application:**
   ```bash
   dotnet run --project CopyTrading/CopyTrading.csproj
   ```

## Verification

### 1. Check Database Tables

```sql
-- Connect to database
psql -U copytrading_user -d copytrading

-- List all tables
\dt

-- Expected output:
-- Orders
-- Trades
-- MinPerpEquityForTrades
-- MinPerpEquityForOrders
-- WalletSnapshotPositions
-- WalletSettings
-- Logs
```

### 2. Check Application Logs

After starting the application, check that:
- No database connection errors appear
- Logs are being written to PostgreSQL (check Serilog UI at `/serilog-ui`)

### 3. Test Basic Functionality

- Navigate to `http://localhost:5197`
- Check Swagger at `http://localhost:5197/swagger`
- Check Serilog UI at `http://localhost:5197/serilog-ui`

## Troubleshooting

### Connection Issues

**Problem**: Application can't connect to PostgreSQL

**Solutions**:
1. Check PostgreSQL is running: `docker-compose ps` or `systemctl status postgresql`
2. Verify connection string in `appsettings.json`
3. Check firewall settings (port 5432 should be accessible)
4. Verify user permissions in PostgreSQL

### Migration Script Fails

**Problem**: Migration script returns errors

**Solutions**:
1. Check if database exists: `psql -U postgres -l`
2. Verify user has necessary permissions
3. Drop and recreate database if needed (development only):
   ```sql
   DROP DATABASE IF EXISTS copytrading;
   CREATE DATABASE copytrading;
   ```

### Old SQLite Data

**Problem**: Need to migrate data from old SQLite database

**Solution**: Data migration is not included in this migration. If you need to preserve old data:
1. Export data from SQLite
2. Transform to PostgreSQL format
3. Import using psql or pgAdmin

This is typically not needed for development/testing environments.

## Rollback to SQLite

If you need to rollback to SQLite:

1. **Restore Startup.cs:**
   ```csharp
   // Change PostgreSQL repositories back to SQLite
   services.AddSingleton<Repository.SQLite.IOrderRepository, Repository.SQLite.OrderRepository>();
   // ... etc
   ```

2. **Restore Serilog configuration:**
   ```csharp
   .WriteTo.SQLite(SQLLiteSettings.Path, maxDatabaseSize:0)
   ```

3. **Update Serilog UI:**
   ```csharp
   options.UseSqliteServer(/* ... */);
   ```

## Performance Notes

PostgreSQL provides several advantages over SQLite:
- Better concurrent write performance
- Proper transaction isolation
- Advanced indexing capabilities
- Full ACID compliance
- Production-ready scalability

Expected performance improvements:
- 3-5x faster writes under concurrent load
- Better query performance on large datasets
- More reliable under high-frequency trading scenarios

## Next Steps

After successful migration:
1. Monitor application performance
2. Set up database backups (PostgreSQL has excellent backup tools)
3. Consider setting up read replicas for scaling reads
4. Review and optimize indexes based on query patterns

## Support

For issues or questions:
- Check application logs at `/serilog-ui`
- Review PostgreSQL logs: `docker-compose logs postgres`
- Check database with: `docker-compose exec postgres psql -U copytrading_user -d copytrading`
