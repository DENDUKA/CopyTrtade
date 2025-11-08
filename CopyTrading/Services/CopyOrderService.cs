using CopyTrading.DataEvents;
using CopyTrading.Extensions;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using CryptoExchange.Net.SharedApis;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService(
    IWalletInfoProvider _walletProvider,
    IExchangeInfoProvider _exchangeInfoProvider,
    CurrentWalletPositionService _currentWalletPositionService,
    PositionMappingService _positionMappingService,
    CopyOrderResultService _resultService,
    FillsOrderService _fillsOrderService,
    ILogger<CopyOrderService> _logger)
{
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public async Task OnNewOrders(OriginalOrder[] orders)
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
                    HandleFilledOrder(order);
                    break;

                case OrderStatus.Canceled:
                    HandleCanceledOrder(order);
                    break;

                case OrderStatus.Rejected:
                    HandleRejectedOrder(order);
                    break;

                case OrderStatus.Triggered:
                    // Triggered ордер уже размещен на бирже, ждем когда станет Open или Filled
                    _logger.LogInformation($"CopyOrderService: Ордер {order.OrderId} триггернулся, ждем исполнения");
                    // Для Triggered не записываем результат, так как это промежуточный статус
                    break;

                case OrderStatus.Unknown:
                    {
                        var errorMsg = "Неизвестный статус ордера (Unknown)";
                        _logger.LogWarning($"CopyOrderService OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }

                case OrderStatus.MarginCanceled:
                    {
                        var errorMsg = "Ордер отменен по марже (MarginCanceled)";
                        _logger.LogWarning($"CopyOrderService OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }

                default:
                    {
                        var errorMsg = $"Необработанный статус ордера: {order.Status}";
                        _logger.LogWarning($"CopyOrderService OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }
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

        // Определяем тип ордера через CurrentWalletPositionService
        var orderSubType = _currentWalletPositionService.GetOrderSubType(order);

        _logger.LogInformation($"CopyOrderService HandleOpenOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction} SubType={orderSubType}");

        switch (orderSubType)
        {
            case OrderSubType.Open:
                _logger.LogInformation($"HandleOpenOrder: Вызываем OpenNewPosition для {order.OrderId}");
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
                ClosePosition(order);
                _logger.LogInformation($"HandleOpenOrder: ClosePosition завершен для {order.OrderId}");
                break;

            case OrderSubType.None:
                {
                    var errorMsg = "OrderSubType.None - невозможно определить тип ордера";
                    _logger.LogError($"CopyOrderService HandleOpenOrder: OrderId={order.OrderId} {errorMsg} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                    SaveFailureResult(order, errorMsg);
                    break;
                }

            default:
                {
                    var errorMsg = $"Неизвестный OrderSubType {orderSubType}";
                    _logger.LogError($"CopyOrderService HandleOpenOrder: OrderId={order.OrderId} {errorMsg} - CopyOrder НЕ БУДЕТ СОЗДАН!");
                    SaveFailureResult(order, errorMsg);
                    break;
                }
        }

        _logger.LogInformation($"HandleOpenOrder END: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");
    }

    /// <summary>
    /// Обработка ордера в статусе Filled - проверяем что наш ордер тоже исполнен
    /// </summary>
    private Task HandleFilledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleFilledOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleFilledOrder)))
            return Task.CompletedTask;

        DataBusEvents.CopyOrderFilled?.Invoke((order, OrderStatus.Filled));

        // TODO: Проверить статус нашего копируемого ордера
        // Если наш ордер не исполнен полностью - залогировать ошибку или предпринять действия
        // Можно получить наш ордер по OrderId трейдера из маппинга

        _logger.LogWarning($"HandleFilledOrder: TODO - проверка исполнения копируемого ордера для {order.OrderId}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработка ордера в статусе Canceled - отменяем наш копируемый ордер
    /// </summary>
    private Task HandleCanceledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleCanceledOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleCanceledOrder)))
            return Task.CompletedTask;

        // Публикуем событие закрытия копируемого ордера
        DataBusEvents.CopyOrderClosed?.Invoke((order, OrderStatus.Canceled));

        // TODO: Отменить наш копируемый ордер через OrdersProvider
        _logger.LogWarning($"HandleCanceledOrder: TODO - отмена копируемого ордера на бирже для {order.OrderId}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработка ордера в статусе Rejected - отменяем наш копируемый ордер (если он был размещен)
    /// </summary>
    private Task HandleRejectedOrder(OriginalOrder order)
    {
        _logger.LogInformation($"HandleRejectedOrder: {order.Symbol} {order.Direction}, OrderId={order.OrderId}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleRejectedOrder)))
            return Task.CompletedTask;

        // Публикуем событие закрытия копируемого ордера
        DataBusEvents.CopyOrderClosed?.Invoke((order, OrderStatus.Rejected));

        // TODO: Отменить наш копируемый ордер через OrdersProvider (если он был размещен)
        _logger.LogWarning($"HandleRejectedOrder: TODO - отмена копируемого ордера на бирже для {order.OrderId}");

        return Task.CompletedTask;
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
            var copyOrder = await CreateCopyOrder(order, OrderSubType.Open);

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            SaveCopyOrderMapping(order, copyOrder);

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"OpenNewPosition ОШИБКА при создании копируемого ордера для {order.OrderId}");
            SaveFailureResult(order, ex.Message);
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

            // Если маппинг не найден - обрабатываем отдельно
            if (mapping == null)
            {
                await HandleMissingMappingForIncrease(order);
                return;
            }

            // Валидация маппинга
            if (mapping.PositionRatio == 0)
            {
                var errorMsg = "mapping.PositionRatio = 0 - позиция была открыта с Quantity=0";
                _logger.LogError($"IncreasePosition: OrderId={order.OrderId} {errorMsg}. CopyOrder НЕ БУДЕТ СОЗДАН!");
                SaveFailureResult(order, errorMsg);
                return;
            }

            // Получаем exchangeInfo для округления
            var exchangeInfo = await TryGetExchangeInfo(order);
            if (exchangeInfo == null) return;

            // Используем СОХРАНЕННУЮ пропорцию, а не текущий баланс!
            var myIncreaseQuantity = order.Quantity * mapping.PositionRatio;
            myIncreaseQuantity = RoundQuantity(myIncreaseQuantity, exchangeInfo);

            // Создаем копируемый ордер
            _logger.LogInformation($"IncreasePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myIncreaseQuantity, mapping.PositionRatio, OrderSubType.Increase);
            _logger.LogInformation($"IncreasePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие и сохраняем результат
            PublishCopyOrderCreated(order, copyOrder);

            // Обновляем только MyQuantity в маппинге (TraderQuantity берем из snapshot, не храним)
            mapping.MyQuantity += myIncreaseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);

            _logger.LogInformation($"IncreasePosition SUCCESS: {order.OrderId} Увеличиваем на {myIncreaseQuantity}, новая позиция: {mapping.MyQuantity}");

            SaveSuccessResult(order, copyOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"IncreasePosition ОШИБКА при увеличении позиции для {order.OrderId}");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    private async Task<decimal?> GetTraderQuantityBeforeOrder(Wallet wallet, string symbol, long orderId, decimal orderQuantity)
    {
        var snapshot = await _currentWalletPositionService.GetSnapshot(wallet);
        var traderPosition = snapshot.Positions.FirstOrDefault(p => p.Symbol == symbol);

        if (traderPosition == null)
        {
            _logger.LogError($"GetTraderQuantityBeforeOrderAsync: OrderId={orderId} Позиция трейдера не найдена в snapshot для символа {symbol}");
            return null;
        }

        var orderFills = _fillsOrderService.GetOrderFillsByOrderId(orderId);

        if (orderFills is null)
        {
            return traderPosition.Quantity;
        }

        var fillQuantityInOrder = orderFills.Trades.Sum(x => x.Quantity);

        var traderTotalQuantity = Math.Abs(traderPosition.Quantity);
        _logger.LogInformation($"GetTraderQuantityBeforeOrderAsync: OrderId={orderId} Итоговая позиция трейдера из snapshot: {traderTotalQuantity}");

        // Вычисляем базовую линию - позицию трейдера ДО increase
        var traderQuantityBeforeIncrease = traderTotalQuantity - Math.Abs(fillQuantityInOrder);
        _logger.LogInformation($"GetTraderQuantityBeforeOrderAsync: OrderId={orderId} Позиция трейдера ДО increase: {traderQuantityBeforeIncrease}, увеличение на: {orderQuantity}");

        return traderQuantityBeforeIncrease;
    }

    #region IncreasePosition Helper Methods

    /// <summary>
    /// Обработать случай отсутствия маппинга при увеличении позиции
    /// Создаем синтетический ордер на открытие и устанавливаем базовую линию
    /// </summary>
    private async Task HandleMissingMappingForIncrease(OriginalOrder order)
    {
        _logger.LogWarning($"IncreasePosition: Маппинг не найден для {order.OrderId} {order.Symbol} {order.Direction}. Синхронизируем с текущей позицией трейдера.");

        // Получаем базовую линию - позицию трейдера ДО increase
        var traderQuantityBeforeIncrease = await GetTraderQuantityBeforeOrder(order.Wallet, order.Symbol, order.OrderId, order.Quantity);

        if (traderQuantityBeforeIncrease is null || traderQuantityBeforeIncrease < 0)
        {
            var errorMsg = "Позиция трейдера не найдена в snapshot или некорректна";
            _logger.LogError($"IncreasePosition: OrderId={order.OrderId} {errorMsg}. CopyOrder НЕ БУДЕТ СОЗДАН!");
            SaveFailureResult(order, errorMsg);
            return;
        }

        // Создаем "синтетический" ордер на открытие позиции
        var syntheticOrder = CreateSyntheticOrderForIncrease(order);

        _logger.LogInformation($"IncreasePosition: OrderId={order.OrderId} Открываем синхронизированную позицию с количеством {syntheticOrder.Quantity}");
        await OpenNewPosition(syntheticOrder);

        // Устанавливаем базовую линию - позицию трейдера ДО increase
        SetBaselineForNewMapping(order, traderQuantityBeforeIncrease.Value);
    }

    /// <summary>
    /// Создать синтетический ордер для открытия позиции при увеличении
    /// </summary>
    private static OriginalOrder CreateSyntheticOrderForIncrease(OriginalOrder order)
    {
        return new OriginalOrder
        {
            OrderId = order.OrderId,
            Wallet = order.Wallet,
            Symbol = order.Symbol,
            Direction = order.Direction,
            Quantity = order.Quantity,  // Только величина увеличения, не вся позиция!
            Price = order.Price,
            Leverage = order.Leverage,
            SubType = OrderSubType.Open,  // Для нас это открытие новой позиции
            Status = order.Status,
            Time = order.Time
        };
    }

    /// <summary>
    /// Установить базовую линию для созданного маппинга
    /// </summary>
    private void SetBaselineForNewMapping(OriginalOrder order, decimal traderQuantityAtEntry)
    {
        var createdMapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction);
        if (createdMapping != null)
        {
            createdMapping.TraderQuantityAtEntry = traderQuantityAtEntry;
            _positionMappingService.SaveOrUpdateMapping(createdMapping);
            _logger.LogInformation($"IncreasePosition: OrderId={order.OrderId} Установлена базовая линия TraderQuantityAtEntry={createdMapping.TraderQuantityAtEntry}");
        }
    }

    #endregion

    #region DecreasePosition Helper Methods

    /// <summary>
    /// Получить маппинг для decrease (используя противоположное direction)
    /// </summary>
    private PositionMapping? GetMappingForDecrease(OriginalOrder order)
    {
        return _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());
    }

    /// <summary>
    /// Получить позицию трейдера из snapshot
    /// </summary>
    private async Task<decimal?> TryGetTraderPosition(OriginalOrder order)
    {
        _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Получаем snapshot для определения реальной позиции трейдера");
        var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
        var traderPosition = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);

        if (traderPosition == null)
        {
            var errorMsg = "Позиция трейдера не найдена в snapshot";
            _logger.LogError($"DecreasePosition: OrderId={order.OrderId} {errorMsg} для {order.Symbol} - CopyOrder НЕ БУДЕТ СОЗДАН!");
            SaveFailureResult(order, errorMsg);
            return null;
        }

        var actualTraderQuantity = Math.Abs(traderPosition.Quantity);
        _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Реальная позиция трейдера из snapshot: {actualTraderQuantity}");
        return actualTraderQuantity;
    }

    /// <summary>
    /// Расчет количества для закрытия позиции
    /// </summary>
    private decimal CalculateDecreaseQuantity(OriginalOrder order, PositionMapping mapping, decimal actualTraderQuantity)
    {
        _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Базовая линия: {mapping.TraderQuantityAtEntry}");

        // Проверяем: трейдер ушел ниже базовой линии?
        if (actualTraderQuantity - order.Quantity <= mapping.TraderQuantityAtEntry)
        {
            // Трейдер закрыл всё что было после нашего входа (и даже больше) - закрываем ВСЮ позицию
            _logger.LogWarning($"DecreasePosition: OrderId={order.OrderId} Трейдер ушел ниже базовой линии ({actualTraderQuantity} <= {mapping.TraderQuantityAtEntry}). Закрываем ВСЮ позицию {mapping.MyQuantity}!");
            return mapping.MyQuantity;
        }

        // Трейдер выше базовой линии - закрываем пропорционально от "позиции над базовой"
        var traderAboveBaseline = actualTraderQuantity - mapping.TraderQuantityAtEntry;
        var closeRatio = order.Quantity / traderAboveBaseline;
        _logger.LogInformation($"DecreasePosition: OrderId={order.OrderId} Трейдер закрывает {closeRatio:P2} от позиции над базовой ({order.Quantity} из {traderAboveBaseline})");

        return mapping.MyQuantity * closeRatio;
    }

    /// <summary>
    /// Обновить или удалить маппинг после decrease
    /// </summary>
    private void UpdateOrDeleteMappingAfterDecrease(OriginalOrder order, PositionMapping mapping, decimal myCloseQuantity)
    {
        // Проверяем: закрыли всю позицию или частично?
        if (myCloseQuantity >= mapping.MyQuantity)
        {
            // Закрыли ВСЁ - удаляем маппинг
            _logger.LogInformation($"DecreasePosition SUCCESS: {order.OrderId} Закрываем ВСЮ позицию {myCloseQuantity}. Удаляем маппинг.");
            _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());
        }
        else
        {
            // Частично закрыли - обновляем маппинг
            mapping.MyQuantity -= myCloseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);
            _logger.LogInformation($"DecreasePosition SUCCESS: {order.OrderId} Закрываем {myCloseQuantity}, осталось: {mapping.MyQuantity}");
        }
    }

    #endregion

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
            // Получаем маппинг (с противоположным direction)
            var mapping = GetMappingForDecrease(order);
            if (mapping == null)
            {
                var errorMsg = "Невозможно скопировать decrease - у нас нет открытой позиции (маппинг не найден)";
                _logger.LogError($"DecreasePosition: OrderId={order.OrderId} {errorMsg}. CopyOrder НЕ БУДЕТ СОЗДАН!");
                SaveFailureResult(order, errorMsg);
                return;
            }

            // Валидация маппинга
            if (mapping.MyQuantity == 0)
            {
                var errorMsg = "mapping.MyQuantity = 0 - позиция не была открыта или уже закрыта";
                _logger.LogError($"DecreasePosition: OrderId={order.OrderId} {errorMsg}. CopyOrder НЕ БУДЕТ СОЗДАН!");
                SaveFailureResult(order, errorMsg);
                return;
            }

            // Получаем позицию трейдера из snapshot
            var actualTraderQuantity = await TryGetTraderPosition(order);
            if (actualTraderQuantity is null) return;

            // Рассчитываем количество для закрытия
            var myCloseQuantity = CalculateDecreaseQuantity(order, mapping, actualTraderQuantity.Value);

            // Получаем exchangeInfo и округляем
            var exchangeInfo = await TryGetExchangeInfo(order);
            if (exchangeInfo == null) return;

            myCloseQuantity = RoundQuantity(myCloseQuantity, exchangeInfo);

            // Создаем копируемый ордер
            _logger.LogInformation($"DecreasePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Decrease);
            _logger.LogInformation($"DecreasePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            // Обновляем или удаляем маппинг
            UpdateOrDeleteMappingAfterDecrease(order, mapping, myCloseQuantity);

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"DecreasePosition ОШИБКА при частичном закрытии позиции для {order.OrderId}");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Полное закрытие позиции (Close)
    /// Закрываем ВСЮ нашу позицию независимо от текущих балансов
    /// </summary>
    private Task ClosePosition(OriginalOrder order)
    {
        _logger.LogInformation($"ClosePosition START: {order.Symbol} {order.Direction}, Quantity={order.Quantity}, OrderId={order.OrderId}");

        try
        {
            // FIX: Close ордер имеет противоположное направление (Short закрывает Long),
            // но маппинг создан с направлением позиции (Long), поэтому используем Opposite()
            var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());

            if (mapping == null)
            {
                var errorMsg = "Невозможно скопировать close - у нас нет открытой позиции (маппинг не найден)";
                _logger.LogError($"ClosePosition: OrderId={order.OrderId} {errorMsg}. CopyOrder НЕ БУДЕТ СОЗДАН!");
                SaveFailureResult(order, errorMsg);
                return Task.CompletedTask;
            }

            // Закрываем ВСЮ нашу позицию
            var myCloseQuantity = mapping.MyQuantity;

            // Проверяем что у нас есть открытая позиция
            if (myCloseQuantity == 0)
            {
                var errorMsg = "mapping.MyQuantity = 0 - позиция не была открыта";
                _logger.LogWarning($"ClosePosition: OrderId={order.OrderId} {errorMsg}. Удаляем маппинг без создания CopyOrder.");
                // FIX: используем Opposite() для корректного удаления маппинга
                _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());
                SaveFailureResult(order, errorMsg);
                return Task.CompletedTask;
            }

            // Создаем копируемый ордер
            _logger.LogInformation($"ClosePosition: Создаем CopyOrder для {order.OrderId}");
            var copyOrder = CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Close);
            _logger.LogInformation($"ClosePosition: CopyOrder создан для {order.OrderId}, CopyOrderId={copyOrder.OrderId}");

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            _logger.LogInformation($"ClosePosition SUCCESS: {order.OrderId} Закрываем полностью {myCloseQuantity}");

            // Удаляем маппинг (позиция полностью закрыта)
            // FIX: используем Opposite() для корректного удаления маппинга
            _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);

            // TODO: Разместить ордер на полное закрытие через OrdersProvider

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ClosePosition ОШИБКА при полном закрытии позиции для {order.OrderId}");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    private async Task<CopyOrderV2> CreateCopyOrder(OriginalOrder order, OrderSubType orderSubType)
    {
		_logger.LogInformation($"OpenNewPosition: Вызываем CreateCopyOrder для {order.OrderId}");

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

		_logger.LogInformation($"OpenNewPosition: CreateCopyOrder завершен для {order.OrderId}, CopyOrderId={newCopyOrder.OrderId}");

		return newCopyOrder;
    }

    /// <summary>
    /// Создать копируемый ордер на основе уже рассчитанного количества
    /// Используется для Increase/Decrease/Close позиций
    /// </summary>
    private static CopyOrderV2 CreateCopyOrderFromQuantity(OriginalOrder order, decimal myQuantity, decimal positionRatio, OrderSubType orderSubType)
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

    #region Helper Methods

    /// <summary>
    /// Проверить что ордер был успешно скопирован ранее
    /// </summary>
    /// <returns>true если ордер был скопирован, false если нет</returns>
    private bool EnsureOrderWasCopied(OriginalOrder order, string methodName)
    {
        var existingResult = _resultService.GetResult(order.OrderId.ToString());
        if (existingResult == null)
        {
            var errorMsg = "Ордер не был скопирован - отсутствует открывающий ордер";
            _logger.LogWarning($"CopyOrderService {methodName}: OrderId={order.OrderId} {errorMsg}");
            SaveFailureResult(order, errorMsg);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Получить ExchangeInfo с обработкой ошибок
    /// </summary>
    private async Task<SharedFuturesSymbol?> TryGetExchangeInfo(OriginalOrder order)
    {
        _logger.LogInformation($"TryGetExchangeInfo: OrderId={order.OrderId} Получаем ExchangeInfo для {order.Symbol}");
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        if (exchangeInfo == null)
        {
            var errorMsg = $"Не удалось получить ExchangeInfo для {order.Symbol}";
            _logger.LogError($"TryGetExchangeInfo: OrderId={order.OrderId} {errorMsg}");
            SaveFailureResult(order, errorMsg);
        }
        else
        {
            _logger.LogInformation($"TryGetExchangeInfo: OrderId={order.OrderId} ExchangeInfo получен, QuantityDecimals={exchangeInfo.QuantityDecimals}");
        }

        return exchangeInfo;
    }

    /// <summary>
    /// Округлить количество согласно правилам биржи
    /// </summary>
    private static decimal RoundQuantity(decimal quantity, SharedFuturesSymbol exchangeInfo)
    {
        return Math.Round(quantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);
    }

    /// <summary>
    /// Опубликовать событие создания копируемого ордера
    /// </summary>
    private void PublishCopyOrderCreated(OriginalOrder originalOrder, CopyOrderV2 copyOrder)
    {
        _logger.LogInformation($"PublishCopyOrderCreated: Публикуем CopyOrderCreated для OrderId={originalOrder.OrderId}");
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
        _logger.LogInformation($"PublishCopyOrderCreated: CopyOrderCreated опубликован для OrderId={originalOrder.OrderId}");
    }

    private void SaveCopyOrderMapping(OriginalOrder order, CopyOrderV2 copyOrder)
    {
        // Сохраняем маппинг между позициями
        var mapping = new PositionMapping
        {
            TraderWallet = order.Wallet,
            MyWallet = _myWallet,
            Symbol = order.Symbol,
            Direction = order.Direction,
            MyQuantity = copyOrder.Quantity,
            PositionRatio = copyOrder.Quantity / order.Quantity,  // Сохраняем пропорцию!
            TraderQuantityAtEntry = 0,  // При обычном Open трейдер открывает позицию с нуля
            LastUpdate = DateTime.UtcNow
        };

        _positionMappingService.SaveOrUpdateMapping(mapping);

        _logger.LogInformation($"OpenNewPosition создан маппинг: {mapping}");
    }

    /// <summary>
    /// Сохранить успешный результат копирования
    /// </summary>
    private void SaveSuccessResult(OriginalOrder order, CopyOrderV2 copyOrder)
    {
        _resultService.SaveSuccess(order.OrderId.ToString(), order.Wallet, order.Symbol, copyOrder.OrderId.ToString());
    }

    /// <summary>
    /// Сохранить неуспешный результат копирования
    /// </summary>
    private void SaveFailureResult(OriginalOrder order, string errorMessage)
    {
        _resultService.SaveFailure(order.OrderId.ToString(), order.Wallet, order.Symbol, errorMessage);
    }

    #endregion
}
