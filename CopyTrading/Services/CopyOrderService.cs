using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService
{
    private readonly OrderService _orderService;
    private readonly IWalletInfoProvider _walletProvider;
    private readonly IExchangeInfoProvider _exchangeInfoProvider;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly PositionMappingService _positionMappingService;
    private readonly ILogger<CopyOrderService> _logger;
    //TODO вынести в конструктор , задаваться должен для каждого экземпляра ( поменять singleton )
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public CopyOrderService(
        OrderService orderService,
        IWalletInfoProvider walletProvider,
        IExchangeInfoProvider exchangeInfoProvider,
        CurrentWalletPositionService currentWalletPositionService,
        PositionMappingService positionMappingService,
        ILogger<CopyOrderService> logger)
    {
        _orderService = orderService;
        _walletProvider = walletProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _currentWalletPositionService = currentWalletPositionService;
        _positionMappingService = positionMappingService;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
    }

    private void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var order in orders)
        {
            OnNewOrder(order);
        }
    }

    private async void OnNewOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService OnNewOrder: {order.Symbol} {order.Direction} OrderId={order.OrderId}, Status={order.Status}");

        switch (order.Status)
        {
            case OrderStatus.Open:
                await HandleOpenOrder(order);
                break;

            case OrderStatus.Filled:
                await HandleFilledOrder(order);
                break;

            case OrderStatus.Canceled:
                await HandleCanceledOrder(order);
                break;

            case OrderStatus.Rejected:
                await HandleRejectedOrder(order);
                break;

            case OrderStatus.Triggered:
                // Triggered ордер уже размещен на бирже, ждем когда станет Open или Filled
                _logger.LogInformation($"CopyOrderService: Ордер {order.OrderId} триггернулся, ждем исполнения");
                break;

            case OrderStatus.Unknown:
            case OrderStatus.MarginCanceled:
            default:
                _logger.LogWarning($"CopyOrderService OnNewOrder: Необработанный статус {order.Status} для ордера {order.OrderId}");
                break;
        }
    }

    /// <summary>
    /// Обработка ордера в статусе Open - размещаем наш копируемый ордер
    /// </summary>
    private async Task HandleOpenOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleOpenOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Определяем тип ордера через CurrentWalletPositionService
        var orderSubType = await _currentWalletPositionService.GetOrderSubType(order);

        _logger.LogInformation($"CopyOrderService HandleOpenOrder: {order.Symbol} {order.Direction} SubType={orderSubType}");

        switch (orderSubType)
        {
            case OrderSubType.Open:
                await OpenNewPosition(order);
                break;

            case OrderSubType.Increase:
                await IncreasePosition(order);
                break;

            case OrderSubType.Decrease:
                await DecreasePosition(order);
                break;

            case OrderSubType.Close:
                await ClosePosition(order);
                break;

            default:
                _logger.LogWarning($"CopyOrderService HandleOpenOrder: Неизвестный OrderSubType {orderSubType} для ордера {order.OrderId}");
                break;
        }
    }

    /// <summary>
    /// Обработка ордера в статусе Filled - проверяем что наш ордер тоже исполнен
    /// </summary>
    private async Task HandleFilledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleFilledOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // TODO: Проверить статус нашего копируемого ордера
        // Если наш ордер не исполнен полностью - залогировать ошибку или предпринять действия
        // Можно получить наш ордер по OrderId трейдера из маппинга

        _logger.LogWarning($"HandleFilledOrder: TODO - проверка исполнения копируемого ордера для {order.OrderId}");
    }

    /// <summary>
    /// Обработка ордера в статусе Canceled - отменяем наш копируемый ордер
    /// </summary>
    private async Task HandleCanceledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleCanceledOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Публикуем событие закрытия копируемого ордера
        DataBusEvents.CopyOrderClosed?.Invoke((order, OrderStatus.Canceled));

        // TODO: Отменить наш копируемый ордер через OrdersProvider
        _logger.LogWarning($"HandleCanceledOrder: TODO - отмена копируемого ордера на бирже для {order.OrderId}");
    }

    /// <summary>
    /// Обработка ордера в статусе Rejected - отменяем наш копируемый ордер (если он был размещен)
    /// </summary>
    private async Task HandleRejectedOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleRejectedOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Публикуем событие закрытия копируемого ордера
        DataBusEvents.CopyOrderClosed?.Invoke((order, OrderStatus.Rejected));

        // TODO: Отменить наш копируемый ордер через OrdersProvider (если он был размещен)
        _logger.LogWarning($"HandleRejectedOrder: TODO - отмена копируемого ордера на бирже для {order.OrderId}");
    }

    /// <summary>
    /// Открытие НОВОЙ позиции (Open)
    /// Создаем копируемый ордер пропорционально балансу и сохраняем маппинг
    /// </summary>
    private async Task OpenNewPosition(OriginalOrder order)
    {
        _logger.LogInformation($"OpenNewPosition: {order.Symbol} {order.Direction}, Quantity={order.Quantity}");

        var copyOrder = await CreateCopyOrder(order, OrderSubType.Open);

        // Публикуем событие создания копируемого ордера
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);

        // Сохраняем маппинг между позициями
        var mapping = new Models.Models.PositionMapping
        {
            TraderWallet = order.Wallet,
            MyWallet = _myWallet,
            Symbol = order.Symbol,
            Direction = order.Direction,
            TraderQuantity = order.Quantity,
            MyQuantity = copyOrder.Quantity,
            PositionRatio = copyOrder.Quantity / order.Quantity,  // Сохраняем пропорцию!
            LastUpdate = DateTime.UtcNow
        };

        _positionMappingService.SaveOrUpdateMapping(mapping);

        _logger.LogInformation($"OpenNewPosition создан маппинг: {mapping}");

        // TODO: Разместить ордер на бирже через OrdersProvider
        _logger.LogInformation($"OpenNewPosition создан копируемый ордер: {order.OrderId} {copyOrder}");
    }

    /// <summary>
    /// Увеличение существующей позиции (Increase)
    /// Копируем ордер используя СОХРАНЕННУЮ пропорцию из маппинга
    /// </summary>
    private async Task IncreasePosition(OriginalOrder order)
    {
        _logger.LogInformation($"IncreasePosition: {order.Symbol} {order.Direction}, Quantity={order.Quantity}");

        var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

        if (mapping == null)
        {
            _logger.LogWarning($"IncreasePosition: Маппинг не найден для {order.Symbol} {order.Direction}. Открываем как новую позицию.");
            await OpenNewPosition(order);
            return;
        }

        // Используем СОХРАНЕННУЮ пропорцию, а не текущий баланс!
        var myIncreaseQuantity = order.Quantity * mapping.PositionRatio;

        // Округляем
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);
        myIncreaseQuantity = Math.Round(myIncreaseQuantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);

        // Создаем копируемый ордер
        var copyOrder = CreateCopyOrderFromQuantity(order, myIncreaseQuantity, mapping.PositionRatio, OrderSubType.Increase);

        // Публикуем событие создания копируемого ордера
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);

        // Обновляем маппинг
        mapping.TraderQuantity += order.Quantity;
        mapping.MyQuantity += myIncreaseQuantity;
        _positionMappingService.SaveOrUpdateMapping(mapping);

        _logger.LogInformation($"IncreasePosition: {order.OrderId} Увеличиваем на {myIncreaseQuantity}, новая позиция: {mapping.MyQuantity}");

        // TODO: Разместить ордер на увеличение через OrdersProvider
    }

    /// <summary>
    /// Частичное закрытие позиции (Decrease)
    /// Закрываем ту же ДОЛЮ от нашей позиции, что и трейдер
    /// </summary>
    private async Task DecreasePosition(OriginalOrder order)
    {
        _logger.LogInformation($"DecreasePosition: {order.Symbol} {order.Direction}, Quantity={order.Quantity}");

        var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

        if (mapping == null)
        {
            _logger.LogWarning($"DecreasePosition: Маппинг не найден для {order.Symbol} {order.Direction}");
            return;
        }

        // Рассчитываем какую долю закрывает трейдер
        decimal closeRatio;
        try
        {
            closeRatio = order.Quantity / mapping.TraderQuantity;
        }
        catch (Exception ex)
        {
            _logger.LogError($"CopyOrderService DecreasePosition: Ошибка при расчете closeRatio деление на 0 для {order.Symbol} {order.Direction}: {ex.Message} {order.Quantity} {mapping.TraderQuantity}");
            return;
        }

        // Закрываем ту же долю от ВАШЕЙ позиции
        var myCloseQuantity = mapping.MyQuantity * closeRatio;

        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);
        myCloseQuantity = Math.Round(myCloseQuantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);

        // Создаем копируемый ордер
        var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Decrease);

        // Публикуем событие создания копируемого ордера
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);

        // Обновляем маппинг
        mapping.TraderQuantity -= order.Quantity;
        mapping.MyQuantity -= myCloseQuantity;
        _positionMappingService.SaveOrUpdateMapping(mapping);

        _logger.LogInformation($"DecreasePosition {order.OrderId}: Закрываем {myCloseQuantity} (closeRatio={closeRatio:P2}), осталось: {mapping.MyQuantity}");

        // TODO: Разместить ордер на частичное закрытие через OrdersProvider
    }

    /// <summary>
    /// Полное закрытие позиции (Close)
    /// Закрываем ВСЮ нашу позицию независимо от текущих балансов
    /// </summary>
    private async Task ClosePosition(OriginalOrder order)
    {
        _logger.LogInformation($"ClosePosition: {order.Symbol} {order.Direction}, Quantity={order.Quantity}");

        var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

        if (mapping == null)
        {
            _logger.LogWarning($"ClosePosition: Маппинг не найден для {order.Symbol} {order.Direction}");
            return;
        }

        // Закрываем ВСЮ нашу позицию
        var myCloseQuantity = mapping.MyQuantity;

        // Создаем копируемый ордер
        var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Close);

        // Публикуем событие создания копируемого ордера
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);

        _logger.LogInformation($"ClosePosition: {order.OrderId} Закрываем полностью {myCloseQuantity}");

        // Удаляем маппинг (позиция полностью закрыта)
        _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

        // TODO: Разместить ордер на полное закрытие через OrdersProvider
    }

    private async Task<CopyOrderV2> CreateCopyOrder(OriginalOrder order, OrderSubType orderSubType)
    {
        // Получаем информацию о кошельке трейдера (копируемый кошелек)
        var traderWalletInfo = await _walletProvider.GetInfo(order.Wallet);

        // Получаем информацию о СВОЕМ кошельке
        var myWalletInfo = await _walletProvider.GetInfo(_myWallet, false);
        var myAccountValue = myWalletInfo.AccountVolume;

        // Вычисляем долю от капитала трейдера (какой % от счета он вкладывает)
        var orderRatio = order.VolumeUsd / traderWalletInfo.AccountVolume;

        // Вычисляем ВАШ объем позиции (та же доля от ВАШЕГО баланса)
        var myVolumeUsd = myAccountValue * orderRatio;

        // Вычисляем количество монет по той же цене
        var myQuantity = myVolumeUsd / order.Price;

        //TODO Подумать как получать Получить Leverage CurrentWalletPositionService.GetLEverage(Wallet, Coin)
        //Либо просто задается один раз для всех Coin или вообще это делать не тут а при размещении ордера

        // Генерируем уникальный временный ID для копируемого ордера
        // После размещения на бирже этот ID будет заменен на ID от биржи
        var tempOrderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var newCopyOrder = new CopyOrderV2()
        {
            OriginalOrder = order,
            OrderId = tempOrderId,
            OriginalOrderId = order.OrderId,
            OrderSubType = orderSubType,
            OrderRatio = orderRatio,
            MyPE = myAccountValue,
            AccountPE = traderWalletInfo.AccountVolume,
            Quantity = myQuantity,
        };

        await CorrectCopyOrder(newCopyOrder);

        return newCopyOrder;
    }

    /// <summary>
    /// Создать копируемый ордер на основе уже рассчитанного количества
    /// Используется для Increase/Decrease/Close позиций
    /// </summary>
    private CopyOrderV2 CreateCopyOrderFromQuantity(OriginalOrder order, decimal myQuantity, decimal positionRatio, OrderSubType orderSubType)
    {
        // Генерируем уникальный временный ID для копируемого ордера
        var tempOrderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var copyOrder = new CopyOrderV2()
        {
            OriginalOrder = order,
            OrderId = tempOrderId,
            OriginalOrderId = order.OrderId,
            OrderSubType = orderSubType,
            OrderRatio = positionRatio,  // Используем сохраненную пропорцию
            MyPE = 0,  // Не актуально для Increase/Decrease/Close
            AccountPE = 0,  // Не актуально для Increase/Decrease/Close
            Quantity = myQuantity,
        };

        return copyOrder;
    }

    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Quantity
    /// </summary>
    private async Task CorrectCopyOrder(CopyOrderV2 copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.OriginalOrder.Symbol);

        copyOrder.Quantity = Math.Round(copyOrder.Quantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);
    }

    private async Task<bool> ValidatePLacedOrder(CopyOrder copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.Symbol);

        if (copyOrder.VolumeUsd < exchangeInfo.MinNotionalValue.Value)
        {
            return false;
        }

        if (copyOrder.Quantity < exchangeInfo.MinTradeQuantity.Value)
        {
            return false;
        }

        return true;
    }
}
