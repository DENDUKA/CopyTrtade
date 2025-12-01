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
    ICurrentWalletPositionService _currentWalletPositionService,
    IPositionMappingService _positionMappingService,
    ICopyOrderResultService _resultService,
    IFillsOrderService _fillsOrderService,
    ICopyTradeWalletSettingsService _walletSettingsService,
    IOrderService _orderService,
    ILogger<CopyOrderService> _logger) : ICopyOrderService
{
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public async Task OnNewOrders(OriginalOrder[] orders)
    {
        ArgumentNullException.ThrowIfNull(orders, nameof(orders));

        try
        {
            foreach (var order in orders)
            {
                await OnNewOrder(order);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CopyOrderService.OnNewOrders: Ошибка при обработке массива ордеров");
        }
    }

    private async Task OnNewOrder(OriginalOrder order)
    {
        try
        {
            _logger.LogInformation($"CopyOrderService.OnNewOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction} Status={order.Status}");

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
                    _logger.LogInformation($"CopyOrderService.OnNewOrder: OrderId={order.OrderId} Triggered, ждем исполнения");
                    // Для Triggered не записываем результат, так как это промежуточный статус
                    break;

                case OrderStatus.Unknown:
                    {
                        var errorMsg = "Неизвестный статус ордера (Unknown)";
                        _logger.LogWarning($"CopyOrderService.OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }

                case OrderStatus.MarginCanceled:
                    {
                        var errorMsg = "Ордер отменен по марже (MarginCanceled)";
                        _logger.LogWarning($"CopyOrderService.OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }

                default:
                    {
                        var errorMsg = $"Необработанный статус ордера: {order.Status}";
                        _logger.LogWarning($"CopyOrderService.OnNewOrder: OrderId={order.OrderId} {errorMsg}");
                        SaveFailureResult(order, errorMsg);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService.OnNewOrder: OrderId={order.OrderId} Ошибка при обработке");
        }
    }

    /// <summary>
    /// Обработка ордера в статусе Open - размещаем наш копируемый ордер
    /// </summary>
    private async Task HandleOpenOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.HandleOpenOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction} SubType={order.SubType}");

        switch (order.SubType)
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

            case OrderSubType.None:
                {
                    var errorMsg = "OrderSubType.None - невозможно определить тип ордера";
                    _logger.LogError($"CopyOrderService.HandleOpenOrder: OrderId={order.OrderId} {errorMsg}");
                    SaveFailureResult(order, errorMsg);
                    break;
                }

            default:
                {
                    var errorMsg = $"Неизвестный OrderSubType {order.SubType}";
                    _logger.LogError($"CopyOrderService.HandleOpenOrder: OrderId={order.OrderId} {errorMsg}");
                    SaveFailureResult(order, errorMsg);
                    break;
                }
        }
    }

    /// <summary>
    /// Обработка ордера в статусе Filled - проверяем что наш ордер тоже исполнен
    /// </summary>
    private Task HandleFilledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.HandleFilledOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleFilledOrder)))
            return Task.CompletedTask;

        _orderService.CloseOrderWithStatus(order.OrderId, OrderStatus.Filled);

        // TODO: Проверить статус нашего копируемого ордера
        // Если наш ордер не исполнен полностью - залогировать ошибку или предпринять действия
        // Можно получить наш ордер по OrderId трейдера из маппинга

        _logger.LogWarning($"CopyOrderService.HandleFilledOrder: OrderId={order.OrderId} TODO - проверка исполнения копируемого ордера");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработка ордера в статусе Canceled - отменяем наш копируемый ордер
    /// </summary>
    private Task HandleCanceledOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.HandleCanceledOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleCanceledOrder)))
            return Task.CompletedTask;

        _orderService.CloseOrderWithStatus(order.OrderId, OrderStatus.Canceled);

        // TODO: Отменить наш копируемый ордер через OrdersProvider
        _logger.LogWarning($"CopyOrderService.HandleCanceledOrder: OrderId={order.OrderId} TODO - отмена копируемого ордера на бирже");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработка ордера в статусе Rejected - отменяем наш копируемый ордер (если он был размещен)
    /// </summary>
    private Task HandleRejectedOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.HandleRejectedOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction}");

        // Проверяем был ли ордер скопирован
        if (!EnsureOrderWasCopied(order, nameof(HandleRejectedOrder)))
            return Task.CompletedTask;

        _orderService.CloseOrderWithStatus(order.OrderId, OrderStatus.Rejected);

        // TODO: Отменить наш копируемый ордер через OrdersProvider (если он был размещен)
        _logger.LogWarning($"CopyOrderService.HandleRejectedOrder: OrderId={order.OrderId} TODO - отмена копируемого ордера на бирже");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Открытие НОВОЙ позиции (Open)
    /// Создаем копируемый ордер пропорционально балансу и сохраняем маппинг
    /// </summary>
    private async Task OpenNewPosition(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.OpenNewPosition: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

        try
        {
            var copyOrder = await CreateCopyOrder(order, OrderSubType.Open);

            PublishCopyOrderCreated(order, copyOrder);
            SaveCopyOrderMapping(order, copyOrder);
            SaveSuccessResult(order, copyOrder);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("не найдены") || ex.Message.Contains("должен быть > 0"))
        {
            // Настройки не найдены или VolumeUsd=0 - это НЕ ошибка, просто не копируем
            _logger.LogWarning($"CopyOrderService.OpenNewPosition: OrderId={order.OrderId} {ex.Message}");
            SaveWarningResult(order, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService.OpenNewPosition: OrderId={order.OrderId} Ошибка");
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
        _logger.LogInformation($"CopyOrderService.IncreasePosition: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

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
                _logger.LogError($"CopyOrderService.IncreasePosition: OrderId={order.OrderId} {errorMsg}");
                SaveFailureResult(order, errorMsg);
                return;
            }

            // Получаем exchangeInfo для округления
            var exchangeInfo = await TryGetExchangeInfo(order);
            if (exchangeInfo == null) return;

            // Используем СОХРАНЕННУЮ пропорцию, а не текущий баланс!
            var myIncreaseQuantity = order.Quantity * mapping.PositionRatio;
            myIncreaseQuantity = RoundQuantity(myIncreaseQuantity, exchangeInfo);

            // Получаем настройки кошелька и применяем коэффициент
            var walletSettings = await _walletSettingsService.Get(order.Wallet);
            myIncreaseQuantity *= walletSettings.CopyKoef;

            // Создаем копируемый ордер
            var copyOrder = await CreateCopyOrderFromQuantity(order, myIncreaseQuantity, mapping.PositionRatio, OrderSubType.Increase, walletSettings);

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            // Обновляем только MyQuantity в маппинге (TraderQuantity берем из snapshot, не храним)
            mapping.MyQuantity += myIncreaseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);

            _logger.LogInformation($"CopyOrderService.IncreasePosition: OrderId={order.OrderId} Success, увеличили на {myIncreaseQuantity}, новая позиция {mapping.MyQuantity}");

            SaveSuccessResult(order, copyOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService.IncreasePosition: OrderId={order.OrderId} Ошибка");
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
            _logger.LogError($"CopyOrderService.GetTraderQuantityBeforeOrder: OrderId={orderId} Позиция трейдера не найдена для {symbol}");
            return null;
        }

        var orderFills = _fillsOrderService.GetOrderFillsByOrderId(orderId);

        if (orderFills is null)
        {
            return traderPosition.Quantity;
        }

        var fillQuantityInOrder = orderFills.Trades.Sum(x => x.Quantity);

        var traderTotalQuantity = Math.Abs(traderPosition.Quantity);
        _logger.LogInformation($"CopyOrderService.GetTraderQuantityBeforeOrder: OrderId={orderId} Итоговая позиция трейдера {traderTotalQuantity}");

        // Вычисляем базовую линию - позицию трейдера ДО increase
        var traderQuantityBeforeIncrease = traderTotalQuantity - Math.Abs(fillQuantityInOrder);
        _logger.LogInformation($"CopyOrderService.GetTraderQuantityBeforeOrder: OrderId={orderId} Позиция ДО increase {traderQuantityBeforeIncrease}, увеличение {orderQuantity}");

        return traderQuantityBeforeIncrease;
    }

    #region IncreasePosition Helper Methods

    /// <summary>
    /// Обработать случай отсутствия маппинга при увеличении позиции
    /// Создаем синтетический ордер на открытие и устанавливаем базовую линию
    /// </summary>
    private async Task HandleMissingMappingForIncrease(OriginalOrder order)
    {
        _logger.LogWarning($"CopyOrderService.HandleMissingMappingForIncrease: OrderId={order.OrderId} Маппинг не найден, синхронизируем с позицией трейдера");

        // Получаем базовую линию - позицию трейдера ДО increase
        var traderQuantityBeforeIncrease = await GetTraderQuantityBeforeOrder(order.Wallet, order.Symbol, order.OrderId, order.Quantity);

        if (traderQuantityBeforeIncrease is null || traderQuantityBeforeIncrease < 0)
        {
            var errorMsg = "Позиция трейдера не найдена в snapshot или некорректна";
            _logger.LogError($"CopyOrderService.HandleMissingMappingForIncrease: OrderId={order.OrderId} {errorMsg}");
            SaveFailureResult(order, errorMsg);
            return;
        }

        // Создаем "синтетический" ордер на открытие позиции
        var syntheticOrder = CreateSyntheticOrderForIncrease(order);

        _logger.LogInformation($"CopyOrderService.HandleMissingMappingForIncrease: OrderId={order.OrderId} Открываем синхронизированную позицию Qty={syntheticOrder.Quantity}");
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
            _logger.LogInformation($"CopyOrderService.SetBaselineForNewMapping: OrderId={order.OrderId} Базовая линия TraderQuantityAtEntry={createdMapping.TraderQuantityAtEntry}");
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
        _logger.LogInformation($"CopyOrderService.TryGetTraderPosition: OrderId={order.OrderId} Получаем snapshot позиции трейдера");
        var snapshot = await _currentWalletPositionService.GetSnapshot(order.Wallet);
        var traderPosition = snapshot.Positions.FirstOrDefault(p => p.Symbol == order.Symbol);

        if (traderPosition == null)
        {
            var errorMsg = "Позиция трейдера не найдена в snapshot";
            _logger.LogError($"CopyOrderService.TryGetTraderPosition: OrderId={order.OrderId} {errorMsg} для {order.Symbol}");
            SaveFailureResult(order, errorMsg);
            return null;
        }

        var actualTraderQuantity = Math.Abs(traderPosition.Quantity);
        _logger.LogInformation($"CopyOrderService.TryGetTraderPosition: OrderId={order.OrderId} Позиция трейдера {actualTraderQuantity}");
        return actualTraderQuantity;
    }

    /// <summary>
    /// Расчет количества для закрытия позиции
    /// </summary>
    private decimal CalculateDecreaseQuantity(OriginalOrder order, PositionMapping mapping, decimal actualTraderQuantity)
    {
        _logger.LogInformation($"CopyOrderService.CalculateDecreaseQuantity: OrderId={order.OrderId} Базовая линия {mapping.TraderQuantityAtEntry}");

        // Проверяем: трейдер ушел ниже базовой линии?
        if (actualTraderQuantity - order.Quantity <= mapping.TraderQuantityAtEntry)
        {
            // Трейдер закрыл всё что было после нашего входа (и даже больше) - закрываем ВСЮ позицию
            _logger.LogWarning($"CopyOrderService.CalculateDecreaseQuantity: OrderId={order.OrderId} Трейдер ниже базовой линии, закрываем ВСЮ позицию {mapping.MyQuantity}");
            return mapping.MyQuantity;
        }

        // Трейдер выше базовой линии - закрываем пропорционально от "позиции над базовой"
        var traderAboveBaseline = actualTraderQuantity - mapping.TraderQuantityAtEntry;
        var closeRatio = order.Quantity / traderAboveBaseline;
        _logger.LogInformation($"CopyOrderService.CalculateDecreaseQuantity: OrderId={order.OrderId} Закрываем {closeRatio:P2} от позиции над базовой");

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
            _logger.LogInformation($"CopyOrderService.UpdateOrDeleteMappingAfterDecrease: OrderId={order.OrderId} Закрываем ВСЮ позицию {myCloseQuantity}, удаляем маппинг");
            _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());
        }
        else
        {
            // Частично закрыли - обновляем маппинг
            mapping.MyQuantity -= myCloseQuantity;
            _positionMappingService.SaveOrUpdateMapping(mapping);
            _logger.LogInformation($"CopyOrderService.UpdateOrDeleteMappingAfterDecrease: OrderId={order.OrderId} Закрыли {myCloseQuantity}, осталось {mapping.MyQuantity}");
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
        _logger.LogInformation($"CopyOrderService.DecreasePosition: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

        try
        {
            // Получаем маппинг (с противоположным direction)
            var mapping = GetMappingForDecrease(order);
            if (mapping == null)
            {
                var errorMsg = "Невозможно скопировать decrease - у нас нет открытой позиции (маппинг не найден)";
                _logger.LogWarning($"CopyOrderService.DecreasePosition: OrderId={order.OrderId} {errorMsg}");
                SaveWarningResult(order, errorMsg);
                return;
            }

            // Валидация маппинга
            if (mapping.MyQuantity == 0)
            {
                var errorMsg = "mapping.MyQuantity = 0 - позиция не была открыта или уже закрыта";
                _logger.LogWarning($"CopyOrderService.DecreasePosition: OrderId={order.OrderId} {errorMsg}");
                SaveWarningResult(order, errorMsg);
                return;
            }

            // Получаем позицию трейдера из snapshot
            var actualTraderQuantity = await TryGetTraderPosition(order);
            if (actualTraderQuantity is null) return;

            // Рассчитываем количество для закрытия
            var myDecreaseQuantity = CalculateDecreaseQuantity(order, mapping, actualTraderQuantity.Value);

            // Применяем коэффициент и округляем
            var walletSettings = await _walletSettingsService.Get(order.Wallet);
            myDecreaseQuantity *= walletSettings.CopyKoef;

            var exchangeInfo = await TryGetExchangeInfo(order);
            if (exchangeInfo == null) return;

            myDecreaseQuantity = RoundQuantity(myDecreaseQuantity, exchangeInfo);

            // Создаем копируемый ордер
            var copyOrder = await CreateCopyOrderFromQuantity(order, myDecreaseQuantity, mapping.PositionRatio, OrderSubType.Decrease, walletSettings);

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            // Обновляем или удаляем маппинг
            UpdateOrDeleteMappingAfterDecrease(order, mapping, myDecreaseQuantity);

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService.DecreasePosition: OrderId={order.OrderId} Ошибка");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Полное закрытие позиции (Close)
    /// Закрываем ВСЮ нашу позицию независимо от текущих балансов
    /// </summary>
    private async Task ClosePosition(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService.ClosePosition: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

        try
        {
            // FIX: Close ордер имеет противоположное направление (Short закрывает Long),
            // но маппинг создан с направлением позиции (Long), поэтому используем Opposite()
            var mapping = _positionMappingService.GetMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());

            if (mapping == null)
            {
                var errorMsg = "Невозможно скопировать close - у нас нет открытой позиции (маппинг не найден)";
                _logger.LogWarning($"CopyOrderService.ClosePosition: OrderId={order.OrderId} {errorMsg}");
                SaveWarningResult(order, errorMsg);
                return;
            }

            // Закрываем ВСЮ нашу позицию
            var myCloseQuantity = mapping.MyQuantity;

            // Проверяем что у нас есть открытая позиция
            if (myCloseQuantity == 0)
            {
                var errorMsg = "mapping.MyQuantity = 0 - позиция не была открыта";
                _logger.LogWarning($"CopyOrderService.ClosePosition: OrderId={order.OrderId} {errorMsg}, удаляем маппинг");
                _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());
                SaveWarningResult(order, errorMsg);
                return;
            }

            // Округляем количество
            var exchangeInfo = await TryGetExchangeInfo(order);
            if (exchangeInfo == null) return;

            myCloseQuantity = RoundQuantity(myCloseQuantity, exchangeInfo);

            // Создаем копируемый ордер
            var copyOrder = await CreateCopyOrderFromQuantity(order, myCloseQuantity, mapping.PositionRatio, OrderSubType.Close);

            // Публикуем событие
            PublishCopyOrderCreated(order, copyOrder);

            _logger.LogInformation($"CopyOrderService.ClosePosition: OrderId={order.OrderId} Success, закрываем полностью {myCloseQuantity}");

            // Удаляем маппинг (позиция полностью закрыта)
            _positionMappingService.DeleteMapping(order.Wallet, _myWallet, order.Symbol, order.Direction.Opposite());

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);

            // TODO: Разместить ордер на полное закрытие через OrdersProvider
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService.ClosePosition: OrderId={order.OrderId} Ошибка");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    private async Task<CopyOrderV2> CreateCopyOrder(OriginalOrder order, OrderSubType orderSubType)
    {
        // Получаем информацию о кошельке трейдера (копируемый кошелек)
        var traderWalletInfo = await _walletProvider.GetInfo(order.Wallet);

        // Получаем настройки для кошелька трейдера
        var walletSettings = await _walletSettingsService.Get(order.Wallet);

        if (walletSettings == null || walletSettings.VolumeUsd <= 0)
        {
            var errorMsg = walletSettings == null
                ? $"Настройки для кошелька {order.Wallet} не найдены - ордер не копируется"
                : $"VolumeUsd для кошелька {order.Wallet} = {walletSettings.VolumeUsd} (должен быть > 0) - ордер не копируется";

            _logger.LogWarning($"CopyOrderService.CreateCopyOrder: OrderId={order.OrderId} {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        // Используем VolumeUsd из настроек как наш виртуальный баланс
        var myAccountValue = walletSettings.VolumeUsd;

        // Вычисляем долю от капитала трейдера (какой % от счета он вкладывает)
        var orderRatio = order.VolumeUsd / traderWalletInfo.AccountVolume;

        // Вычисляем ВАШ объем позиции (та же доля от ВАШЕГО баланса)
        var myVolumeUsd = myAccountValue * orderRatio;

        // Применяем CopyKoef если он есть в настройках
        if (walletSettings != null && walletSettings.CopyKoef != 1.0m)
        {
            myVolumeUsd *= walletSettings.CopyKoef;
        }

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
            WalletSettings = walletSettings,
        };

        await CorrectCopyOrder(newCopyOrder);

        return newCopyOrder;
    }

    /// <summary>
    /// Создать копируемый ордер на основе уже рассчитанного количества
    /// Используется для Increase/Decrease/Close позиций
    /// </summary>
    private async Task<CopyOrderV2> CreateCopyOrderFromQuantity(OriginalOrder order, decimal myQuantity, decimal positionRatio,  OrderSubType orderSubType, CopyTradeWalletSettings walletSettings =null)
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
            WalletSettings = walletSettings,
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

        if (exchangeInfo == null)
        {
            _logger.LogError($"CopyOrderService.CorrectCopyOrder: OrderId={copyOrder.OriginalOrderId} Не удалось получить ExchangeInfo для {copyOrder.OriginalOrder.Symbol}");
            return;
        }

        copyOrder.Quantity = Math.Round(copyOrder.Quantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);
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
            _logger.LogWarning($"CopyOrderService.{methodName}: OrderId={order.OrderId} {errorMsg}");
            SaveWarningResult(order, errorMsg);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Получить ExchangeInfo с обработкой ошибок
    /// </summary>
    private async Task<SharedFuturesSymbol?> TryGetExchangeInfo(OriginalOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        if (exchangeInfo == null)
        {
            var errorMsg = $"Не удалось получить ExchangeInfo для {order.Symbol}";
            _logger.LogError($"CopyOrderService.TryGetExchangeInfo: OrderId={order.OrderId} {errorMsg}");
            SaveFailureResult(order, errorMsg);
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
        DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);
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
            LastUpdate = DateTime.Now
        };

        _positionMappingService.SaveOrUpdateMapping(mapping);

        _logger.LogInformation($"CopyOrderService.SaveCopyOrderMapping: OrderId={order.OrderId} Создан маппинг {mapping}");
    }

    /// <summary>
    /// Сохранить успешный результат копирования
    /// </summary>
    private void SaveSuccessResult(OriginalOrder order, CopyOrderV2 copyOrder)
    {
        _resultService.SaveSuccess(order.OrderId.ToString(), order.Wallet, order.Symbol, copyOrder.OrderId.ToString());
    }

    /// <summary>
    /// Сохранить неуспешный результат копирования (ошибка)
    /// </summary>
    private void SaveFailureResult(OriginalOrder order, string errorMessage)
    {
        _resultService.SaveFailure(order.OrderId.ToString(), order.Wallet, order.Symbol, errorMessage);
    }

    /// <summary>
    /// Сохранить предупреждение (ордер не скопирован, но это не ошибка)
    /// </summary>
    private void SaveWarningResult(OriginalOrder order, string warningMessage)
    {
        _resultService.SaveWarning(order.OrderId.ToString(), order.Wallet, order.Symbol, warningMessage);
    }

    #endregion
}
