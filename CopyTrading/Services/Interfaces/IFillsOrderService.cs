using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис корреляции ордеров и сделок (трейдов).
///
/// ОСНОВНЫЕ ОБЯЗАННОСТИ:
/// 1. Сопоставление трейдов с их ордерами в реальном времени
/// 2. Отслеживание состояния исполнения ордеров (количество, статус)
/// 3. Генерация событий OrderFinished при достижении финальных статусов
/// 4. Обработка pending трейдов, которые приходят раньше своих ордеров
/// 5. Валидация корректности исполнения (соответствие количества трейдов ордеру)
/// 6. Автоматическая очистка памяти от старых завершенных ордеров
///
/// АРХИТЕКТУРНЫЕ ОСОБЕННОСТИ:
/// - НЕ подписан напрямую на DataBusEvents.NewOrders и DataBusEvents.NewTrades
/// - Получает данные через методы OnNewOrders() и OnNewTrades(), которые вызывают
///   OrderService и TradeService соответственно (паттерн "Медиатор")
/// - Хранит все ордера в памяти (ConcurrentDictionary) с их трейдами
/// - Поддерживает словарь _pendingTrades для трейдов без соответствующих ордеров
/// - Автоматически очищает память: при превышении 2000 ордеров удаляет старые завершенные до 500
///
/// ПОТОК ОБРАБОТКИ ОРДЕРОВ:
/// 1. OrderService получает новые ордера через WebSocket
/// 2. OrderService вызывает OnNewOrders() передавая массив ордеров
/// 3. FillsOrderService создает OrderFills для каждого нового ордера
/// 4. При получении трейдов через OnNewTrades() сопоставляет их с ордерами
/// 5. Отслеживает количество исполненного объема
/// 6. Генерирует событие OrderFinished при достижении финального статуса
///
/// ИСПОЛЬЗОВАНИЕ:
/// - CurrentWalletPositionService - для получения pending ордеров и пересчета SubType
/// - CopyOrderService - для проверки наличия открытых позиций
/// - OrderService - для добавления новых ордеров и обновления SubType
/// - TradeService - для корреляции трейдов с ордерами
///
/// АКТИВАЦИЯ:
/// Должен быть явно активирован в Startup.Configure() через serviceProvider.GetService<FillsOrderService>()
/// для гарантии выполнения конструктора и регистрации обработчиков.
/// </summary>
public interface IFillsOrderService
{
    void OnNewOrders(OriginalOrder[] orders);

    void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades);

    OrderFills[] GetAllOrderFills();

    OrderFills? GetOrderFillsByOrderId(long orderId);

    OrderFills[] GetOpenOrdersByWallet(Wallet wallet);

    OrderFills[] GetPendingOrdersByWalletAndSymbol(Wallet wallet, string symbol);

    bool UpdateOrderSubType(long orderId, OrderSubType newSubType);

    int AddHistoricalOrders(OriginalOrder[] orders);
}
