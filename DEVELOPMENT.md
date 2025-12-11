# Локальная разработка CopyTrading

Этот гайд описывает как запускать приложение **локально** (не в Docker), при этом используя базы данных в Docker контейнерах.

## 🏗️ Архитектура

```
┌─────────────────────┐
│   Ваш компьютер     │
│                     │
│  ┌──────────────┐   │
│  │ CopyTrading  │   │
│  │ (dotnet run) │   │
│  └──────┬───────┘   │
│         │           │
│         │ localhost:5432 (PostgreSQL)
│         │ ./SQLliteBD/ (SQLite файлы)
│         │           │
│  ┌──────▼───────────┴──────┐
│  │   Docker              │
│  │  ┌────────────────┐   │
│  │  │   PostgreSQL   │   │
│  │  │   :5432        │   │
│  │  └────────────────┘   │
│  │                        │
│  │  Volume: postgres_data │
│  └────────────────────────┘
│                     │
│  📁 ./SQLliteBD/    │
│     ├── logs.db     │
│     └── CopyTradingDB.db
└─────────────────────┘
```

## 🚀 Быстрый старт

### 1. Запустить базы данных в Docker

```bash
# Запустить только PostgreSQL (без приложения)
docker-compose -f docker-compose.dev.yml up -d

# Проверить что PostgreSQL запущен
docker-compose -f docker-compose.dev.yml ps
```

Вы увидите:
```
NAME                          IMAGE                COMMAND                  SERVICE    PORTS
copytrading-postgres-dev      postgres:16-alpine   "docker-entrypoint.s…"   postgres   0.0.0.0:5432->5432/tcp
```

### 2. Создать папку для SQLite (если еще не создана)

```bash
# Windows (PowerShell)
New-Item -Path "SQLliteBD" -ItemType Directory -Force

# Linux/Mac
mkdir -p SQLliteBD
```

### 3. Запустить приложение локально

```bash
# Из корневой директории проекта
dotnet run --project CopyTrading/CopyTrading.csproj
```

Или используя горячую перезагрузку:

```bash
dotnet watch --project CopyTrading/CopyTrading.csproj
```

### 4. Готово! 🎉

Приложение доступно по адресу:
- **Приложение**: http://localhost:5000
- **Serilog UI**: http://localhost:5000/serilog-ui
- **Swagger**: http://localhost:5000/swagger

## 📊 Где хранятся данные

| Что | Где хранится | Как получить доступ |
|-----|--------------|---------------------|
| **PostgreSQL данные** (Orders, Trades, WalletInfo) | Docker volume `postgres_data_dev` | `docker exec -it copytrading-postgres-dev psql -U copytrading_user -d copytrading` |
| **Логи Serilog** (SQLite) | `./SQLliteBD/logs.db` | Открыть через любой SQLite клиент или Serilog UI |
| **Другие данные** (SQLite) | `./SQLliteBD/CopyTradingDB.db` | Открыть через любой SQLite клиент |

## 🔧 Полезные команды

### PostgreSQL

```bash
# Подключиться к PostgreSQL в Docker
docker exec -it copytrading-postgres-dev psql -U copytrading_user -d copytrading

# Просмотр логов PostgreSQL
docker-compose -f docker-compose.dev.yml logs -f postgres

# Остановить PostgreSQL
docker-compose -f docker-compose.dev.yml down

# Очистить данные PostgreSQL (удалить volume)
docker-compose -f docker-compose.dev.yml down -v
```

### SQLite

```bash
# Посмотреть логи через sqlite3 (если установлен)
sqlite3 SQLliteBD/logs.db "SELECT * FROM logs ORDER BY Timestamp DESC LIMIT 10"

# Посмотреть размер файлов
# Windows (PowerShell)
Get-ChildItem SQLliteBD -Recurse | Select-Object Name, Length

# Linux/Mac
ls -lh SQLliteBD/
```

### Приложение

```bash
# Сборка
dotnet build CopyTrading.sln

# Запуск с горячей перезагрузкой
dotnet watch --project CopyTrading/CopyTrading.csproj

# Запуск тестов
dotnet test CopyTriding.Test/CopyTriding.Test.csproj

# Очистка
dotnet clean CopyTrading.sln
```

## ⚙️ Конфигурация

### appsettings.Development.json

Настройки для локальной разработки уже настроены:

```json
{
  "PostgreSQL": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "copytrading",
    "Username": "copytrading_user",
    "Password": "copytrading_password"
  }
}
```

### Переменные окружения (опционально)

Вы можете переопределить настройки через переменные окружения:

```bash
# Windows (PowerShell)
$env:PostgreSQL__Host = "localhost"
$env:PostgreSQL__Port = "5432"
dotnet run --project CopyTrading/CopyTrading.csproj

# Linux/Mac
export PostgreSQL__Host=localhost
export PostgreSQL__Port=5432
dotnet run --project CopyTrading/CopyTrading.csproj
```

## 🐛 Отладка

### Проблема: Приложение не может подключиться к PostgreSQL

**Решение:**

1. Проверьте что PostgreSQL запущен:
   ```bash
   docker-compose -f docker-compose.dev.yml ps
   ```

2. Проверьте что порт 5432 свободен:
   ```bash
   # Windows (PowerShell)
   netstat -ano | findstr :5432

   # Linux/Mac
   lsof -i :5432
   ```

3. Попробуйте подключиться вручную:
   ```bash
   docker exec -it copytrading-postgres-dev psql -U copytrading_user -d copytrading
   ```

### Проблема: SQLite файлы не создаются

**Решение:**

1. Проверьте что папка `SQLliteBD` существует
2. Проверьте права доступа на папку
3. Посмотрите логи приложения на наличие ошибок доступа к файлам

### Проблема: Порт 5432 уже занят

**Решение:**

Измените порт в `docker-compose.dev.yml`:

```yaml
ports:
  - "5433:5432"  # Используем 5433 вместо 5432
```

И в `appsettings.Development.json`:

```json
{
  "PostgreSQL": {
    "Port": 5433
  }
}
```

## 🔄 Переключение между режимами

### Локальная разработка → Docker

```bash
# Остановить локальное приложение (Ctrl+C)
# Остановить dev базы данных
docker-compose -f docker-compose.dev.yml down

# Запустить всё в Docker
docker-compose up -d
```

### Docker → Локальная разработка

```bash
# Остановить Docker приложение
docker-compose down

# Запустить только базы данных
docker-compose -f docker-compose.dev.yml up -d

# Запустить приложение локально
dotnet run --project CopyTrading/CopyTrading.csproj
```

## 📝 Рекомендации

1. **Используйте `dotnet watch`** для автоматической перезагрузки при изменении кода
2. **Не коммитьте** файлы `*.db` в Git (они в `.gitignore`)
3. **Бэкапьте** PostgreSQL данные перед очисткой volumes:
   ```bash
   docker exec copytrading-postgres-dev pg_dump -U copytrading_user copytrading > backup.sql
   ```
4. **Используйте Serilog UI** для просмотра логов: http://localhost:5000/serilog-ui

## 🎯 IDE Setup

### Visual Studio
1. Откройте `CopyTrading.sln`
2. Установите `CopyTrading` как Startup Project
3. Нажмите F5 для запуска с отладкой

### Visual Studio Code
1. Откройте папку проекта
2. Установите расширение "C# Dev Kit"
3. Используйте `F5` или Run and Debug

### Rider
1. Откройте `CopyTrading.sln`
2. Run Configuration будет создана автоматически
3. Нажмите Shift+F10 для запуска
