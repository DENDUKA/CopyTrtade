using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для реализации стратегии "Just-in-Time Provisioning" / "Active Window Strategy"
/// Находит ближайшие ордера для копирования по цене
/// </summary>
public class ActiveWindowService(
    IFillsOrderService fillsOrderService,
    ILogger<ActiveWindowService> logger) : IActiveWindowService
{
    private readonly IFillsOrderService _fillsOrderService = fillsOrderService;
    private readonly ILogger<ActiveWindowService> _logger = logger;

    private const int DefaultOrderCount = 3;

    /// <summary>
    /// Получает ближайшие ордера для указанного кошелька и символа.
    /// Long ордера: сортируются по цене от высокой к низкой (ближайшие к исполнению при падении цены)
    /// Short ордера: сортируются по цене от низкой к высокой (ближайшие к исполнению при росте цены)
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <param name="count">Количество ордеров для каждого направления (по умолчанию 3)</param>
    /// <returns>Кортеж с массивами Long и Short ордеров</returns>
    public (OrderFills[] LongOrders, OrderFills[] ShortOrders) GetNearestOrders(Wallet wallet, string symbol, int count = DefaultOrderCount)
    {
        _logger.LogInformation($"ActiveWindowService.GetNearestOrders: Wallet={wallet.Value}, Symbol={symbol}, Count={count}");

        // Получаем все pending ордера для указанного кошелька и символа
        var allPendingOrders = _fillsOrderService.GetPendingOrdersByWalletAndSymbol(wallet, symbol);

        // Фильтруем Long ордера и сортируем по цене от высокой к низкой
        // (ближайшие к исполнению при падении цены)
        var longOrders = allPendingOrders
            .Where(orderFills => orderFills.OriginalOrder.Direction == Direction.Long)
            .OrderByDescending(orderFills => orderFills.OriginalOrder.Price)
            .Take(count)
            .ToArray();

        // Фильтруем Short ордера и сортируем по цене от низкой к высокой
        // (ближайшие к исполнению при росте цены)
        var shortOrders = allPendingOrders
            .Where(orderFills => orderFills.OriginalOrder.Direction == Direction.Short)
            .OrderBy(orderFills => orderFills.OriginalOrder.Price)
            .Take(count)
            .ToArray();

        _logger.LogInformation(
            $"ActiveWindowService.GetNearestOrders: Найдено {longOrders.Length} Long ордеров, {shortOrders.Length} Short ордеров");

        return (longOrders, shortOrders);
    }
}
