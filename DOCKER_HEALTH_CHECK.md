# Docker Health Check

## Описание

При запуске приложения автоматически проверяется доступность всех необходимых зависимостей:
- **Redis** - для кэширования и хранения данных
- **PostgreSQL** - для хранения данных в БД

## Как это работает

### 1. Автоматическая проверка при старте

В `Startup.Configure()` при старте приложения вызывается `DockerHealthCheckService.CheckAllDependencies()`, который:
- Проверяет подключение к Redis (ping)
- Проверяет подключение к PostgreSQL (запрос версии)
- **Если контейнеры недоступны** - автоматически пытается запустить их через `docker-compose up -d`
- Ожидает 15 секунд для полного запуска контейнеров
- Повторно проверяет подключение
- Логирует результаты проверки

### 2. Настройки подключения

Сервис использует настройки из `appsettings.json` или переменных окружения:

**Redis:**
```json
{
  "Redis": {
    "Host": "localhost",
    "Port": "6379",
    "Password": ""
  }
}
```

**PostgreSQL:**
```json
{
  "PostgreSQL": {
    "Host": "localhost",
    "Port": "5432",
    "Database": "copytrading",
    "Username": "copytrading_user",
    "Password": "copytrading_password"
  }
}
```

### 3. Логи при запуске

**При успешном запуске (контейнеры уже работают):**
```
=== Проверка доступности зависимостей ===
Проверка Redis: localhost:6379
✅ Redis: Подключение успешно (localhost:6379)
Проверка PostgreSQL: localhost:5432/copytrading
✅ PostgreSQL: Подключение успешно (Version: PostgreSQL 16.x)
✅ Все зависимости доступны
==========================================
```

**При автоматическом запуске контейнеров:**
```
=== Проверка доступности зависимостей ===
Проверка Redis: localhost:6379
❌ Redis: Не удалось подключиться
Проверка PostgreSQL: localhost:5432/copytrading
❌ PostgreSQL: Не удалось подключиться
❌ Некоторые зависимости недоступны. Попытка запустить Docker контейнеры...
🐳 Запуск Docker контейнеров: docker-compose up -d
📁 Рабочая директория: C:\Program\CopyTrtade
✅ Docker контейнеры запущены успешно
⏳ Ожидание запуска контейнеров (15 секунд)...
🔄 Повторная проверка зависимостей...
Проверка Redis: localhost:6379
✅ Redis: Подключение успешно (localhost:6379)
Проверка PostgreSQL: localhost:5432/copytrading
✅ PostgreSQL: Подключение успешно (Version: PostgreSQL 16.x)
✅ Все зависимости доступны
==========================================
```

**Если автоматический запуск не удался:**
```
=== Проверка доступности зависимостей ===
Проверка Redis: localhost:6379
❌ Redis: Не удалось подключиться
Проверка PostgreSQL: localhost:5432/copytrading
❌ PostgreSQL: Не удалось подключиться
❌ Некоторые зависимости недоступны. Попытка запустить Docker контейнеры...
🐳 Запуск Docker контейнеров: docker-compose up -d
❌ Не удалось запустить Docker контейнеры. Exit code: 1
💡 Убедитесь, что Docker Desktop запущен и docker-compose установлен
❌ Не все зависимости доступны. Приложение может работать некорректно.
==========================================
⚠️  Приложение запущено с недоступными зависимостями. Некоторые функции могут не работать.
```

## Запуск Docker контейнеров

### Запустить все контейнеры:
```bash
docker-compose up -d
```

### Проверить статус контейнеров:
```bash
docker-compose ps
```

### Проверить логи контейнеров:
```bash
docker-compose logs postgres
docker-compose logs redis
docker-compose logs copytrading-app
```

### Остановить все контейнеры:
```bash
docker-compose down
```

### Перезапустить контейнеры:
```bash
docker-compose restart
```

## Ручная проверка зависимостей

### Проверка Redis:
```bash
# Через docker:
docker exec copytrading-redis redis-cli ping

# Локально (если установлен redis-cli):
redis-cli -h localhost -p 6379 ping
```

### Проверка PostgreSQL:
```bash
# Через docker:
docker exec copytrading-postgres psql -U copytrading_user -d copytrading -c "SELECT version();"

# Локально (если установлен psql):
psql -h localhost -p 5432 -U copytrading_user -d copytrading -c "SELECT version();"
```

## Troubleshooting

### Если Redis недоступен:
1. Проверьте, что контейнер запущен: `docker ps | grep redis`
2. Проверьте логи: `docker logs copytrading-redis`
3. Попробуйте перезапустить: `docker restart copytrading-redis`

### Если PostgreSQL недоступен:
1. Проверьте, что контейнер запущен: `docker ps | grep postgres`
2. Проверьте логи: `docker logs copytrading-postgres`
3. Попробуйте перезапустить: `docker restart copytrading-postgres`
4. Проверьте healthcheck: `docker inspect copytrading-postgres | grep Health`

### Если приложение не запускается:
1. Убедитесь, что все контейнеры подняты: `docker-compose ps`
2. Проверьте логи приложения: `docker logs copytrading-app`
3. Запустите health check вручную через API (если приложение запустилось):
   - GET `/health` (если эндпоинт добавлен)

## Дополнительные возможности

### IDockerHealthCheckService методы:

```csharp
// Проверить все зависимости
await healthCheckService.CheckAllDependencies();

// Проверить только Redis
await healthCheckService.CheckRedis();

// Проверить только PostgreSQL
await healthCheckService.CheckPostgreSQL();
```

### Использование в коде:

```csharp
// Inject в конструктор
private readonly IDockerHealthCheckService _healthCheck;

public MyService(IDockerHealthCheckService healthCheck)
{
    _healthCheck = healthCheck;
}

// Проверить перед выполнением операции
if (!await _healthCheck.CheckRedis())
{
    _logger.LogWarning("Redis недоступен, используем fallback");
    // fallback логика
}
```

## Конфигурация для разных окружений

### Development (локально без Docker):
```json
{
  "Redis": {
    "Host": "localhost",
    "Port": "6379"
  },
  "PostgreSQL": {
    "Host": "localhost",
    "Port": "5432"
  }
}
```

### Production (в Docker):
```json
{
  "Redis": {
    "Host": "redis",
    "Port": "6379"
  },
  "PostgreSQL": {
    "Host": "postgres",
    "Port": "5432"
  }
}
```

Переменные окружения в `docker-compose.yml` автоматически переопределяют эти настройки.
