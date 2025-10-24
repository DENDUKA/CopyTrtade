# Система копирования позиций с отслеживанием (CopyOrder V1)

## Обзор

Реализована система пропорционального копирования торговых позиций с сохранением коэффициента пропорции между позициями трейдера и копируемыми позициями. Система работает независимо от изменения балансов счетов и корректно обрабатывает все типы операций: открытие, увеличение, частичное и полное закрытие позиций.

---

## Архитектура решения

### Компоненты системы

#### 1. **PositionMapping** (`CopyTrading.Models/Models/PositionMapping.cs`)
Модель для хранения связи между позициями:

```csharp
public class PositionMapping
{
    public Wallet TraderWallet { get; set; }      // Кошелек трейдера
    public Wallet MyWallet { get; set; }          // Ваш кошелек
    public string Symbol { get; set; }            // Символ (BTC, ETH, ...)
    public Direction Direction { get; set; }      // Long/Short

    public decimal TraderQuantity { get; set; }   // Текущая позиция трейдера
    public decimal MyQuantity { get; set; }       // Ваша текущая позиция

    // КЛЮЧЕВОЙ ПАРАМЕТР: Сохраняется при открытии позиции!
    public decimal PositionRatio { get; set; }    // MyQuantity / TraderQuantity

    public DateTime LastUpdate { get; set; }
}
```

**Ключевая идея:** `PositionRatio` рассчитывается при первом открытии позиции и **сохраняется** для всех последующих операций с этой позицией.

#### 2. **PositionMappingService** (`CopyTrading/Services/PositionMappingService.cs`)
Сервис для управления маппингами в памяти:

- Хранение: `ConcurrentDictionary<string, PositionMapping>` (in-memory)
- Потокобезопасность: Использует `ConcurrentDictionary`
- Жизненный цикл: Singleton
- CRUD операции: Get, Save/Update, Delete

#### 3. **CopyOrderService** (`CopyTrading/Services/CopyOrderService.cs`)
Обновленная логика обработки ордеров с обработкой различных статусов ордеров.

**Обработка статусов ордеров:**
- **Open** → Размещаем наш копируемый ордер
- **Filled** → Проверяем что наш ордер тоже исполнен полностью
- **Canceled** → Отменяем наш ордер
- **Rejected** → Отменяем наш ордер
- **Triggered** → Логируем, ждем когда ордер станет Open или Filled

---

## Типы операций с позициями

Когда ордер приходит в статусе **Open**, определяется тип операции с позицией:

### 1️⃣ OpenNewPosition - Открытие новой позиции

**Когда:** Трейдер открывает новую позицию (OrderSubType.Open)

**Логика:**
```csharp
// 1. Создаем копируемый ордер с пропорциональным расчетом
var copyOrder = await CreateCopyOrder(order);

// 2. Сохраняем маппинг с ключевым параметром PositionRatio
var mapping = new PositionMapping
{
    TraderWallet = order.Wallet,
    MyWallet = _myWallet,
    Symbol = order.Symbol,
    Direction = order.Direction,
    TraderQuantity = order.Quantity,
    MyQuantity = copyOrder.Quantity,
    PositionRatio = copyOrder.Quantity / order.Quantity,  // ← СОХРАНЯЕМ ПРОПОРЦИЮ!
    LastUpdate = DateTime.UtcNow
};

_positionMappingService.SaveOrUpdateMapping(mapping);

// 3. TODO: Разместить ордер на бирже через OrdersProvider
```

**Пример:**
```
Трейдер: баланс $20,000, открывает 0.02 BTC × $50,000 = $1,000 (5% от счета)
Вы: баланс $2,000, открываете 0.002 BTC × $50,000 = $100 (тоже 5% от счета)

Маппинг:
  TraderQuantity = 0.02 BTC
  MyQuantity = 0.002 BTC
  PositionRatio = 0.002 / 0.02 = 0.1  ← Сохраняется для дальнейших операций!
```

---

### 2️⃣ IncreasePosition - Увеличение позиции

**Когда:** Трейдер увеличивает существующую позицию (OrderSubType.Increase)

**Логика:**
```csharp
// 1. Получаем существующий маппинг
var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

if (mapping == null)
{
    // Если маппинга нет - открываем как новую позицию
    await OpenNewPosition(order);
    return;
}

// 2. Используем СОХРАНЕННУЮ пропорцию (не текущий баланс!)
var myIncreaseQuantity = order.Quantity * mapping.PositionRatio;

// 3. Округляем до точности биржи
var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);
myIncreaseQuantity = Math.Round(myIncreaseQuantity, exchangeInfo.QuantityDecimals!.Value);

// 4. Обновляем маппинг
mapping.TraderQuantity += order.Quantity;
mapping.MyQuantity += myIncreaseQuantity;
_positionMappingService.SaveOrUpdateMapping(mapping);

// 5. TODO: Разместить ордер на увеличение через OrdersProvider
```

**Пример:**
```
Исходная позиция:
  Трейдер: 10 BTC, Вы: 1 BTC, PositionRatio = 0.1

Трейдер увеличивает на 5 BTC:
  Вы увеличиваете на: 5 × 0.1 = 0.5 BTC

Новая позиция:
  Трейдер: 15 BTC, Вы: 1.5 BTC, PositionRatio = 0.1 (не изменился!)
```

**❗ Важно:** Используется **сохраненный** `PositionRatio`, а НЕ текущий баланс! Это гарантирует правильную пропорцию даже если балансы изменились.

---

### 3️⃣ DecreasePosition - Частичное закрытие

**Когда:** Трейдер частично закрывает позицию (OrderSubType.Decrease)

**Логика:**
```csharp
// 1. Получаем маппинг
var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

if (mapping == null)
{
    _logger.LogWarning($"Маппинг не найден для закрытия позиции");
    return;
}

// 2. Рассчитываем какую ДОЛЮ закрывает трейдер
var closeRatio = order.Quantity / mapping.TraderQuantity;

// 3. Закрываем ту же ДОЛЮ от ВАШЕЙ позиции
var myCloseQuantity = mapping.MyQuantity * closeRatio;

// 4. Округляем
var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);
myCloseQuantity = Math.Round(myCloseQuantity, exchangeInfo.QuantityDecimals!.Value);

// 5. Обновляем маппинг
mapping.TraderQuantity -= order.Quantity;
mapping.MyQuantity -= myCloseQuantity;
_positionMappingService.SaveOrUpdateMapping(mapping);

// 6. TODO: Разместить ордер на частичное закрытие через OrdersProvider
```

**Пример:**
```
Текущая позиция:
  Трейдер: 10 BTC, Вы: 1 BTC

Трейдер закрывает 5 BTC (50% позиции):
  closeRatio = 5 / 10 = 0.5
  Вы закрываете: 1 × 0.5 = 0.5 BTC (тоже 50%)

Остаток позиции:
  Трейдер: 5 BTC, Вы: 0.5 BTC
```

**❗ Важно:** Закрывается та же **доля** (процент), что и у трейдера, независимо от абсолютных величин.

---

### 4️⃣ ClosePosition - Полное закрытие

**Когда:** Трейдер полностью закрывает позицию (OrderSubType.Close)

**Логика:**
```csharp
// 1. Получаем маппинг
var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

if (mapping == null)
{
    _logger.LogWarning($"Маппинг не найден для закрытия позиции");
    return;
}

// 2. Закрываем ВСЮ вашу позицию
var myCloseQuantity = mapping.MyQuantity;

_logger.LogInformation($"Закрываем полностью {myCloseQuantity}");

// 3. УДАЛЯЕМ маппинг (позиция больше не существует)
_positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

// 4. TODO: Разместить ордер на полное закрытие через OrdersProvider
```

**Пример:**
```
Текущая позиция:
  Трейдер: 3.7 BTC, Вы: 0.42 BTC

Трейдер закрывает полностью 3.7 BTC:
  Вы закрываете: 0.42 BTC (всю вашу позицию)

Результат:
  Позиция закрыта, маппинг удален
```

**❗ Важно:** Закрывается **ВСЯ** ваша позиция, независимо от количества в ордере трейдера.

---

## Ключевые особенности реализации

### ✅ 1. Независимость от изменения балансов

**Проблема:** Балансы счетов постоянно меняются из-за прибыли/убытков.

**Решение:** Сохранение `PositionRatio` при открытии позиции.

**Пример:**
```
День 1 - Открытие позиции:
  Трейдер: баланс $20,000, открывает позицию на $1,000
  Вы: баланс $2,000, открываете позицию на $100
  PositionRatio = 0.1 ← СОХРАНЯЕТСЯ!

День 7 - Увеличение позиции:
  Трейдер: баланс $25,000 (вырос), увеличивает позицию на 1 BTC
  Вы: баланс $1,500 (упал), увеличиваете на 0.1 BTC

Используется СОХРАНЕННЫЙ PositionRatio = 0.1, а НЕ текущие балансы!
Если бы пересчитывали: $1,500 / $25,000 = 0.06 ← НЕПРАВИЛЬНО!
```

### ✅ 2. Пропорциональное закрытие

**Проблема:** Как закрыть "правильную" часть позиции?

**Решение:** Расчет через долю (closeRatio), а не абсолютные значения.

**Пример:**
```
Позиция: Трейдер 10 BTC, Вы 1 BTC

Сценарий 1: Трейдер закрывает 5 BTC (50%)
  closeRatio = 5 / 10 = 0.5
  Вы закрываете: 1 × 0.5 = 0.5 BTC ✅

Сценарий 2: Трейдер закрывает 2 BTC (20%)
  closeRatio = 2 / 10 = 0.2
  Вы закрываете: 1 × 0.2 = 0.2 BTC ✅

Сценарий 3: Трейдер закрывает 10 BTC (100%)
  Вы закрываете всю позицию: 1 BTC ✅
```

### ✅ 3. Поддержка разнонаправленных позиций

**Биржа HyperLiquid:** Поддерживает одновременно Long и Short позиции по одному символу.

**Решение:** Маппинги хранятся с учетом Direction.

**Пример:**
```
Ключ маппинга: "{TraderWallet}_{MyWallet}_{Symbol}_{Direction}"

Трейдер может одновременно иметь:
  BTC Long: 5 BTC
  BTC Short: 2 BTC

Маппинги:
  "0xTrader_0xMe_BTC_Long" → PositionRatio = 0.1
  "0xTrader_0xMe_BTC_Short" → PositionRatio = 0.15

Копируются независимо друг от друга!
```

### ✅ 4. Автоматическое определение типа операции

**CurrentWalletPositionService:** Отслеживает текущие позиции и определяет OrderSubType.

**Логика определения:**
```csharp
Если позиции нет → OrderSubType.Open
Если позиция есть:
  - Направление совпадает → OrderSubType.Increase
  - Направление противоположное:
    - Закрывает часть → OrderSubType.Decrease
    - Закрывает полностью → OrderSubType.Close
```

### ✅ 5. Округление до точности биржи

**Проблема:** Биржа требует определенное количество десятичных знаков.

**Решение:** `ExchangeInfoProvider.GetExchangeInfo(symbol).QuantityDecimals`

**Пример:**
```csharp
BTC: QuantityDecimals = 5 → 0.00123 BTC ✅
SOL: QuantityDecimals = 2 → 12.34 SOL ✅
ETH: QuantityDecimals = 4 → 1.2345 ETH ✅

Расчет: myQuantity = 0.123456789 BTC
После округления: 0.12345 BTC (до 5 знаков)
```

---

## Граничные случаи и обработка ошибок

### 1. Маппинг не найден при увеличении позиции
```csharp
if (mapping == null)
{
    _logger.LogWarning("Маппинг не найден. Открываем как новую позицию.");
    await OpenNewPosition(order);
    return;
}
```

**Причины:** Рестарт приложения, очистка маппингов, рассинхронизация.

**Решение:** Автоматически открываем как новую позицию.

### 2. Маппинг не найден при закрытии позиции
```csharp
if (mapping == null)
{
    _logger.LogWarning("Маппинг не найден для закрытия позиции");
    return; // Не размещаем ордер
}
```

**Причины:** Позиция не была открыта через систему копирования.

**Решение:** Логируем предупреждение, не размещаем ордер.

### 3. Деление на ноль при расчете closeRatio
```csharp
var closeRatio = order.Quantity / mapping.TraderQuantity;
```

**Защита:** Если `mapping.TraderQuantity == 0`, это ошибка в логике (не должно происходить).

**Решение:** Добавить проверку и логирование ошибки.

---

## TODO: Следующие шаги

### 🔴 Критично - Размещение ордеров на бирже

Сейчас логика только **создает** копируемые ордера, но НЕ размещает их на бирже.

**Нужно добавить:**
```csharp
// В OpenNewPosition:120
await PlaceOrderOnExchange(copyOrder);

// В IncreasePosition:155
await PlaceIncreaseOrderOnExchange(order.Symbol, order.Direction, myIncreaseQuantity, order.Price);

// В DecreasePosition:191
await PlaceDecreaseOrderOnExchange(order.Symbol, order.Direction, myCloseQuantity, order.Price);

// В ClosePosition:218
await PlaceCloseOrderOnExchange(order.Symbol, order.Direction, myCloseQuantity, order.Price);
```

**Использовать:** `OrdersProvider` из HyperLiquid.Net

### 🔴 Критично - Обработка статусов Filled, Canceled, Rejected

**Текущее состояние:** Методы созданы но не реализованы (только TODO и логирование).

**HandleFilledOrder (строка 126):**
```csharp
// TODO: Проверить статус нашего копируемого ордера
// Если наш ордер не исполнен полностью - залогировать ошибку или предпринять действия
// Можно получить наш ордер по OrderId трейдера из маппинга
```

**HandleCanceledOrder (строка 140):**
```csharp
// TODO: Отменить наш копируемый ордер через OrdersProvider
// Можно получить наш ордер по OrderId трейдера из маппинга
```

**HandleRejectedOrder (строка 153):**
```csharp
// TODO: Отменить наш копируемый ордер через OrdersProvider (если он был размещен)
// Можно получить наш ордер по OrderId трейдера из маппинга
```

**Решение:** Нужен маппинг OrderId трейдера → OrderId копируемого ордера для отслеживания и управления.

### 🟡 Важно - Синхронизация при старте

При перезапуске приложения маппинги теряются (in-memory).

**Варианты решения:**
1. Восстановление из текущих позиций на бирже
2. Сохранение маппингов в SQLite
3. Hybrid: in-memory + периодическое сохранение в БД

### 🟡 Важно - Обработка ошибок размещения

Что делать если ордер не разместился:
- Недостаточно средств
- Минимальный объем не достигнут
- Сетевая ошибка

**Решение:** Система повторных попыток, логирование, уведомления.

### 🟢 Желательно - Множественные трейдеры

Сейчас `_myWallet` захардкожен в `CopyOrderService`.

**Будущее:** Передавать список копируемых кошельков в конструктор.

---

## Примеры использования

### Сценарий 1: Полный жизненный цикл позиции с обработкой статусов

```
1. Трейдер размещает ордер Long BTC: 1 BTC × $50,000 = $50,000
   → Статус: Open → HandleOpenOrder → OpenNewPosition
   → Вы размещаете ордер: 0.1 BTC
   → Маппинг создан: TraderQuantity=1, MyQuantity=0.1, Ratio=0.1

   → Статус: Filled → HandleFilledOrder
   → Проверяем что наш ордер тоже исполнен

2. Трейдер увеличивает позицию: +0.5 BTC
   → Статус: Open → HandleOpenOrder → IncreasePosition
   → Вы увеличиваете: +0.05 BTC (0.5 × 0.1)
   → Маппинг обновлен: TraderQuantity=1.5, MyQuantity=0.15, Ratio=0.1

   → Статус: Filled → HandleFilledOrder
   → Проверяем исполнение

3. Трейдер частично закрывает: -0.75 BTC (50%)
   → Статус: Open → HandleOpenOrder → DecreasePosition
   → Вы закрываете: -0.075 BTC (тоже 50%)
   → Маппинг обновлен: TraderQuantity=0.75, MyQuantity=0.075, Ratio=0.1

   → Статус: Filled → HandleFilledOrder
   → Проверяем исполнение

4. Трейдер закрывает полностью: -0.75 BTC
   → Статус: Open → HandleOpenOrder → ClosePosition
   → Вы закрываете: -0.075 BTC (всю позицию)
   → Маппинг удален

   → Статус: Filled → HandleFilledOrder
   → Проверяем полное исполнение
```

### Сценарий 2: Изменение балансов не влияет

```
День 1:
  Трейдер: $20,000, открывает $2,000 (10%)
  Вы: $5,000, открываете $500 (10%)
  Ratio = 0.25

День 30 (балансы изменились):
  Трейдер: $35,000 (прибыль +$15k), увеличивает на $1,000
  Вы: $4,000 (убыток -$1k), увеличиваете на $250 (1000 × 0.25)

Используется СОХРАНЕННЫЙ Ratio=0.25!
НЕ пересчитывается как $4,000/$35,000 = 0.114 ❌
```

### Сценарий 3: Обработка отмены ордеров

```
1. Трейдер размещает ордер Long ETH: 10 ETH × $3,000 = $30,000
   → Статус: Open → HandleOpenOrder → OpenNewPosition
   → Вы размещаете ордер: 1 ETH
   → Маппинг создан

2. Трейдер отменяет свой ордер (передумал)
   → Статус: Canceled → HandleCanceledOrder
   → Вы отменяете свой ордер через OrdersProvider
   → Маппинг остается (позиция не была открыта)

3. Трейдер снова пытается разместить ордер
   → Статус: Rejected → HandleRejectedOrder
   → Вы отменяете свой ордер (если был размещен)
```

### Сценарий 4: Частичное исполнение (будущая функциональность)

```
1. Трейдер размещает большой ордер: 100 BTC
   → Статус: Open → Вы размещаете: 10 BTC

2. Ордер трейдера исполнен частично (50 BTC)
   → Статус: Filled (Partial)
   → TODO: Проверить частичное исполнение нашего ордера

3. Трейдер отменяет остаток
   → Статус: Canceled → Отменяем остаток нашего ордера
```

---

## Технические детали

### Потокобезопасность
- `ConcurrentDictionary` для хранения маппингов
- `async/await` для всех операций с БД и API
- Логирование всех операций для отладки

### Производительность
- In-memory хранение маппингов → O(1) доступ
- Минимум вызовов API биржи
- Кеширование ExchangeInfo

### Логирование
Все операции логируются с уровнем Information:
```csharp
_logger.LogInformation($"OpenNewPosition: {symbol} {direction}, Quantity={quantity}");
_logger.LogInformation($"Маппинг сохранен: {mapping}");
_logger.LogInformation($"IncreasePosition: Увеличиваем на {myIncreaseQuantity}");
```

---

## Заключение

Реализованная система обеспечивает:

✅ Точное пропорциональное копирование позиций
✅ Независимость от изменения балансов
✅ Корректную обработку всех типов операций
✅ Поддержку разнонаправленных позиций
✅ Потокобезопасность и производительность

**Следующий шаг:** Интеграция с `OrdersProvider` для реального размещения ордеров на бирже HyperLiquid.
