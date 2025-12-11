# 🚀 Быстрый старт CopyTrading

## Выберите режим работы

### 1️⃣ Локальная разработка (рекомендуется для разработчиков)

**Приложение** работает локально, **базы данных** в Docker.

```bash
# Шаг 1: Запустить только базы данных
docker-compose -f docker-compose.dev.yml up -d

# Шаг 2: Запустить приложение
dotnet run --project CopyTrading/CopyTrading.csproj
```

📖 Подробнее: [DEVELOPMENT.md](./DEVELOPMENT.md)

---

### 2️⃣ Полный Docker (для продакшена)

**Всё** работает в Docker контейнерах.

```bash
# Запустить всё в Docker
docker-compose up -d
```

📖 Подробнее: [DOCKER.md](./DOCKER.md)

---

## 📊 Доступ к приложению

После запуска открыть в браузере:

| Сервис | URL |
|--------|-----|
| Главная | http://localhost:5000 |
| Swagger API | http://localhost:5000/swagger |
| Serilog UI (логи) | http://localhost:5000/serilog-ui |

---

## 🛠️ Полезные команды

### Остановить всё

```bash
# Локальная разработка: остановить приложение (Ctrl+C) и базы данных
docker-compose -f docker-compose.dev.yml down

# Полный Docker: остановить всё
docker-compose down
```

### Посмотреть логи

```bash
# Локальная разработка: логи в консоли где запущен dotnet run
# Также в браузере: http://localhost:5000/serilog-ui

# Полный Docker: логи контейнера
docker-compose logs -f copytrading-app
```

### Подключиться к PostgreSQL

```bash
# Локальная разработка
docker exec -it copytrading-postgres-dev psql -U copytrading_user -d copytrading

# Полный Docker
docker exec -it copytrading-postgres psql -U copytrading_user -d copytrading
```

---

## 🗂️ Структура баз данных

| База данных | Что хранит | Локальная разработка | Полный Docker |
|-------------|------------|----------------------|---------------|
| **PostgreSQL** | Orders, Trades, WalletInfo, WalletSettings | Docker (localhost:5432) | Docker (внутренняя сеть) |
| **SQLite logs.db** | Логи Serilog | `./SQLliteBD/logs.db` | Volume: `sqlite_logs` |
| **SQLite CopyTradingDB.db** | Другие данные (если есть) | `./SQLliteBD/CopyTradingDB.db` | Volume: `sqlite_logs` |

---

## ❓ Проблемы?

### Порт 5432 уже занят

```bash
# Найти процесс использующий порт
netstat -ano | findstr :5432  # Windows
lsof -i :5432                 # Linux/Mac

# Остановить существующий PostgreSQL или изменить порт в docker-compose
```

### PostgreSQL не запускается

```bash
# Проверить логи
docker-compose -f docker-compose.dev.yml logs postgres

# Пересоздать контейнер
docker-compose -f docker-compose.dev.yml down
docker-compose -f docker-compose.dev.yml up -d
```

### Приложение не может подключиться к PostgreSQL

1. Проверьте что PostgreSQL запущен: `docker ps`
2. Проверьте настройки в `CopyTrading/appsettings.Development.json`
3. Убедитесь что используется правильный connection string

---

## 🔧 Переменные окружения

Переменная `DOTNET_RUNNING_IN_CONTAINER` автоматически устанавливается в Docker и определяет пути к SQLite:

| Режим | Значение | SQLite путь |
|-------|----------|-------------|
| **Docker** | `true` | `/app/data/sqlite/logs.db` |
| **Локально** | не установлена | `./SQLliteBD/logs.db` |

Для тестирования Docker логики локально:

```powershell
# Windows
$env:DOTNET_RUNNING_IN_CONTAINER="true"
dotnet run --project CopyTrading/CopyTrading.csproj

# Linux/Mac
DOTNET_RUNNING_IN_CONTAINER=true dotnet run --project CopyTrading/CopyTrading.csproj
```

📖 Подробнее: [ENVIRONMENT_VARIABLES.md](./ENVIRONMENT_VARIABLES.md)

---

## 📚 Дополнительные ресурсы

- [DEVELOPMENT.md](./DEVELOPMENT.md) - Подробная инструкция по локальной разработке
- [DOCKER.md](./DOCKER.md) - Подробная инструкция по Docker
- [DBEAVER_SQLITE.md](./DBEAVER_SQLITE.md) - Подключение DBeaver к SQLite базам данных
- [ENVIRONMENT_VARIABLES.md](./ENVIRONMENT_VARIABLES.md) - Настройка переменных окружения
- [CLAUDE.md](./CLAUDE.md) - Документация по архитектуре проекта
