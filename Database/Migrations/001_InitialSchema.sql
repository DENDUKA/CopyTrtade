-- CopyTrading PostgreSQL Initial Schema Migration
-- This script creates all tables needed for the CopyTrading application

-- =============================================
-- Orders Table
-- =============================================
CREATE TABLE IF NOT EXISTS Orders (
    OrderId BIGINT NOT NULL PRIMARY KEY,
    Wallet TEXT NOT NULL,
    Time TEXT NOT NULL,
    Symbol TEXT NOT NULL,
    Direction TEXT NOT NULL,
    Price NUMERIC NOT NULL,
    Size NUMERIC NOT NULL,
    Value NUMERIC NOT NULL,
    Status TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_orders_wallet ON Orders(Wallet);
CREATE INDEX IF NOT EXISTS idx_orders_symbol ON Orders(Symbol);
CREATE INDEX IF NOT EXISTS idx_orders_time ON Orders(Time);

-- =============================================
-- Trades Table
-- =============================================
CREATE TABLE IF NOT EXISTS Trades (
    TradeId BIGINT NOT NULL PRIMARY KEY,
    Wallet TEXT NOT NULL,
    TradeVolume NUMERIC NOT NULL,
    Symbol TEXT NOT NULL,
    Time TEXT NOT NULL,
    OrderId BIGINT NOT NULL,
    Direction TEXT NOT NULL,
    Price NUMERIC NOT NULL,
    Quantity NUMERIC NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_trades_wallet ON Trades(Wallet);
CREATE INDEX IF NOT EXISTS idx_trades_symbol ON Trades(Symbol);
CREATE INDEX IF NOT EXISTS idx_trades_orderid ON Trades(OrderId);
CREATE INDEX IF NOT EXISTS idx_trades_time ON Trades(Time);

-- =============================================
-- MinPerpEquityForTrades Table
-- =============================================
CREATE TABLE IF NOT EXISTS MinPerpEquityForTrades (
    TradeId BIGINT NOT NULL PRIMARY KEY,
    WalletPerpEquity NUMERIC NOT NULL,
    MinPerpEquityForCopyTrade NUMERIC NOT NULL,
    Spread NUMERIC NOT NULL,
    DeltaTimeS NUMERIC NOT NULL,
    SubType TEXT
);

CREATE INDEX IF NOT EXISTS idx_minpe_trades_subtype ON MinPerpEquityForTrades(SubType);

-- =============================================
-- MinPerpEquityForOrders Table
-- =============================================
CREATE TABLE IF NOT EXISTS MinPerpEquityForOrders (
    OrderId BIGINT NOT NULL PRIMARY KEY,
    AccountVolume NUMERIC NOT NULL,
    MinPE NUMERIC NOT NULL,
    SubType TEXT NOT NULL
);

-- =============================================
-- WalletSnapshotPositions Table
-- =============================================
CREATE TABLE IF NOT EXISTS WalletSnapshotPositions (
    Id SERIAL PRIMARY KEY,
    Wallet TEXT NOT NULL,
    DateTime TEXT NOT NULL,
    Positions TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_walletsnapshot_wallet ON WalletSnapshotPositions(Wallet);
CREATE INDEX IF NOT EXISTS idx_walletsnapshot_datetime ON WalletSnapshotPositions(DateTime);

-- =============================================
-- WalletSettings Table
-- =============================================
CREATE TABLE IF NOT EXISTS WalletSettings (
    Wallet TEXT NOT NULL PRIMARY KEY,
    ValueUsd NUMERIC NOT NULL,
    CopyKoef NUMERIC NOT NULL
);
