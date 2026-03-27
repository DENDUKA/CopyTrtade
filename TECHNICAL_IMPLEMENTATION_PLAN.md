# Technical Implementation Plan — CopyTrading

> Дата: 2026-03-27
> Основано на: `PRD.md`, `AGENTS.md`, текущей архитектуре проекта

---

## 1. Цель документа

Этот документ описывает поэтапный технический план реализации следующих направлений:

- персистентный `PositionMapping` и `OrderMapping`
- идемпотентность `NewOrders` / `NewTrades`
- recovery after restart
- dry-run / paper-trading режим
- kill switch
- reconciliation job
- dead letter / failed actions queue
- audit trail
- отказ от `SQLliteBD` как основного runtime-state хранилища
- удаление V1 copy pipeline
- архитектурная декомпозиция: `IEventBus`, отказ от `async void`, разделение decision/execution

Документ ориентирован на реализацию без разрушения текущего рабочего потока `OrderService -> CopyOrderService2 -> OrdersProvider`.

---

## 2. Принципы внедрения

### 2.1 Что нельзя ломать

- Двухэтапный расчёт `OrderSubType` в `OrderService`
- Текущий порядок обработки `NewOrders` и `NewTrades`
- Корреляцию ордеров и трейдов в `FillsOrderService`
- Snapshot-логику в `CurrentWalletPositionService` как источник истины по позиции трейдера

### 2.2 Стратегия внедрения

- Внедрять новые компоненты параллельно текущей логике, затем переключать потребителей
- Сначала добавить наблюдаемость и персистентность, потом менять execution path
- Все критичные шаги вводить через feature flags
- Каждую фазу сопровождать тестами и безопасным rollback path

### 2.3 Целевой технический результат

- После рестарта система восстанавливает состояние автоматически
- Повторные события не приводят к повторным сделкам
- Решение о копировании и фактическое размещение разделены
- Runtime state не зависит от `SQLliteBD`
- В коде остаётся только V2 copy pipeline

---

## 3. Целевая архитектура

### 3.1 Новые логические слои

**Decision Engine**
- принимает `OriginalOrder` / состояние позиции / mappings / конфигурацию
- возвращает `CopyDecision`
- не знает о HyperLiquid API напрямую

**Execution Engine**
- принимает `CopyDecision`
- выполняет `PlaceOrder`, `CancelOrder`, reconciliation actions
- умеет работать в `dry-run` и `live` режиме

**State Store**
- хранит mappings, dedup-state, audit trail, failed actions, runtime flags

**Event Bus Abstraction**
- интерфейс над текущим `DataBusEvents`
- сначала обёртка, потом постепенный отказ от static pub/sub

### 3.2 Новые сущности

- `PositionMapping`
- `OrderMapping`
- `ProcessedEventMarker`
- `CopyDecision`
- `ExecutionCommand`
- `ExecutionResult`
- `FailedAction`
- `AuditRecord`
- `SystemModeState`
- `ReconciliationReport`

---

## 4. Поэтапный план

## Phase 0 — Подготовка и стабилизация

### Цель

Подготовить кодовую базу к безопасному рефакторингу без изменения бизнес-поведения.

### Изменения

- ввести интерфейсы:
  - `IClock`
  - `IIdGenerator`
  - `IEventBus`
- добавить feature flags:
  - `CopyTrading:DryRunEnabled`
  - `CopyTrading:KillSwitchEnabled`
  - `CopyTrading:RecoveryEnabled`
  - `CopyTrading:ReconciliationEnabled`
- выделить текущие места с `async void`
- описать все места публикации и подписки на `DataBusEvents`
- пометить V1 copy pipeline как deprecated в коде и DI

### Затрагиваемые файлы/области

- `CopyTrading/DataEvents/`
- `CopyTrading/Services/OrderService.cs`
- `CopyTrading/Services/TradeService.cs`
- `CopyTrading/Services/CopyOrderService.cs`
- `CopyTrading/Services/CopyOrderService2.cs`
- `Startup` / DI registration
- `Settings/*`

### Результат

- код готов к постепенному внедрению новых компонентов
- есть карта текущих event-chain и async-boundary

### Тесты

- unit tests на `IClock`, `IIdGenerator`, `IEventBus` adapters
- smoke tests на старый pipeline после DI-изменений

---

## Phase 1 — Персистентный runtime-state

### Цель

Убрать критичную зависимость от in-memory состояния для mappings и dedup.

### Что внедряем

- `IPositionMappingRepository`
- `IOrderMappingRepository`
- `IProcessedEventRepository`
- `IAuditTrailRepository`
- `IFailedActionRepository`

### Минимальный набор таблиц/коллекций

#### PositionMappings

- `Id`
- `TraderWallet`
- `MyWallet`
- `Symbol`
- `Direction`
- `TraderQuantity`
- `MyQuantity`
- `PositionRatio`
- `TraderQuantityAtEntry`
- `Status`
- `CreatedAt`
- `UpdatedAt`

#### OrderMappings

- `Id`
- `TraderOrderId`
- `MyOrderId`
- `TraderWallet`
- `MyWallet`
- `Symbol`
- `OrderSubType`
- `Status`
- `OriginalPrice`
- `OriginalQuantity`
- `CopiedQuantity`
- `CreatedAt`
- `UpdatedAt`

#### ProcessedEvents

- `Id`
- `EventType`
- `ExternalId`
- `Hash`
- `ProcessedAt`
- `ExpiresAt`

#### FailedActions

- `Id`
- `ActionType`
- `Payload`
- `Reason`
- `Attempts`
- `LastError`
- `NextRetryAt`
- `Status`
- `CreatedAt`
- `UpdatedAt`

#### AuditTrail

- `Id`
- `CorrelationId`
- `TraderOrderId`
- `MyOrderId`
- `Stage`
- `Payload`
- `CreatedAt`

### Важные решения

- не привязывать новые репозитории к `SQLliteBD`
- repository interfaces должны быть независимы от конкретной БД
- migration path должен позволять временный dual-write при переходе

### Затрагиваемые сервисы

- `CopyOrderService2`
- `CopyOrderStorageService`
- `CopyOrderResultService`
- новый `PositionMappingService`
- новый `OrderMappingService`

### Результат

- mappings и markers сохраняются вне памяти
- система готова к recovery после рестарта

### Тесты

- repository integration tests
- tests на create/update/delete mapping lifecycle
- tests на optimistic concurrency / duplicate insert

---

## Phase 2 — Идемпотентность event intake и side effects

### Цель

Сделать обработку `NewOrders` и `NewTrades` безопасной при повторной доставке.

### Подход

**На входе**
- проверка `ProcessedEvents` до начала побочной обработки
- выделение ключа идемпотентности:
  - для ордеров: `OrderId` + `Status` + существенные поля
  - для трейдов: `TradeId` или стабильный hash полезной нагрузки

**На выходе**
- `PlaceOrder` и `CancelOrder` должны быть защищены от повторного исполнения
- повторная обработка не должна повторно создавать `OrderMapping`

### Затрагиваемые сервисы

- `OrderService`
- `TradeService`
- `CopyOrderService2`
- `FillsOrderService`
- `CurrentWalletPositionService`

### Результат

- duplicate delivery не приводит к duplicate execution

### Тесты

- повтор одного и того же `NewOrder`
- повтор одного и того же `NewTrade`
- race test: два одинаковых сообщения почти одновременно

---

## Phase 3 — Recovery after restart

### Цель

После старта приложения автоматически восстанавливать рабочее состояние.

### Новый startup workflow

1. инициализировать репозитории и mode-state
2. загрузить `PositionMappings`, `OrderMappings`, `ProcessedEvents`
3. загрузить незавершённые `CopyOrder`
4. запросить открытые ордера трейдеров и пользователя
5. запросить актуальные snapshot позиций
6. восстановить in-memory state:
   - `FillsOrderService`
   - `CopyOrderStorageService`
   - snapshot current positions
7. выполнить стартовую reconciliation
8. только после этого включить live execution

### Новый компонент

- `StartupRecoveryService`

### Риски

- устаревший mapping может конфликтовать с реальным состоянием биржи
- snapshot и order state могут быть в разных временных точках

### Способ снижения рисков

- маркировать восстановленные данные как `Recovered`
- первая reconciliation должна переводить их в `Confirmed` или `Conflict`

### Тесты

- restart with open positions
- restart with pending orders
- restart after partial fill
- restart with stale mapping

---

## Phase 4 — Dry-run и kill switch

### Цель

Добавить безопасный режим эксплуатации без отключения мониторинга.

### Dry-run

**Поведение**
- `Decision Engine` работает полностью
- `Execution Engine` не вызывает реальные `PlaceOrder` / `CancelOrder`
- создаётся виртуальный `ExecutionResult`
- в audit trail пишется, что действие было simulated

### Kill switch

**Поведение**
- intake событий продолжается
- snapshots и UI продолжают обновляться
- любые execution-команды отклоняются до отправки на биржу
- причина блокировки логируется и попадает в audit trail

### Новый компонент

- `ISystemModeService`

### Тесты

- live mode -> dry-run
- live mode -> kill switch on
- kill switch не ломает snapshot updates

---

## Phase 5 — Reconciliation job

### Цель

Автоматически выявлять расхождения между локальным состоянием и фактическим состоянием биржи.

### Новый компонент

- `ReconciliationJob`
- `IReconciliationService`

### Виды сверки

**Orders reconciliation**
- активные ордера трейдера на бирже vs локальные pending orders
- активные ордера пользователя на бирже vs `OrderMapping`

**Position reconciliation**
- snapshot позиции трейдера vs `PositionMapping`
- реальные позиции пользователя vs локальные copy-position expectations

**Execution reconciliation**
- `CopyOrder` status vs `OrderMapping` status vs exchange order status

### Выходные данные

- `ReconciliationReport`
- severity:
  - `Info`
  - `Warning`
  - `Critical`

### Авто-действия

- safe retry
- перевод failed action в retry queue
- пометка mapping как conflict
- alert/logging

### Тесты

- missing order
- unexpected open order
- mapping mismatch
- position mismatch after partial fill

---

## Phase 6 — Dead letter queue и retry policy

### Цель

Не терять ошибочные действия и обрабатывать их отдельно от основного realtime pipeline.

### Новый компонент

- `FailedActionProcessor`

### Что попадает в queue

- неудачный `PlaceOrder`
- неудачный `CancelOrder`
- reconciliation action, не выполненный автоматически
- восстановление state, завершившееся конфликтом

### Retry policy

- exponential backoff
- max attempts
- перевод в terminal failed status
- ручной retry через admin endpoint/UI в будущем

### Тесты

- transient API error
- validation error without infinite retry
- retry preserves idempotency key

---

## Phase 7 — Audit trail

### Цель

Сделать разбор инцидентов и спорных сделок детерминированным.

### Что писать в аудит

- входной `TraderOrder`
- результат расчёта `OrderSubType`
- `CopyDecision`
- execution command
- response биржи
- fills
- close/cancel/reconcile result
- source mode: `Live`, `DryRun`, `Recovery`, `Reconciliation`

### Требования

- единый `CorrelationId` на цепочку
- audit не должен зависеть от UI
- записи должны быть пригодны для реконструкции сценария

### Тесты

- одна полная цепочка open -> increase -> decrease -> close
- восстановление цепочки по `TraderOrderId`

---

## Phase 8 — Удаление V1 и архитектурная чистка

### Цель

Сократить техдолг после стабилизации новой архитектуры.

### Что удалить

- `CopyOrderService` V1
- все DI-регистрации и вызовы, связанные только с V1
- тесты, фикстуры и документацию по V1

### Что рефакторим

- прямые вызовы `DataBusEvents` через адаптер `IEventBus`
- замена `async void` на `Task`-based flow
- выделение:
  - `ICopyDecisionEngine`
  - `ICopyExecutionEngine`

### Критерий завершения

- в коде не осталось production-путей V1
- решение и исполнение разделены по сервисам

---

## Phase 9 — Отказ от `SQLliteBD`

### Цель

Полностью убрать `SQLliteBD` из критичного runtime-контура.

### Шаги

1. перевести runtime-state на новое хранилище
2. убрать зависимость сервисов от файлового SQLite
3. оставить только миграционный read-only путь при необходимости
4. обновить документацию, startup, env settings, docker config
5. удалить неиспользуемые репозитории и настройки

### Важно

- этот этап делать после стабильного recovery, reconciliation и audit trail
- логирование Serilog в SQLite тоже желательно отдельно пересмотреть, чтобы не смешивать operational logging и state-store

### Критерий завершения

- production запуск не требует папки `SQLliteBD`
- runtime state не хранится в файловой БД

---

## 5. Предлагаемый порядок реализации

Рекомендуемый порядок:

1. `Phase 0` — интерфейсы, flags, карта async/event проблем
2. `Phase 1` — persistent mappings + processed events + audit base
3. `Phase 2` — idempotency
4. `Phase 3` — recovery after restart
5. `Phase 4` — dry-run + kill switch
6. `Phase 5` — reconciliation job
7. `Phase 6` — failed actions queue
8. `Phase 7` — полный audit trail
9. `Phase 8` — удалить V1 и разрезать architecture boundaries
10. `Phase 9` — убрать `SQLliteBD`

---

## 6. Какие сервисы будут затронуты сильнее всего

### Высокое влияние

- `CopyTrading/Services/CopyOrderService2.cs`
- `CopyTrading/Services/OrderService.cs`
- `CopyTrading/Services/TradeService.cs`
- `CopyTrading/Services/FillsOrderService.cs`
- `CopyTrading/Services/CurrentWalletPositionService.cs`
- `CopyTrading/Providers/Hyperliquid/Providers/OrdersProvider.cs`
- `Startup` / composition root

### Среднее влияние

- `CopyTrading/Services/CopyOrderStorageService.cs`
- `CopyTrading/Services/CopyOrderResultService.cs`
- Quartz jobs
- repository layer
- settings/configuration layer

### Низкое влияние

- controllers/UI, если ограничиться отображением mode-state и diagnostics

---

## 7. Feature flags и безопасное включение

Рекомендуемые флаги:

- `CopyTrading:UsePersistentMappings`
- `CopyTrading:UseProcessedEventDedup`
- `CopyTrading:EnableStartupRecovery`
- `CopyTrading:EnableDryRun`
- `CopyTrading:EnableKillSwitch`
- `CopyTrading:EnableReconciliation`
- `CopyTrading:EnableFailedActionProcessor`
- `CopyTrading:UseEventBusAdapter`

Стратегия включения:

1. deploy с выключенными флагами
2. включить persistent storage в shadow mode
3. включить dedup
4. включить recovery
5. включить reconciliation
6. только потом начать удаление старых путей

---

## 8. План тестирования

### Unit tests

- decision logic по `OrderSubType`
- idempotency checks
- dry-run branching
- kill switch blocking
- retry policy

### Integration tests

- mapping persistence
- restart recovery
- reconciliation against mocked exchange state
- failed action retry with mocked provider

### End-to-end сценарии

1. `Open -> Increase -> Decrease -> Close`
2. duplicate `NewOrder`
3. duplicate `NewTrade`
4. restart in the middle of open position lifecycle
5. cancel from trader side
6. exchange API transient failure
7. dry-run mode on real event flow
8. kill switch during active intake

---

## 9. Основные риски

### Риск 1: Нарушение текущего порядка обработки событий

Снижение риска:
- не менять порядок `OrderService` / `TradeService` до появления тестов
- оборачивать новую логику вокруг существующей, а не вместо неё на первом этапе

### Риск 2: Конфликт recovery и live intake

Снижение риска:
- запуск live execution только после завершения recovery phase
- блокировка execution-команд на время восстановления

### Риск 3: Ложные дубликаты при идемпотентности

Снижение риска:
- использовать составной ключ + hash payload
- отдельно тестировать legitimate status changes одного order id

### Риск 4: Слишком ранний отказ от `SQLliteBD`

Снижение риска:
- не удалять старое хранилище до завершения recovery/reconciliation
- сначала dual-read или migration tooling

---

## 10. Определение готовности

Техническая реализация считается завершённой, когда:

- повторная доставка событий безопасна
- рестарт не приводит к потере бизнес-контекста
- все critical execution действия трассируются через audit trail
- failed actions не теряются
- V1 полностью удалён
- `SQLliteBD` не нужен для production runtime-state
- event-driven архитектура не опирается на `async void` и static bus напрямую

---

## 11. Рекомендуемый первый sprint

Если начинать прямо сейчас, самый разумный первый sprint такой:

1. Ввести `IClock`, `IIdGenerator`, `IEventBus`
2. Добавить `ProcessedEvents` + базовый dedup на `NewOrders`
3. Добавить `PositionMapping` / `OrderMapping` repositories
4. Перевести `CopyOrderService2` на persistent mappings
5. Подготовить `StartupRecoveryService` skeleton
6. Написать тесты на duplicate order и restart recovery baseline

Это даст максимальное снижение риска при минимальном вторжении в доменную логику.
