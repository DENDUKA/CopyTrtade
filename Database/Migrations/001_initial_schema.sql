-- CopyTrading Database Initial Schema
-- Migration: 001
-- Description: Create initial database schema with all tables

-- Create MinPerpEquityForOrders table
CREATE TABLE IF NOT EXISTS "MinPerpEquityForOrders" (
    "OrderId" BIGINT NOT NULL PRIMARY KEY,
    "AccountVolume" DOUBLE PRECISION NOT NULL,
    "MinPE" DOUBLE PRECISION NOT NULL,
    "SubType" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "idx_minpe_orders_subtype" ON "MinPerpEquityForOrders"("SubType");

-- Create MinPerpEquityForTrades table
CREATE TABLE IF NOT EXISTS "MinPerpEquityForTrades" (
    "TradeId" BIGINT NOT NULL PRIMARY KEY,
    "WalletPerpEquity" DOUBLE PRECISION NOT NULL,
    "MinPerpEquityForCopyTrade" DOUBLE PRECISION NOT NULL,
    "Spread" DOUBLE PRECISION NOT NULL,
    "DeltaTimeS" DOUBLE PRECISION NOT NULL,
    "SubType" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "idx_minpe_trades_subtype" ON "MinPerpEquityForTrades"("SubType");

-- Create Orders table
CREATE TABLE IF NOT EXISTS "Orders" (
    "OrderId" BIGINT NOT NULL PRIMARY KEY,
    "Wallet" TEXT NOT NULL,
    "Time" TIMESTAMP NOT NULL,
    "Symbol" TEXT NOT NULL,
    "Direction" TEXT NOT NULL,
    "Price" DOUBLE PRECISION NOT NULL,
    "Size" DOUBLE PRECISION NOT NULL,
    "Value" DOUBLE PRECISION NOT NULL,
    "Status" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "idx_orders_wallet" ON "Orders"("Wallet");
CREATE INDEX IF NOT EXISTS "idx_orders_symbol" ON "Orders"("Symbol");
CREATE INDEX IF NOT EXISTS "idx_orders_time" ON "Orders"("Time");
CREATE INDEX IF NOT EXISTS "idx_orders_status" ON "Orders"("Status");
CREATE INDEX IF NOT EXISTS "idx_orders_wallet_symbol" ON "Orders"("Wallet", "Symbol");

-- Create Trades table
CREATE TABLE IF NOT EXISTS "Trades" (
    "TradeId" BIGINT NOT NULL PRIMARY KEY,
    "Wallet" TEXT NOT NULL,
    "TradeVolume" DOUBLE PRECISION NOT NULL,
    "Symbol" TEXT NOT NULL,
    "Time" TIMESTAMP NOT NULL,
    "OrderId" BIGINT NOT NULL,
    "Direction" TEXT NOT NULL,
    "Price" DOUBLE PRECISION,
    "Quantity" DOUBLE PRECISION
);

CREATE INDEX IF NOT EXISTS "idx_trades_wallet" ON "Trades"("Wallet");
CREATE INDEX IF NOT EXISTS "idx_trades_symbol" ON "Trades"("Symbol");
CREATE INDEX IF NOT EXISTS "idx_trades_time" ON "Trades"("Time");
CREATE INDEX IF NOT EXISTS "idx_trades_orderid" ON "Trades"("OrderId");
CREATE INDEX IF NOT EXISTS "idx_trades_wallet_symbol" ON "Trades"("Wallet", "Symbol");

-- Create WalletSettings table
CREATE TABLE IF NOT EXISTS "WalletSettings" (
    "Wallet" TEXT NOT NULL PRIMARY KEY,
    "ValueUsd" DOUBLE PRECISION NOT NULL,
    "CopyKoef" DOUBLE PRECISION NOT NULL DEFAULT 1
);

-- Create WalletSnapshotPositions table
CREATE TABLE IF NOT EXISTS "WalletSnapshotPositions" (
    "Wallet" TEXT NOT NULL,
    "DateTime" TIMESTAMP NOT NULL,
    "Positions" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "idx_wallet_snapshots_wallet" ON "WalletSnapshotPositions"("Wallet");
CREATE INDEX IF NOT EXISTS "idx_wallet_snapshots_datetime" ON "WalletSnapshotPositions"("DateTime");
CREATE INDEX IF NOT EXISTS "idx_wallet_snapshots_wallet_datetime" ON "WalletSnapshotPositions"("Wallet", "DateTime");

-- Insert migration version
CREATE TABLE IF NOT EXISTS "_migrations" (
    "version" INTEGER NOT NULL PRIMARY KEY,
    "applied_at" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "description" TEXT
);

INSERT INTO "_migrations" ("version", "description")
VALUES (1, 'Initial schema creation')
ON CONFLICT ("version") DO NOTHING;
