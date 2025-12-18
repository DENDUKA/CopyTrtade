# CopyTrading

ASP.NET Core 8.0 приложение для отслеживания и копирования торговой активности криптовалют из отслеживаемых кошельков на бирже HyperLiquid.

## Технологии

### Основной стек

**Платформа и язык:**
- **ASP.NET Core 8.0** - веб-приложение
- **C#** - язык программирования

### Базы данных

**Реляционные БД:**
- **PostgreSQL** - основная реляционная БД
- **SQLite** - используется для логов Serilog

**NoSQL/Кэширование:**
- **InfluxDB** (v4.18.0) - база данных временных рядов для ордеров, трейдов и свечей
- **Redis** - кэширование данных

### Real-time коммуникация

- **WebSocket** (WatsonWebsocket v4.1.6) - подписки на обновления от биржи HyperLiquid
- **SignalR** - real-time обновления в UI

### Планирование и фоновые задачи

- **Quartz.NET** (v3.15.0) - планировщик задач

### Логирование и мониторинг

- **Serilog** - структурированное логирование
  - Console sink
  - File sink (ежедневная ротация)
  - SQLite sink
  - **Serilog.UI** - веб-интерфейс для просмотра логов (`/serilog-ui`)

### API и документация

- **Swagger/OpenAPI** (Swashbuckle.AspNetCore v6.8.0) - документация API (`/swagger`)
- **ASP.NET Core Controllers** - RESTful API

### Внешние интеграции

- **HyperLiquid.Net** (v2.13.1) - клиентская библиотека для биржи HyperLiquid

### Тестирование

- **xUnit** - фреймворк для unit-тестов
- **Moq** - мокирование зависимостей
- **FluentAssertions** - выразительные assertions
- **coverlet.collector** - покрытие кода

### Контейнеризация

- **Docker** - контейнеризация приложения (docker-compose.yml)

### Архитектурные паттерны

- **Event Bus** - централизованная шина событий (`DataBusEvents`)
- **Mediator Pattern** - TradeService и OrderService как медиаторы
- **Repository Pattern** - разделение слоя данных (Influx, SQLite, PostgreSQL, Redis)
- **Dependency Injection** - встроенный DI контейнер ASP.NET Core

## Команды для сборки и запуска

### Сборка
```bash
dotnet build CopyTrading.sln
```

### Запуск
```bash
dotnet run --project CopyTrading/CopyTrading.csproj
```

### Тестирование
```bash
# Запустить все тесты
dotnet test CopyTriding.Test/CopyTriding.Test.csproj

# Запустить конкретный тест
dotnet test CopyTriding.Test/CopyTriding.Test.csproj --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### Очистка
```bash
dotnet clean CopyTrading.sln
```

### Docker
```bash
# Запуск через Docker Compose
docker-compose up -d

# Просмотр логов
docker-compose logs -f

# Остановка
docker-compose down
```

## Архитектура

Подробное описание архитектуры, компонентов и бизнес-логики доступно в [CLAUDE.md](CLAUDE.md).

## Эндпоинты

- **Swagger UI**: `/swagger` - документация API
- **Serilog UI**: `/serilog-ui` - просмотр логов
- **Health Check**: через `HealthCheckController`

## Структура проекта

- **CopyTrading** - Основное ASP.NET Core веб-приложение
- **CopyTrading.Models** - Общие доменные модели и value objects
- **CopyTriding.Test** - xUnit тесты с использованием Moq и FluentAssertions
- **SQLliteBD** - Содержит файл базы данных SQLite (CopyTraidingDB.db)
