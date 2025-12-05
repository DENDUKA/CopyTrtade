using CopyTrading.Mappers;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Options;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class OrdersProvider : IOrdersProvider
{
    private readonly HyperLiquidRestClient _hyperLiquidRestClient;
    private readonly ILogger<OrdersProvider> _logger;

    //TODO Key Secret вынести в secret.json
    private readonly string key = "1";
    private readonly string secret = "1";

    public OrdersProvider(ILogger<OrdersProvider> logger)
    {
        _hyperLiquidRestClient = new HyperLiquidRestClient(new Action<HyperLiquidRestOptions>(options =>
        {
            options.ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(key, secret);
        }));
        _logger = logger;
    }

    /// <summary>
    /// Получает все активные (открытые) ордера для указанного кошелька
    /// </summary>
    /// <param name="wallet">Кошелек трейдера</param>
    /// <returns>Массив активных ордеров или пустой массив при ошибке</returns>
    public async Task<OriginalOrder[]> GetActiveOrders(Wallet wallet)
    {
        try
        {
            var response = await _hyperLiquidRestClient.FuturesApi.Trading.GetOpenOrdersAsync(wallet.Value);

            if (response.Success)
            {
                _logger.LogInformation($"GetActiveOrders: Получено {response.Data.Count()} активных ордеров для {wallet}");
                return response.Data.Select(x => x.ToBll(wallet)).ToArray();
            }
            else
            {
                _logger.LogError($"GetActiveOrders: Ошибка при получении активных ордеров для {wallet}: {response.Error?.Message}");
                return [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"GetActiveOrders: Исключение при получении активных ордеров для {wallet}");
            return [];
        }
    }

    public async Task<long?> UpdateLeverage(NewLeverageModel newLeverage)
    {
        try
        {
            //var response = await _hyperLiquidRestClient.FuturesApi.Trading.SetLeverageAsync(,);
            //TODO
            //https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint#update-leverage
            //нужен api c заданием vaultAddress (странно что нет у JKorf)

        }
        catch (Exception ex)
        {
            _logger.LogError($"OrdersProvider Exception {ex.Message}");
        }

        return null;
    }

    public async Task<long?> PlaceOrder()
    {
        try
        {
            var response = await _hyperLiquidRestClient.FuturesApi.Trading.PlaceOrderAsync("ETH", OrderSide.Buy, OrderType.Market, 1, 4400);

            if (response.Success)
            {
                return response.Data.OrderId;
            }
            else
            {
                _logger.LogError($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NewOrder Exception {ex.Message}");
        }

        return null;
    }

    public async Task<bool> CloseOrder(string symbol, long orderId)
    {
        try
        {
            var response = await _hyperLiquidRestClient.FuturesApi.Trading.CancelOrderByClientOrderIdAsync(symbol, orderId.ToString());

            if (response is not null && response.Success)
            {
                //TODO надо получить ответ
                return true;
            }
            else
            {
                _logger.LogError($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NewOrder Exception {ex.Message}");
        }

        return false;
    }

    #region Copy Trading Methods

    /// <summary>
    /// Размещает ордер на открытие новой позиции (копирование)
    /// </summary>
    /// <param name="copyOrder">Копируемый ордер с рассчитанными параметрами</param>
    /// <returns>OrderId размещенного ордера или null при ошибке</returns>
    public async Task<long?> PlaceOpenPositionOrder(CopyOrderV2 copyOrder)
    {
        //TODO: Реализовать размещение ордера на бирже
        // 1. Получить параметры из copyOrder.OriginalOrder (Symbol, Direction, Price, Leverage)
        // 2. Использовать copyOrder.Quantity для размера позиции
        // 3. Разместить ордер через _hyperLiquidRestClient.FuturesApi.Trading.PlaceOrderAsync
        // 4. Вернуть OrderId размещенного ордера

        _logger.LogWarning($"PlaceOpenPositionOrder: TODO - размещение ордера {copyOrder.OriginalOrder.Symbol} {copyOrder.OriginalOrder.Direction} Quantity={copyOrder.Quantity}");

        return null;
    }

    /// <summary>
    /// Размещает ордер на увеличение существующей позиции
    /// </summary>
    /// <param name="symbol">Символ (BTC, ETH, ...)</param>
    /// <param name="direction">Направление (Long/Short)</param>
    /// <param name="quantity">Количество для увеличения</param>
    /// <param name="price">Цена размещения</param>
    /// <param name="leverage">Плечо</param>
    /// <returns>OrderId размещенного ордера или null при ошибке</returns>
    public async Task<long?> PlaceIncreasePositionOrder(string symbol, CopyTrading.Models.Models.Enums.Direction direction, decimal quantity, decimal price, int leverage)
    {
        //TODO: Реализовать размещение ордера на увеличение позиции
        // 1. Конвертировать Direction в OrderSide (Long → Buy, Short → Sell)
        // 2. Разместить ордер через PlaceOrderAsync
        // 3. Вернуть OrderId

        _logger.LogWarning($"PlaceIncreasePositionOrder: TODO - увеличение позиции {symbol} {direction} Quantity={quantity} Price={price} Leverage={leverage}");

        return null;
    }

    /// <summary>
    /// Размещает ордер на частичное закрытие позиции
    /// </summary>
    /// <param name="symbol">Символ (BTC, ETH, ...)</param>
    /// <param name="direction">Направление ТЕКУЩЕЙ позиции (Long/Short)</param>
    /// <param name="quantity">Количество для закрытия</param>
    /// <param name="price">Цена закрытия</param>
    /// <returns>OrderId размещенного ордера или null при ошибке</returns>
    public async Task<long?> PlaceDecreasePositionOrder(string symbol, CopyTrading.Models.Models.Enums.Direction direction, decimal quantity, decimal price)
    {
        //TODO: Реализовать размещение ордера на частичное закрытие
        // 1. Конвертировать Direction в противоположный OrderSide (Long → Sell, Short → Buy)
        // 2. Разместить ордер через PlaceOrderAsync
        // 3. Вернуть OrderId

        _logger.LogWarning($"PlaceDecreasePositionOrder: TODO - частичное закрытие {symbol} {direction} Quantity={quantity} Price={price}");

        return null;
    }

    /// <summary>
    /// Размещает ордер на полное закрытие позиции
    /// </summary>
    /// <param name="symbol">Символ (BTC, ETH, ...)</param>
    /// <param name="direction">Направление ТЕКУЩЕЙ позиции (Long/Short)</param>
    /// <param name="quantity">Количество для закрытия (вся позиция)</param>
    /// <param name="price">Цена закрытия</param>
    /// <returns>OrderId размещенного ордера или null при ошибке</returns>
    public async Task<long?> PlaceClosePositionOrder(string symbol, CopyTrading.Models.Models.Enums.Direction direction, decimal quantity, decimal price)
    {
        //TODO: Реализовать размещение ордера на полное закрытие
        // Аналогично PlaceDecreasePositionOrder, но закрывает всю позицию
        // 1. Конвертировать Direction в противоположный OrderSide
        // 2. Разместить ордер через PlaceOrderAsync
        // 3. Вернуть OrderId

        _logger.LogWarning($"PlaceClosePositionOrder: TODO - полное закрытие {symbol} {direction} Quantity={quantity} Price={price}");

        return null;
    }

    /// <summary>
    /// Отменяет ордер по его ID
    /// </summary>
    /// <param name="symbol">Символ</param>
    /// <param name="orderId">ID ордера для отмены</param>
    /// <returns>true если ордер успешно отменен</returns>
    public async Task<bool> CancelOrderById(string symbol, long orderId)
    {
        //TODO: Реализовать отмену ордера
        // Использовать существующий метод CloseOrder или CancelOrderByClientOrderIdAsync

        _logger.LogWarning($"CancelOrderById: TODO - отмена ордера {symbol} OrderId={orderId}");

        return false;
    }

    /// <summary>
    /// Проверяет статус исполнения ордера
    /// </summary>
    /// <param name="orderId">ID ордера для проверки</param>
    /// <returns>Статус ордера или null если не найден</returns>
    public async Task<CopyTrading.Models.Models.Enums.Order.OrderStatus?> CheckOrderStatus(long orderId)
    {
        //TODO: Реализовать проверку статуса ордера
        // 1. Получить информацию об ордере через API
        // 2. Вернуть текущий статус (Open, Filled, Canceled, etc.)

        _logger.LogWarning($"CheckOrderStatus: TODO - проверка статуса ордера OrderId={orderId}");

        return null;
    }

    #endregion

    #region IOrdersProvider Implementation

    /// <summary>
    /// Размещает копируемый ордер (реализация интерфейса IOrdersProvider)
    /// </summary>
    public async Task<CopyTrading.Models.Models.Enums.Order.OrderPlaceResult> Place(CopyOrder order)
    {
        _logger.LogWarning($"Place: TODO - размещение копируемого ордера {order.OriginalOrder.Symbol} {order.OriginalOrder.Direction}");

        // TODO: Реализовать размещение ордера через HyperLiquid API
        // Временно возвращаем Ok для тестирования
        return CopyTrading.Models.Models.Enums.Order.OrderPlaceResult.Ok;
    }

    /// <summary>
    /// Закрывает ордер по ID (реализация интерфейса IOrdersProvider)
    /// </summary>
    public async Task<CopyTrading.Models.Models.Enums.Order.OrderPlaceResult> Close(long orderId)
    {
        _logger.LogWarning($"Close: TODO - закрытие ордера OrderId={orderId}");

        // TODO: Реализовать закрытие ордера через HyperLiquid API
        // Временно возвращаем Ok для тестирования
        return CopyTrading.Models.Models.Enums.Order.OrderPlaceResult.Ok;
    }

    /// <summary>
    /// Помечает ордер как исполненный (только для тестирования)
    /// </summary>
    public async Task<CopyTrading.Models.Models.Enums.Order.OrderPlaceResult> Filled(long orderId)
    {
        _logger.LogWarning($"Filled: TODO - пометка ордера как исполненного OrderId={orderId}");

        // TODO: Реализовать для тестирования
        // Временно возвращаем Ok для тестирования
        return CopyTrading.Models.Models.Enums.Order.OrderPlaceResult.Ok;
    }

    #endregion

}
