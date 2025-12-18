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

    // ========== POSITION MAPPINGS ==========

    /// <summary>
    /// Сохраняет маппинг позиции в Redis
    /// </summary>
    Task SavePositionMapping(Models.Models.PositionMapping mapping);

    /// <summary>
    /// Получает маппинг позиции по ключу
    /// </summary>
    Task<Models.Models.PositionMapping?> GetPositionMapping(string key);

    /// <summary>
    /// Проверяет существование маппинга
    /// </summary>
    Task<bool> PositionMappingExists(string key);

    /// <summary>
    /// Удаляет маппинг позиции
    /// </summary>
    Task DeletePositionMapping(string key);

    /// <summary>
    /// Загружает все маппинги позиций
    /// </summary>
    Task<Dictionary<string, Models.Models.PositionMapping>> LoadAllPositionMappings();

    /// <summary>
    /// Загружает все маппинги для указанного трейдера
    /// </summary>
    Task<Models.Models.PositionMapping[]> GetMappingsByTrader(Models.Values.Wallet traderWallet);

    // ========== BASELINE POSITIONS ==========

    /// <summary>
    /// Сохраняет базовую позицию в Redis
    /// Ключ: {Wallet}_{Symbol}
    /// Значение: decimal (количество с знаком: положительное = Long, отрицательное = Short)
    /// </summary>
    Task SaveBaselinePosition(Models.Values.Wallet wallet, string symbol, decimal quantity);

    /// <summary>
    /// Получает базовую позицию
    /// </summary>
    Task<decimal?> GetBaselinePosition(Models.Values.Wallet wallet, string symbol);

    /// <summary>
    /// Удаляет базовую позицию
    /// </summary>
    Task DeleteBaselinePosition(Models.Values.Wallet wallet, string symbol);

    /// <summary>
    /// Загружает все базовые позиции
    /// </summary>
    Task<Dictionary<string, decimal>> LoadAllBaselinePositions();

    // ========== COPY ORDERS ==========

    /// <summary>
    /// Сохраняет копируемый ордер в Redis
    /// </summary>
    Task SaveCopyOrder(Models.Models.Orders.CopyOrderV2 copyOrder);

    /// <summary>
    /// Получает копируемый ордер по ID
    /// </summary>
    Task<Models.Models.Orders.CopyOrderV2?> GetCopyOrder(long orderId);

    /// <summary>
    /// Удаляет копируемый ордер
    /// </summary>
    Task DeleteCopyOrder(long orderId);

    /// <summary>
    /// Загружает все копируемые ордера
    /// </summary>
    Task<Dictionary<long, Models.Models.Orders.CopyOrderV2>> LoadAllCopyOrders();

    /// <summary>
    /// Получает копируемые ордера по оригинальному ID ордера
    /// </summary>
    Task<Models.Models.Orders.CopyOrderV2[]> GetCopyOrdersByOriginalId(long originalOrderId);

    /// <summary>
    /// Получает копируемые ордера по кошельку и символу трейдера
    /// </summary>
    Task<Models.Models.Orders.CopyOrderV2[]> GetCopyOrdersByWalletAndSymbol(Models.Values.Wallet traderWallet, string symbol);

    /// <summary>
    /// Удаляет несколько копируемых ордеров за один вызов
    /// </summary>
    Task DeleteCopyOrders(IEnumerable<long> orderIds);

    // ========== COPY ORDER RESULTS ==========

    /// <summary>
    /// Сохраняет результат копирования ордера
    /// </summary>
    Task SaveCopyOrderResult(Models.Models.CopyOrderResult result);

    /// <summary>
    /// Получает результат копирования по ID оригинального ордера
    /// </summary>
    Task<Models.Models.CopyOrderResult?> GetCopyOrderResult(string originalOrderId);

    /// <summary>
    /// Удаляет результат копирования
    /// </summary>
    Task DeleteCopyOrderResult(string originalOrderId);

    /// <summary>
    /// Загружает все результаты копирования
    /// </summary>
    Task<Dictionary<string, Models.Models.CopyOrderResult>> LoadAllCopyOrderResults();

    /// <summary>
    /// Удаляет несколько результатов за один вызов
    /// </summary>
    Task DeleteCopyOrderResults(IEnumerable<string> originalOrderIds);

    // ========== COPY TRADE WALLET SETTINGS ==========

    /// <summary>
    /// Сохраняет настройки копитрейда для кошелька
    /// </summary>
    Task SaveWalletSettings(Models.Models.CopyTradeWalletSettings settings);

    /// <summary>
    /// Получает настройки копитрейда по кошельку
    /// </summary>
    Task<Models.Models.CopyTradeWalletSettings?> GetWalletSettings(Models.Values.Wallet wallet);

    /// <summary>
    /// Удаляет настройки копитрейда для кошелька
    /// </summary>
    Task DeleteWalletSettings(Models.Values.Wallet wallet);

    /// <summary>
    /// Загружает все настройки копитрейда
    /// </summary>
    Task<Dictionary<Models.Values.Wallet, Models.Models.CopyTradeWalletSettings>> LoadAllWalletSettings();

    // ========== WALLET POSITION SNAPSHOTS ==========

    /// <summary>
    /// Сохраняет снапшот позиций кошелька
    /// </summary>
    Task SaveWalletSnapshot(Models.Models.WalletPositionsSnapshot snapshot);

    /// <summary>
    /// Получает снапшот позиций кошелька
    /// </summary>
    Task<Models.Models.WalletPositionsSnapshot?> GetWalletSnapshot(Models.Values.Wallet wallet);

    /// <summary>
    /// Удаляет снапшот позиций кошелька
    /// </summary>
    Task DeleteWalletSnapshot(Models.Values.Wallet wallet);

    /// <summary>
    /// Загружает все снапшоты позиций кошельков
    /// </summary>
    Task<Dictionary<Models.Values.Wallet, Models.Models.WalletPositionsSnapshot>> LoadAllWalletSnapshots();

    /// <summary>
    /// Очищает все снапшоты позиций
    /// </summary>
    Task ClearAllWalletSnapshots();
}
