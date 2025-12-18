using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;

namespace CopyTrading.Repository.RedisInterfaces;

/// <summary>
/// Общий репозиторий для работы с Redis.
/// Инкапсулирует все детали работы с Redis (ключи, структуры данных и т.д.)
/// Может быть расширен для работы с данными различных сервисов.
/// </summary>
public interface IRedisRepository
{
    // ========== ORDERS ==========

    /// <summary>
    /// Сохраняет ордер в Redis
    /// </summary>
    Task SaveOrder(OrderFills orderFills);

    /// <summary>
    /// Получает ордер по ID
    /// </summary>
    Task<OrderFills?> GetOrder(long orderId);

    /// <summary>
    /// Проверяет существование ордера
    /// </summary>
    Task<bool> OrderExists(long orderId);

    /// <summary>
    /// Загружает все ордера из Redis
    /// </summary>
    Task<Dictionary<long, OrderFills>> LoadAllOrders();

    /// <summary>
    /// Получает pending ордера для указанного кошелька и символа
    /// </summary>
    Task<OrderFills[]> GetPendingOrdersByWalletAndSymbol(string wallet, string symbol);

    /// <summary>
    /// Обновляет SubType для ордера
    /// </summary>
    Task<bool> UpdateOrderSubType(long orderId, OrderSubType newSubType);

    /// <summary>
    /// Удаляет ордер из Redis
    /// </summary>
    Task DeleteOrder(long orderId);

    // ========== PENDING TRADES ==========

    /// <summary>
    /// Сохраняет pending трейд в Redis
    /// </summary>
    Task SavePendingTrade(OriginalTrade trade);

    /// <summary>
    /// Получает pending трейд по ID
    /// </summary>
    Task<OriginalTrade?> GetPendingTrade(long tradeId);

    /// <summary>
    /// Проверяет существование pending трейда
    /// </summary>
    Task<bool> PendingTradeExists(long tradeId);

    /// <summary>
    /// Загружает все pending трейды из Redis
    /// </summary>
    Task<Dictionary<long, OriginalTrade>> LoadAllPendingTrades();

    /// <summary>
    /// Удаляет pending трейд из Redis
    /// </summary>
    Task DeletePendingTrade(long tradeId);

    // ========== ORDER ERRORS ==========

    /// <summary>
    /// Сохраняет ошибку ордера в Redis
    /// </summary>
    Task SaveOrderError(long orderId, string errorMessage);

    /// <summary>
    /// Получает ошибку ордера по ID
    /// </summary>
    Task<string?> GetOrderError(long orderId);

    /// <summary>
    /// Проверяет наличие ошибки у ордера
    /// </summary>
    Task<bool> OrderHasError(long orderId);

    /// <summary>
    /// Загружает все ошибки ордеров из Redis
    /// </summary>
    Task<Dictionary<long, string>> LoadAllOrderErrors();

    /// <summary>
    /// Удаляет ошибку ордера из Redis
    /// </summary>
    Task DeleteOrderError(long orderId);

    // ========== BATCH OPERATIONS ==========

    /// <summary>
    /// Удаляет несколько ордеров из Redis за один вызов
    /// </summary>
    Task DeleteOrders(IEnumerable<long> orderIds);
}
