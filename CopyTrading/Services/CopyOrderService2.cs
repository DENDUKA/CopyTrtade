using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
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
public class CopyOrderService2(
    IActiveWindowService _activeWindowService,
    IBaselinePositionService _baselinePositionService,
    ICurrentWalletPositionService _currentWalletPositionService,
    ICopyOrderStorageService _copyOrderStorageService,
    IWalletInfoProvider _walletProvider,
    IExchangeInfoProvider _exchangeInfoProvider,
    ICopyTradeWalletSettingsService _walletSettingsService,
    ICopyOrderResultService _copyOrderResultService,
    ILogger<CopyOrderService2> _logger) : ICopyOrderService
{
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public async Task OnNewOrders(OriginalOrder[] orders)
    {
        ArgumentNullException.ThrowIfNull(orders, nameof(orders));

        var marketOrders = orders.Where(o => o.Type == OrderType.Market).ToArray();
        var limitOrders = orders.Where(o => o.Type == OrderType.Limit).ToArray();
        var noneOrders = orders.Where(o => o.Type == OrderType.None).ToArray();

        await OnMarketOrdersHandler(marketOrders);
        await OnLimitOrdersHandler(limitOrders);
        OnNoneOrdersHandler(noneOrders);
    }

    private async Task OnLimitOrdersHandler(OriginalOrder[] orders)
    {
        if (orders.Length == 0) return;

        var orderGroups = orders
            .GroupBy(order => (order.Wallet, order.Symbol))
            .ToArray();

        _logger.LogInformation($"CopyOrderService2.OnNewOrders: Получено {orders.Length} ордеров в {orderGroups.Length} группах (Wallet, Symbol)");

        // Обрабатываем каждую группу
        foreach (var group in orderGroups)
        {
            var (wallet, symbol) = group.Key;
            var groupOrders = group.ToArray();

            await ProcessOrdersForWalletAndSymbol(wallet, symbol);
        }
    }



    private async Task OnMarketOrdersHandler(OriginalOrder[] orders)
    {

    }

    /// <summary>
    /// Обрабатывает группу ордеров для одного кошелька и символа
    /// </summary>
    private async Task ProcessOrdersForWalletAndSymbol(Wallet wallet, string symbol)
    {
        var nearestLongOrders = _activeWindowService.GetNearestOrders(wallet, symbol, Direction.Long, count: 3);
        var nearestShortOrders = _activeWindowService.GetNearestOrders(wallet, symbol, Direction.Short, count: 3);

        var copyedOrders = _copyOrderStorageService.GetOrdersByWalletAndSymbol(wallet, symbol);

        var allNearestOrders = nearestLongOrders.Concat(nearestShortOrders).ToArray();

        // Определяем какие ордера нужно скопировать и какие отменить
        var (ordersToCopy, ordersToCancel) = FindOrdersToProcessing(allNearestOrders, copyedOrders);

        CancelOrders(ordersToCancel);

        await TryCopyOrders(ordersToCopy);

        _logger.LogInformation(
            $"CopyOrderService2.ProcessOrdersForWalletAndSymbol: Wallet={wallet.Value}, Symbol={symbol}, " +
            $"NearestLong={nearestLongOrders.Length}, NearestShort={nearestShortOrders.Length}, " +
            $"AlreadyCopied={copyedOrders.Length}, ToCopy={ordersToCopy.Length}, ToCancel={ordersToCancel.Length}");
    }

    private async Task TryCopyOrders(OriginalOrder[] ordersToCopy)
    {
        if (ordersToCopy.Length == 0) return;

        foreach (var order in ordersToCopy)
        {
            try
            {
                await TryCopyOrder(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CopyOrderService2.TryCopyOrders: OrderId={order.OrderId} Ошибка при копировании");
            }
        }
    }

    /// <summary>
    /// Копирует один ордер с учетом baseline позиции
    /// </summary>
    private async Task TryCopyOrder(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService2.TryCopyOrder: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

        // Проверяем пересекает ли ордер baseline
        var baselineCheckResult = await _baselinePositionService.WillOrderCloseBelowBaseline(order.OrderId);

        switch (baselineCheckResult)
        {
            case BaselineCheckResult.AboveBaseline:
                // Ордер НЕ пересекает baseline - копируем полностью
                _logger.LogInformation($"CopyOrderService2.TryCopyOrder: OrderId={order.OrderId} Копируем полностью (AboveBaseline)");
                await CopyOrderFully(order);
                break;

            case BaselineCheckResult.CrossesBaseline:
                // Ордер пересекает baseline - требуется частичное копирование
                _logger.LogWarning(
                    $"CopyOrderService2.TryCopyOrder: OrderId={order.OrderId} " +
                    $"Ордер пересекает baseline (CrossesBaseline) - частичное копирование пока не реализовано");
                // TODO: Реализовать частичное копирование
                break;

            case BaselineCheckResult.AlreadyBelowBaseline:
                // Baseline уже была пересечена - НЕ копируем
                _logger.LogInformation(
                    $"CopyOrderService2.TryCopyOrder: OrderId={order.OrderId} " +
                    $"НЕ копируем - baseline уже пересечена (AlreadyBelowBaseline)");
                break;

            default:
                _logger.LogWarning($"CopyOrderService2.TryCopyOrder: OrderId={order.OrderId} Неизвестный BaselineCheckResult={baselineCheckResult}");
                break;
        }
    }

    /// <summary>
    /// Копирует ордер полностью (без ограничений по baseline)
    /// </summary>
    private async Task CopyOrderFully(OriginalOrder order)
    {
        _logger.LogInformation($"CopyOrderService2.CopyOrderFully: OrderId={order.OrderId} {order.Symbol} {order.Direction} Qty={order.Quantity}");

        try
        {
            // Создаем копируемый ордер
            var copyOrder = await CreateCopyOrder(order);

            // Публикуем событие создания копируемого ордера
            DataBusEvents.CopyOrderCreated?.Invoke(copyOrder);

            // Сохраняем результат
            SaveSuccessResult(order, copyOrder);

            _logger.LogInformation(
                $"CopyOrderService2.CopyOrderFully: OrderId={order.OrderId} Success. " +
                $"Копируемый ордер создан: Qty={copyOrder.Quantity}, Price={order.Price}, Ratio={copyOrder.OrderRatio:P2}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("не найдены") || ex.Message.Contains("должен быть > 0"))
        {
            // Настройки не найдены или VolumeUsd=0 - это НЕ ошибка, просто не копируем
            _logger.LogWarning($"CopyOrderService2.CopyOrderFully: OrderId={order.OrderId} {ex.Message}");
            SaveWarningResult(order, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CopyOrderService2.CopyOrderFully: OrderId={order.OrderId} Ошибка при создании копируемого ордера");
            SaveFailureResult(order, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Создает копируемый ордер на основе оригинального ордера трейдера
    /// Рассчитывает пропорциональное количество на основе соотношения балансов
    /// </summary>
    private async Task<CopyOrderV2> CreateCopyOrder(OriginalOrder order)
    {
        // Получаем информацию о кошельке трейдера
        var traderWalletInfo = await _walletProvider.GetInfo(order.Wallet);

        // Получаем настройки для кошелька трейдера
        var walletSettings = await _walletSettingsService.Get(order.Wallet);

        if (walletSettings == null || walletSettings.VolumeUsd <= 0)
        {
            var errorMsg = walletSettings == null
                ? $"Настройки для кошелька {order.Wallet} не найдены - ордер не копируется"
                : $"VolumeUsd для кошелька {order.Wallet} = {walletSettings.VolumeUsd} (должен быть > 0) - ордер не копируется";

            _logger.LogWarning($"CopyOrderService2.CreateCopyOrder: OrderId={order.OrderId} {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        // Используем VolumeUsd из настроек как наш виртуальный баланс
        var myAccountValue = walletSettings.VolumeUsd;

        // Вычисляем долю от капитала трейдера (какой % от счета он вкладывает)
        var orderRatio = order.VolumeUsd / traderWalletInfo.AccountVolume;

        // Вычисляем ВАШ объем позиции (та же доля от ВАШЕГО баланса)
        var myVolumeUsd = myAccountValue * orderRatio;

        myVolumeUsd *= walletSettings.CopyKoef;        

        // Вычисляем количество монет по той же цене
        var myQuantity = myVolumeUsd / order.Price;

        // Получаем exchangeInfo для округления
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);
        if (exchangeInfo == null)
        {
            var errorMsg = $"Не удалось получить ExchangeInfo для {order.Symbol}";
            _logger.LogError($"CopyOrderService2.CreateCopyOrder: OrderId={order.OrderId} {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        // Округляем количество согласно правилам биржи
        myQuantity = Math.Round(myQuantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);

        // Генерируем уникальный временный ID для копируемого ордера
        var tempOrderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var newCopyOrder = new CopyOrderV2()
        {
            OriginalOrder = order,
            OrderId = tempOrderId,
            OriginalOrderId = order.OrderId,
            OrderSubType = OrderSubType.None,
            OrderRatio = orderRatio,
            MyPE = myAccountValue,
            AccountPE = traderWalletInfo.AccountVolume,
            Quantity = myQuantity,
            WalletSettings = walletSettings,
        };

        _logger.LogDebug(
            $"CopyOrderService2.CreateCopyOrder: OrderId={order.OrderId} " +
            $"TraderVolume={order.VolumeUsd:F2}, TraderPE={traderWalletInfo.AccountVolume:F2}, " +
            $"MyPE={myAccountValue:F2}, Ratio={orderRatio:P2}, MyVolume={myVolumeUsd:F2}, MyQty={myQuantity}");

        return newCopyOrder;
    }

    /// <summary>
    /// Сохранить успешный результат копирования
    /// </summary>
    private void SaveSuccessResult(OriginalOrder order, CopyOrderV2 copyOrder)
    {
        _copyOrderResultService.SaveSuccess(order.OrderId.ToString(), order.Wallet, order.Symbol, copyOrder.OrderId.ToString());
    }

    /// <summary>
    /// Сохранить неуспешный результат копирования (ошибка)
    /// </summary>
    private void SaveFailureResult(OriginalOrder order, string errorMessage)
    {
        _copyOrderResultService.SaveFailure(order.OrderId.ToString(), order.Wallet, order.Symbol, errorMessage);
    }

    /// <summary>
    /// Сохранить предупреждение (ордер не скопирован, но это не ошибка)
    /// </summary>
    private void SaveWarningResult(OriginalOrder order, string warningMessage)
    {
        _copyOrderResultService.SaveWarning(order.OrderId.ToString(), order.Wallet, order.Symbol, warningMessage);
    }

    private void CancelOrders(CopyOrderV2[] ordersToCancel)
    {
        if (ordersToCancel.Length == 0) return;

        foreach (var copyOrder in ordersToCancel)
        {
            _logger.LogInformation(
                $"CopyOrderService2.CancelOrders: OrderId={copyOrder.OriginalOrderId} " +
                $"Публикуем событие CopyOrderCancelRequested");

            // Публикуем событие отмены копируемого ордера
            DataBusEvents.CopyOrderCancelRequested?.Invoke(copyOrder.OriginalOrderId);
        }
    }

    /// <summary>
    /// Находит ордера для копирования и отмены на основе ближайших и уже скопированных
    /// </summary>
    /// <param name="orders">Все ближайшие ордера трейдера (Long + Short)</param>
    /// <param name="copyOrdersForPair">Уже скопированные ордера для данной пары (wallet, symbol)</param>
    /// <returns>Кортеж: (ордера для копирования, ордера для отмены)</returns>
    private static (OriginalOrder[] OrdersToCopy, CopyOrderV2[] OrdersToCancel) FindOrdersToProcessing(
        OriginalOrder[] orders,
        CopyOrderV2[] copyOrdersForPair)
    {
        // Находим ордера, которые нужно скопировать (ближайшие, но еще не скопированные)
        var ordersToCopy = orders
            .Where(orderFills => !copyOrdersForPair.Any(co => co.OriginalOrderId == orderFills.OrderId))
            .ToArray();

        // Находим копируемые ордера, которые больше не являются ближайшими (нужно отменить)
        var ordersToCancel = copyOrdersForPair
            .Where(co => !orders.Any(nearest => nearest.OrderId == co.OriginalOrderId))
            .ToArray();

        return (ordersToCopy, ordersToCancel);
    }

    private void OnNoneOrdersHandler(OriginalOrder[] orders)
    {
        if (orders.Length == 0)
            return;

        var orderIds = string.Join(", ", orders.Select(o => o.OrderId));
        _logger.LogWarning($"CopyOrderService2.OnNoneOrdersHandler: Получены ордера с типом None. Количество: {orders.Length}, OrderIds: [{orderIds}]");
    }
}
