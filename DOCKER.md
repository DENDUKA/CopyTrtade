# Docker Setup для CopyTrading

> 💡 **Локальная разработка?** Если вы хотите запускать приложение локально (не в Docker), но использовать базы данных в Docker, смотрите [DEVELOPMENT.md](./DEVELOPMENT.md)

## Архитектура

Проект использует Docker Compose для запуска следующих сервисов:

- **PostgreSQL** - основная база данных для Orders, Trades, WalletInfo, WalletSettings
- **copytrading-app** - ASP.NET Core 8.0 приложение
- **SQLite** (в контейнере приложения) - логи Serilog

## Быстрый старт

### 1. Запустить все сервисы

```bash
docker-compose up -d
```

### 2. Проверить статус

```bash
docker-compose ps
```

### 3. Просмотр логов

```bash
# Все сервисы
docker-compose logs -f

# Только приложение
docker-compose logs -f copytrading-app

# Только PostgreSQL
docker-compose logs -f postgres
```

### 4. Остановить сервисы

```bash
docker-compose down
```

### 5. Полная очистка (включая volumes)

```bash
docker-compose down -v
```

## Доступ к сервисам

- **Приложение**: http://localhost:5000
- **Serilog UI**: http://localhost:5000/serilog-ui
- **Swagger**: http://localhost:5000/swagger
- **PostgreSQL**: localhost:5432
  - Database: `copytrading`
  - Username: `copytrading_user`
  - Password: `copytrading_password`

## Volumes

- `postgres_data` - данные PostgreSQL (Orders, Trades, WalletInfo)
- `sqlite_logs` - логи Serilog в SQLite

## Переменные окружения

Настройки PostgreSQL можно изменить в `docker-compose.yml`:

```yaml
environment:
  PostgreSQL__Host: postgres
  PostgreSQL__Port: 5432
  PostgreSQL__Database: copytrading
  PostgreSQL__Username: copytrading_user
  PostgreSQL__Password: copytrading_password
```

## Пересборка образа

После изменения кода:

```bash
docker-compose build copytrading-app
docker-compose up -d copytrading-app
```

Или сразу:

```bash
docker-compose up -d --build
```

## Отладка

### Зайти внутрь контейнера

```bash
docker exec -it copytrading-app /bin/bash
```

### Проверить логи SQLite

```bash
docker exec -it copytrading-app ls -la /app/data/sqlite/
```

### Проверить подключение к PostgreSQL

```bash
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading
```

## Локальная разработка

Для локальной разработки используйте `dotnet run`:

```bash
dotnet run --project CopyTrading/CopyTrading.csproj
```

При локальном запуске:
- SQLite логи сохраняются в `./SQLliteBD/logs.db`
- PostgreSQL должен быть доступен на `localhost:5432` (можно запустить только PostgreSQL через Docker)

```bash
# Запустить только PostgreSQL для локальной разработки
docker-compose up -d postgres
```
