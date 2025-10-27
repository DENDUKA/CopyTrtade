using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
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

    private async void OnNewOrders(OriginalOrder[] orders)
    {
        try
        {
            foreach (var order in orders)
            {
                await OnNewOrder(order);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке массива ордеров в CopyOrderService");
        }
    }

    private async Task OnNewOrder(OriginalOrder order)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при обработке ордера {order.OrderId} в CopyOrderService");
        }
    }

    /// <summary>
    /// Обработка ордера в статусе Open - размещаем наш копируемый ордер
    /// </summary>
    private async Task HandleOpenOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleOpenOrder START: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // КРИТИЧЕСКИ ВАЖНО: Сначала проверяем наличие маппинга
        // Если маппинга нет - значит мы еще не открывали позицию для этого символа/направления,
        // даже если у трейдера позиция уже открыта
        var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

        if (mapping == null)
        {
            // Маппинга нет - это наша первая позиция для этого символа/направления
            _logger.LogInformation($"HandleOpenOrder: Маппинг не найден для {order.OrderId} - открываем как новую позицию (Open)");
            await OpenNewPosition(order);
            _logger.LogInformation($"HandleOpenOrder: OpenNewPosition завершен для {order.OrderId}");
            _logger.LogInformation($"HandleOpenOrder END: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");
            return;
        }

        // Маппинг существует - определяем тип ордера через CurrentWalletPositionService
        _logger.LogInformation($"HandleOpenOrder: Маппинг найден для {order.OrderId}, определяем OrderSubType через CurrentWalletPositionService");
        var orderSubType = await _currentWalletPositionService.GetOrderSubType(order);

        _logger.LogInformation($"CopyOrderService HandleOpenOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction} SubType={orderSubType}");

        switch (orderSubType)
        {
            case OrderSubType.Open:
                // Это не должно происходить, так как маппинг уже существует
                _logger.LogWarning($"HandleOpenOrder: OrderSubType.Open при существующем маппинге для {order.OrderId} - открываем как новую позицию");
                await OpenNewPosition(order);
                _logger.LogInformation($"HandleOpenOrder: OpenNewPosition завершен для {order.OrderId}");
                break;

            case OrderSubType.Increase:
                _logger.LogInformation($"HandleOpenOrder: Вызываем IncreasePosition для {order.OrderId}");
                await IncreasePosition(order);
                _logger.LogInformation($"HandleOpenOrder: IncreasePosition завершен для {order.OrderId}");
                break;

            case OrderSubType.Decrease:
                _logger.LogInformation($"HandleOpenOrder: Вызываем DecreasePosition для {order.OrderId}");
                await DecreasePosition(order);
                _logger.LogInformation($"HandleOpenOrder: DecreasePosition завершен для {order.OrderId}");
                break;

            case OrderSubType.Close:
                _logger.LogInformation($"HandleOpenOrder: Вызываем ClosePosition для {order.OrderId}");
                await ClosePosition(order);
                _logger.LogInformation($"HandleOpenOrder: ClosePosition завершен для {order.OrderId}");
                break;

            case OrderSubType.None:
                _logger.LogWarning($"CopyOrderService HandleOpenOrder: OrderSubType.None для ордера {order.OrderId} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                break;

            default:
                _logger.LogWarning($"CopyOrderService HandleOpenOrder: Неизвестный OrderSubType {orderSubType} для ордера {order.OrderId} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                break;
        }

        _logger.LogInformation($"HandleOpenOrder END: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");
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
        _logger.LogInformation($"OpenNewPosition START: {order.Symbol} {order.Direction}, Quantity={order.Quantity}, OrderId={order.OrderId}");

        try
        {
            _logger.LogInformation($"OpenNewPosition: Вызываем CreateCopyOrder для {order.OrderId}");
            var copyOrder = await CreateCopyOrder(order, OrderSubType.Open);
            _logger.LogInformation($"OpenNewPosition: CreateCopyOrder завершен для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие создания копируемого ордера
            _logger.LogInformation($"OpenNewPosition: Публикуем CopyOrderCreated для {order.OrderId}");
            DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
            _logger.LogInformation($"OpenNewPosition: CopyOrderCreated опубликован для {order.OrderId}");

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
            _logger.LogInformation($"OpenNewPosition SUCCESS: создан копируемый ордер для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"OpenNewPosition ОШИБКА при создании копируемого ордера для {order.OrderId}");
            throw;
        }
    }

    /// <summary>
    /// Увеличение существующей позиции (Increase)
    /// Копируем ордер используя СОХРАНЕННУЮ пропорцию из маппинга
    /// ВАЖНО: TraderQuantity не обновляем, берем из snapshot при необходимости
    /// </summary>
    private async Task IncreasePosition(OriginalOrder order)
    {
        _logger.LogInformation($"IncreasePosition START: {order.Symbol} {order.Direction}, Quantity={order.Quantity}, OrderId={order.OrderId}");

        try
        {
            var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

            if (mapping == null)
            {
                _logger.LogWarning($"IncreasePosition: Маппинг не найден для {order.OrderId} {order.Symbol} {order.Direction}. Открываем как новую позицию.");
                await OpenNewPosition(order);
                return;
            }

            // Проверяем что у нас есть открытая позиция с валидной пропорцией
            if (mapping.PositionRatio == 0)
            {
                _logger.LogWarning($"IncreasePosition: OrderId={order.OrderId} mapping.PositionRatio = 0 для {order.Symbol} {order.Direction} - позиция была открыта с Quantity=0. CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            // Используем СОХРАНЕННУЮ пропорцию, а не текущий баланс!
            var myIncreaseQuantity = order.Quantity * mapping.PositionRatio;

            // Округляем
            _logger.LogInformation($"IncreasePosition: OrderId={order.OrderId} Получаем ExchangeInfo для {order.Symbol}");
            var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

            if (exchangeInfo == null)
            {
                _logger.LogError($"IncreasePosition: OrderId={order.OrderId} Не удалось получить ExchangeInfo для {order.Symbol} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            _logger.LogInformation($"IncreasePosition: OrderId={order.OrderId} ExchangeInfo получен, QuantityDecimals={exchangeInfo.QuantityDecimals}");
            myIncreaseQuantity = Math.Round(myIncreaseQuantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);

            // Создаем копируемый ордер
            _logger.LogInformation($"IncreasePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myIncreaseQuantity, mapping.PositionRatio, OrderSubType.Increase);
            _logger.LogInformation($"IncreasePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие создания копируемого ордера
            _logger.LogInformation($"IncreasePosition: Публикуем CopyOrderCreated для {order.OrderId}");
            DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
            _logger.LogInformation($"IncreasePosition: CopyOrderCreated опубликован для {order.OrderId}");

            // Обновляем только MyQuantity в маппинге (TraderQuantity берем из snapshot, не храним)
            mapping.MyQuantity += myIncreaseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);

            _logger.LogInformation($"IncreasePosition SUCCESS: {order.OrderId} Увеличиваем на {myIncreaseQuantity}, новая позиция: {mapping.MyQuantity}");

            // TODO: Разместить ордер на увеличение через OrdersProvider
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"IncreasePosition ОШИБКА при увеличении позиции для {order.OrderId}");
            throw;
        }
    }

    /// <summary>
    /// Частичное закрытие позиции (Decrease)
    /// Закрываем ту же ДОЛЮ от нашей позиции, что и трейдер
    /// ВАЖНО: Используем snapshot как единственный источник истины для позиции трейдера
    /// </summary>
    private async Task DecreasePosition(OriginalOrder order)
    {
        _logger.LogInformation($"DecreasePosition START: {order.Symbol} {order.Direction}, Quantity={order.Quantity}, OrderId={order.OrderId}");

        try
        {
            var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

            if (mapping == null)
            {
                _logger.LogWarning($"DecreasePosition: Маппинг не найден для {order.OrderId} {order.Symbol} {order.Direction} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            // Получаем РЕАЛЬНУЮ позицию трейдера из snapshot (единственный источник истины!)
            _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Получаем snapshot для определения реальной позиции трейдера");
            var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
            var traderPosition = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);

            if (traderPosition == null)
            {
                _logger.LogError($"DecreasePosition: OrderId{order.OrderId} Позиция трейдера не найдена в snapshot для {order.Symbol} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            // Реальное количество позиции трейдера (всегда положительное)
            var actualTraderQuantity = Math.Abs(traderPosition.Quantity);
            _logger.LogInformation($"DecreasePosition: OrderId{order.OrderId} Реальная позиция трейдера из snapshot: {actualTraderQuantity}");

            if (actualTraderQuantity == 0)
            {
                _logger.LogWarning($"DecreasePosition: Реальная позиция трейдера = 0 для {order.Symbol} - возможно уже закрыта. CopyOrder НЕ БУДЕТ СОЗДАН! OrderId{order.OrderId}");
                return;
            }

            // Проверяем что у нас есть открытая позиция
            if (mapping.MyQuantity == 0)
            {
                _logger.LogWarning($"DecreasePosition: OrderId={order.OrderId} mapping.MyQuantity = 0 для {order.Symbol} {order.Direction} - позиция не была открыта или уже закрыта. CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            // Рассчитываем какую долю закрывает трейдер (теперь деление на 0 невозможно!)
            var closeRatio = order.Quantity / actualTraderQuantity;
            _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Трейдер закрывает {closeRatio:P2} позиции ({order.Quantity} из {actualTraderQuantity})");

            // Закрываем ту же долю от НАШЕЙ позиции
            var myCloseQuantity = mapping.MyQuantity * closeRatio;

            _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Получаем ExchangeInfo для {order.Symbol}");
            var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

            if (exchangeInfo == null)
            {
                _logger.LogError($"DecreasePosition: OrderId={order.OrderId} Не удалось получить ExchangeInfo для {order.Symbol} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} ExchangeInfo получен, QuantityDecimals={exchangeInfo.QuantityDecimals}");
            myCloseQuantity = Math.Round(myCloseQuantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);

            // Создаем копируемый ордер
            _logger.LogInformation($"DecreasePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Decrease);
            _logger.LogInformation($"DecreasePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие создания копируемого ордера
            _logger.LogInformation($"DecreasePosition: Публикуем CopyOrderCreated для {order.OrderId}");
            DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
            _logger.LogInformation($"DecreasePosition: CopyOrderCreated опубликован для {order.OrderId}");

            // Обновляем только MyQuantity в маппинге (TraderQuantity берем из snapshot, не храним)
            mapping.MyQuantity -= myCloseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);

            _logger.LogInformation($"DecreasePosition SUCCESS: {order.OrderId} Закрываем {myCloseQuantity} (closeRatio={closeRatio:P2}), осталось: {mapping.MyQuantity}");

            // TODO: Разместить ордер на частичное закрытие через OrdersProvider
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"DecreasePosition ОШИБКА при частичном закрытии позиции для {order.OrderId}");
            throw;
        }
    }

    /// <summary>
    /// Полное закрытие позиции (Close)
    /// Закрываем ВСЮ нашу позицию независимо от текущих балансов
    /// </summary>
    private async Task ClosePosition(OriginalOrder order)
    {
        _logger.LogInformation($"ClosePosition START: {order.Symbol} {order.Direction}, Quantity={order.Quantity}, OrderId={order.OrderId}");

        try
        {
            var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

            if (mapping == null)
            {
                _logger.LogWarning($"ClosePosition: Маппинг не найден для {order.OrderId} {order.Symbol} {order.Direction} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                return;
            }

            // Закрываем ВСЮ нашу позицию
            var myCloseQuantity = mapping.MyQuantity;

            // Проверяем что у нас есть открытая позиция
            if (myCloseQuantity == 0)
            {
                _logger.LogWarning($"ClosePosition: OrderId={order.OrderId} mapping.MyQuantity = 0 для {order.Symbol} {order.Direction} - позиция не была открыта. Удаляем маппинг без создания CopyOrder.");
                _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);
                return;
            }

            // Создаем копируемый ордер
            _logger.LogInformation($"ClosePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Close);
            _logger.LogInformation($"ClosePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие создания копируемого ордера
            _logger.LogInformation($"ClosePosition: Публикуем CopyOrderCreated для {order.OrderId}");
            DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
            _logger.LogInformation($"ClosePosition: CopyOrderCreated опубликован для {order.OrderId}");

            _logger.LogInformation($"ClosePosition SUCCESS: {order.OrderId} Закрываем полностью {myCloseQuantity}");

            // Удаляем маппинг (позиция полностью закрыта)
            _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);

            // TODO: Разместить ордер на полное закрытие через OrdersProvider
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ClosePosition ОШИБКА при полном закрытии позиции для {order.OrderId}");
            throw;
        }
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
        _logger.LogInformation($"CorrectCopyOrder: OrderId={copyOrder.OriginalOrderId} Получаем ExchangeInfo для {copyOrder.OriginalOrder.Symbol}");
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.OriginalOrder.Symbol);

        if (exchangeInfo == null)
        {
            _logger.LogError($"CorrectCopyOrder: OrderId={copyOrder.OriginalOrderId} Не удалось получить ExchangeInfo для {copyOrder.OriginalOrder.Symbol} - используем Quantity без округления!");
            return;
        }

        _logger.LogInformation($"CorrectCopyOrder: OrderId={copyOrder.OriginalOrderId} ExchangeInfo получен, QuantityDecimals={exchangeInfo.QuantityDecimals}");
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
