# Анализ проблемы: mapping.TraderQuantity становится 0

## Обнаруженная проблема

В логах появляется много ошибок:
```
CopyOrderService DecreasePosition: Ошибка при расчете closeRatio деление на 0
```

Происходит в `CopyOrderService.cs:339`:
```csharp
closeRatio = order.Quantity / mapping.TraderQuantity;  // Деление на 0!
```

## Почему mapping.TraderQuantity может стать 0

### Сценарий 1: Множественные DecreasePosition без синхронизации с реальными позициями

**Проблема**: `mapping.TraderQuantity` изменяется на основе **ордеров**, а не на основе **реальных позиций**.

#### Как это работает сейчас:

1. **OpenNewPosition** (line 236):
```csharp
TraderQuantity = order.Quantity  // Например: 0.5
```

2. **IncreasePosition** (line 302):
```csharp
mapping.TraderQuantity += order.Quantity;  // 0.5 + 0.3 = 0.8
```

3. **DecreasePosition** (line 373):
```csharp
mapping.TraderQuantity -= order.Quantity;  // 0.8 - 0.4 = 0.4
```

#### Критическая проблема:

**Mapping обновляется на основе ордеров, но CurrentWalletPositionService следит за реальными позициями через трейды.**

**Пример сценария, ведущего к TraderQuantity = 0:**

```
Шаг 1: Трейдер открывает SHORT BCH 0.5
  → HandleOpenOrder
  → mapping создается: TraderQuantity = 0.5 ✅

Шаг 2: Трейдер увеличивает SHORT BCH на 0.3 (Order A)
  → IncreasePosition
  → mapping.TraderQuantity = 0.5 + 0.3 = 0.8 ✅

Шаг 3: Трейдер частично закрывает позицию 0.4 (Order B)
  → DecreasePosition
  → closeRatio = 0.4 / 0.8 = 0.5 (закрывает 50%)
  → mapping.TraderQuantity = 0.8 - 0.4 = 0.4 ✅

Шаг 4: Трейдер закрывает еще 0.4 (Order C)
  → DecreasePosition
  → closeRatio = 0.4 / 0.4 = 1.0 (закрывает 100%)
  → mapping.TraderQuantity = 0.4 - 0.4 = 0 ❌

Шаг 5: Трейдер пытается закрыть еще немного (Order D) - может быть из-за неполного исполнения
  → DecreasePosition
  → closeRatio = 0.1 / 0 = ДЕЛЕНИЕ НА 0! 💥
```

**Почему это происходит:**
- `GetOrderSubType` смотрит на реальные позиции трейдера через `CurrentWalletPositionService`
- Если у трейдера еще есть позиция (даже 0.01), он возвращает `Decrease`
- Но `mapping.TraderQuantity` уже достиг 0 из-за накопленных вычитаний

### Сценарий 2: Race condition с неисполненными ордерами

```
Шаг 1: Трейдер открывает SHORT BCH 1.0 (Order A)
  → HandleOpenOrder → OpenNewPosition
  → mapping: TraderQuantity = 1.0 ✅
  → НО: Наш ордер еще не исполнился!

Шаг 2: Трейдер закрывает SHORT BCH 1.0 (Order B) - БЫСТРО
  → HandleOpenOrder → GetOrderSubType видит, что у трейдера еще есть позиция
  → Определяет как Close
  → ClosePosition → удаляет mapping! ❌

Шаг 3: Трейдер снова открывает SHORT BCH 0.5 (Order C)
  → HandleOpenOrder → проверяет mapping → mapping = null (удален в шаге 2)
  → OpenNewPosition
  → mapping: TraderQuantity = 0.5 ✅

Шаг 4: Наконец исполняются трейды для Orders A и B
  → FillsOrderService.OnTrades
  → CurrentWalletPositionService.AddTrade обновляет позиции
  → Но mapping уже пересоздан с новыми значениями!

Шаг 5: Трейдер частично закрывает (Order D) 0.3
  → GetOrderSubType видит позицию (исказившуюся из-за старых трейдов)
  → DecreasePosition
  → mapping.TraderQuantity = 0.5 - 0.3 = 0.2 ✅

Шаг 6: Старый трейд от Order A наконец обрабатывается
  → Меняет snapshot позиций, но НЕ обновляет mapping

Шаг 7: Трейдер закрывает еще 0.2 (Order E)
  → mapping.TraderQuantity = 0.2 - 0.2 = 0 ❌

Шаг 8: GetOrderSubType видит, что у трейдера РЕАЛЬНО есть позиция (из-за старого трейда)
  → Возвращает Decrease
  → DecreasePosition: closeRatio = quantity / 0 = ДЕЛЕНИЕ НА 0! 💥
```

### Сценарий 3: Десинхронизация между ордерами и трейдами

**Ключевая проблема архитектуры:**

1. **CurrentWalletPositionService** отслеживает позиции через **трейды** (реальные исполнения)
2. **PositionMappingService** обновляется через **ордера** (намерения трейдера)

**Они могут разойтись!**

```
GetOrderSubType:
  → Проверяет snapshot.Positions (обновляется через AddTrade)
  → Видит, что у трейдера позиция SHORT 0.8

DecreasePosition:
  → Использует mapping.TraderQuantity (обновляется через ордера)
  → mapping.TraderQuantity = 0 (уже вычитали через предыдущие ордера)

  → РЕЗУЛЬТАТ: GetOrderSubType говорит "Decrease", но mapping.TraderQuantity = 0!
```

## Почему метод UpdateTraderQuantity не используется

В `PositionMappingService.cs:70-82` есть метод:
```csharp
public bool UpdateTraderQuantity(Wallet traderWallet, Wallet myWallet,
    string symbol, Direction direction, decimal newQuantity)
{
    var mapping = GetMapping(traderWallet, myWallet, symbol, direction);
    if (mapping == null)
    {
        _logger.LogWarning($"Маппинг не найден для обновления TraderQuantity: {symbol} {direction}");
        return false;
    }

    mapping.TraderQuantity = newQuantity;
    SaveOrUpdateMapping(mapping);
    return true;
}
```

**Этот метод никогда не вызывается в production коде!**

Поиск по всему коду показывает, что `UpdateTraderQuantity` определен, но не используется. Это означает, что:
- Mapping.TraderQuantity обновляется только через `+=` и `-=` в IncreasePosition/DecreasePosition
- НЕТ синхронизации с реальными позициями из CurrentWalletPositionService
- Накапливаются ошибки округления и расхождения

## Источник расхождения: два независимых источника истины

### CurrentWalletPositionService (через трейды):
```csharp
public async Task<OrderSubType> AddTrade(OriginalTrade trade)
{
    var openPos = _walletPositionSnapshot[trade.Wallet].Positions
        .Where(p => p.Symbol == trade.Symbol).ToArray();

    if (openPos[0].Direction == trade.Direction)
    {
        openPos[0].Quantity += trade.RealQuantity;  // ← Источник истины #1
        return OrderSubType.Increase;
    }
    // ...
}
```

### CopyOrderService (через ордера):
```csharp
private async Task DecreasePosition(OriginalOrder order)
{
    mapping.TraderQuantity -= order.Quantity;  // ← Источник истины #2
}
```

**ДВА РАЗНЫХ ИСТОЧНИКА ИСТИНЫ!**
- `snapshot.Positions[].Quantity` (из трейдов, реальное исполнение)
- `mapping.TraderQuantity` (из ордеров, намерения)

## Возможные причины расхождения

### 1. Неполное исполнение ордера
```
Order: Quantity = 1.0
Trade: RealQuantity = 0.95 (неполное исполнение, комиссии, проскальзывание)

snapshot.Positions.Quantity = 0.95
mapping.TraderQuantity -= 1.0  // Вычли больше, чем реально было!
```

### 2. Ордера приходят быстрее, чем трейды
```
T=0: Order A (Open SHORT 1.0) → mapping.TraderQuantity = 1.0
T=1: Order B (Decrease 0.5) → mapping.TraderQuantity = 0.5
T=2: Order C (Decrease 0.5) → mapping.TraderQuantity = 0
T=3: Trade для Order A только сейчас исполнился → snapshot.Quantity = 1.0
T=4: Order D (Decrease 0.2) → GetOrderSubType видит snapshot.Quantity = 1.0 → Decrease
     → DecreasePosition: 0.2 / 0 = ERROR!
```

### 3. Множественные частичные исполнения одного ордера
```
Order A: Close 1.0
  → mapping.TraderQuantity -= 1.0 = 0

Но Order A исполнился в 3 трейда:
  Trade 1: 0.4
  Trade 2: 0.4
  Trade 3: 0.2

Каждый трейд обновляет snapshot.Quantity -= realQuantity
Но mapping.TraderQuantity уже = 0 после первого ордера
```

## Решения

### Вариант 1: Синхронизировать mapping с реальными позициями

Использовать `UpdateTraderQuantity` в `FillsOrderService` после обработки трейда:

```csharp
public async Task OnTrade(OriginalTrade trade)
{
    var orderSubType = await _currentWalletPositionService.AddTrade(trade);

    // После обновления snapshot - синхронизируем mapping
    var snapshot = await _currentWalletPositionService.GetSnapshot(trade.Wallet);
    var position = snapshot.Positions.FirstOrDefault(p => p.Symbol == trade.Symbol);

    if (position != null)
    {
        _positionMappingService.UpdateTraderQuantity(
            trade.Wallet,
            _myWallet,
            trade.Symbol,
            position.Direction,
            Math.Abs(position.Quantity)  // Реальное количество из snapshot
        );
    }
}
```

### Вариант 2: Использовать snapshot как единственный источник истины

Не хранить `TraderQuantity` в mapping, а всегда получать из snapshot:

```csharp
private async Task DecreasePosition(OriginalOrder order)
{
    var mapping = _positionMappingService.GetMapping(...);

    // Получаем РЕАЛЬНУЮ позицию трейдера из snapshot
    var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
    var position = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);

    if (position == null)
    {
        _logger.LogWarning($"Позиция не найдена в snapshot");
        return;
    }

    var actualTraderQuantity = Math.Abs(position.Quantity);  // ← Единственный источник истины

    if (actualTraderQuantity == 0)
    {
        _logger.LogWarning($"Реальная позиция трейдера = 0, пропускаем DecreasePosition");
        return;
    }

    decimal closeRatio = order.Quantity / actualTraderQuantity;  // ← Больше не может быть деления на 0
}
```

### Вариант 3: Защитная проверка перед делением (временное решение)

```csharp
if (mapping.TraderQuantity == 0)
{
    _logger.LogError($"DecreasePosition: mapping.TraderQuantity = 0 для {order.OrderId}. " +
                     "Возможна десинхронизация между ордерами и трейдами. " +
                     "Пытаемся получить реальную позицию из snapshot...");

    var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
    var position = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);

    if (position != null)
    {
        var actualQuantity = Math.Abs(position.Quantity);
        _logger.LogWarning($"Обнаружена десинхронизация! " +
                          $"mapping.TraderQuantity=0, но snapshot.Quantity={actualQuantity}. " +
                          $"Обновляем mapping...");

        mapping.TraderQuantity = actualQuantity;
        _positionMappingService.SaveOrUpdateMapping(mapping);
    }
    else
    {
        _logger.LogError($"Позиция не найдена в snapshot. CopyOrder НЕ БУДЕТ СОЗДАН!");
        return;
    }
}

closeRatio = order.Quantity / mapping.TraderQuantity;
```

## Рекомендуемое решение

**Вариант 2 (использовать snapshot как единственный источник истины)** - наиболее надежный:

**Преимущества:**
- Избавляемся от двух источников истины
- Snapshot обновляется через трейды (реальные исполнения)
- Невозможно деление на 0 - проверяем перед расчетом
- Автоматически учитываются неполные исполнения, комиссии, проскальзывание

**Изменения:**
1. Убрать все `mapping.TraderQuantity +=` и `-=` из CopyOrderService
2. В DecreasePosition получать реальное количество из snapshot
3. В mapping хранить только MyQuantity и PositionRatio (для масштабирования наших ордеров)
4. Использовать метод `UpdateTraderQuantity` только если нужно явно синхронизировать

**Mapping должен хранить:**
- `MyQuantity` - сколько у НАС открыто (мы контролируем это значение)
- `PositionRatio` - пропорция для масштабирования (рассчитывается один раз при Open)
- `TraderQuantity` - можно использовать для отладки/логирования, но НЕ для расчетов

## История изменений

### 2025-10-27
- 📝 Создан анализ проблемы деления на 0 в DecreasePosition
- 🔍 Обнаружена десинхронизация между ордерами и трейдами
- 💡 Предложены три варианта решения
