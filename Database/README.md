# PostgreSQL Database Setup

## Описание

Этот проект использует PostgreSQL для хранения данных. База данных запускается в Docker контейнере.

## Быстрый старт

### 1. Запуск PostgreSQL в Docker

```bash
# Из корневой директории проекта
docker-compose up -d
```

Эта команда:
- Создаст и запустит контейнер PostgreSQL
- Автоматически применит начальную миграцию из `Database/Migrations/001_initial_schema.sql`
- Создаст volume для постоянного хранения данных

### 2. Проверка статуса

```bash
# Проверить, что контейнер запущен
docker-compose ps

# Посмотреть логи
docker-compose logs postgres

# Проверить здоровье контейнера
docker-compose ps
```

### 3. Подключение к базе данных

**Параметры подключения:**
- **Host:** localhost
- **Port:** 5432
- **Database:** copytrading
- **Username:** copytrading_user
- **Password:** copytrading_password

**Connection String:**
```
Host=localhost;Port=5432;Database=copytrading;Username=copytrading_user;Password=copytrading_password
```

### 4. Подключение через psql (опционально)

```bash
# Подключиться к базе через Docker
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading

# Основные команды в psql:
# \dt              - показать все таблицы
# \d "Orders"      - показать структуру таблицы Orders
# \q               - выйти из psql
```

## Управление контейнером

```bash
# Остановить контейнер
docker-compose stop

# Запустить контейнер
docker-compose start

# Перезапустить контейнер
docker-compose restart

# Остановить и удалить контейнер (данные сохранятся в volume)
docker-compose down

# Удалить контейнер и все данные (ОСТОРОЖНО!)
docker-compose down -v
```

## Миграции

### Структура

Миграции хранятся в `Database/Migrations/` и нумеруются последовательно:
- `001_initial_schema.sql` - Начальная схема базы данных

### Применение миграций

Миграции автоматически применяются при первом запуске контейнера.

Для ручного применения миграции:

```bash
# Применить SQL файл в уже запущенный контейнер
docker exec -i copytrading-postgres psql -U copytrading_user -d copytrading < Database/Migrations/001_initial_schema.sql
```

### Создание новой миграции

1. Создайте файл `Database/Migrations/00X_description.sql`
2. Добавьте SQL команды
3. Примените миграцию вручную или перезапустите контейнер

Пример:

```sql
-- Migration: 002
-- Description: Add new column

ALTER TABLE "Orders" ADD COLUMN "NewColumn" TEXT;

INSERT INTO "_migrations" ("version", "description")
VALUES (2, 'Add new column to Orders table')
ON CONFLICT ("version") DO NOTHING;
```

## Таблицы в базе данных

### Logs
Логи приложения (используется Serilog)

### Orders
Основная таблица ордеров с индексами по:
- Wallet
- Symbol
- Time
- Status
- Wallet + Symbol (композитный)

### Trades
Таблица сделок с индексами по:
- Wallet
- Symbol
- Time
- OrderId
- Wallet + Symbol (композитный)

### MinPerpEquityForOrders
Минимальный Perpetual Equity для ордеров

### MinPerpEquityForTrades
Минимальный Perpetual Equity для сделок

### WalletSettings
Настройки кошельков для копирования

### WalletSnapshotPositions
Снимки позиций кошельков с индексами по:
- Wallet
- DateTime
- Wallet + DateTime (композитный)

### _migrations
Служебная таблица для отслеживания версий миграций

## Backup и Restore

### Backup

```bash
# Создать backup всей базы данных
docker exec copytrading-postgres pg_dump -U copytrading_user copytrading > backup_$(date +%Y%m%d_%H%M%S).sql

# Backup только данных (без схемы)
docker exec copytrading-postgres pg_dump -U copytrading_user --data-only copytrading > backup_data.sql

# Backup только схемы (без данных)
docker exec copytrading-postgres pg_dump -U copytrading_user --schema-only copytrading > backup_schema.sql
```

### Restore

```bash
# Восстановить из backup
docker exec -i copytrading-postgres psql -U copytrading_user copytrading < backup.sql
```

## Мониторинг и отладка

### Просмотр логов PostgreSQL

```bash
docker-compose logs -f postgres
```

### Проверка подключений

```bash
# Зайти в psql и выполнить запрос
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading -c "SELECT * FROM pg_stat_activity;"
```

### Проверка размера базы данных

```bash
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading -c "SELECT pg_size_pretty(pg_database_size('copytrading'));"
```

## Troubleshooting

### Контейнер не запускается

1. Проверьте, что порт 5432 не занят:
   ```bash
   netstat -ano | findstr :5432
   ```

2. Проверьте логи контейнера:
   ```bash
   docker-compose logs postgres
   ```

### Ошибки при подключении

1. Убедитесь, что контейнер запущен:
   ```bash
   docker-compose ps
   ```

2. Проверьте health check:
   ```bash
   docker inspect copytrading-postgres | findstr Health
   ```

### Сброс всех данных

```bash
# Остановить и удалить контейнер с volume
docker-compose down -v

# Запустить заново
docker-compose up -d
```

## Production рекомендации

Для production окружения рекомендуется:

1. Вынести пароли в переменные окружения или secrets
2. Настроить регулярные backups
3. Настроить мониторинг (например, через Prometheus + Grafana)
4. Увеличить ресурсы контейнера при необходимости
5. Использовать managed PostgreSQL сервис (AWS RDS, Azure Database, etc.)
