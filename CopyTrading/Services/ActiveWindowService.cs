using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Orders;
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
    /// Получает ближайшие ордера для указанного кошелька, символа и направления.
    /// Long ордера: сортируются по цене от высокой к низкой (ближайшие к исполнению при падении цены)
    /// Short ордера: сортируются по цене от низкой к высокой (ближайшие к исполнению при росте цены)
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <param name="direction">Направление ордеров (Long или Short)</param>
    /// <param name="count">Количество ордеров (по умолчанию 3)</param>
    /// <returns>Массив ближайших ордеров указанного направления</returns>
    public async Task<OriginalOrder[]> GetNearestOrders(Wallet wallet, string symbol, Direction direction, int count = DefaultOrderCount)
    {
        _logger.LogInformation($"ActiveWindowService.GetNearestOrders: Wallet={wallet.Value}, Symbol={symbol}, Direction={direction}, Count={count}");

        // Получаем все pending ордера для указанного кошелька и символа
        var allPendingOrders = await _fillsOrderService.GetPendingOrdersByWalletAndSymbol(wallet, symbol);

        // Фильтруем ордера по направлению и сортируем
        OriginalOrder[] nearestOrders = [];

        if (direction == Direction.Long)
        {
            // Long ордера: сортируем по цене от высокой к низкой
            // (ближайшие к исполнению при падении цены)
            nearestOrders = [.. allPendingOrders                
                .Where(orderFills => orderFills.OriginalOrder.Direction == Direction.Long)
                .Select(orderFills => orderFills.OriginalOrder)
                .OrderByDescending(order => order.Price)
                .Take(count)];
        }
        else if (direction == Direction.Short)
        {
            // Short ордера: сортируем по цене от низкой к высокой
            // (ближайшие к исполнению при росте цены)
            nearestOrders = [.. allPendingOrders
                .Where(orderFills => orderFills.OriginalOrder.Direction == Direction.Short)
                .Select(orderFills => orderFills.OriginalOrder)
                .OrderBy(order => order.Price)
                .Take(count)];
        }

        _logger.LogInformation(
            $"ActiveWindowService.GetNearestOrders: Найдено {nearestOrders.Length} {direction} ордеров");

        return nearestOrders;
    }
}
