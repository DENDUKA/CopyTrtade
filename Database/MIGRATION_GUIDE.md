# Руководство по миграции с SQLite на PostgreSQL

## Обзор

Этот документ содержит пошаговые инструкции по миграции с SQLite на PostgreSQL.

## Предварительные требования

- Docker Desktop установлен и запущен
- .NET 8.0 SDK установлен
- Python 3.x установлен (для скрипта миграции данных)

## Шаг 1: Подготовка

### 1.1 Создайте backup текущей SQLite базы данных

```bash
# Из корневой директории проекта
cp SQLliteBD/CopyTradingDB.db SQLliteBD/CopyTradingDB.db.backup
```

### 1.2 Установите необходимые NuGet пакеты

```bash
dotnet restore
```

Пакеты уже добавлены в `CopyTrading.csproj`:
- Npgsql v8.0.3
- Serilog.Sinks.PostgreSQL v2.4.0
- Serilog.UI.PostgreSqlProvider v3.2.0

## Шаг 2: Запуск PostgreSQL

### 2.1 Запустите Docker контейнер с PostgreSQL

```bash
# Из корневой директории проекта
docker-compose up -d
```

Это создаст и запустит PostgreSQL контейнер с автоматическим применением начальной миграции.

### 2.2 Проверьте статус контейнера

```bash
docker-compose ps
docker-compose logs postgres
```

Вы должны увидеть:
```
copytrading-postgres | ... PostgreSQL init process complete; ready for start up
```

### 2.3 Проверьте подключение к базе данных

```bash
# Подключиться через psql
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading

# В psql выполните:
\dt
# Должен показать список всех таблиц
\q
# Выйти из psql
```

## Шаг 3: Миграция данных (опционально)

Если у вас есть данные в SQLite, которые нужно перенести в PostgreSQL:

### Вариант A: Использование Python скрипта (рекомендуется)

```bash
# Установите зависимости
pip install psycopg2-binary

# Запустите скрипт миграции
python Database/Scripts/migrate_sqlite_to_postgres.py
```

### Вариант B: Использование PowerShell скрипта

```powershell
# Запустите PowerShell скрипт
.\Database\Scripts\migrate_sqlite_to_postgres.ps1
```

**Примечание:** PowerShell скрипт экспортирует данные в CSV. Для полной миграции рекомендуется использовать Python скрипт.

### Вариант C: Ручная миграция через CSV

1. Экспортируйте данные из SQLite в CSV:

```bash
# Для каждой таблицы
sqlite3 SQLliteBD/CopyTradingDB.db
.mode csv
.headers on
.output orders.csv
SELECT * FROM "Orders";
.quit
```

2. Импортируйте CSV в PostgreSQL:

```bash
docker exec -i copytrading-postgres psql -U copytrading_user -d copytrading <<EOF
\COPY "Orders" FROM '/path/to/orders.csv' CSV HEADER;
EOF
```

## Шаг 4: Обновление кода приложения

### 4.1 Обновите Repository классы

Вам нужно обновить все классы репозиториев для использования PostgreSQL вместо SQLite.

**Найдите все репозитории:**

```bash
# Найти все SQLite репозитории
grep -r "Microsoft.Data.Sqlite" CopyTrading/Repository/
```

**Основные изменения:**

1. Замените `Microsoft.Data.Sqlite` на `Npgsql`:

```csharp
// Было:
using Microsoft.Data.Sqlite;
using var connection = new SqliteConnection(SQLLiteSettings.Path);

// Стало:
using Npgsql;
var connectionString = _postgresSettings.GetConnectionString();
using var connection = new NpgsqlConnection(connectionString);
```

2. Обновите параметры запросов с `@param` (оставьте как есть, Npgsql поддерживает `@` для параметров):

```csharp
// Оба варианта работают в Npgsql
cmd.Parameters.AddWithValue("@orderId", orderId);
// или
cmd.Parameters.AddWithValue("orderId", orderId);
```

3. Обновите типы данных:

```csharp
// SQLite
command.Parameters.AddWithValue("@timestamp", timestamp.ToString("o"));

// PostgreSQL (лучше использовать native типы)
command.Parameters.AddWithValue("@timestamp", timestamp);
```

### 4.2 Обновите Serilog конфигурацию

В файле `Program.cs` или `Startup.cs` обновите Serilog для использования PostgreSQL:

```csharp
// Было (SQLite):
Log.Logger = new LoggerConfiguration()
    .WriteTo.SQLite(SQLLiteSettings.Path)
    .CreateLogger();

// Стало (PostgreSQL):
var postgresSettings = builder.Configuration
    .GetSection("PostgreSQL")
    .Get<PostgreSQLSettings>();

Log.Logger = new LoggerConfiguration()
    .WriteTo.PostgreSQL(
        connectionString: postgresSettings.GetConnectionString(),
        tableName: "Logs",
        needAutoCreateTable: false)
    .CreateLogger();
```

### 4.3 Обновите Dependency Injection

В `Startup.cs` или `Program.cs`:

```csharp
// Добавьте конфигурацию PostgreSQL
services.Configure<PostgreSQLSettings>(
    builder.Configuration.GetSection("PostgreSQL"));

services.AddSingleton<PostgreSQLSettings>(sp =>
    sp.GetRequiredService<IOptions<PostgreSQLSettings>>().Value);
```

### 4.4 Обновите appsettings.json

Файл уже обновлен с настройками PostgreSQL. При необходимости измените параметры подключения:

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

## Шаг 5: Тестирование

### 5.1 Пересоберите проект

```bash
dotnet clean
dotnet build
```

### 5.2 Запустите приложение

```bash
dotnet run --project CopyTrading/CopyTrading.csproj
```

### 5.3 Проверьте подключение к базе данных

Проверьте логи приложения на наличие ошибок подключения.

### 5.4 Запустите тесты

```bash
dotnet test
```

## Шаг 6: Production развертывание

### 6.1 Обновите переменные окружения

Создайте файл `.env` на основе `.env.example`:

```bash
cp .env.example .env
```

Обновите значения в `.env`:

```env
POSTGRES_HOST=your-production-host
POSTGRES_PORT=5432
POSTGRES_DB=copytrading
POSTGRES_USER=your-production-user
POSTGRES_PASSWORD=your-secure-password
```

### 6.2 Используйте переменные окружения в appsettings

В `appsettings.Production.json`:

```json
{
  "PostgreSQL": {
    "Host": "${POSTGRES_HOST}",
    "Port": "${POSTGRES_PORT}",
    "Database": "${POSTGRES_DB}",
    "Username": "${POSTGRES_USER}",
    "Password": "${POSTGRES_PASSWORD}"
  }
}
```

### 6.3 Безопасность

**ВАЖНО:** Не коммитьте файл `.env` с реальными паролями в git!

Добавьте в `.gitignore`:

```
.env
appsettings.Production.json
```

## Откат к SQLite (если нужно)

Если что-то пошло не так и нужно вернуться к SQLite:

### 1. Остановите PostgreSQL контейнер

```bash
docker-compose down
```

### 2. Откатите изменения в appsettings.json

```json
{
  "DatabaseProvider": "SQLite"
}
```

### 3. Восстановите backup SQLite базы данных

```bash
cp SQLliteBD/CopyTradingDB.db.backup SQLliteBD/CopyTradingDB.db
```

### 4. Откатите изменения в коде

```bash
git checkout HEAD -- CopyTrading/Repository/
git checkout HEAD -- CopyTrading/Program.cs
```

## Troubleshooting

### Проблема: Контейнер не запускается

**Решение:** Проверьте, что порт 5432 не занят:

```bash
netstat -ano | findstr :5432
```

Если порт занят, измените порт в `docker-compose.yml`:

```yaml
ports:
  - "5433:5432"  # Внешний порт 5433 вместо 5432
```

### Проблема: Ошибка подключения "password authentication failed"

**Решение:** Пересоздайте контейнер с новыми credentials:

```bash
docker-compose down -v  # Удалит volume с данными!
docker-compose up -d
```

### Проблема: Миграция данных прерывается с ошибками

**Решение:** Проверьте логи и попробуйте мигрировать таблицы по одной:

```python
# В migrate_sqlite_to_postgres.py
TABLES = ['Orders']  # Только одна таблица
```

### Проблема: Несовместимость типов данных

**Решение:** Обновите скрипт миграции для конвертации типов:

```python
# Конвертация текстовых дат в timestamp
if column_name in ['Time', 'DateTime', 'Timestamp']:
    value = datetime.fromisoformat(value) if value else None
```

## Полезные команды

### Проверка версии PostgreSQL

```bash
docker exec copytrading-postgres psql -U copytrading_user -d copytrading -c "SELECT version();"
```

### Просмотр размера таблиц

```bash
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading -c "
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
"
```

### Backup PostgreSQL базы данных

```bash
docker exec copytrading-postgres pg_dump -U copytrading_user copytrading > backup_$(date +%Y%m%d).sql
```

### Restore PostgreSQL базы данных

```bash
docker exec -i copytrading-postgres psql -U copytrading_user copytrading < backup_20241210.sql
```

## Дополнительная информация

- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Npgsql Documentation](https://www.npgsql.org/doc/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Serilog PostgreSQL Sink](https://github.com/b00ted/serilog-sinks-postgresql)
