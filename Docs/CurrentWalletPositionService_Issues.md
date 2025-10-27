# Проблемы CurrentWalletPositionService - Расхождение позиций

## Обнаруженные проблемы

### ❌ Проблема 1: VolumeUsd НЕ обновляется при изменении Quantity

**Код (строки 70, 85):**
```csharp
// Increase (line 70)
openPos[0].Quantity += trade.RealQuantity;  // ✅ Обновляется
// НО VolumeUsd НЕ обновляется! ❌

// Decrease (line 85)
openPos[0].Quantity += trade.RealQuantity;  // ✅ Обновляется
// НО VolumeUsd НЕ обновляется! ❌
```

**Что происходит:**
1. Trade приходит: Quantity = 0.5, VolumeUsd = 15000
2. Обновляем: `openPos[0].Quantity += 0.5` → Quantity теперь 1.5
3. **НО**: `openPos[0].VolumeUsd` остается прежним!
4. Результат: Quantity = 1.5, но VolumeUsd не соответствует реальному объему

**Правильно должно быть:**
```csharp
// Increase
openPos[0].Quantity += trade.RealQuantity;
openPos[0].VolumeUsd += trade.VolumeUsd;  // ← ДОБАВИТЬ!

// Decrease
openPos[0].Quantity += trade.RealQuantity;  // += потому что RealQuantity отрицательное для противоположного направления
openPos[0].VolumeUsd -= trade.VolumeUsd;  // ← ДОБАВИТЬ!
```

---

### ❌ Проблема 2: Некорректный расчет AverageEntryPrice при Decrease

**Код (строка 84):**
```csharp
openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd - trade.VolumeUsd) / (openPos[0].Quantity - trade.Quantity);
```

**Проблема**: Используется `trade.Quantity` вместо `trade.RealQuantity`!

**Почему это важно:**
- `trade.Quantity` - всегда ПОЛОЖИТЕЛЬНОЕ число (модуль)
- `trade.RealQuantity` - учитывает направление (может быть отрицательным для Short)

**Пример проблемы:**
```
Текущая позиция: SHORT, Quantity = -1.0, VolumeUsd = 30000
Trade закрывает: LONG 0.5, trade.Quantity = 0.5, trade.RealQuantity = 0.5

Текущий расчет (НЕПРАВИЛЬНО):
AverageEntryPrice = (30000 - 15000) / (-1.0 - 0.5) = 15000 / -1.5 = -10000 ❌

Правильный расчет:
openPos[0].Quantity + trade.RealQuantity = -1.0 + 0.5 = -0.5 (осталось)
AverageEntryPrice = (30000 - 15000) / -0.5 = 15000 / -0.5 = -30000 ✅
```

**Правильно:**
```csharp
openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd - trade.VolumeUsd) / (openPos[0].Quantity + trade.RealQuantity);
```

---

### ❌ Проблема 3: Некорректная проверка при Decrease

**Код (строка 82):**
```csharp
if (Math.Abs(openPos[0].Quantity) > Math.Abs(trade.Quantity))
```

**Проблема**: Сравнивается с `trade.Quantity` вместо того, чтобы проверить результирующее количество.

**Что происходит:**
```
Позиция: SHORT -0.3
Trade: LONG 0.5 (закрывает SHORT)

Проверка: Math.Abs(-0.3) > Math.Abs(0.5) → 0.3 > 0.5 → false ❌
Результат: код идет в else (строка 90) и логирует ошибку

НО на самом деле это ПЕРЕВОРОТ позиции:
-0.3 + 0.5 = +0.2 (закрыли SHORT и открыли LONG на 0.2)
```

**Это не баг, но требует обработки!** Нужно добавить логику для переворота позиции (Close + Open в противоположном направлении).

---

### ❌ Проблема 4: Отсутствует обновление VolumeUsd в логике Increase/Decrease AverageEntryPrice

**Код (строка 63):**
```csharp
openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd + trade.VolumeUsd) / (openPos[0].Quantity + trade.Quantity);
```

Затем (строка 70):
```csharp
openPos[0].Quantity += trade.RealQuantity;
```

**Проблема**: Рассчитываем AverageEntryPrice с новым VolumeUsd, но САМО ЗНАЧЕНИЕ VolumeUsd НЕ ОБНОВЛЯЕМ!

**Результат:**
- AverageEntryPrice рассчитан правильно
- Но при следующем трейде будет использован СТАРЫЙ VolumeUsd
- Накапливается ошибка

---

### ❌ Проблема 5: Сравнение с trade.Quantity вместо проверки результата

**Код (строка 63):**
```csharp
openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd + trade.VolumeUsd) / (openPos[0].Quantity + trade.Quantity);
```

**Проблема**: Используется `trade.Quantity` (положительное) вместо `trade.RealQuantity` (учитывает направление).

**Пример:**
```
Позиция: SHORT, Quantity = -1.0
Trade: SHORT 0.5 (увеличиваем SHORT)
  trade.Quantity = 0.5
  trade.RealQuantity = -0.5

Текущий расчет (НЕПРАВИЛЬНО):
(VolumeUsd + trade.VolumeUsd) / (-1.0 + 0.5) = ... / -0.5 ❌

Правильно:
(VolumeUsd + trade.VolumeUsd) / (-1.0 + (-0.5)) = ... / -1.5 ✅
```

---

## Корректное решение

### Исправленный метод AddTrade:

```csharp
public async Task<OrderSubType> AddTrade(OriginalTrade trade)
{
    if (!_walletPositionSnapshot.ContainsKey(trade.Wallet))
    {
        _logger.LogError($"CurrentWalletPositionService Snapshot для {trade.Wallet} не инициализирован");
    }

    var semaphore = _walletSemaphores.GetOrAdd(trade.Wallet, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
        var openPos = _walletPositionSnapshot[trade.Wallet].Positions.Where(p => p.Symbol == trade.Symbol).ToArray();

        if (openPos.Length == 0)
        {
            var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet, false);

            if (!walletInfo.Positions.TryGetValue(trade.Symbol, out var position))
            {
                _logger.LogError($"CurrentWalletPositionService AddTrade не удалось найти позицию {trade.Symbol} у кошелька {trade.Wallet}");
                return OrderSubType.None;
            }

            _walletPositionSnapshot[trade.Wallet].Positions.Add(position);
            return OrderSubType.Open;
        }

        if (openPos.Length == 1)
        {
            if (openPos[0].Direction == trade.Direction)
            {
                // INCREASE: Увеличение позиции в том же направлении

                // Рассчитываем новое среднее
                var newQuantity = openPos[0].Quantity + trade.RealQuantity;
                var newVolumeUsd = openPos[0].VolumeUsd + trade.VolumeUsd;

                try
                {
                    openPos[0].AverageEntryPrice = newVolumeUsd / newQuantity;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CurrentWalletPositionService AddTrade Деление на ноль при расчете AverageEntryPrice! Quantity: {newQuantity}, Volume: {newVolumeUsd}");
                }

                // ✅ ОБНОВЛЯЕМ И Quantity И VolumeUsd!
                openPos[0].Quantity = newQuantity;
                openPos[0].VolumeUsd = newVolumeUsd;

                return OrderSubType.Increase;
            }
            else
            {
                // Противоположное направление - закрытие или уменьшение
                var newQuantity = openPos[0].Quantity + trade.RealQuantity;

                if (newQuantity == 0)
                {
                    // CLOSE: Полное закрытие позиции
                    _walletPositionSnapshot[trade.Wallet].Positions.Remove(openPos[0]);
                    return OrderSubType.Close;
                }

                if (Math.Sign(openPos[0].Quantity) == Math.Sign(newQuantity))
                {
                    // DECREASE: Частичное закрытие (направление не изменилось)
                    var newVolumeUsd = openPos[0].VolumeUsd - trade.VolumeUsd;

                    try
                    {
                        openPos[0].AverageEntryPrice = newVolumeUsd / newQuantity;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"CurrentWalletPositionService AddTrade Деление на ноль при Decrease! Quantity: {newQuantity}, Volume: {newVolumeUsd}");
                    }

                    // ✅ ОБНОВЛЯЕМ И Quantity И VolumeUsd!
                    openPos[0].Quantity = newQuantity;
                    openPos[0].VolumeUsd = newVolumeUsd;

                    return OrderSubType.Decrease;
                }
                else
                {
                    // FLIP: Переворот позиции (Close + Open в противоположном направлении)
                    // Например: SHORT -0.3 + LONG 0.5 = LONG +0.2
                    _logger.LogWarning($"CurrentWalletPositionService AddTrade ПЕРЕВОРОТ позиции {trade.Symbol} у {trade.Wallet}! " +
                                     $"Было: {openPos[0].Direction} {openPos[0].Quantity}, " +
                                     $"Стало: {(newQuantity > 0 ? "Long" : "Short")} {newQuantity}");

                    // Для переворота нужно:
                    // 1. Удалить старую позицию
                    _walletPositionSnapshot[trade.Wallet].Positions.Remove(openPos[0]);

                    // 2. Запросить новую позицию у провайдера
                    var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet, false);
                    if (walletInfo.Positions.TryGetValue(trade.Symbol, out var newPosition))
                    {
                        _walletPositionSnapshot[trade.Wallet].Positions.Add(newPosition);
                    }

                    // Возвращаем Close (так как текущая позиция закрылась)
                    return OrderSubType.Close;
                }
            }
        }

        if (openPos.Length > 1)
        {
            _logger.LogError($"CurrentWalletPositionService AddTrade обнаружено две открытые позиции (разнонаправленные) для {trade.Symbol} у кошелька {trade.Wallet}");
            return OrderSubType.None;
        }
    }
    finally
    {
        semaphore.Release();
    }

    return OrderSubType.None;
}
```

## Ключевые изменения:

1. ✅ **ВСЕГДА обновляем VolumeUsd** вместе с Quantity
2. ✅ **Используем RealQuantity** для расчета AverageEntryPrice
3. ✅ **Рассчитываем newQuantity один раз** и используем для всех проверок
4. ✅ **Добавлена обработка переворота позиции** (Flip)
5. ✅ **Используем Math.Sign** для проверки изменения направления
6. ✅ **Детальное логирование** для отладки

## История изменений

### 2025-10-27
- 📝 Обнаружены критические проблемы с обновлением VolumeUsd
- 📝 Обнаружена проблема с использованием trade.Quantity вместо trade.RealQuantity
- 📝 Добавлена обработка переворота позиции
- 💡 Предложено корректное решение
