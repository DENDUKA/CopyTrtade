# CopyOrderService - Логика определения типа ордера

## Обзор

`CopyOrderService` отвечает за копирование ордеров трейдера. Ключевой момент - правильное определение типа операции (Open/Increase/Decrease/Close) для создания соответствующего копируемого ордера.

## Критическая проблема и её решение

### ❌ Проблема: Race Condition при определении типа ордера

**Сценарий**: Трейдер быстро размещает несколько ордеров подряд по одному символу.

**Что происходило (НЕПРАВИЛЬНО)**:
1. Трейдер открывает SHORT позицию по BCH (Ордер A)
2. `GetOrderSubType` анализирует позиции **трейдера** → определяет как `Open` ✅
3. Вызывается `OpenNewPosition` → создается CopyOrder
4. **НО**: Наш ордер еще не исполнился, маппинг еще не создан
5. Трейдер увеличивает позицию (Ордер B)
6. `GetOrderSubType` видит, что у трейдера уже есть SHORT по BCH → определяет как `Increase`
7. Вызывается `IncreasePosition` → ищет маппинг
8. **ОШИБКА**: Маппинга нет! (первый ордер еще не исполнился)
9. CopyOrder не создается ❌

### ✅ Решение: Проверка маппинга ПЕРЕД определением типа

**Ключевой принцип**:
> Маппинг = наша открытая позиция. Если маппинга нет - значит мы еще не открывали позицию для этого символа/направления, независимо от того, есть ли позиция у трейдера.

**Правильный алгоритм** (HandleOpenOrder):

```csharp
private async Task HandleOpenOrder(OriginalOrder order)
{
    // 1. СНАЧАЛА проверяем наличие маппинга
    var mapping = _positionMappingService.GetMapping(
        order.Wallet,
        _myWallet,
        order.Symbol,
        order.Direction
    );

    // 2. Если маппинга НЕТ - это ВСЕГДА Open
    if (mapping == null)
    {
        // У нас нет позиции - открываем новую
        await OpenNewPosition(order);
        return;
    }

    // 3. Только если маппинг ЕСТЬ - определяем тип через GetOrderSubType
    var orderSubType = await _currentWalletPositionService.GetOrderSubType(order);

    // 4. Выполняем соответствующее действие
    switch (orderSubType)
    {
        case OrderSubType.Increase:
            await IncreasePosition(order);
            break;
        case OrderSubType.Decrease:
            await DecreasePosition(order);
            break;
        case OrderSubType.Close:
            await ClosePosition(order);
            break;
    }
}
```

## Поток обработки ордера

### Сценарий: Трейдер открывает и увеличивает SHORT по BCH

#### Ордер A - Открытие позиции

```
1. Ордер A приходит (SHORT BCH, 0.5)
   ↓
2. HandleOpenOrder START
   ↓
3. Проверка маппинга для (TraderWallet, MyWallet, "BCH", Short)
   → Маппинга НЕТ
   ↓
4. Лог: "Маппинг не найден для OrderId - открываем как новую позицию (Open)"
   ↓
5. OpenNewPosition
   ├─ CreateCopyOrder (OrderSubType.Open)
   ├─ Публикация DataBusEvents.CopyOrderCreated
   └─ Создание маппинга:
      {
        TraderQuantity: 0.5,
        MyQuantity: 0.05,  // пропорционально балансу
        PositionRatio: 0.1
      }
   ↓
6. HandleOpenOrder END
```

#### Ордер B - Увеличение позиции

```
1. Ордер B приходит (SHORT BCH, 0.3)
   ↓
2. HandleOpenOrder START
   ↓
3. Проверка маппинга для (TraderWallet, MyWallet, "BCH", Short)
   → Маппинг НАЙДЕН ✅
   ↓
4. Лог: "Маппинг найден для OrderId, определяем OrderSubType"
   ↓
5. GetOrderSubType анализирует позиции трейдера
   → У трейдера SHORT 0.5, приходит ордер SHORT 0.3
   → Определяет как Increase ✅
   ↓
6. IncreasePosition
   ├─ Получает маппинг (PositionRatio: 0.1)
   ├─ Вычисляет myIncreaseQuantity = 0.3 * 0.1 = 0.03
   ├─ CreateCopyOrderFromQuantity (OrderSubType.Increase)
   ├─ Публикация DataBusEvents.CopyOrderCreated
   └─ Обновление маппинга:
      {
        TraderQuantity: 0.8,  // 0.5 + 0.3
        MyQuantity: 0.08,     // 0.05 + 0.03
        PositionRatio: 0.1    // остается неизменным!
      }
   ↓
7. HandleOpenOrder END
```

## Типы операций

### Open - Открытие новой позиции
- **Условие**: Маппинга не существует
- **Действие**:
  - Создается CopyOrder с размером пропорциональным балансу
  - Создается маппинг с сохранением пропорции (PositionRatio)
- **Маппинг**: Создается новый

### Increase - Увеличение позиции
- **Условие**:
  - Маппинг существует
  - У трейдера есть позиция в том же направлении
  - Ордер увеличивает эту позицию
- **Действие**:
  - Создается CopyOrder используя **сохраненную** пропорцию из маппинга
  - MyQuantity = OrderQuantity * PositionRatio
- **Маппинг**: Обновляется (TraderQuantity +=, MyQuantity +=)

### Decrease - Частичное закрытие позиции
- **Условие**:
  - Маппинг существует
  - У трейдера есть позиция в противоположном направлении
  - Размер ордера меньше размера позиции
- **Действие**:
  - Вычисляется доля закрытия: closeRatio = OrderQuantity / TraderQuantity
  - Закрывается та же доля нашей позиции: MyQuantity * closeRatio
- **Маппинг**: Обновляется (TraderQuantity -=, MyQuantity -=)

### Close - Полное закрытие позиции
- **Условие**:
  - Маппинг существует
  - У трейдера есть позиция в противоположном направлении
  - Размер ордера равен размеру позиции
- **Действие**:
  - Закрывается ВСЯ наша позиция (MyQuantity)
- **Маппинг**: Удаляется

## Важные замечания

### 1. Сохранение пропорции (PositionRatio)

**Критически важно**: PositionRatio рассчитывается один раз при открытии позиции и сохраняется в маппинге.

```csharp
// При открытии позиции (OpenNewPosition)
PositionRatio = MyQuantity / TraderQuantity

// При увеличении (IncreasePosition) - используем СОХРАНЕННУЮ пропорцию
MyIncreaseQuantity = OrderQuantity * mapping.PositionRatio
```

**Почему это важно**:
- Баланс трейдера может измениться между открытием и увеличением позиции
- Если пересчитывать пропорцию каждый раз - размеры позиций разойдутся
- Сохраненная пропорция гарантирует одинаковое % изменение

### 2. Асинхронная обработка

Все методы обработки ордеров асинхронные с `await` для последовательного выполнения:

```csharp
private async void OnNewOrders(OriginalOrder[] orders)
{
    foreach (var order in orders)
    {
        await OnNewOrder(order);  // ✅ Ждем завершения каждого ордера
    }
}
```

**Ранее было** (НЕПРАВИЛЬНО):
```csharp
private void OnNewOrders(OriginalOrder[] orders)
{
    foreach (var order in orders)
    {
        OnNewOrder(order);  // ❌ Fire-and-forget, race condition!
    }
}
```

### 3. Обработка null из GetExchangeInfo

Все методы, использующие `GetExchangeInfo`, проверяют результат на null:

```csharp
var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

if (exchangeInfo == null)
{
    _logger.LogError($"Не удалось получить ExchangeInfo для {order.Symbol} - CopyOrder НЕ БУДЕТ СОЗДАН!");
    return;
}

// Безопасное использование
myQuantity = Math.Round(myQuantity, exchangeInfo.QuantityDecimals!.Value, ...);
```

## Инициализация сервисов

**Критически важно**: `CopyOrderStorageService` должен быть инициализирован в `Startup.Configure`:

```csharp
public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider)
{
    // ...

    serviceProvider.GetService<FillsOrderService>();
    serviceProvider.GetService<CopyOrderService>();

    // ✅ Инициализация CopyOrderStorageService для подписки на события
    serviceProvider.GetRequiredService<CopyOrderStorageService>();

    // Инициализация RealtimeUpdateService
    serviceProvider.GetRequiredService<BlazorUI.Services.RealtimeUpdateService>();
}
```

**Почему это важно**:
- Конструктор `CopyOrderStorageService` подписывается на события `DataBusEvents.CopyOrderCreated` и `DataBusEvents.CopyOrderClosed`
- Без инициализации подписка не происходит
- Созданные CopyOrder не попадают в хранилище

## Логирование

Детальное логирование на каждом этапе позволяет отследить путь ордера:

```
HandleOpenOrder START: BCH Short, OrderId=212866465121
HandleOpenOrder: Маппинг не найден для 212866465121 - открываем как новую позицию (Open)
OpenNewPosition START: BCH Short, Quantity=0.933, OrderId=212866465121
OpenNewPosition: Вызываем CreateCopyOrder для 212866465121
OpenNewPosition: CreateCopyOrder завершен для 212866465121, CopyOrderId=1730025775123
OpenNewPosition: Публикуем CopyOrderCreated для 212866465121
OpenNewPosition: CopyOrderCreated опубликован для 212866465121
OpenNewPosition SUCCESS: создан копируемый ордер для 212866465121, CopyOrderId=1730025775123
HandleOpenOrder: OpenNewPosition завершен для 212866465121
HandleOpenOrder END: BCH Short, OrderId=212866465121
```

## Важные архитектурные решения

### Snapshot как единственный источник истины для позиции трейдера

**Проблема**: Ранее `mapping.TraderQuantity` обновлялась через `+=` и `-=` в методах IncreasePosition/DecreasePosition на основе ордеров. Это приводило к десинхронизации с реальными позициями, которые отслеживаются через трейды в `CurrentWalletPositionService`.

**Решение**: Начиная с 2025-10-27, `mapping.TraderQuantity` больше не обновляется в IncreasePosition/DecreasePosition. Вместо этого:

- В **DecreasePosition** реальное количество позиции трейдера получается из snapshot:
  ```csharp
  var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
  var traderPosition = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);
  var actualTraderQuantity = Math.Abs(traderPosition.Quantity);
  ```

- **Mapping хранит**:
  - `MyQuantity` - наша позиция (мы контролируем это значение через += и -=)
  - `PositionRatio` - пропорция для масштабирования (рассчитывается один раз при Open)
  - `TraderQuantity` - устанавливается при открытии, но НЕ используется для расчетов

**Преимущества**:
- Невозможно деление на 0 - проверяем перед расчетом
- Автоматически учитываются неполные исполнения, комиссии, проскальзывание
- Один источник истины для позиции трейдера (snapshot из трейдов)
- Избегаем накопления ошибок при множественных операциях

## История изменений

### 2025-10-27
- ✅ **КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ**: Snapshot теперь единственный источник истины для TraderQuantity
- ✅ DecreasePosition: получаем actualTraderQuantity из snapshot вместо mapping.TraderQuantity
- ✅ IncreasePosition: удалено обновление mapping.TraderQuantity
- ✅ DecreasePosition: удалено обновление mapping.TraderQuantity
- ✅ Исправлена проблема деления на 0 в DecreasePosition
- 📝 Создан детальный анализ проблемы в TraderQuantity_Zero_Analysis.md

### 2025-10-26
- ✅ Исправлена логика определения типа ордера: проверка маппинга перед GetOrderSubType
- ✅ Добавлены проверки на null для GetExchangeInfo
- ✅ Добавлена инициализация CopyOrderStorageService в Startup
- ✅ Исправлен async/await: OnNewOrder изменен с async void на async Task
- ✅ Добавлено детальное логирование на всех этапах обработки
- ✅ Добавлена обработка OrderSubType.None
