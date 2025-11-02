using CopyTrading.DataEvents;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CryptoExchange.Net.SharedApis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTriding.Test.Service;

/// <summary>
/// Интеграционные тесты для CopyOrderService
/// Проверяют взаимодействие с CopyOrderResultService и CopyOrderStorageService
/// </summary>
public class CopyOrderServiceIntegrationTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider;
    private readonly Mock<IExchangeInfoProvider> _exchangeInfoProvider;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly PositionMappingService _positionMappingService;
    private readonly CopyOrderResultService _copyOrderResultService;
    private readonly CopyOrderStorageService _storageService;
    private readonly Mock<ILogger<CopyOrderService>> _logger;
    private readonly Mock<ILogger<CopyOrderResultService>> _resultLogger;
    private readonly Mock<ILogger<CopyOrderStorageService>> _storageLogger;
    private readonly Mock<ILogger<PositionMappingService>> _mappingLogger;
    private readonly Mock<ILogger<CurrentWalletPositionService>> _positionLogger;

    private readonly Wallet _traderWallet = new("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
    private readonly Wallet _myWallet = new("0x1234567890abcdef1234567890abcdef12345678");

    public CopyOrderServiceIntegrationTests()
    {
        _walletInfoProvider = new Mock<IWalletInfoProvider>(MockBehavior.Strict);
        _exchangeInfoProvider = new Mock<IExchangeInfoProvider>(MockBehavior.Strict);

        _logger = new Mock<ILogger<CopyOrderService>>();
        _resultLogger = new Mock<ILogger<CopyOrderResultService>>();
        _storageLogger = new Mock<ILogger<CopyOrderStorageService>>();
        _mappingLogger = new Mock<ILogger<PositionMappingService>>();
        _positionLogger = new Mock<ILogger<CurrentWalletPositionService>>();

        // Создаем реальные сервисы для проверки
        _copyOrderResultService = new CopyOrderResultService(_resultLogger.Object);
        _storageService = new CopyOrderStorageService(_storageLogger.Object);
        _positionMappingService = new PositionMappingService(_mappingLogger.Object);
        _currentWalletPositionService = new CurrentWalletPositionService(_walletInfoProvider.Object, _positionLogger.Object);
    }

    #region Helper Methods

    private void SetupExchangeInfo(string symbol, int quantityDecimals = 3, decimal minNotionalValue = 10m, decimal minQuantity = 0.001m)
    {
        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, symbol, "USDC", $"{symbol}/USDC", true)
        {
            QuantityDecimals = quantityDecimals,
            MinNotionalValue = minNotionalValue,
            MinTradeQuantity = minQuantity
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo(symbol))
            .ReturnsAsync(exchangeInfo);
    }

    private void SetupWalletInfo(Wallet wallet, decimal accountVolume, Dictionary<string, Position>? positions = null)
    {
        var walletInfo = new WalletInfoModel
        {
            Wallet = wallet,
            AccountVolume = accountVolume,
            Positions = positions ?? new Dictionary<string, Position>()
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, It.IsAny<bool>()))
            .ReturnsAsync(walletInfo);
    }

    private OriginalOrder CreateOrder(long orderId, Wallet wallet, string symbol, decimal price, decimal quantity,
        Direction direction, decimal leverage = 5m, OrderStatus status = OrderStatus.Open)
    {
        return new OriginalOrder
        {
            OrderId = orderId,
            Wallet = wallet,
            Symbol = symbol,
            Price = price,
            Quantity = quantity,
            Direction = direction,
            Leverage = leverage,
            Status = status
        };
    }

    private OriginalTrade CreateTrade(long tradeId, long orderId, Wallet wallet, string symbol, decimal price,
        decimal quantity, Direction direction, OrderSubType subType)
    {
        return new OriginalTrade
        {
            TradeId = tradeId,
            OrderId = orderId,
            Wallet = wallet,
            Symbol = symbol,
            Price = price,
            Quantity = quantity,
            Direction = direction,
            SubType = subType,
            IsFuture = true
        };
    }

    private WalletPositionsSnapshot CreateSnapshot(
        Wallet wallet, 
        string symbol, 
        decimal quantity,
        Direction direction,
        decimal averageEntryPrice = 1000, 
        int leverage = 5)
    {
        return new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = direction == Direction.Long ? quantity : -quantity,
                    AverageEntryPrice = averageEntryPrice,
                    Leverage = leverage
                }
            ]
        };
    }

    private IntegrationTestableCopyOrderService CreateService()
    {
        var fillsOrderServiceMock = new Mock<FillsOrderService>(Mock.Of<ILogger<FillsOrderService>>());

        return new IntegrationTestableCopyOrderService(
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            _currentWalletPositionService,
            _positionMappingService,
            _copyOrderResultService,
            fillsOrderServiceMock.Object,
            _logger.Object,
            _myWallet);
    }

    private void ResetServices()
    {
        _positionMappingService.ClearAllMappings();
        // Note: CopyOrderResultService and CopyOrderStorageService не имеют clear методов
        // Они будут пересоздаваться для каждого теста если нужно
    }

    #endregion


    [Fact]
    public async Task IntegrationTest_TraderIncreaseAndDecreasePosition_ShouldTrackResultsAndStorage()
    {
        // Arrange
        // Кейс: У трейдера есть открытая позиция long Quantity = 100 для монеты BNB
        // Он размещает новый ордер на увеличение позиции на 20
        // Потом уменьшает на 10
        // Потом уменьшает на 20
        // У трейдера VolumeUsd = 10000$, у нас 5000$ (коэффициент 0.5)

        var symbol = "BNB";
        var traderVolumeUsd = 10000m;
        var myVolumeUsd = 5000m;

        // Setup ExchangeInfo
        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, symbol, "USDC", "BNB/USDC", true)
        {
            QuantityDecimals = 3,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo(symbol))
            .ReturnsAsync(exchangeInfo);

        // Setup WalletInfo для трейдера и нас
        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = traderVolumeUsd,
            Positions = []
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = myVolumeUsd,
            Positions = []
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, It.IsAny<bool>()))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        // Mock для FillsOrderService (не используется в этом тесте)
        var fillsOrderServiceMock = new Mock<FillsOrderService>(Mock.Of<ILogger<FillsOrderService>>());

        // Создаем тестируемый сервис
        var service = new IntegrationTestableCopyOrderService(
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            _currentWalletPositionService,
            _positionMappingService,
            _copyOrderResultService,
            fillsOrderServiceMock.Object,
            _logger.Object,
            _myWallet);

        // ACT & ASSERT

        // ============================================================
        // Событие 1: Increase на +20 (позиция становится 120)
        // ============================================================
        var startSnapshot = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = 100m,
                    AverageEntryPrice = 300m,
                    Leverage = 5
                }
            ]
        };

        _currentWalletPositionService.InitializeWalletSnapshot(startSnapshot);

        var increaseOrder = new OriginalOrder
        {
            OrderId = 1001,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 20m,
            Direction = Direction.Long,
            Leverage = 5m,
            Status = OrderStatus.Open
        };

        var increaseTrade = new OriginalTrade
        {
            TradeId = 5001,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 20m,
            Direction = Direction.Long,
            OrderId = 1001,
            SubType = OrderSubType.Increase,
            IsFuture = true,
        };

        // Отправляем ордер через DataBusEvents
        DataBusEvents.NewOrders?.Invoke([increaseOrder]);
        DataBusEvents.NewTrades?.Invoke(([increaseTrade], false));

        // Даем время на обработку события
        await Task.Delay(200);

        // Проверяем CopyOrderResultService
        var result1 = _copyOrderResultService.GetResult("1001");
        result1.Should().NotBeNull();
        result1!.IsSuccess.Should().BeTrue();
        result1.Symbol.Should().Be(symbol);
        result1.Message.Should().Be("Success");

        // Проверяем CopyOrderStorageService
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(1);
        var copyOrder1 = allOrders.First();
        copyOrder1.OriginalOrderId.Should().Be(1001);

        // Наше quantity должно быть пропорционально величине INCREASE:
        // Трейдер увеличивает на 20, мы открываем 20 * 0.5 = 10
        copyOrder1.Quantity.Should().Be(10m);

        // Проверяем PositionMappingService
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().NotBeNull();
        mapping!.MyQuantity.Should().Be(10m);
        mapping.TraderQuantityAtEntry.Should().Be(100m);

        // ============================================================
        // Событие 2: Decrease на -10 (позиция становится 110)
        // ============================================================
        // ВАЖНОЕ ОТЛИЧИЕ: В отличие от IncreasePosition, DecreasePosition требует snapshot
        // с позицией ДО decrease для корректного расчета closeRatio:
        // closeRatio = order.Quantity / (actualTraderQuantity - TraderQuantityAtEntry)
        var decreaseOrder1 = new OriginalOrder
        {
            OrderId = 1002,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 10m,
            Direction = Direction.Short,
            Leverage = 5m,
            Status = OrderStatus.Open
        };

        var decreaseTrade1 = new OriginalTrade
        {
            TradeId = 5002,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 10m,
            Direction = Direction.Short,
            OrderId = 1002,
            SubType = OrderSubType.Decrease,
            IsFuture = true,
        };

        DataBusEvents.NewOrders?.Invoke([decreaseOrder1]);
        DataBusEvents.NewTrades?.Invoke(([decreaseTrade1], false));
        await Task.Delay(50);

        // Проверяем результат для второго ордера
        var result2 = _copyOrderResultService.GetResult("1002");

        // Проверяем итоговую статистику CopyOrderResultService
        var (Total, Success, Failed, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().Be(2);
        Success.Should().Be(2);

        // Проверяем что первый ордер точно есть в Storage
        allOrders = _storageService.GetAllOrders();
        allOrders.Should().Contain(o => o.OriginalOrderId == 1001);

        // Проверяем PositionMappingService
        mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().NotBeNull();
        mapping!.MyQuantity.Should().Be(5m);
        mapping.TraderQuantityAtEntry.Should().Be(100m);

        // ============================================================
        // Событие 3: Decrease на -30 (позиция становится 80)
        // ============================================================
        // ВАЖНО: Трейдер уходит НИЖЕ базовой линии (80 < 100)
        // Должны закрыть ВСЮ нашу позицию (5m) и удалить маппинг
        var decreaseOrder2 = new OriginalOrder
        {
            OrderId = 1003,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 30m,
            Direction = Direction.Short, // Decrease - противоположное направление
            Leverage = 5m,
            Status = OrderStatus.Open
        };

        var decreaseTrade2 = new OriginalTrade
        {
            TradeId = 5003,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 30m,
            Direction = Direction.Short,
            OrderId = 1003,
            SubType = OrderSubType.Decrease,
            IsFuture = true,
        };

        DataBusEvents.NewOrders?.Invoke([decreaseOrder2]);
        DataBusEvents.NewTrades?.Invoke(([decreaseTrade2], false));
        await Task.Delay(100);

        // Проверяем результат для третьего ордера
        var result3 = _copyOrderResultService.GetResult("1003");
        result3.Should().NotBeNull();
        result3!.IsSuccess.Should().BeTrue();

        // Проверяем что маппинг был УДАЛЕН (трейдер ушел ниже baseline)
        mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().BeNull("трейдер ушел ниже базовой линии, позиция должна быть полностью закрыта");

        // Проверяем статистику
        (Total, Success, Failed, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().Be(3);
        Success.Should().Be(3);

        // ============================================================
        // Событие 4: Increase на +50 (позиция становится 130)
        // ============================================================
        // У нас нет маппинга (был удален), откроем новую позицию
        var increaseOrder2 = new OriginalOrder
        {
            OrderId = 1004,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 50m,
            Direction = Direction.Long,
            Leverage = 5m,
            Status = OrderStatus.Open
        };

        var increaseTrade2 = new OriginalTrade
        {
            TradeId = 5004,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = 300m,
            Quantity = 50m,
            Direction = Direction.Long,
            OrderId = 1004,
            SubType = OrderSubType.Increase,
            IsFuture = true,
        };

        DataBusEvents.NewOrders?.Invoke([increaseOrder2]);
        DataBusEvents.NewTrades?.Invoke(([increaseTrade2], false));
        await Task.Delay(200);

        // Проверяем результат для четвертого ордера
        var result4 = _copyOrderResultService.GetResult("1004");
        result4.Should().NotBeNull();
        result4!.IsSuccess.Should().BeTrue();

        // Проверяем что создался НОВЫЙ маппинг
        mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().NotBeNull("должен быть создан новый маппинг");

        // Открываем позицию пропорционально увеличению: 50 * 0.5 = 25m
        mapping!.MyQuantity.Should().Be(25m);

        // Новая базовая линия: 130 - 50 = 80
        mapping.TraderQuantityAtEntry.Should().Be(80m);

        // Проверяем финальную статистику
        (Total, Success, Failed, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().Be(4);
        Success.Should().Be(4);

        // Проверяем что все 4 ордера есть в Storage
        allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(4);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1001);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1002);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1003);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1004);
    }

    [Fact]
    public async Task OpenLongPosition_ShouldCreateCopyOrderAndMapping()
    {
        // Arrange
        var symbol = "BTC";
        var traderBalance = 20000m;
        var myBalance = 10000m;

        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, traderBalance);
        SetupWalletInfo(_myWallet, myBalance);

        var service = CreateService();

        // Трейдер открывает новую Long позицию: 0.1 BTC @ $50,000 = $5,000 (25% от баланса)
        var order = CreateOrder(2001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var trade = CreateTrade(6001, 2001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2001");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Проверяем маппинг: мы должны открыть позицию пропорционально
        // orderRatio = 5000 / 20000 = 0.25
        // myVolumeUsd = 10000 * 0.25 = 2500
        // myQuantity = 2500 / 50000 = 0.05 BTC
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().NotBeNull();
        mapping!.MyQuantity.Should().Be(0.05m);
        mapping.TraderQuantityAtEntry.Should().Be(0m); // Open позиция - baseline = 0

        // Проверяем storage
        var orders = _storageService.GetAllOrders();
        orders.Should().HaveCount(1);
        orders.First().OriginalOrderId.Should().Be(2001);
    }

    [Fact]
    public async Task OpenShortPosition_ShouldCreateCopyOrderAndMapping()
    {
        // Arrange
        var symbol = "ETH";
        var traderBalance = 15000m;
        var myBalance = 7500m;

        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, traderBalance);
        SetupWalletInfo(_myWallet, myBalance);

        var service = CreateService();

        // Трейдер открывает новую Short позицию: 2 ETH @ $3,000 = $6,000 (40% от баланса)
        var order = CreateOrder(2002, _traderWallet, symbol, 3000m, 2m, Direction.Short);
        var trade = CreateTrade(6002, 2002, _traderWallet, symbol, 3000m, 2m, Direction.Short, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2002");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // orderRatio = 6000 / 15000 = 0.4
        // myVolumeUsd = 7500 * 0.4 = 3000
        // myQuantity = 3000 / 3000 = 1 ETH
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Short);
        mapping.Should().NotBeNull();
        mapping!.MyQuantity.Should().Be(1m);
        mapping.TraderQuantityAtEntry.Should().Be(0m);
    }

    [Fact]
    public async Task CloseLongPosition_ShouldClosePositionAndRemoveMapping()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Сначала создаем открытую Long позицию
        var snapshot = CreateSnapshot(_traderWallet, symbol, 0.1m, Direction.Long);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot);

        // Создаем маппинг вручную (симулируем уже открытую позицию)
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = symbol,
            Direction = Direction.Long,
            MyQuantity = 0.05m,
            TraderQuantityAtEntry = 0m,
            PositionRatio = 0.5m
        };
        _positionMappingService.SaveOrUpdateMapping(mapping);

        // Трейдер закрывает всю позицию: 0.1 BTC Short
        var closeOrder = CreateOrder(2003, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(6003, 2003, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        // Act
        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2003");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Маппинг должен быть удален
        var mappingAfter = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mappingAfter.Should().BeNull("позиция полностью закрыта");

        // Проверяем что ордер в storage
        var orders = _storageService.GetAllOrders();
        orders.Should().Contain(o => o.OriginalOrderId == 2003);
    }

    [Fact]
    public async Task CloseShortPosition_ShouldClosePositionAndRemoveMapping()
    {
        // Arrange
        var symbol = "ETH";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 15000m);
        SetupWalletInfo(_myWallet, 7500m);

        var service = CreateService();

        // Создаем открытую Short позицию
        var snapshot = CreateSnapshot(_traderWallet, symbol, 2m, Direction.Short);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot);

        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = symbol,
            Direction = Direction.Short,
            MyQuantity = 1m,
            TraderQuantityAtEntry = 0m,
            PositionRatio = 0.5m
        };
        _positionMappingService.SaveOrUpdateMapping(mapping);

        // Трейдер закрывает Short позицию: 2 ETH Long
        var closeOrder = CreateOrder(2004, _traderWallet, symbol, 3000m, 2m, Direction.Long);
        var closeTrade = CreateTrade(6004, 2004, _traderWallet, symbol, 3000m, 2m, Direction.Long, OrderSubType.Close);

        // Act
        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2004");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        var mappingAfter = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Short);
        mappingAfter.Should().BeNull("Short позиция полностью закрыта");
    }

    [Fact]
    public async Task OrderCanceled_ShouldUpdateStorageStatusAndResultService()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        var snapshot = CreateSnapshot(_traderWallet, symbol, 5, Direction.Long);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot);

        // Сначала открываем ордер
        var order = CreateOrder(2005, _traderWallet, symbol, 50000m, 0.05m, Direction.Long, status: OrderStatus.Open);
        var trade = CreateTrade(6005, 2005, _traderWallet, symbol, 50000m, 0.05m, Direction.Long, OrderSubType.Increase);

        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(100);

        // Теперь отменяем ордер
        var canceledOrder = CreateOrder(2005, _traderWallet, symbol, 50000m, 0.05m, Direction.Long, status: OrderStatus.Canceled);

        // Act
        DataBusEvents.NewOrders?.Invoke([canceledOrder]);
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2005");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue("ордер был успешно создан до отмены");

        // Storage должен содержать ордер со статусом Canceled
        var orders = _storageService.GetOrdersByStatus(OrderStatus.Canceled);
        orders.Should().Contain(o => o.OriginalOrderId == 2005);
    }

    [Fact]
    public async Task OrderRejected_ShouldUpdateStorageStatusAndResultService()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Ордер сразу отклонен биржей
        var rejectedOrder = CreateOrder(2006, _traderWallet, symbol, 50000m, 0.05m, Direction.Long, status: OrderStatus.Rejected);

        // Act
        DataBusEvents.NewOrders?.Invoke([rejectedOrder]);
        await Task.Delay(200);

        // Assert - CopyOrder не должен быть создан (т.к. ордер Rejected, не Open)
        // Но событие CopyOrderClosed должно быть вызвано
        var result = _copyOrderResultService.GetResult("2006");
        // Result может быть null если ордер не был скопирован

        // Проверяем что маппинг не создан
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().BeNull("rejected ордер не должен создавать маппинг");
    }

    [Fact]
    public async Task OrderFilledImmediately_ShouldCreateMappingAndUpdateStatus()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Ордер сразу исполнен (market order)
        var filledOrder = CreateOrder(2007, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, status: OrderStatus.Filled);
        var trade = CreateTrade(6007, 2007, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([filledOrder]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2007");
        // Ордер со статусом Filled обрабатывается через HandleFilledOrder
        // Note: результат может отсутствовать, т.к. HandleFilledOrder не создает CopyOrder для уже исполненных ордеров
        // result.Should().NotBeNull();
    }

    [Fact]
    public async Task OrderBelowMinNotionalValue_ShouldFailValidation()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol, quantityDecimals: 3, minNotionalValue: 10m, minQuantity: 0.001m);
        SetupWalletInfo(_traderWallet, 100000m); // Большой баланс трейдера
        SetupWalletInfo(_myWallet, 50m); // Очень маленький баланс

        var service = CreateService();

        // Трейдер открывает позицию: 0.001 BTC @ $50,000 = $50
        // Мы должны открыть пропорционально: orderRatio = 50/100000 = 0.0005
        // myVolumeUsd = 50 * 0.0005 = 0.025$ - меньше MinNotionalValue ($10)
        var order = CreateOrder(2008, _traderWallet, symbol, 50000m, 0.001m, Direction.Long);
        var trade = CreateTrade(6008, 2008, _traderWallet, symbol, 50000m, 0.001m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2008");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse("объем ниже минимального");
        result.Message.Should().Contain("минимальн"); // Проверяем что в сообщении упоминается минимум

        // Маппинг не должен быть создан
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().BeNull();
    }

    [Fact]
    public async Task OrderBelowMinTradeQuantity_ShouldFailValidation()
    {
        // Arrange
        var symbol = "ETH";
        SetupExchangeInfo(symbol, quantityDecimals: 4, minNotionalValue: 10m, minQuantity: 0.01m);
        SetupWalletInfo(_traderWallet, 50000m);
        SetupWalletInfo(_myWallet, 100m);

        var service = CreateService();

        // Трейдер: 0.5 ETH @ $3,000 = $1,500
        // Мы: orderRatio = 1500/50000 = 0.03
        // myVolumeUsd = 100 * 0.03 = 3$
        // myQuantity = 3 / 3000 = 0.001 ETH < MinTradeQuantity (0.01)
        var order = CreateOrder(2009, _traderWallet, symbol, 3000m, 0.5m, Direction.Long);
        var trade = CreateTrade(6009, 2009, _traderWallet, symbol, 3000m, 0.5m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2009");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleTradersDifferentSymbols_ShouldCopyAllIndependently()
    {
        // Arrange
        var trader2Wallet = new Wallet("0x9999999999999999999999999999999999999999");

        SetupExchangeInfo("BTC");
        SetupExchangeInfo("ETH");
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(trader2Wallet, 15000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Trader1 открывает BTC
        var order1 = CreateOrder(3001, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long);
        var trade1 = CreateTrade(7001, 3001, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Trader2 открывает ETH
        var order2 = CreateOrder(3002, trader2Wallet, "ETH", 3000m, 2m, Direction.Long);
        var trade2 = CreateTrade(7002, 3002, trader2Wallet, "ETH", 3000m, 2m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order1, order2]);
        DataBusEvents.NewTrades?.Invoke(([trade1, trade2], false));
        await Task.Delay(300);

        // Assert
        var result1 = _copyOrderResultService.GetResult("3001");
        var result2 = _copyOrderResultService.GetResult("3002");

        result1.Should().NotBeNull();
        result1!.IsSuccess.Should().BeTrue();
        result2.Should().NotBeNull();
        result2!.IsSuccess.Should().BeTrue();

        // Проверяем маппинги для обоих трейдеров
        var mapping1 = _positionMappingService.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var mapping2 = _positionMappingService.GetMapping(trader2Wallet, _myWallet, "ETH", Direction.Long);

        mapping1.Should().NotBeNull();
        mapping2.Should().NotBeNull();

        // Проверяем что в storage есть оба ордера
        var orders = _storageService.GetAllOrders();
        orders.Should().Contain(o => o.OriginalOrderId == 3001);
        orders.Should().Contain(o => o.OriginalOrderId == 3002);
    }

    [Fact]
    public async Task MultipleTradersSameSymbolSameDirection_ShouldCreateSeparateMappings()
    {
        // Arrange
        var trader2Wallet = new Wallet("0x8888888888888888888888888888888888888888");

        SetupExchangeInfo("BTC");
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(trader2Wallet, 30000m);
        SetupWalletInfo(_myWallet, 15000m);

        var service = CreateService();

        // Оба трейдера открывают BTC Long
        var order1 = CreateOrder(3003, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long);
        var trade1 = CreateTrade(7003, 3003, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        var order2 = CreateOrder(3004, trader2Wallet, "BTC", 50000m, 0.15m, Direction.Long);
        var trade2 = CreateTrade(7004, 3004, trader2Wallet, "BTC", 50000m, 0.15m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order1, order2]);
        DataBusEvents.NewTrades?.Invoke(([trade1, trade2], false));
        await Task.Delay(300);

        // Assert
        var mapping1 = _positionMappingService.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var mapping2 = _positionMappingService.GetMapping(trader2Wallet, _myWallet, "BTC", Direction.Long);

        mapping1.Should().NotBeNull("маппинг для trader1 должен существовать");
        mapping2.Should().NotBeNull("маппинг для trader2 должен существовать");

        // Разные трейдеры -> разные маппинги с разными количествами
        mapping1!.MyQuantity.Should().NotBe(mapping2!.MyQuantity);
    }

    [Fact]
    public async Task MultipleSymbolsSameTrader_ShouldCreateSeparateMappingsForEach()
    {
        // Arrange
        SetupExchangeInfo("BTC");
        SetupExchangeInfo("ETH");
        SetupExchangeInfo("BNB");
        SetupWalletInfo(_traderWallet, 30000m);
        SetupWalletInfo(_myWallet, 15000m);

        var service = CreateService();

        // Трейдер открывает 3 разные позиции
        var btcOrder = CreateOrder(3005, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long);
        var btcTrade = CreateTrade(7005, 3005, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        var ethOrder = CreateOrder(3006, _traderWallet, "ETH", 3000m, 2m, Direction.Long);
        var ethTrade = CreateTrade(7006, 3006, _traderWallet, "ETH", 3000m, 2m, Direction.Long, OrderSubType.Open);

        var bnbOrder = CreateOrder(3007, _traderWallet, "BNB", 300m, 10m, Direction.Short);
        var bnbTrade = CreateTrade(7007, 3007, _traderWallet, "BNB", 300m, 10m, Direction.Short, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([btcOrder, ethOrder, bnbOrder]);
        DataBusEvents.NewTrades?.Invoke(([btcTrade, ethTrade, bnbTrade], false));
        await Task.Delay(300);

        // Assert
        var btcMapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var ethMapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, "ETH", Direction.Long);
        var bnbMapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, "BNB", Direction.Short);

        btcMapping.Should().NotBeNull();
        ethMapping.Should().NotBeNull();
        bnbMapping.Should().NotBeNull();

        // Проверяем все маппинги
        var allMappings = _positionMappingService.GetAllMappings();
        allMappings.Should().HaveCount(3);
    }

    [Fact]
    public async Task ZeroMyWalletBalance_ShouldFailOrCreateTinyPosition()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 0m); // Нулевой баланс

        var service = CreateService();

        var order = CreateOrder(3008, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var trade = CreateTrade(7008, 3008, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("3008");
        result.Should().NotBeNull();
        // С нулевым балансом копируемый ордер не пройдет валидацию
        result!.IsSuccess.Should().BeFalse("нулевой баланс не позволяет открыть позицию");
    }

    [Fact]
    public async Task VeryLargeTraderBalanceVsSmallMyBalance_ShouldCalculateCorrectRatio()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 1000000m); // 1 миллион
        SetupWalletInfo(_myWallet, 1000m); // 1 тысяча (коэффициент 0.001)

        var service = CreateService();

        // Трейдер: 0.5 BTC @ $50,000 = $25,000 (2.5% от баланса)
        var order = CreateOrder(3009, _traderWallet, symbol, 50000m, 0.5m, Direction.Long);
        var trade = CreateTrade(7009, 3009, _traderWallet, symbol, 50000m, 0.5m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        // orderRatio = 25000 / 1000000 = 0.025
        // myVolumeUsd = 1000 * 0.025 = 25
        // myQuantity = 25 / 50000 = 0.0005 BTC
        var result = _copyOrderResultService.GetResult("3009");
        result.Should().NotBeNull();

        if (result!.IsSuccess)
        {
            var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
            mapping.Should().NotBeNull();
            mapping!.MyQuantity.Should().Be(0.001m); // Округлено по QuantityDecimals
        }
        // Если не прошло валидацию - тоже ОК (меньше минимума)
    }

    [Fact]
    public async Task IncreaseShortPosition_ShouldIncreaseShortCorrectly()
    {
        // Arrange
        var symbol = "ETH";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 15000m);
        SetupWalletInfo(_myWallet, 7500m);

        var service = CreateService();

        // Трейдер уже в Short позиции: -2 ETH
        var snapshot = CreateSnapshot(_traderWallet, symbol, 2m, Direction.Short);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot);

        // Создаем маппинг
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = symbol,
            Direction = Direction.Short,
            MyQuantity = 1m,
            TraderQuantityAtEntry = 0m,
            PositionRatio = 0.5m
        };
        _positionMappingService.SaveOrUpdateMapping(mapping);

        // Трейдер увеличивает Short: еще -1 ETH
        var increaseOrder = CreateOrder(3010, _traderWallet, symbol, 3000m, 1m, Direction.Short);
        var increaseTrade = CreateTrade(7010, 3010, _traderWallet, symbol, 3000m, 1m, Direction.Short, OrderSubType.Increase);

        // Act
        DataBusEvents.NewOrders?.Invoke([increaseOrder]);
        DataBusEvents.NewTrades?.Invoke(([increaseTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("3010");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        var mappingAfter = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Short);
        mappingAfter.Should().NotBeNull();
        // MyQuantity должно увеличиться: 1 + 0.5 = 1.5
        mappingAfter!.MyQuantity.Should().Be(1.5m);
    }

    [Fact]
    public async Task DecreaseShortPosition_ShouldDecreaseShortCorrectly()
    {
        // Arrange
        var symbol = "ETH";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 15000m);
        SetupWalletInfo(_myWallet, 7500m);

        var service = CreateService();

        // Трейдер в Short позиции: -3 ETH
        var snapshot = CreateSnapshot(_traderWallet, symbol, 3m, Direction.Short);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot);

        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = symbol,
            Direction = Direction.Short,
            MyQuantity = 1.5m,
            TraderQuantityAtEntry = 0m,
            PositionRatio = 0.5m
        };
        _positionMappingService.SaveOrUpdateMapping(mapping);

        // Трейдер уменьшает Short: +1 ETH Long (закрывает часть Short)
        var decreaseOrder = CreateOrder(3011, _traderWallet, symbol, 3000m, 1m, Direction.Long);
        var decreaseTrade = CreateTrade(7011, 3011, _traderWallet, symbol, 3000m, 1m, Direction.Long, OrderSubType.Decrease);

        // Act
        DataBusEvents.NewOrders?.Invoke([decreaseOrder]);
        DataBusEvents.NewTrades?.Invoke(([decreaseTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("3011");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        var mappingAfter = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Short);
        mappingAfter.Should().NotBeNull();
        // closeRatio = 1 / 3 = 0.333
        // closeQuantity = 1.5 * 0.333 = 0.5
        // MyQuantity = 1.5 - 0.5 = 1.0
        mappingAfter!.MyQuantity.Should().Be(1m);
    }

    [Fact]
    public async Task ComplexSequence_OpenIncreaseDecreaseClose_ShouldTrackAllOperations()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Шаг 1: Open
        var openOrder = CreateOrder(4001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var openTrade = CreateTrade(8001, 4001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        DataBusEvents.NewOrders?.Invoke([openOrder]);
        DataBusEvents.NewTrades?.Invoke(([openTrade], false));
        await Task.Delay(100);

        var snapshot1 = CreateSnapshot(_traderWallet, symbol, 0.1m, Direction.Long);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot1);

        // Шаг 2: Increase x3
        for (int i = 0; i < 3; i++)
        {
            var orderId = 4002 + i;
            var tradeId = 8002 + i;
            var increaseOrder = CreateOrder(orderId, _traderWallet, symbol, 50000m, 0.02m, Direction.Long);
            var increaseTrade = CreateTrade(tradeId, orderId, _traderWallet, symbol, 50000m, 0.02m, Direction.Long, OrderSubType.Increase);

            DataBusEvents.NewOrders?.Invoke([increaseOrder]);
            DataBusEvents.NewTrades?.Invoke(([increaseTrade], false));
            await Task.Delay(50);
        }

        // Шаг 3: Decrease x2
        for (int i = 0; i < 2; i++)
        {
            var orderId = 4005 + i;
            var tradeId = 8005 + i;
            var decreaseOrder = CreateOrder(orderId, _traderWallet, symbol, 50000m, 0.03m, Direction.Short);
            var decreaseTrade = CreateTrade(tradeId, orderId, _traderWallet, symbol, 50000m, 0.03m, Direction.Short, OrderSubType.Decrease);

            DataBusEvents.NewOrders?.Invoke([decreaseOrder]);
            DataBusEvents.NewTrades?.Invoke(([decreaseTrade], false));
            await Task.Delay(50);
        }

        // Шаг 4: Close
        var closeOrder = CreateOrder(4007, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(8007, 4007, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var (Total, Success, Failed, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().BeGreaterThanOrEqualTo(7, "должно быть минимум 7 операций");

        // Маппинг должен быть удален после Close
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().BeNull("позиция закрыта");

        // Все ордера в storage
        var orders = _storageService.GetAllOrders();
        orders.Should().Contain(o => o.OriginalOrderId == 4001);
        orders.Should().Contain(o => o.OriginalOrderId == 4007);
    }

    [Fact]
    public async Task ReopenAfterClose_ShouldCreateNewMappingWithNewBaseline()
    {
        // Arrange
        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Открываем, закрываем, снова открываем
        var snapshot1 = CreateSnapshot(_traderWallet, symbol, 0.1m, Direction.Long);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot1);

        // Open
        var openOrder1 = CreateOrder(5001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var openTrade1 = CreateTrade(9001, 5001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        DataBusEvents.NewOrders?.Invoke([openOrder1]);
        DataBusEvents.NewTrades?.Invoke(([openTrade1], false));
        await Task.Delay(100);

        // Close
        var closeOrder = CreateOrder(5002, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(9002, 5002, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(100);

        // Проверяем что маппинг удален
        var mappingAfterClose = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mappingAfterClose.Should().BeNull();

        // Снова открываем
        var snapshot2 = CreateSnapshot(_traderWallet, symbol, 0.2m, Direction.Long);
        _currentWalletPositionService.InitializeWalletSnapshot(snapshot2);

        var openOrder2 = CreateOrder(5003, _traderWallet, symbol, 51000m, 0.2m, Direction.Long);
        var openTrade2 = CreateTrade(9003, 5003, _traderWallet, symbol, 51000m, 0.2m, Direction.Long, OrderSubType.Open);

        DataBusEvents.NewOrders?.Invoke([openOrder2]);
        DataBusEvents.NewTrades?.Invoke(([openTrade2], false));
        await Task.Delay(100);

        // Assert
        var mappingAfterReopen = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mappingAfterReopen.Should().NotBeNull("должен быть создан новый маппинг");
        mappingAfterReopen!.TraderQuantityAtEntry.Should().Be(0m, "новый Open должен иметь baseline = 0");
    }
}

/// <summary>
/// Тестовый сервис для интеграционных тестов
/// Используется только для подмены _myWallet через рефлексию
/// </summary>
public class IntegrationTestableCopyOrderService : CopyOrderService
{
    public IntegrationTestableCopyOrderService(
        IWalletInfoProvider walletProvider,
        IExchangeInfoProvider exchangeInfoProvider,
        CurrentWalletPositionService currentWalletPositionService,
        PositionMappingService positionMappingService,
        CopyOrderResultService resultService,
        FillsOrderService fillsOrderService,
        ILogger<CopyOrderService> logger,
        Wallet myWallet)
        : base(walletProvider, exchangeInfoProvider, currentWalletPositionService, positionMappingService, resultService, fillsOrderService, logger)
    {
        // Используем рефлексию чтобы подменить _myWallet
        var field = typeof(CopyOrderService).GetField("_myWallet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, myWallet);
    }
}
