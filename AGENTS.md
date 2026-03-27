# AGENTS.md

Этот файл содержит рекомендации для Codex (Codex.ai/code) при работе с кодом в этом репозитории.

## Обзор проекта

CopyTrading - это ASP.NET Core 8.0 приложение, которое отслеживает и копирует торговую активность криптовалют из отслеживаемых кошельков на бирже HyperLiquid. Система использует WebSocket подписки для получения обновлений ордеров и сделок в реальном времени, обрабатывает их через событийно-ориентированную архитектуру и может реплицировать сделки на основе настраиваемых коэффициентов.

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

## Архитектура

### Структура проекта

- **CopyTrading** - Основное ASP.NET Core веб-приложение
- **CopyTrading.Models** - Общие доменные модели и value objects
- **CopyTriding.Test** - xUnit тесты с использованием Moq и FluentAssertions
- **SQLliteBD** - Содержит файл базы данных SQLite (CopyTraidingDB.db)

### Основные компоненты

#### Событийно-ориентированный поток данных

Приложение использует централизованный паттерн шины событий (`DataBusEvents` в `CopyTrading/DataEvents/DataBusEvents.cs`) с тремя основными событиями:

- `NewTrades` - Генерируется при получении новых сделок из WebSocket подписок
- `NewOrders` - Генерируется при получении новых ордеров из WebSocket подписок
- `OrderFinished` - Генерируется когда ордер достигает финального статуса (Filled/Canceled/Rejected)

#### Ключевые сервисы (Singleton жизненный цикл)

**TradeService** (`CopyTrading/Services/TradeService.cs`)
- **Центральный медиатор** для распределения событий `NewTrades`
- Подписывается на событие `DataBusEvents.NewTrades` в конструкторе
- **НЕ** публикует события напрямую - вместо этого вызывает методы других сервисов:
  - `_fillsOrderService.OnNewTrades(newTrades)` - передает трейды для корреляции с ордерами
  - `_currentWalletPositionService.OnNewTrades(newTrades)` - обновляет снапшоты позиций
  - `_realtimeUpdateService.OnNewTrades(newTrades)` - отправляет в UI через SignalR
- Сохраняет трейды в репозитории (InfluxDB и SQLite)
- **ВАЖНО:** `FillsOrderService` и `CurrentWalletPositionService` НЕ подписаны напрямую на `DataBusEvents.NewTrades`, они получают данные через методы `OnNewTrades`, которые вызывает `TradeService`
- Активируется в `Startup.Configure` через `serviceProvider.GetService<TradeService>()`

**OrderService** (`CopyTrading/Services/OrderService.cs`)
- **Центральный медиатор** для распределения событий `NewOrders`
- Подписывается на событие `DataBusEvents.NewOrders` в конструкторе
- **Последовательность обработки в `OnNewOrders()`:**
  1. **Для каждого нового ордера:**
     - Рассчитывает `SubType` через `CurrentWalletPositionService.GetOrderSubType()` (строка 86)
     - Сохраняет ордер в SQLite
     - Рассчитывает минимальный Perpetual Equity через `CalculateMinPerpEquityForOrder()`
     - Сохраняет MinPE в БД
     - Логирует задержку с сервером
  2. **После обработки всех ордеров:**
     - Отправляет в UI через `_realtimeUpdateService.OnNewOrders()`
     - Добавляет в `_fillsOrderService.OnNewOrders()` для корреляции со сделками
     - Пересчитывает SubType для всех pending ордеров через `RecalculateSubTypesForOrders()`
     - Запускает копирование через `_copyOrderService.OnNewOrders()`
- **ВАЖНО:** `FillsOrderService`, `CopyOrderService` и `RealtimeUpdateService` НЕ подписаны напрямую на `DataBusEvents.NewOrders`, они получают данные через методы `OnNewOrders`, которые вызывает `OrderService`
- Активируется в `Startup.Configure` через `serviceProvider.GetService<OrderService>()`

**Расчет SubType (OrderSubType) для ордеров:**

`OrderSubType` определяет тип ордера относительно существующей позиции: Open (новая позиция), Increase (увеличение), Decrease (уменьшение), Close (закрытие). Расчет происходит в два этапа:

**Этап 1: Первичный расчет для новых ордеров** (`OrderService.OnNewOrders`, строка 86)
- Выполняется ДО добавления ордера в `FillsOrderService`
- Вызывается `CurrentWalletPositionService.GetOrderSubType(order)` для каждого нового ордера
- При расчете используется параметр `excludeOrderId: order.OrderId, onlyEarlierOrders: true`
- Это означает: учитываются только pending ордера с OrderId < текущего
- **Критично:** Новые ордера из текущего массива НЕ влияют друг на друга при первичном расчете

**Этап 2: Пересчет для всех pending ордеров** (`OrderService.RecalculateSubTypesForOrders`, строка 100)
- Выполняется ПОСЛЕ добавления всех новых ордеров в `FillsOrderService`
- Группирует ордера по `(Wallet, Symbol)` и вызывает `CurrentWalletPositionService.RecalculateSubTypesForSymbol()`
- Пересчитывает SubType для ВСЕХ pending ордеров (включая только что добавленные)
- Обновляет SubType через `FillsOrderService.UpdateOrderSubType()` если значение изменилось
- Теперь новые ордера учитываются при расчете позиций для старых pending ордеров

**Почему два этапа расчета:**

Если в массиве `orders[]` приходит несколько ордеров одновременно (например, OrderId [100, 50, 200]):
- Без первичного расчета: если добавить все в FillsOrderService сразу, то при расчете SubType для ордера 100, ордер 50 (меньший ID) уже будет в системе и повлияет на расчет, хотя оба пришли одновременно
- С первичным расчетом: каждый новый ордер видит только "старые" pending ордера, что корректно отражает состояние на момент создания
- Пересчет на втором этапе: обновляет SubType для старых pending ордеров с учетом новых, что правильно отражает изменение состояния системы

**Метод `CalculateMinPerpEquityForOrder()`** (`OrderService.cs:110-120`)
- **Единственная ответственность:** рассчитать минимальный Perpetual Equity для ордера
- **НЕ рассчитывает SubType** - это делается отдельно в `OnNewOrders()`
- Принимает уже рассчитанный `order.SubType` и сохраняет его в `MinPEForOrder` для БД
- Возвращает объект `MinPEForOrder` с полями: OrderId, AccountVolume, MinPE, SubType

**FillsOrderService** (`CopyTrading/Services/FillsOrderService.cs`)
- Получает трейды через метод `OnNewTrades`, который вызывает `TradeService` (НЕ подписан напрямую на `DataBusEvents.NewTrades`)
- Получает ордера через метод `OnNewOrders`, который вызывает `OrderService` (НЕ подписан напрямую на `DataBusEvents.NewOrders`)
- Отслеживает исполнение ордеров путем сопоставления ордеров с их сделками
- Поддерживает concurrent словари для корреляции сделок с ордерами
- Обрабатывает pending сделки, которые приходят раньше своих ордеров
- Генерирует события `OrderFinished` когда ордера достигают финальных статусов
- **Методы для работы с OrderSubType:**
  - `GetPendingOrdersByWalletAndSymbol(Wallet, Symbol)` (строки 52-61):
    - Возвращает все pending (не финальные) ордера для указанной пары кошелек+символ
    - Используется `CurrentWalletPositionService` для пересчета SubType
  - `UpdateOrderSubType(long orderId, OrderSubType newSubType)` (строки 63-82):
    - Обновляет SubType для указанного ордера в памяти (в `_orders` словаре)
    - Возвращает true если ордер найден и обновлен, false если не найден
    - Логирует изменение: старое значение → новое значение
  - `AddHistoricalOrders(OriginalOrder[] orders)` (строки 92-130):
    - Добавляет исторические/открытые ордера в память без генерации событий
    - Игнорирует дубликаты по OrderId (если ордер уже существует)
    - Возвращает количество добавленных ордеров
    - Используется при подписке на трейдера для загрузки уже существующих открытых ордеров
- **Автоматическая очистка памяти:**
  - Хранит все ордера в `ConcurrentDictionary<long, OrderFills> _orders`
  - Когда количество ордеров превышает **2000**, запускается автоматическая очистка
  - Удаляет старые **завершенные** ордера (Filled, Canceled, Rejected) начиная с самых старых
  - Очистка продолжается пока количество не уменьшится до **500** ордеров
  - **Открытые ордера НИКОГДА не удаляются** - только завершенные
  - Метод `CleanupOldCompletedOrdersIfNeeded()` вызывается автоматически после каждой обработки ордеров
  - Процесс очистки логируется (INFO уровень для статистики, DEBUG для деталей)
- Активируется в `Startup.Configure` через `serviceProvider.GetService<FillsOrderService>()`

**CopyOrderService** (`CopyTrading/Services/CopyOrderService.cs`)
- Получает ордера через метод `OnNewOrders`, который вызывает `OrderService` (НЕ подписан напрямую на `DataBusEvents.NewOrders`)
- Автоматически реплицирует ордера из отслеживаемых кошельков
- Рассчитывает коэффициенты ордеров на основе размеров счетов
- Проверяет ордера на соответствие минимальным требованиям биржи (min notional value, min quantity)
- Корректирует количество в ордере на основе правил десятичной точности биржи
- Сейчас настроен с `MyWallet` из `WalletSettings` (захардкожено, отмечено как TODO)
- Активируется в `Startup.Configure` через `serviceProvider.GetService<CopyOrderService>()`

**CopyOrderStorageService** (`CopyTrading/Services/CopyOrderStorageService.cs`)
- Хранит и управляет всеми копируемыми ордерами в памяти
- Подписывается на события `DataBusEvents.CopyOrderCreated`, `CopyOrderClosed`, `CopyOrderFilled`
- Предоставляет методы для получения статистики и фильтрации ордеров по статусу
- **Автоматическая очистка памяти:**
  - Хранит копируемые ордера в `ConcurrentDictionary<long, CopyOrderV2> _copyOrders`
  - Когда количество ордеров превышает **2000**, запускается автоматическая очистка
  - Удаляет **все старые ордера** (независимо от статуса) начиная с самых старых по времени
  - Очистка продолжается пока количество не уменьшится до **500** ордеров
  - Метод `CleanupOldOrdersIfNeeded()` вызывается автоматически при добавлении ордера через `AddOrder()`
  - Процесс очистки логируется (INFO уровень для статистики, DEBUG для деталей)
  - Отличие от `FillsOrderService`: удаляет ВСЕ ордера по времени, а не только завершенные
- Активируется в `Startup.Configure` через `serviceProvider.GetRequiredService<CopyOrderStorageService>()`

**CopyOrderResultService** (`CopyTrading/Services/CopyOrderResultService.cs`)
- Отслеживает результаты попыток копирования ордеров (успешные и неудачные)
- Хранит информацию о каждой попытке: OriginalOrderId, TraderWallet, Symbol, успех/ошибка, timestamp
- Предоставляет методы для получения результатов (все, успешные, неудачные, за период)
- Предоставляет статистику (общее количество, успешные, неудачные, процент успеха)
- **Автоматическая очистка памяти:**
  - Хранит результаты в `ConcurrentDictionary<string, CopyOrderResult> _results`
  - Когда количество результатов превышает **2000**, запускается автоматическая очистка
  - Удаляет **все старые результаты** (независимо от статуса) начиная с самых старых по времени (Timestamp)
  - Очистка продолжается пока количество не уменьшится до **500** результатов
  - Метод `CleanupOldResultsIfNeeded()` вызывается автоматически после каждого сохранения результата через `SaveSuccess()` и `SaveFailure()`
  - Процесс очистки логируется (INFO уровень для статистики, DEBUG для деталей)

**CurrentWalletPositionService** (`CopyTrading/Services/CurrentWalletPositionService.cs`)
- Получает трейды через метод `OnNewTrades`, который вызывает `TradeService` (НЕ подписан напрямую на `DataBusEvents.NewTrades`)
- Поддерживает снапшоты позиций кошельков в реальном времени (`WalletPositionsSnapshot`)
- Используется для умной синхронизации при копировании ордеров
- **Ключевые методы для расчета OrderSubType:**
  - `GetOrderSubType(OriginalOrder order)` (строки 194-215):
    - Определяет тип ордера: Open, Increase, Decrease или Close
    - Рассчитывает потенциальную позицию через `CalculatePotentialPosition(order)`
    - Передает только объект ордера, фильтрация по цене исполнения происходит внутри метода
    - Вызывает `DetermineOrderSubType()` для финальной классификации
  - `RecalculateSubTypesForSymbol(Wallet wallet, string symbol)` (строки 231-277):
    - Получает все pending ордера для указанной пары (wallet, symbol) через `FillsOrderService`
    - Пересчитывает SubType для каждого pending ордера
    - Обновляет значения через `FillsOrderService.UpdateOrderSubType()` если изменились
    - Логирует количество обновленных ордеров
  - `CalculatePotentialPosition(OriginalOrder order)` (строки 84-136):
    - Рассчитывает потенциальную позицию = реальная позиция + pending ордера, которые исполнятся раньше
    - **Фильтрация по цене исполнения (не по OrderId!):**
      - Для **Long ордеров** (buy limit) по цене P: учитываются только Long ордера с ценой > P
        - Логика: Long лимит исполняется когда цена падает. Если есть Long ордер с ценой выше текущего, он исполнится раньше
      - Для **Short ордеров** (sell limit) по цене P: учитываются ВСЕ Long ордера + Short ордера с ценой < P
        - Логика: Short лимит исполняется когда цена растет. Все Long ордера закрываются при росте, плюс Short ордера с более низкой ценой
    - Используется для определения SubType без влияния самого ордера и ордеров, которые исполнятся позже

**OrderService, CandleService**
- Различные сервисы бизнес-логики для обработки специфичных доменных операций

#### Интеграция с HyperLiquid

**Провайдеры** (`CopyTrading/Providers/Hyperliquid/Providers/`)
- `WalletInfoProvider` - Получает информацию о кошельке и исторические данные
- `OrdersProvider` - Размещает и управляет ордерами
  - `GetActiveOrders(Wallet wallet)` - Получает все открытые (активные) ордера для указанного кошелька
  - Используется при подписке для загрузки существующих открытых ордеров трейдера
- `ExchangeInfoProvider` - Получает метаданные биржи (минимальные значения, десятичные знаки и т.д.)
- `CandlesProvider` (KlinesProvider) - Получает данные свечей

**Подписчики** (`CopyTrading/Providers/Hyperliquid/Subscribers/`)
- `OrdersTradesSubscriber` - Управляет WebSocket подписками на ордера и сделки
  - Отслеживает статус подписок для каждого кошелька
  - Подписывается на обновления ордеров и сделок отдельно
  - Публикует полученные данные в `DataBusEvents`
  - Использует рекурсивную логику повторных попыток для неудачных подписок (отмечено как TODO для конвертации в while loop)
  - **Загрузка исторических ордеров:**
    - После успешной подписки на новые ордера автоматически загружает все открытые ордера трейдера
    - Вызывает `OrdersProvider.GetActiveOrders()` для получения ордеров из API
    - Добавляет их в `FillsOrderService` через метод `AddHistoricalOrders()`
    - Дубликаты по OrderId игнорируются (если ордер пришел через WebSocket раньше)
    - **Пересчет SubType:** После загрузки ордеров вызывает `CurrentWalletPositionService.RecalculateSubTypesForSymbol()` для каждого уникального символа
    - Это гарантирует что исторические ордера получают корректный SubType с учетом текущих позиций и других pending ордеров
- `OrderBookSubscriber` - Подписывается на обновления книги ордеров

#### Хранение данных

**InfluxDB репозитории** (`CopyTrading/Repository/Influx/`)
- `OrderRepository` - Хранение временных рядов для ордеров
- `TradeRepository` - Хранение временных рядов для сделок
- `CandlesRepository` - Хранение временных рядов для свечей
- Конфигурация через `InfluxSettings`

**SQLite репозитории** (`CopyTrading/Repository/SQLite/`)
- `OrderRepository` - Реляционное хранилище для ордеров
- `TradeRepository` - Реляционное хранилище для сделок
- `WalletInfoRepository` - Снапшоты кошельков и позиций
- Путь к базе данных определен в `SQLLiteSettings.Path`

#### Запланированные задачи

**CollectTradeInfoJob** (`CopyTrading/QuartzJobs/CollectTradeInfoJob.cs`)
- Quartz.NET запланированная задача
- Запускается каждые 20 минут (cron: `0 0/20 * * * ?`)
- Также запускается сразу при старте
- В данный момент закомментирована/неактивна

### Конфигурация

**WalletSettings** (`CopyTrading/Settings/WalletSettings.cs`)
- `TrackedWallets[]` - Массив из 26 кошельков, отслеживаемых для копи-трейдинга
- `MyWallet` - Кошелек, используемый для размещения копируемых ордеров
- `TestFillTrackedWallets[]` - Тестовые адреса кошельков

**SQLLiteSettings** (`CopyTrading/Settings/SQLLiteSettings.cs`)
- Централизованная конфигурация пути к базе данных SQLite
- Используется как для логирования Serilog, так и для репозиториев приложения

**InfluxSettings** (`CopyTrading/Settings/InfluxSettings.cs`)
- Конфигурация подключения к InfluxDB

### Логирование

- Использует Serilog с несколькими выходами (sinks):
  - Вывод в консоль
  - Логирование в файл: `logs/copytrading-.log` (ежедневная ротация)
  - База данных SQLite для структурированных логов
- Serilog UI доступен по эндпоинту `/serilog-ui` (настроен через пакет `Serilog.UI`)

### API эндпоинты

Контроллеры в `CopyTrading/Controllers/`:
- `OrdersController` - Управление ордерами
- `TradesController` - Запросы по сделкам
- `CandlesController` - Данные свечей
- `WalletInfoController` - Информация о кошельках
- `HealthCheckController` - Статус здоровья

Swagger UI доступен в режиме разработки по адресу `/swagger`

## Заметки по разработке

### Паттерн инициализации сервисов

`FillsOrderService` и `CopyOrderService` явно активируются в `Startup.Configure()` с использованием `serviceProvider.GetService<T>()`. Это гарантирует, что их конструкторы выполняются и регистрируются подписки на события, даже если они никуда не инжектятся. Это сделано намеренно для singleton сервисов, которым нужно запустить фоновую обработку.

### Паттерн шины событий

Сервисы общаются через статический класс `DataBusEvents`. При добавлении новых обработчиков событий:
1. Подписывайтесь на события в конструкторах сервисов
2. Обеспечьте потокобезопасность для конкурентных обработчиков событий
3. Помните, что события срабатывают синхронно в потоке подписчика

**Важная особенность архитектуры (Паттерн "Медиатор"):**
- `DataBusEvents.NewTrades` → **только TradeService подписан** → TradeService вызывает методы `OnNewTrades()` у FillsOrderService, CurrentWalletPositionService и RealtimeUpdateService
- `DataBusEvents.NewOrders` → **только OrderService подписан** → OrderService вызывает методы `OnNewOrders()` у FillsOrderService, CopyOrderService и RealtimeUpdateService
- `DataBusEvents.OrderFinished` → генерируется FillsOrderService

Это сделано для централизации логики обработки трейдов и ордеров (сохранение в БД, рассылка в UI, рассылка в другие сервисы) в одном месте - TradeService и OrderService.

**При написании тестов:**
Необходимо создавать экземпляры `TradeService` и `OrderService` в setup методах тестов, даже если они не используются напрямую:
- **TradeService** при создании автоматически подписывается на `DataBusEvents.NewTrades` и транслирует их в `FillsOrderService`, `CurrentWalletPositionService` и `RealtimeUpdateService` через вызов их методов `OnNewTrades()`. Без TradeService эти сервисы не получат трейды.
- **OrderService** при создании автоматически подписывается на `DataBusEvents.NewOrders` и:
  - Рассчитывает SubType для каждого нового ордера
  - Транслирует ордера в `FillsOrderService`, `CopyOrderService` и `RealtimeUpdateService` через вызов их методов `OnNewOrders()`
  - Пересчитывает SubType для всех pending ордеров через `RecalculateSubTypesForOrders()`
  - **Критично:** Без OrderService ордера не получат правильный SubType, и CopyOrderService не сможет корректно копировать ордера

### Управление WebSocket подписками

`OrdersTradesSubscriber` поддерживает раздельное отслеживание подписок на ордера и сделки. Каждый кошелек требует две подписки. Неудачные подписки автоматически повторяются рекурсивно (рассмотрите рефакторинг в итеративный подход согласно TODO).

### Корреляция данных

`FillsOrderService` выполняет сложную задачу корреляции сделок с ордерами:
- Ордера могут приходить до или после своих сделок
- Использует словарь `_pendingTrades` для сделок без соответствующих ордеров
- Проверяет, что исполненные ордера соответствуют агрегированным количествам сделок

### Логика пропорционального копирования ордеров

При копировании ордеров (`CopyOrderService`) реализован пропорциональный расчет на основе **доли от капитала** трейдера:

**Шаг 1: Получение информации о балансах**
```csharp
traderWalletInfo = await _walletProvider.GetInfo(order.Wallet);       // Баланс трейдера
myWalletInfo = await _walletProvider.GetInfo(_myWallet, false);       // Ваш баланс
myAccountValue = myWalletInfo.AccountVolume;
```

**Шаг 2: Расчет доли от капитала трейдера**
```csharp
orderRatio = order.VolumeUsd / traderWalletInfo.AccountVolume;
// Например: $1,000 / $20,000 = 0.05 (трейдер вкладывает 5% своего счета)
```

**Шаг 3: Расчет вашего объема позиции (та же доля)**
```csharp
myVolumeUsd = myAccountValue * orderRatio;
// Например: $2,000 × 0.05 = $100 (вы тоже вкладываете 5% своего счета)
```

**Шаг 4: Расчет количества монет**
```csharp
myQuantity = myVolumeUsd / order.Price;
// Например: $100 / $50,000 = 0.002 BTC
```

**Шаг 5: Коррекция и валидация**
- Округление до точности биржи через `ExchangeInfoProvider.GetExchangeInfo(symbol).QuantityDecimals`
- Проверка на соответствие `MinNotionalValue` (минимум $10)
- Проверка на соответствие `MinTradeQuantity`

**Важно:**
- Leverage копируется из оригинального ордера (но логика установки leverage для копируемого ордера - TODO)
- Если трейдер использует высокое плечо (например, позиция = 300% от счета), вы тоже откроете позицию = 300% от своего счета
- Маржа масштабируется пропорционально: если трейдер вкладывает 50% своего счета в маржу, вы тоже вкладываете 50%

**Пример:**
- Трейдер: баланс $20,000, открывает 0.02 BTC × $50,000 = $1,000 (5% от счета)
- Вы: баланс $2,000, откроете 0.002 BTC × $50,000 = $100 (тоже 5% от счета)
- Пропорция сохранена: обе позиции составляют 5% от соответствующих балансов

### Умная синхронизация при копировании ордеров

Система обрабатывает различные типы ордеров (`OrderSubType`) и применяет разные стратегии в зависимости от наличия маппинга позиции:

#### Обработка ордеров типа Increase (увеличение позиции)

**Случай 1: Маппинг существует (мы уже отслеживаем позицию)**
- Используется **сохраненная пропорция** из маппинга (`mapping.PositionRatio`)
- Увеличиваем нашу позицию: `myIncreaseQuantity = order.Quantity * mapping.PositionRatio`
- Обновляем `mapping.MyQuantity += myIncreaseQuantity`

**Случай 2: Маппинг НЕ существует (первый раз видим позицию трейдера)**
- Применяется **умная синхронизация** (CopyOrderService.cs:247-291)
- Получаем snapshot текущей позиции трейдера через `CurrentWalletPositionService.GetSnapshot()`
- Создаем "синтетический" ордер на **всю текущую позицию трейдера**, а не только на increase
- Открываем пропорциональную позицию, синхронизированную с трейдером
- **Устанавливаем базовую линию:** `TraderQuantityAtEntry = traderTotalQuantity - order.Quantity`
  - Это позиция трейдера **ДО** increase (например: 110 - 10 = 100)

**Пример синхронизации:**
```
Ситуация:
- У трейдера уже есть long 100 BTC
- Трейдер делает increase +10 BTC (итого 110 BTC)
- У нас нет позиции (нет маппинга)

Без синхронизации (старая логика):
- Копируем только +10 → открываем позицию 1 BTC (при коэффициенте 0.1)
- ❌ Рассинхрон: У трейдера 110 BTC, у нас 1 BTC

С умной синхронизацией (текущая логика):
- Получаем snapshot → видим итоговую позицию трейдера 110 BTC
- Создаем синтетический ордер на 110 BTC
- Открываем позицию 11 BTC (110 × 0.1)
- ✅ Синхронизация: У трейдера 110 BTC, у нас 11 BTC (пропорция сохранена)
```

#### Обработка ордеров типа Decrease (уменьшение позиции)

**Логика "Базовой линии":**

Система использует концепцию "базовой линии" (`TraderQuantityAtEntry`) - позиции трейдера **ДО** первого increase, который мы скопировали. Это позволяет отслеживать только те изменения, которые произошли после нашего входа.

**Маппинг существует:**
1. Получаем **реальную позицию трейдера** из snapshot: `actualTraderQuantity`
2. Сравниваем с базовой линией: `if (actualTraderQuantity <= mapping.TraderQuantityAtEntry)`

**Случай A: Трейдер ушел ниже базовой линии**
- Трейдер закрыл всё что было после нашего входа (и даже больше)
- **Действие:** Закрываем ВСЮ нашу позицию (`myCloseQuantity = mapping.MyQuantity`)
- Удаляем маппинг

**Случай B: Трейдер выше базовой линии**
- Рассчитываем "позицию над базовой": `traderAboveBaseline = actualTraderQuantity - TraderQuantityAtEntry`
- Рассчитываем долю закрытия: `closeRatio = order.Quantity / traderAboveBaseline`
- Закрываем ту же долю: `myCloseQuantity = mapping.MyQuantity * closeRatio`
- Обновляем `mapping.MyQuantity -= myCloseQuantity`

**Пример:**
```
Базовая линия: 100 BTC (позиция трейдера при нашем входе)
Increase +10 → 110: открываем 1 BTC, TraderQuantityAtEntry=100
Increase +5  → 115: добавляем 0.5 BTC, TraderQuantityAtEntry=100 (не меняется), total=1.5

Decrease -5  → 110:
  actualTraderQuantity = 110 > 100 (выше базовой)
  traderAboveBaseline = 110 - 100 = 10
  closeRatio = 5 / 10 = 0.5
  myCloseQuantity = 1.5 * 0.5 = 0.75
  Осталось: 0.75 BTC ✅

Decrease -20 → 90:
  actualTraderQuantity = 90 <= 100 (ниже базовой!)
  Закрываем ВСЁ: 0.75 BTC
  Удаляем маппинг ✅
```

**Маппинг НЕ существует:**
- **Не копируем** ордер (нечего уменьшать)
- Логируем: `"OrderId={order.OrderId} Невозможно скопировать decrease - у нас нет открытой позиции"`

#### Обработка ордеров типа Close (полное закрытие)

**Маппинг существует:**
- Закрываем **всю** нашу позицию: `myCloseQuantity = mapping.MyQuantity`
- Удаляем маппинг через `_positionMappingService.DeleteMapping()`

**Маппинг НЕ существует:**
- **Не копируем** ордер (нечего закрывать)
- Логируем: `"OrderId={order.OrderId} Невозможно скопировать close - у нас нет открытой позиции"`

#### Обработка ордеров типа Open (открытие новой позиции)

- Всегда создается новый маппинг
- Рассчитывается пропорция на основе текущих балансов
- Сохраняется `mapping.PositionRatio` для будущих операций Increase/Decrease
- **Устанавливаем базовую линию:** `TraderQuantityAtEntry = 0` (трейдер открывает позицию с нуля)

#### Таблица поведения

| Тип ордера | Маппинг существует | Маппинг НЕ существует |
|------------|-------------------|----------------------|
| **Open** | Ошибка (уже есть позиция) | ✅ Открываем новую позицию |
| **Increase** | ✅ Увеличиваем по пропорции | ✅ **Синхронизация** с текущей позицией |
| **Decrease** | ✅ Уменьшаем по доле | ❌ Не копируем, логируем |
| **Close** | ✅ Закрываем полностью | ❌ Не копируем, логируем |

## Зависимости

Ключевые NuGet пакеты:
- **HyperLiquid.Net** (v2.13.1) - Клиентская библиотека биржи
- **Quartz** (v3.15.0) - Планирование задач
- **Serilog** - Структурированное логирование с UI
- **InfluxDB.Client** (v4.18.0) - База данных временных рядов
- **Microsoft.Data.Sqlite** (v9.0.9) - Доступ к SQLite
- **WatsonWebsocket** (v4.1.6) - Поддержка WebSocket
- **Swashbuckle.AspNetCore** (v6.8.0) - Swagger/OpenAPI

Зависимости для тестирования:
- xUnit, Moq, FluentAssertions, coverlet.collector
