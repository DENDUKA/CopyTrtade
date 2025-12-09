# Quick Start Guide

## Автоматический запуск (рекомендуется)

Приложение **автоматически запустит PostgreSQL** при старте:

```bash
dotnet run --project CopyTrading/CopyTrading.csproj
```

Приложение проверит:
- ✅ Доступность Docker
- ✅ Статус PostgreSQL контейнера
- ✅ Запустит все контейнеры если они не работают (PostgreSQL + pgAdmin + Adminer)
- ✅ Дождется готовности базы данных
- ✅ Автоматически применит миграции

## Что вы увидите:

```
[Docker] Checking PostgreSQL container status...
[Docker] PostgreSQL container is not running, starting all containers (PostgreSQL, pgAdmin, Adminer)...
[Docker] Docker containers started, waiting for PostgreSQL to be ready...
[Docker] PostgreSQL is ready and accepting connections
[INF] Запуск приложения CopyTrading
[INF] Now listening on: http://localhost:5197
```

## Доступные эндпоинты:

- **Приложение**: http://localhost:5197
- **Swagger API**: http://localhost:5197/swagger
- **Serilog UI (логи)**: http://localhost:5197/serilog-ui
- **pgAdmin (просмотр БД)**: http://localhost:5050
  - Email: `admin@admin.com`
  - Password: `admin`
  - ✅ **БД уже настроена**: Сервер "CopyTrading PostgreSQL" автоматически добавлен в список
- **Adminer (просмотр БД)**: http://localhost:8080
  - System: `PostgreSQL`
  - Server: `postgres`
  - Username: `copytrading_user`
  - Password: `copytrading_password`
  - Database: `copytrading`

## Управление Docker контейнерами вручную:

```bash
# Посмотреть статус всех контейнеров
docker-compose ps

# Запустить все контейнеры
docker-compose up -d

# Запустить только PostgreSQL
docker-compose up -d postgres

# Запустить PostgreSQL + pgAdmin
docker-compose up -d postgres pgadmin

# Запустить PostgreSQL + Adminer
docker-compose up -d postgres adminer

# Остановить все контейнеры
docker-compose down

# Посмотреть логи PostgreSQL
docker-compose logs -f postgres

# Посмотреть логи pgAdmin
docker-compose logs -f pgadmin

# Подключиться к БД через командную строку
docker-compose exec postgres psql -U copytrading_user -d copytrading
```

## Если Docker недоступен:

Установите PostgreSQL локально и обновите `appsettings.json`:

```json
{
  "PostgreSQL": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "copytrading",
    "Username": "your_user",
    "Password": "your_password"
  }
}
```

## Дополнительная информация:

- Полная инструкция по миграции: `MIGRATION_TO_POSTGRESQL.md`
- Docker Compose конфигурация: `docker-compose.yml`
- SQL миграции: `Database/Migrations/`
