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
///
/// АРХИТЕКТУРНЫЕ ОСОБЕННОСТИ:
/// - НЕ подписан напрямую на DataBusEvents.NewOrders и DataBusEvents.NewTrades
/// - Получает данные через методы OnNewOrders() и OnNewTrades(), которые вызывают
///   OrderService и TradeService соответственно (паттерн "Медиатор")
/// - Хранит все данные в Redis (OrderFills, PendingTrades, OrderErrors)
/// - Прямой доступ к Redis через IRedisRepository без кеширования в памяти
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
    Task OnNewOrders(OriginalOrder[] orders);

    void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades);

    Task<OrderFills[]> GetAllOrderFills();

    Task<OrderFills?> GetOrderFillsByOrderId(long orderId);

    Task<OrderFills[]> GetOpenOrdersByWallet(Wallet wallet);

    Task<OrderFills[]> GetPendingOrdersByWalletAndSymbol(Wallet wallet, string symbol);

    Task<bool> UpdateOrderSubType(long orderId, OrderSubType newSubType);

    Task<int> AddHistoricalOrders(OriginalOrder[] orders);
}
