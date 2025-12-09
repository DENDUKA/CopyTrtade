# PowerShell script to migrate data from SQLite to PostgreSQL
# Requires: .NET 8.0 SDK

param(
    [string]$SqlitePath = "$PSScriptRoot\..\..\SQLliteBD\CopyTradingDB.db",
    [string]$PostgresHost = "localhost",
    [int]$PostgresPort = 5432,
    [string]$PostgresDatabase = "copytrading",
    [string]$PostgresUser = "copytrading_user",
    [string]$PostgresPassword = "copytrading_password"
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "SQLite to PostgreSQL Migration Script" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Check if SQLite database exists
if (-not (Test-Path $SqlitePath)) {
    Write-Host "Error: SQLite database not found at: $SqlitePath" -ForegroundColor Red
    exit 1
}

Write-Host "SQLite Database: $SqlitePath" -ForegroundColor Green
Write-Host "PostgreSQL Host: $PostgresHost" -ForegroundColor Green
Write-Host "PostgreSQL Database: $PostgresDatabase" -ForegroundColor Green
Write-Host ""

# Connection strings
$sqliteConnectionString = "Data Source=$SqlitePath"
$postgresConnectionString = "Host=$PostgresHost;Port=$PostgresPort;Database=$PostgresDatabase;Username=$PostgresUser;Password=$PostgresPassword"

# Tables to migrate
$tables = @(
    "Logs",
    "MinPerpEquityForOrders",
    "MinPerpEquityForTrades",
    "Orders",
    "Trades",
    "WalletSettings",
    "WalletSnapshotPositions"
)

$totalMigrated = 0
$startTime = Get-Date

# Load required assemblies
Add-Type -Path "$env:USERPROFILE\.nuget\packages\microsoft.data.sqlite\9.0.10\lib\net8.0\Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue
Add-Type -Path "$env:USERPROFILE\.nuget\packages\npgsql\8.0.3\lib\net8.0\Npgsql.dll" -ErrorAction SilentlyContinue

# Alternative: Use dotnet to run C# code inline
$csharpCode = @"
using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Npgsql;

public class DataMigrator
{
    public static void Migrate(string sqliteConnStr, string pgConnStr, string tableName)
    {
        Console.WriteLine($"Migrating table: {tableName}");

        using var sqliteConn = new SqliteConnection(sqliteConnStr);
        using var pgConn = new NpgsqlConnection(pgConnStr);

        sqliteConn.Open();
        pgConn.Open();

        // Get data from SQLite
        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{tableName}\"";

        using var reader = cmd.ExecuteReader();
        var schemaTable = reader.GetSchemaTable();

        if (!reader.HasRows)
        {
            Console.WriteLine($"  Table is empty, skipping");
            return;
        }

        // Get column names
        var columns = new System.Collections.Generic.List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        // Build INSERT statement
        var columnNames = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var placeholders = string.Join(", ", Enumerable.Range(1, columns.Count).Select(i => $"@p{i}"));
        var insertSql = $"INSERT INTO \"{tableName}\" ({columnNames}) VALUES ({placeholders})";

        int migrated = 0;
        int skipped = 0;

        // Insert data into PostgreSQL
        while (reader.Read())
        {
            try
            {
                using var insertCmd = new NpgsqlCommand(insertSql, pgConn);

                for (int i = 0; i < columns.Count; i++)
                {
                    var value = reader.GetValue(i);
                    insertCmd.Parameters.AddWithValue($"@p{i + 1}", value ?? DBNull.Value);
                }

                insertCmd.ExecuteNonQuery();
                migrated++;

                if (migrated % 100 == 0)
                {
                    Console.WriteLine($"  Progress: {migrated} rows");
                }
            }
            catch (Exception ex)
            {
                skipped++;
                Console.WriteLine($"  Warning: Failed to insert row: {ex.Message}");
            }
        }

        Console.WriteLine($"  Completed: {migrated} rows migrated, {skipped} rows skipped");
    }
}
"@

Write-Host "Note: This PowerShell script requires the Python version for full functionality." -ForegroundColor Yellow
Write-Host "Please use the Python script: python Database\Scripts\migrate_sqlite_to_postgres.py" -ForegroundColor Yellow
Write-Host ""
Write-Host "Alternative: You can export SQLite data to CSV and import to PostgreSQL" -ForegroundColor Yellow
Write-Host ""

# Simple CSV export approach
Write-Host "Exporting SQLite data to CSV files..." -ForegroundColor Cyan

foreach ($table in $tables) {
    Write-Host "Exporting table: $table" -ForegroundColor Green

    $csvPath = "$PSScriptRoot\..\Exports\$table.csv"
    $exportDir = Split-Path $csvPath -Parent

    if (-not (Test-Path $exportDir)) {
        New-Item -ItemType Directory -Path $exportDir -Force | Out-Null
    }

    # Use sqlite3 command if available
    $sqlite3Exe = "sqlite3"
    if (Get-Command $sqlite3Exe -ErrorAction SilentlyContinue) {
        & $sqlite3Exe $SqlitePath ".mode csv" ".headers on" ".output $csvPath" "SELECT * FROM `"$table`";" ".quit"
        Write-Host "  Exported to: $csvPath" -ForegroundColor Green
    } else {
        Write-Host "  sqlite3 command not found. Skipping CSV export." -ForegroundColor Yellow
        Write-Host "  Install sqlite3: https://www.sqlite.org/download.html" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "To complete the migration:" -ForegroundColor Yellow
Write-Host "1. Install Python 3: https://www.python.org/downloads/" -ForegroundColor White
Write-Host "2. Install required packages: pip install psycopg2-binary" -ForegroundColor White
Write-Host "3. Run: python Database\Scripts\migrate_sqlite_to_postgres.py" -ForegroundColor White

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Script completed in $duration seconds" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
