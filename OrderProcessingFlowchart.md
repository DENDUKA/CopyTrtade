# Дерево обработки ордеров и трейдов (Copy Trading)

## Точки входа (Events от сервера)

### 1. `DataBusEvents.NewOrders`
Событие приходит когда ордер трейдера **изменяет статус**

**Данные:**
- `OriginalOrder[]` - массив ордеров
- Каждый ордер содержит:
  - `OrderId` - ID ордера
  - `Wallet` - кошелек трейдера
  - `Symbol` - символ (BTC, ETH, etc.)
  - `Price` - цена
  - `Quantity` - количество
  - `Direction` - направление (Long/Short)
  - `Leverage` - плечо
  - `Status` - **КЛЮЧЕВОЕ ПОЛЕ**: Open, Filled, Canceled, Rejected, Triggered
  - `SubType` - тип операции с позицией (Open, Increase, Decrease, Close)

### 2. `DataBusEvents.NewTrades`
Событие приходит когда ордер трейдера **исполнен** (полностью или частично)

**Данные:**
- `OriginalTrade[]` - массив трейдов
- `bool IsSnapshot` - флаг снапшота
- Каждый трейд содержит:
  - `OrderId` - ID ордера который исполнился
  - `TradeId` - ID трейда
  - `Wallet` - кошелек трейдера
  - `Symbol` - символ
  - `Quantity` - количество исполненное
  - `Price` - цена исполнения
  - `Direction` - направление
  - `SubType` - тип операции

---

## 🌳 Дерево решений для NewOrders Event

```
┌─────────────────────────────────────┐
│ NewOrders Event                     │
│ OriginalOrder[] orders              │
└──────────────┬──────────────────────┘
               │
               ▼
       ┌───────────────┐
       │ Фильтрация    │
       │ По Wallet     │ ◄─── Копируем только ордера от определенных трейдеров
       └───────┬───────┘
               │
               ▼
       ┌─────────────────────────────────────────────────┐
       │ switch (order.Status)                           │
       └─────────────────────────────────────────────────┘
               │
        ┌──────┴──────┬───────────┬──────────┬──────────┬──────────┐
        │             │           │          │          │          │
        ▼             ▼           ▼          ▼          ▼          ▼
    ┌─────┐      ┌────────┐  ┌────────┐ ┌────────┐ ┌─────────┐ ┌────────┐
    │Open │      │Filled  │  │Canceled│ │Rejected│ │Triggered│ │Unknown │
    └──┬──┘      └───┬────┘  └───┬────┘ └───┬────┘ └────┬────┘ └───┬────┘
       │             │           │          │           │          │
       │             │           │          │           │          │
       ▼             ▼           ▼          ▼           ▼          ▼
```

---

## 📊 Детальная обработка: Order.Status = Open

**Смысл:** Трейдер РАЗМЕСТИЛ ордер на бирже, но он еще НЕ исполнен

```
┌────────────────────────────────┐
│ Order.Status = Open            │
│ Трейдер разместил ордер        │
└───────────┬────────────────────┘
            │
            ▼
    ┌───────────────────┐
    │ CurrentWallet     │
    │ PositionService   │
    │ .GetOrderSubType()│
    └────────┬──────────┘
             │
      ┌──────┴──────┬──────────┬──────────┬────────┐
      │             │          │          │        │
      ▼             ▼          ▼          ▼        ▼
   ┌─────┐      ┌────────┐ ┌────────┐ ┌──────┐ ┌──────┐
   │Open │      │Increase│ │Decrease│ │Close │ │None  │
   └──┬──┘      └───┬────┘ └───┬────┘ └──┬───┘ └──┬───┘
      │             │          │         │        │
      │             │          │         │        │
```

### Order.SubType = Open (Открытие НОВОЙ позиции)

**Условия:**
- У трейдера НЕТ открытой позиции по этому Symbol + Direction
- Трейдер открывает НОВУЮ позицию

**Действия:**
```
1. CreateCopyOrder(order)
   ├─ Получить баланс трейдера: traderWallet.AccountVolume
   ├─ Получить МОЙ баланс: myWallet.AccountVolume
   ├─ Рассчитать orderRatio = order.VolumeUsd / traderBalance
   ├─ Рассчитать myVolumeUsd = myBalance * orderRatio
   ├─ Рассчитать myQuantity = myVolumeUsd / order.Price
   └─ Округлить по ExchangeInfo.QuantityDecimals

2. OrdersProvider.PlaceOpenPositionOrder(copyOrder)
   ├─ Разместить НАШ ордер на бирже
   ├─ Получить наш OrderId
   └─ Вернуть myOrderId

3. Сохранить маппинг позиций:
   ├─ PositionMapping:
   │  ├─ TraderWallet = order.Wallet
   │  ├─ MyWallet = myWallet
   │  ├─ Symbol = order.Symbol
   │  ├─ Direction = order.Direction
   │  ├─ TraderQuantity = order.Quantity
   │  ├─ MyQuantity = copyOrder.Quantity
   │  └─ PositionRatio = myQuantity / order.Quantity ◄── КЛЮЧЕВОЙ ПАРАМЕТР!
   └─ PositionMappingService.SaveOrUpdateMapping(mapping)

4. Сохранить маппинг ордеров:
   ├─ OrderMapping:
   │  ├─ TraderOrderId = order.OrderId
   │  ├─ MyOrderId = myOrderId
   │  ├─ Symbol = order.Symbol
   │  ├─ Status = Open
   │  └─ Timestamp = DateTime.UtcNow
   └─ OrderMappingService.Save(orderMapping) ◄── TODO: Создать сервис

5. Логирование:
   └─ "Размещен ордер на открытие: {symbol} {direction} Quantity={myQuantity} TraderOrderId={order.OrderId} MyOrderId={myOrderId}"
```

**Граничные случаи:**
- ❌ Недостаточно средств → Логировать ошибку, не создавать маппинг
- ❌ Минимальный объем не достигнут → Логировать предупреждение, не размещать ордер
- ❌ Ошибка при размещении → Логировать ошибку, повторить попытку?

---

### Order.SubType = Increase (Увеличение позиции)

**Условия:**
- У трейдера ЕСТЬ открытая позиция по Symbol + Direction
- Трейдер добавляет к позиции в ТОМ ЖЕ направлении

**Действия:**
```
1. Получить маппинг позиции:
   └─ mapping = PositionMappingService.GetMapping(traderWallet, myWallet, symbol, direction)

2. Проверить наличие маппинга:
   ├─ if (mapping == null)
   │  ├─ Логировать: "Маппинг не найден, открываем как новую позицию"
   │  └─ GOTO: Order.SubType = Open
   └─ else: продолжить

3. Рассчитать количество для увеличения:
   ├─ myIncreaseQuantity = order.Quantity * mapping.PositionRatio ◄── Используем СОХРАНЕННЫЙ ratio!
   └─ Округлить по ExchangeInfo.QuantityDecimals

4. OrdersProvider.PlaceIncreasePositionOrder(symbol, direction, myIncreaseQuantity, order.Price, leverage)
   ├─ Разместить НАШ ордер на увеличение
   ├─ Получить наш OrderId
   └─ Вернуть myOrderId

5. Обновить маппинг позиции:
   ├─ mapping.TraderQuantity += order.Quantity
   ├─ mapping.MyQuantity += myIncreaseQuantity
   ├─ mapping.LastUpdate = DateTime.UtcNow
   └─ PositionMappingService.SaveOrUpdateMapping(mapping)

6. Сохранить маппинг ордера:
   └─ OrderMappingService.Save(traderOrderId, myOrderId, symbol, Open)

7. Логирование:
   └─ "Увеличение позиции: {symbol} {direction} +{myIncreaseQuantity} (Trader: +{order.Quantity})"
```

**Граничные случаи:**
- ❌ Маппинг не найден → Открыть как новую позицию
- ❌ Ордер не размещен → Логировать ошибку, НЕ обновлять маппинг

---

### Order.SubType = Decrease (Частичное закрытие)

**Условия:**
- У трейдера ЕСТЬ позиция
- Трейдер закрывает ЧАСТЬ позиции (противоположным ордером)
- order.Quantity < текущая позиция трейдера

**Действия:**
```
1. Получить маппинг позиции:
   └─ mapping = PositionMappingService.GetMapping(traderWallet, myWallet, symbol, direction)

2. Проверить наличие маппинга:
   ├─ if (mapping == null)
   │  ├─ Логировать: "Маппинг не найден для закрытия позиции"
   │  └─ RETURN (не размещаем ордер)
   └─ else: продолжить

3. Рассчитать долю закрытия:
   ├─ closeRatio = order.Quantity / mapping.TraderQuantity
   ├─ myCloseQuantity = mapping.MyQuantity * closeRatio ◄── Закрываем ту же ДОЛЮ
   └─ Округлить по ExchangeInfo.QuantityDecimals

4. OrdersProvider.PlaceDecreasePositionOrder(symbol, direction, myCloseQuantity, order.Price)
   ├─ Разместить НАШ ордер на частичное закрытие
   ├─ Получить наш OrderId
   └─ Вернуть myOrderId

5. Обновить маппинг позиции:
   ├─ mapping.TraderQuantity -= order.Quantity
   ├─ mapping.MyQuantity -= myCloseQuantity
   ├─ mapping.LastUpdate = DateTime.UtcNow
   └─ PositionMappingService.SaveOrUpdateMapping(mapping)

6. Сохранить маппинг ордера:
   └─ OrderMappingService.Save(traderOrderId, myOrderId, symbol, Open)

7. Логирование:
   └─ "Частичное закрытие: {symbol} {direction} -{myCloseQuantity} (closeRatio={closeRatio:P2})"
```

**Граничные случаев:**
- ❌ Маппинг не найден → НЕ размещать ордер
- ❌ mapping.TraderQuantity == 0 → Ошибка деления на ноль, логировать критическую ошибку

---

### Order.SubType = Close (Полное закрытие)

**Условия:**
- Трейдер закрывает ВСЮ позицию
- order.Quantity == текущая позиция трейдера

**Действия:**
```
1. Получить маппинг позиции:
   └─ mapping = PositionMappingService.GetMapping(traderWallet, myWallet, symbol, direction)

2. Проверить наличие маппинга:
   ├─ if (mapping == null)
   │  ├─ Логировать: "Маппинг не найден для полного закрытия"
   │  └─ RETURN
   └─ else: продолжить

3. Получить количество для закрытия:
   └─ myCloseQuantity = mapping.MyQuantity ◄── Закрываем ВСЮ нашу позицию

4. OrdersProvider.PlaceClosePositionOrder(symbol, direction, myCloseQuantity, order.Price)
   ├─ Разместить НАШ ордер на полное закрытие
   ├─ Получить наш OrderId
   └─ Вернуть myOrderId

5. УДАЛИТЬ маппинг позиции:
   └─ PositionMappingService.DeleteMapping(traderWallet, myWallet, symbol, direction)

6. Сохранить маппинг ордера:
   └─ OrderMappingService.Save(traderOrderId, myOrderId, symbol, Open)

7. Логирование:
   └─ "Полное закрытие позиции: {symbol} {direction} Quantity={myCloseQuantity}"
```

**Граничные случаи:**
- ❌ Маппинг не найден → НЕ размещать ордер

---

## 📊 Детальная обработка: Order.Status = Filled

**Смысл:** Ордер трейдера полностью ИСПОЛНЕН

```
┌────────────────────────────────┐
│ Order.Status = Filled          │
│ Ордер трейдера исполнен        │
└───────────┬────────────────────┘
            │
            ▼
    ┌──────────────────┐
    │ Получить наш     │
    │ OrderId из       │
    │ OrderMapping     │
    └────────┬─────────┘
             │
      ┌──────┴──────┐
      │             │
      ▼             ▼
  ┌────────┐    ┌─────────┐
  │Найден  │    │НЕ найден│
  └───┬────┘    └────┬────┘
      │              │
      ▼              ▼
```

**Действия:**
```
1. Получить маппинг ордера:
   └─ orderMapping = OrderMappingService.GetByTraderOrderId(order.OrderId)

2. Проверить наличие:
   ├─ if (orderMapping == null)
   │  ├─ Логировать: "Маппинг ордера не найден для OrderId={order.OrderId}"
   │  └─ RETURN (возможно, это не копируемый трейдер)
   └─ else: продолжить

3. Проверить статус НАШЕГО ордера:
   ├─ myOrderStatus = OrdersProvider.CheckOrderStatus(orderMapping.MyOrderId)
   └─ switch (myOrderStatus)
       ├─ Filled ✅
       │  ├─ Логировать: "Ордер исполнен успешно"
       │  ├─ Обновить orderMapping.Status = Filled
       │  └─ OrderMappingService.Update(orderMapping)
       │
       ├─ Open ⚠️
       │  ├─ Логировать WARNING: "Ордер трейдера исполнен, но НАШ ордер еще Open"
       │  └─ Решение: Подождать? Отменить и разместить market-ордер?
       │
       ├─ Canceled ❌
       │  ├─ Логировать ERROR: "Ордер трейдера исполнен, но НАШ ордер был отменен!"
       │  └─ Критическая ошибка → разместить market-ордер для синхронизации?
       │
       └─ null/Unknown ❓
          ├─ Логировать ERROR: "Не удалось получить статус нашего ордера"
          └─ Повторить запрос?

4. Логирование итога:
   └─ "Ордер Filled: TraderOrderId={order.OrderId} MyOrderId={orderMapping.MyOrderId} Status={myOrderStatus}"
```

**Важные моменты:**
- ⚠️ Ордер трейдера может исполниться БЫСТРЕЕ чем наш → это нормально
- ❌ Если наш ордер Canceled, а ордер трейдера Filled → рассинхронизация позиций!
- 💡 Возможное решение: разместить market-ордер для синхронизации

---

## 📊 Детальная обработка: Order.Status = Canceled

**Смысл:** Трейдер ОТМЕНИЛ свой ордер (передумал)

```
┌────────────────────────────────┐
│ Order.Status = Canceled        │
│ Трейдер отменил ордер          │
└───────────┬────────────────────┘
            │
            ▼
    ┌──────────────────┐
    │ Получить наш     │
    │ OrderId из       │
    │ OrderMapping     │
    └────────┬─────────┘
             │
      ┌──────┴──────┐
      │             │
      ▼             ▼
  ┌────────┐    ┌─────────┐
  │Найден  │    │НЕ найден│
  └───┬────┘    └────┬────┘
      │              │
      ▼              ▼
```

**Действия:**
```
1. Получить маппинг ордера:
   └─ orderMapping = OrderMappingService.GetByTraderOrderId(order.OrderId)

2. Проверить наличие:
   ├─ if (orderMapping == null)
   │  ├─ Логировать: "Маппинг ордера не найден, нечего отменять"
   │  └─ RETURN
   └─ else: продолжить

3. Отменить НАШ ордер:
   ├─ success = OrdersProvider.CancelOrderById(order.Symbol, orderMapping.MyOrderId)
   └─ if (success)
       ├─ Логировать: "Наш ордер отменен успешно"
       ├─ orderMapping.Status = Canceled
       └─ OrderMappingService.Update(orderMapping)
      else
       ├─ Логировать ERROR: "Не удалось отменить наш ордер MyOrderId={orderMapping.MyOrderId}"
       └─ Проверить статус ордера - возможно уже исполнен?

4. Проверить влияние на позицию:
   ├─ Если это был ордер на Open → позиция НЕ была открыта, маппинг позиции НЕ создан
   ├─ Если это был ордер на Increase → позиция НЕ увеличена, маппинг остался прежним
   ├─ Если это был ордер на Decrease/Close → позиция НЕ закрыта, маппинг остался прежним
   └─ НИЧЕГО дополнительно делать не нужно

5. Логирование:
   └─ "Ордер отменен: TraderOrderId={order.OrderId} MyOrderId={orderMapping.MyOrderId}"
```

**Граничные случаи:**
- ⚠️ Наш ордер уже исполнен (частично или полностью) → рассинхронизация!
- 💡 Решение: проверить статус перед отменой, если Filled → нужна корректировка позиции

---

## 📊 Детальная обработка: Order.Status = Rejected

**Смысл:** Биржа ОТКЛОНИЛА ордер трейдера (недостаточно средств, неправильные параметры и т.д.)

**Действия:**
```
АНАЛОГИЧНО Order.Status = Canceled

1. Получить маппинг ордера
2. Отменить наш ордер (если был размещен)
3. Обновить статус в маппинге
4. Логировать: "Ордер трейдера отклонен, наш ордер отменен"
```

---

## 📊 Детальная обработка: Order.Status = Triggered

**Смысл:** Stop/Limit ордер сработал, теперь размещен на бирже как обычный ордер

**Действия:**
```
1. Логировать информацию:
   └─ "Ордер триггернулся: OrderId={order.OrderId} Symbol={order.Symbol}"

2. Ничего не делать:
   └─ Ордер уже был размещен при статусе Open
   └─ Ждем когда придет статус Filled

3. (Опционально) Проверить наш ордер:
   └─ Убедиться что наш ордер тоже активен
```

---

## 🌳 Дерево решений для NewTrades Event

```
┌─────────────────────────────────────┐
│ NewTrades Event                     │
│ (OriginalTrade[], IsSnapshot)       │
└──────────────┬──────────────────────┘
               │
               ▼
       ┌───────────────┐
       │ IsSnapshot?   │
       └───────┬───────┘
               │
        ┌──────┴──────┐
        │             │
        ▼             ▼
    ┌───────┐    ┌────────────┐
    │ true  │    │   false    │
    │(игнор)│    │(обработка) │
    └───────┘    └──────┬─────┘
                        │
                        ▼
                ┌──────────────┐
                │ Фильтрация   │
                │ по Wallet    │
                └──────┬───────┘
                       │
                       ▼
            ┌──────────────────────┐
            │ CurrentWalletPosition│
            │ Service.AddTrade()   │
            └──────────────────────┘
                       │
                       │ Обновляет snapshot трейдера
                       │ Возвращает OrderSubType
                       │
                       ▼
```

**Цель NewTrades:**
- Обновить snapshot позиций трейдера
- Убедиться что позиции синхронизированы

**Действия для COPY TRADING:**
```
1. Проверить IsSnapshot:
   ├─ if (IsSnapshot == true)
   │  └─ RETURN (это начальная загрузка, не копируем)
   └─ else: продолжить

2. Фильтровать по Wallet:
   └─ trades = trades.Where(t => копируемые кошельки)

3. Для каждого трейда:
   └─ CurrentWalletPositionService.AddTrade(trade)
      └─ Обновляет внутренний snapshot позиций трейдера

4. (Опционально) Проверить синхронизацию:
   ├─ Сравнить позицию трейдера из snapshot с нашим маппингом
   ├─ Если есть расхождения → логировать WARNING
   └─ Возможно, пересинхронизировать?
```

**Важно:**
- ✅ NewTrades приходит ПОСЛЕ того как ордер исполнен
- ✅ Для копирования используем NewOrders (статус Open/Filled)
- ✅ NewTrades используем для синхронизации и проверки

---

## 📋 Итоговая таблица: Когда что делать

| Event | Status/Условие | Действие | Метод |
|-------|---------------|----------|-------|
| **NewOrders** | Status=Open + SubType=Open | Открыть новую позицию | `OpenNewPosition()` |
| **NewOrders** | Status=Open + SubType=Increase | Увеличить позицию | `IncreasePosition()` |
| **NewOrders** | Status=Open + SubType=Decrease | Частично закрыть | `DecreasePosition()` |
| **NewOrders** | Status=Open + SubType=Close | Полностью закрыть | `ClosePosition()` |
| **NewOrders** | Status=Filled | Проверить исполнение | `HandleFilledOrder()` |
| **NewOrders** | Status=Canceled | Отменить наш ордер | `HandleCanceledOrder()` |
| **NewOrders** | Status=Rejected | Отменить наш ордер | `HandleRejectedOrder()` |
| **NewOrders** | Status=Triggered | Логировать | - |
| **NewTrades** | IsSnapshot=false | Обновить snapshot | `AddTrade()` |
| **NewTrades** | IsSnapshot=true | Игнорировать | - |

---

## 🚨 Критические проблемы и решения

### Проблема 1: Рассинхронизация позиций

**Сценарий:**
1. Трейдер размещает ордер (Open)
2. Мы размещаем наш ордер (Open)
3. Ордер трейдера исполняется (Filled)
4. НАШ ордер НЕ исполняется (все еще Open) или отменяется

**Результат:** Позиция трейдера открыта, наша НЕТ → рассинхронизация!

**Решение:**
```
Option 1: Market Order Fallback
- При Filled трейдера проверить наш статус
- Если наш ордер НЕ Filled через N секунд:
  - Отменить limit ордер
  - Разместить market ордер
  - Гарантированное исполнение

Option 2: Timeout + Retry
- Ждать X секунд после Filled трейдера
- Проверить статус нашего ордера
- Если НЕ исполнен → переразместить с лучшей ценой

Option 3: Допустить расхождение
- Логировать расхождение
- Не предпринимать действий
- При следующем Increase/Decrease синхронизируется автоматически?
```

### Проблема 2: Частичное исполнение

**Сценарий:**
1. Трейдер размещает ордер на 10 BTC
2. Исполнилось только 5 BTC
3. Трейдер отменяет остаток
4. У нас исполнилось 0.7 BTC вместо 0.5 BTC

**Решение:**
```
- Отслеживать Filled Quantity, а не только статус
- Сохранять в OrderMapping:
  - PlannedQuantity = 1.0 BTC
  - ActualFilledQuantity = 0.7 BTC
- При расчете пропорций использовать ФАКТИЧЕСКОЕ количество
```

### Проблема 3: Потеря OrderMapping при рестарте

**Сценарий:**
1. Разместили ордера
2. Приложение перезапустилось
3. OrderMapping потерян
4. Не можем отменить наши ордера при Canceled трейдера

**Решение:**
```
- Сохранять OrderMapping в SQLite
- При старте восстанавливать активные ордера
- Синхронизировать с биржей через API GetOpenOrders()
```

---

## ✅ TODO: Что нужно реализовать

### 1. Создать OrderMappingService
```csharp
public class OrderMappingService
{
    // Маппинг между OrderId трейдера и нашим OrderId
    Dictionary<long, OrderMapping> _mappings;

    void Save(long traderOrderId, long myOrderId, string symbol, OrderStatus status);
    OrderMapping? GetByTraderOrderId(long traderOrderId);
    void UpdateStatus(long traderOrderId, OrderStatus newStatus);
}
```

### 2. Модель OrderMapping
```csharp
public class OrderMapping
{
    long TraderOrderId;
    long MyOrderId;
    string Symbol;
    OrderStatus Status;
    DateTime CreatedAt;
    DateTime? FilledAt;
}
```

### 3. Реализовать методы в OrdersProvider
- Все 6 методов уже добавлены, нужна реализация

### 4. Логика обработки Filled
- Проверка статуса нашего ордера
- Обработка рассинхронизации
- Market order fallback?

### 5. Логика обработки Canceled/Rejected
- Отмена нашего ордера
- Проверка что ордер действительно отменен

---

**Следующий шаг:** Хотите чтобы я начал реализацию OrderMappingService и OrderMapping модели?
