# PostgreSQL Migration Guide

## Prerequisites

1. Install PostgreSQL (version 12 or later)
2. Create database and user:

```sql
CREATE DATABASE copytrading;
CREATE USER copytrading_user WITH PASSWORD 'copytrading_password';
GRANT ALL PRIVILEGES ON DATABASE copytrading TO copytrading_user;
```

## Running Migrations

### Option 1: Using psql

```bash
psql -h localhost -U copytrading_user -d copytrading -f 001_InitialSchema.sql
```

### Option 2: Using pgAdmin

1. Open pgAdmin
2. Connect to your PostgreSQL server
3. Right-click on the `copytrading` database
4. Select Query Tool
5. Open and execute `001_InitialSchema.sql`

### Option 3: Using docker-compose

If you're using the provided docker-compose.yml:

```bash
docker-compose up -d postgres
docker-compose exec postgres psql -U copytrading_user -d copytrading -f /migrations/001_InitialSchema.sql
```

## Configuration

Update `appsettings.json` with your PostgreSQL connection details:

```json
{
  "PostgreSQL": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "copytrading",
    "Username": "copytrading_user",
    "Password": "copytrading_password"
  },
  "DatabaseProvider": "PostgreSQL"
}
```

## Verification

After running the migration, verify tables were created:

```sql
\dt
```

You should see:
- Orders
- Trades
- MinPerpEquityForTrades
- MinPerpEquityForOrders
- WalletSnapshotPositions
- WalletSettings
- Logs
