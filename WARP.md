# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Быстрые команды (dotnet)

- Сборка решения:
  - `dotnet build CopyTrading.sln`
- Запуск приложения (Swagger/SignalR/Serilog UI):
  - `dotnet run --project CopyTrading/CopyTrading.csproj`
  - Локально по умолчанию: `http://localhost:5197` (см. `CopyTrading/Properties/launchSettings.json`)
  - Swagger: `http://localhost:5197/swagger`
  - Serilog UI (логи): `http://localhost:5197/serilog-ui`
- Горячий перезапуск при разработке:
  - `dotnet watch --project CopyTrading/CopyTrading.csproj run`
- Тесты (xUnit):
  - Все тесты: `dotnet test CopyTriding.Test/CopyTriding.Test.csproj`
  - Один тест: `dotnet test CopyTriding.Test/CopyTriding.Test.csproj --filter "FullyQualifiedName~ИмяКлассаТеста.ИмяМетода"`
  - Покрытие: `dotnet test CopyTriding.Test/CopyTriding.Test.csproj --collect:"XPlat Code Coverage"`
- Форматирование/линт по .editorconfig:
  - Исправить: `dotnet format`
  - Проверить без изменений: `dotnet format --verify-no-changes`

## Полезные локальные эндпоинты для отладки

Базовый URL при локальном запуске: `http://localhost:5197`

- Подписки на ордера/сделки отслеживаемых кошельков (запускают основной поток данных):
  - `GET /Orders/SubscribeToTrackedWallets`
  - `GET /Trades/SubscribeToTrackedWallets`
- Подписка на конкретный кошелёк:
  - `GET /Orders/SubscribeToNewOrders?wallet={WALLET}`
  - `GET /Trades/SubscribeToWallet?wallet={WALLET}`
- Прочее:
  - Swagger: `/swagger`
  - Serilog UI: `/serilog-ui`

## Высокоуровневая архитектура

Проект на .NET 8 состоит из трёх проектов:
- `CopyTrading` — ASP.NET Core (веб-API + Blazor Server/SignalR) с бизнес-логикой и провайдерами.
- `CopyTrading.Models` — доменные модели и enum’ы.
- `CopyTriding.Test` — модульные/интеграционные тесты (xUnit, Moq, FluentAssertions, coverlet.collector).

Ключевая идея — событийная шина `DataBusEvents` (`CopyTrading/DataEvents/DataBusEvents.cs`) между подписчиками биржи и доменными сервисами:
- События:
  - `NewOrders(OriginalOrder[])` — новые/изменившиеся ордера трейдеров.
  - `NewTrades((OriginalTrade[] Trades, bool IsSnapshot))` — сделки, снапшоты и инкрементальные обновления.
  - `OrderFinished(OrderFills)` — ордер дошёл до финального состояния.
  - `CopyOrderCreated(CopyOrderV2)`, `CopyOrderClosed((OriginalOrder, OrderStatus))`, `CopyOrderFilled((OriginalOrder, OrderStatus))` — события жизненного цикла копируемых ордеров.

Поток данных (big picture):
1) Подписчики HyperLiquid (WebSocket) → `OrdersTradesSubscriber` (`CopyTrading/Providers/Hyperliquid/Subscribers/OrdersTradesSubscriber.cs`).
   - Поддерживает отдельные подписки на ордера и сделки по множеству кошельков.
   - По приходу данных маппит их и публикует в `DataBusEvents`.
2) Доменные сервисы:
   - `CurrentWalletPositionService` — поддерживает in-memory snapshot позиций по кошелькам (ConcurrentDictionary + семафоры), определяет `OrderSubType` для ордеров (Open/Increase/Decrease/Close), отдаёт snapshot/плечо по символу.
   - `PositionMappingService` — in-memory маппинг позиций трейдер → моя позиция (ключ: трейдерский кошелёк + мой кошелёк + символ + направление). Хранит `PositionRatio` и текущие количества.
   - `CopyOrderService` — основной обработчик `NewOrders`: по `OrderStatus` и `OrderSubType` создаёт/изменяет копируемые ордера (расчёт количества с учётом сохранённого `PositionRatio`), публикует `CopyOrder*` события. Интеграция размещения/отмены на бирже помечена TODO и должна идти через `OrdersProvider`.
   - `CopyTradeVolumePerWalletService` — хранит настройки по Wallet (USD и CopyKoef), API принимает только модель `CopyTradeWalletSettings` в `CreateOrUpdate`/`Delete`.
   - `FillsOrderService` — коррелирует сделки и ордера, формирует `OrderFills` и триггерит `OrderFinished` (инициализируется в `Startup.Configure`).
3) Хранилища:
   - InfluxDB репозитории (`CopyTrading/Repository/Influx/*`) для временных рядов (ордера/сделки/свечи).
   - SQLite репозитории (`CopyTrading/Repository/SQLite/*`) для реляционных данных (ордера, сделки, wallet snapshots, настройки Wallet).
     - `WalletSettingsRepository.cs` — работает с таблицей `WalletSettings` (обязательные поля: `Wallet` TEXT PK, `ValueUsd` REAL, `CopyKoef` REAL, у `CopyKoef` дефолт 1.0). Миграций не выполняет, колонка `CopyKoef` обязательна.
   - Serilog также пишет логи в SQLite (таблица `Logs`).
4) Веб-слой и UI real-time:
   - Контроллеры (`CopyTrading/Controllers/*`): `OrdersController`, `TradesController`, `WalletInfoController`, `CandlesController`, `HealthCheckController` — REST API для подписок, сбора истории и получения данных.
   - Blazor UI страницы (`CopyTrading/BlazorUI/Pages/*`):
     - `WalletSettingsPage.razor` → маршрут `/wallet-settings` для управления Wallet-настройками (USD и CopyKoef).
   - SignalR Hub (`CopyTrading/BlazorUI/Hubs/CopyTradingHub.cs`) и сервис `RealtimeUpdateService` — ретрансляция событий `DataBusEvents` в UI (каналы `ReceiveOrders`, `ReceiveTrades`, `PositionsUpdated`, `ReceiveOrderFinished`). В `Startup` зарегистрирован Blazor Server и маршруты (`MapBlazorHub`, `MapFallbackToPage("/_Host")`).

Инициализация и запуск сервисов:
- В `Program.cs` настраивается Serilog и создаётся хост со `Startup`.
- В `Startup.ConfigureServices` регистрируются все singleton-сервисы, провайдеры/подписчики HyperLiquid, репозитории, SignalR/Blazor, Serilog UI.
- В `Startup.Configure` сервисы, которым нужны подписки/фоновая работа, явно «пробуждаются» через `serviceProvider.GetRequiredService(...)`.

Конфигурация:
- `CopyTrading/appsettings.json` — уровни логирования и `AllowedHosts` (доп. настройки берутся из кодовых `Settings/*`: `WalletSettings`, `SQLLiteSettings`, `InfluxSettings`).
- `CopyTrading/Properties/launchSettings.json` — локальный профиль запуска (порт).

Замечания по интеграции с биржей:
- `OrdersProvider` содержит заглушки для операций размещения/отмены и проверки статусов; ключ/секрет сейчас заданы как «1» и должны быть вынесены в секреты/конфигурацию перед продакшеном.

## Важные выдержки из CLAUDE.md (применимо и к Warp)
- Используйте команды сборки/запуска/тестов, указанные выше.
- Сервисы с подписками (например, `FillsOrderService`, `CopyOrderService`) должны инициализироваться при старте, иначе они не подпишутся на `DataBusEvents`.
- Шина событий централизована (`DataBusEvents`); подписывайтесь в конструкторах и учитывайте потокобезопасность.

## Типичный сценарий разработки/отладки
1) Запустить сервер: `dotnet run --project CopyTrading/CopyTrading.csproj` (Swagger и Serilog UI доступны по URL выше).
2) Открыть `/wallet-settings` — страница управления настройками (USD и CopyKoef). Ввод дробной части допускает точку и запятую.
3) Инициализировать поток данных, вызвав:
   - `GET /Orders/SubscribeToTrackedWallets`
   - `GET /Trades/SubscribeToTrackedWallets`
4) Смотреть логи и состояние в `/serilog-ui`, а также проверять события через SignalR Hub (клиент UI получит `ReceiveOrders`/`ReceiveTrades`).
5) Запускать тесты/покрытие и форматирование кода командами из раздела «Быстрые команды».

### Примечание (сборка)
- Если при `dotnet build` видите ошибки `MSB3021/MSB3027` о блокировке `CopyTrading.exe`, закройте запущенное приложение перед сборкой.

## Правила проекта (именование)
- Не использовать суффикс `Async` в именах методов, даже если они возвращают `Task`.
