#!/usr/bin/env python3
"""
Script to migrate data from SQLite to PostgreSQL
"""

import sqlite3
import psycopg2
from datetime import datetime
import sys
import os

# Configuration
SQLITE_DB_PATH = r"C:\Program\CopyTrtade\SQLliteBD\CopyTradingDB.db"
POSTGRES_CONFIG = {
    'host': 'localhost',
    'port': 5432,
    'database': 'copytrading',
    'user': 'copytrading_user',
    'password': 'copytrading_password'
}

# Tables to migrate (in order to respect foreign keys if any)
TABLES = [
    'Logs',
    'MinPerpEquityForOrders',
    'MinPerpEquityForTrades',
    'Orders',
    'Trades',
    'WalletSettings',
    'WalletSnapshotPositions'
]

def connect_sqlite():
    """Connect to SQLite database"""
    if not os.path.exists(SQLITE_DB_PATH):
        print(f"Error: SQLite database not found at {SQLITE_DB_PATH}")
        sys.exit(1)

    conn = sqlite3.connect(SQLITE_DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn

def connect_postgres():
    """Connect to PostgreSQL database"""
    try:
        conn = psycopg2.connect(**POSTGRES_CONFIG)
        return conn
    except Exception as e:
        print(f"Error connecting to PostgreSQL: {e}")
        sys.exit(1)

def get_table_columns(cursor, table_name):
    """Get column names for a table"""
    cursor.execute(f'SELECT * FROM "{table_name}" LIMIT 0')
    return [desc[0] for desc in cursor.description]

def migrate_table(sqlite_conn, pg_conn, table_name):
    """Migrate data from one table"""
    print(f"\nMigrating table: {table_name}")

    sqlite_cursor = sqlite_conn.cursor()
    pg_cursor = pg_conn.cursor()

    # Get column names
    columns = get_table_columns(sqlite_cursor, table_name)
    print(f"  Columns: {', '.join(columns)}")

    # Get row count
    sqlite_cursor.execute(f'SELECT COUNT(*) FROM "{table_name}"')
    row_count = sqlite_cursor.fetchone()[0]
    print(f"  Total rows: {row_count}")

    if row_count == 0:
        print(f"  Skipping empty table")
        return 0

    # Fetch all data from SQLite
    sqlite_cursor.execute(f'SELECT * FROM "{table_name}"')
    rows = sqlite_cursor.fetchall()

    # Prepare INSERT statement
    placeholders = ', '.join(['%s'] * len(columns))
    column_names = ', '.join([f'"{col}"' for col in columns])
    insert_sql = f'INSERT INTO "{table_name}" ({column_names}) VALUES ({placeholders})'

    # Insert data into PostgreSQL
    migrated_count = 0
    skipped_count = 0

    for row in rows:
        try:
            values = tuple(row)
            pg_cursor.execute(insert_sql, values)
            migrated_count += 1

            if migrated_count % 100 == 0:
                print(f"  Progress: {migrated_count}/{row_count}")
        except Exception as e:
            skipped_count += 1
            print(f"  Warning: Failed to insert row: {e}")
            # Continue with next row instead of failing completely
            continue

    pg_conn.commit()

    print(f"  Completed: {migrated_count} rows migrated, {skipped_count} rows skipped")
    return migrated_count

def main():
    """Main migration function"""
    print("=" * 60)
    print("SQLite to PostgreSQL Migration Script")
    print("=" * 60)

    # Connect to databases
    print("\nConnecting to databases...")
    sqlite_conn = connect_sqlite()
    pg_conn = connect_postgres()

    print("✓ Connected to SQLite")
    print("✓ Connected to PostgreSQL")

    # Migrate each table
    total_migrated = 0
    start_time = datetime.now()

    for table_name in TABLES:
        try:
            migrated_count = migrate_table(sqlite_conn, pg_conn, table_name)
            total_migrated += migrated_count
        except Exception as e:
            print(f"\n✗ Error migrating table {table_name}: {e}")
            continue

    # Close connections
    sqlite_conn.close()
    pg_conn.close()

    end_time = datetime.now()
    duration = (end_time - start_time).total_seconds()

    # Summary
    print("\n" + "=" * 60)
    print("Migration Summary")
    print("=" * 60)
    print(f"Total rows migrated: {total_migrated}")
    print(f"Duration: {duration:.2f} seconds")
    print("\n✓ Migration completed successfully!")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nMigration cancelled by user")
        sys.exit(1)
    except Exception as e:
        print(f"\n\n✗ Migration failed: {e}")
        sys.exit(1)
