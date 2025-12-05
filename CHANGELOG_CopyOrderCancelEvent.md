# Changelog: Реализация отмены копируемых ордеров через событие

## Дата: 2025-12-05

## Описание
Реализована отмена копируемых ордеров через событийную архитектуру (DataBusEvents) вместо прямой зависимости между сервисами.

## Изменения

### 1. DataBusEvents.cs
**Добавлено:**
- Новое событие `CopyOrderCancelRequested` типа `Action<long>`
- Параметр события: `OriginalOrderId` - ID оригинального ордера трейдера
- Добавлена очистка события в метод `ClearAllSubscriptions()`

### 2. CopyOrderService2.cs
**Изменено:**
- Метод `CancelOrders()` теперь публикует событие `DataBusEvents.CopyOrderCancelRequested`
- Для каждого отменяемого ордера вызывается `DataBusEvents.CopyOrderCancelRequested?.Invoke(copyOrder.OriginalOrderId)`
- Удалена зависимость от `IOrderService` (решена циклическая зависимость)

### 3. OrderService.cs
**Добавлено:**
- Подписка на событие `DataBusEvents.CopyOrderCancelRequested` в конструкторе
- Метод-обработчик `OnCopyOrderCancelRequested(long originalOrderId)`:
  - Логирует получение события
  - Вызывает существующий метод `CloseOrderWithStatus(originalOrderId, OrderStatus.Canceled)`
  - Обрабатывает исключения с логированием

## Архитектура

```
CopyOrderService2 (Publisher)
    ↓ (публикует)
DataBusEvents.CopyOrderCancelRequested
    ↓ (подписан)
OrderService (Subscriber)
    ↓ (вызывает)
CloseOrderWithStatus()
    ↓ (обновляет)
CopyOrderStorageService
```

## Преимущества решения

1. **Разрыв циклической зависимости**: `OrderService` больше не зависит от `ICopyOrderService` напрямую
2. **Слабая связанность**: Сервисы общаются через события, а не через прямые вызовы
3. **Расширяемость**: Другие сервисы могут подписаться на событие отмены
4. **Единообразие**: Использует ту же паттерн событийной архитектуры, что и другие компоненты системы

## Тестирование

- ✅ Проект успешно компилируется без ошибок
- ✅ Сервис успешно запускается
- ✅ Нет циклических зависимостей

## Следующие шаги (опционально)

1. Реализовать фактическую отмену ордера на бирже через `OrdersProvider.CancelOrder()`
2. Добавить публикацию события `DataBusEvents.CopyOrderClosed` после успешной отмены
3. Добавить unit-тесты для проверки работы события
