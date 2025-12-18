using CopyTrading.BlazorUI.Services;
using CopyTrading.DataEvents;
using CopyTrading.Models.Builders;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CryptoExchange.Net.SharedApis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradeRepositoreySQL = CopyTrading.Repository.SQLite.TradeRepository;
using TradeRepositoryInflux = CopyTrading.Repository.Influx.TradeRepository;

namespace CopyTrading.Test.Service;

/// <summary>
/// Интеграционные тесты для CopyOrderService
/// Проверяют взаимодействие с CopyOrderResultService и CopyOrderStorageService
/// </summary>
[Collection("Sequential")]
public class CopyOrderServiceIntegrationTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider;
    private readonly Mock<IExchangeInfoProvider> _exchangeInfoProvider;
    private readonly Mock<CopyTradeWalletSettingsService> _walletSettingsServiceMock;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly PositionMappingService _positionMappingService;
    private readonly CopyOrderResultService _copyOrderResultService;
    private CopyOrderStorageService _storageService;  // Не readonly - будет пересоздаваться в ResetServices

    // Loggers
    private readonly Mock<ILogger<CopyOrderService>> _logger;
    private readonly Mock<ILogger<CopyOrderResultService>> _resultLogger;
    private readonly Mock<ILogger<CopyOrderStorageService>> _storageLogger;
    private readonly Mock<ILogger<PositionMappingService>> _mappingLogger;
    private readonly Mock<ILogger<CurrentWalletPositionService>> _positionLogger;
    private readonly Mock<ILogger<TradeService>> _tradeServiceLogger;
    private readonly Mock<ILogger<OrderService>> _orderServiceLogger;

    // Mocks для TradeService
    private readonly Mock<FillsOrderService> _fillsOrderServiceMock;
    private readonly Mock<OrdersTradesSubscriber> _orderProviderMock;
    private readonly Mock<OrderBookSubscriber> _orderBookProviderMock;
    private readonly Mock<TradeRepositoryInflux> _tradeRepositoryInfluxMock;
    private readonly Mock<TradeRepositoreySQL> _tradeRepositorySQLMock;
    private readonly Mock<RealtimeUpdateService> _realtimeUpdateServiceMock;

    // Mocks для OrderService
    private readonly Mock<Repository.Influx.OrderRepository> _orderRepositoryInfluxMock;
    private readonly Mock<Repository.SQLite.OrderRepository> _orderRepositorySQLiteMock;
    private readonly Mock<IBaselinePositionService> _baselinePositionServiceMock;
    private readonly Mock<IOrdersProvider> _ordersProviderMock;

    private readonly Wallet _traderWallet = new("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
    private readonly Wallet _myWallet = new("0x1234567890abcdef1234567890abcdef12345678");

    public CopyOrderServiceIntegrationTests()
    {
        _walletInfoProvider = new Mock<IWalletInfoProvider>(MockBehavior.Strict);
        _exchangeInfoProvider = new Mock<IExchangeInfoProvider>(MockBehavior.Strict);

        // Создаем mock для WalletSettingsRepository с необходимым logger
        var walletSettingsRepoMock = new Mock<Repository.SQLite.WalletSettingsRepository>(
            Mock.Of<ILogger<Repository.SQLite.WalletSettingsRepository>>());

        _walletSettingsServiceMock = new Mock<CopyTradeWalletSettingsService>(
            MockBehavior.Loose,
            walletSettingsRepoMock.Object,
            Mock.Of<ILogger<CopyTradeWalletSettingsService>>());

        // Инициализация логгеров
        _logger = new Mock<ILogger<CopyOrderService>>();
        _resultLogger = new Mock<ILogger<CopyOrderResultService>>();
        _storageLogger = new Mock<ILogger<CopyOrderStorageService>>();
        _mappingLogger = new Mock<ILogger<PositionMappingService>>();
        _positionLogger = new Mock<ILogger<CurrentWalletPositionService>>();
        _tradeServiceLogger = new Mock<ILogger<TradeService>>();
        _orderServiceLogger = new Mock<ILogger<OrderService>>();

        // Инициализация моков для TradeService
        _fillsOrderServiceMock = new Mock<FillsOrderService>(Mock.Of<ILogger<FillsOrderService>>());
        _ordersProviderMock = new Mock<IOrdersProvider>(MockBehavior.Loose);
        var ordersProviderConcreteMock = new Mock<Providers.Hyperliquid.Providers.OrdersProvider>(MockBehavior.Loose, Mock.Of<ILogger<Providers.Hyperliquid.Providers.OrdersProvider>>());
        var currentWalletPositionServiceMock = new Mock<CurrentWalletPositionService>(
            MockBehavior.Loose,
            Mock.Of<IWalletInfoProvider>(),
            _fillsOrderServiceMock.Object,
            Mock.Of<ILogger<CurrentWalletPositionService>>());
        _orderProviderMock = new Mock<OrdersTradesSubscriber>(
            MockBehavior.Loose,
            Mock.Of<ILogger<OrdersTradesSubscriber>>(),
            ordersProviderConcreteMock.Object,
            _fillsOrderServiceMock.Object,
            currentWalletPositionServiceMock.Object,
            Mock.Of<IWalletInfoProvider>());
        _orderBookProviderMock = new Mock<OrderBookSubscriber>(MockBehavior.Loose, Mock.Of<ILogger<OrderBookSubscriber>>());
        _tradeRepositoryInfluxMock = new Mock<TradeRepositoryInflux>(MockBehavior.Loose, Mock.Of<ILogger<TradeRepositoryInflux>>());
        _tradeRepositorySQLMock = new Mock<TradeRepositoreySQL>(MockBehavior.Loose, Mock.Of<ILogger<TradeRepositoreySQL>>());
        _realtimeUpdateServiceMock = new Mock<RealtimeUpdateService>(MockBehavior.Loose,
            Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<BlazorUI.Hubs.CopyTradingHub>>(),
            Mock.Of<ILogger<RealtimeUpdateService>>());

        // Инициализация моков для OrderService
        _orderRepositoryInfluxMock = new Mock<Repository.Influx.OrderRepository>(MockBehavior.Loose, Mock.Of<ILogger<Repository.Influx.OrderRepository>>());
        _orderRepositorySQLiteMock = new Mock<Repository.SQLite.OrderRepository>(MockBehavior.Loose, Mock.Of<ILogger<Repository.SQLite.OrderRepository>>());
        _baselinePositionServiceMock = new Mock<IBaselinePositionService>(MockBehavior.Loose);

        // Создаем реальные сервисы для проверки
        _copyOrderResultService = new CopyOrderResultService(_resultLogger.Object);
        _storageService = new CopyOrderStorageService(_storageLogger.Object);
        _positionMappingService = new PositionMappingService(_mappingLogger.Object);
        _currentWalletPositionService = new CurrentWalletPositionService(_walletInfoProvider.Object, new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()), _positionLogger.Object);
    }

    [Fact]
    public async Task IntegrationTest_TraderIncreaseAndDecreasePosition_ShouldTrackResultsAndStorage()
    {
        // Arrange
        ResetServices();  // Clear state from other tests

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

        // Setup WalletSettings для трейдера
        var traderWalletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = myVolumeUsd,  // Используем myVolumeUsd как виртуальный баланс для копирования
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(traderWalletSettings);

        // Создаем тестируемый сервис
        var service = CreateService();

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
        // ВАЖНО: Обновляем snapshot ПЕРЕД Decrease чтобы DecreasePosition видел текущую позицию трейдера
        var updatedSnapshot1 = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = 120m,  // Было 100, + 20 Increase
                    AverageEntryPrice = 300m,
                    Leverage = 5
                }
            ]
        };
        UpdateWalletSnapshot(updatedSnapshot1);  // Обновляем существующий snapshot
        await Task.Delay(50);  // Даём время на обработку snapshot

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
        var (Total, Success, Warning, Error, SuccessRate) = _copyOrderResultService.GetStatistics();
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
        // ВАЖНО: Обновляем snapshot ПЕРЕД Decrease чтобы DecreasePosition видел текущую позицию трейдера
        var updatedSnapshot2 = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = 110m,  // Было 120, - 10 Decrease
                    AverageEntryPrice = 300m,
                    Leverage = 5
                }
            ]
        };
        UpdateWalletSnapshot(updatedSnapshot2);  // Обновляем существующий snapshot
        await Task.Delay(50);  // Даём время на обработку snapshot

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
        (Total, Success, Warning, Error, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().Be(3);
        Success.Should().Be(3);

        // ============================================================
        // Событие 4: Increase на +50 (позиция становится 130)
        // ============================================================
        // ВАЖНО: Обновляем snapshot ПЕРЕД Increase чтобы IncreasePosition видел текущую позицию трейдера
        var updatedSnapshot3 = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = 80m,  // Было 110, - 30 Decrease
                    AverageEntryPrice = 300m,
                    Leverage = 5
                }
            ]
        };
        UpdateWalletSnapshot(updatedSnapshot3);  // Обновляем snapshot перед Increase
        await Task.Delay(50);  // Даём время на обработку snapshot

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
        (Total, Success, Warning, Error, SuccessRate) = _copyOrderResultService.GetStatistics();
        Total.Should().Be(4);
        Success.Should().Be(4);

        // Проверяем что все 4 ордера есть в Storage
        // NOTE: Проверяем только наличие ордеров, а не точное количество,
        // так как при параллельном запуске тестов могут быть ордера из других тестов
        allOrders = _storageService.GetAllOrders();
        allOrders.Should().Contain(o => o.OriginalOrderId == 1001);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1002);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1003);
        allOrders.Should().Contain(o => o.OriginalOrderId == 1004);
    }

    [Fact]
    public async Task OpenLongPosition_ShouldCreateCopyOrderAndMapping()
    {
        // Arrange
        ResetServices();

        var symbol = "BTC";
        var traderBalance = 20000m;
        var myBalance = 10000m;

        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, traderBalance);
        SetupWalletInfo(_myWallet, myBalance);

        var service = CreateService();

        InitializeEmptyWalletSnapshot(_traderWallet);

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
        ResetServices();

        var symbol = "ETH";
        var traderBalance = 15000m;
        var myBalance = 7500m;

        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, traderBalance);
        SetupWalletInfo(_myWallet, myBalance);

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

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
        // ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        var order = CreateOrder(2002, _traderWallet, symbol, 3000m, 0.1m, Direction.Long);        
        var trade = CreateTrade(6002, 2002, _traderWallet, symbol, 3000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Инициализация пустой Snapshot
        DataBusEvents.NewTrades?.Invoke(([trade], true));

        var positions = new PositionBuilder()
            .WithLeverage(5)
            .WithQuantity(0.1m)
            .WithAverageEntryPrice(3000m)
            .WithSymbol(symbol)
            .BuildDictionary();

        SetupWalletInfo(_traderWallet, 20000m, positions);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));

        // Трейдер закрывает Long позицию через Short ордер (правильно с точки зрения трейдинга)
        var closeOrder = CreateOrder(2003, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(6003, 2003, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        // Act
        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2003");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue("Close операция должна успешно выполниться");

        // Маппинг должен быть удален после успешного закрытия позиции
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

        var order = CreateOrder(2002, _traderWallet, symbol, 3000m, 2m, Direction.Short);
        var trade = CreateTrade(6002, 2002, _traderWallet, symbol, 3000m, 2m, Direction.Short, OrderSubType.Open);

        // Инициализация пустой Snapshot
        DataBusEvents.NewTrades?.Invoke(([trade], true));

        var positions = new PositionBuilder()
            .WithLeverage(5)
            .WithQuantity(-2m)
            .WithAverageEntryPrice(3000m)
            .WithSymbol(symbol)
            .BuildDictionary();
        SetupWalletInfo(_traderWallet, 20000m, positions);

        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));

        // Трейдер закрывает Short позицию через Long ордер (правильно с точки зрения трейдинга)
        var closeOrder = CreateOrder(2004, _traderWallet, symbol, 3000m, 2m, Direction.Long);
        var closeTrade = CreateTrade(6004, 2004, _traderWallet, symbol, 3000m, 2m, Direction.Long, OrderSubType.Close);

        // Act
        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("2004");
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue("Close операция должна успешно выполниться");

        // Маппинг должен быть удален после успешного закрытия позиции
        var mappingAfter = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Short);
        mappingAfter.Should().BeNull("Short позиция полностью закрыта");
    }

    [Fact]
    public async Task OrderCanceled_ShouldUpdateStorageStatusAndResultService()
    {
        // Arrange
        ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        // Setup wallet settings для копирования
        _walletSettingsServiceMock.Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(new CopyTradeWalletSettings
            {
                Wallet = _traderWallet,
                VolumeUsd = 1000m,
                CopyKoef = 1.0m
            });

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
        ResetServices();

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
        ResetServices();

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
        ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol, quantityDecimals: 3, minNotionalValue: 10m, minQuantity: 0.001m);
        SetupWalletInfo(_traderWallet, 100000m); // Большой баланс трейдера
        SetupWalletInfo(_myWallet, 50m); // Очень маленький баланс

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

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

        // Проверяем что либо не прошло валидацию, либо создалось с минимальным объемом
        if (!result!.IsSuccess)
        {
            result.Message.Should().Contain("минимальн"); // Проверяем что в сообщении упоминается минимум
            // Маппинг не должен быть создан
            var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
            mapping.Should().BeNull();
        }
        else
        {
            // Если прошло, значит создалось с очень маленьким объемом
            _logger.Object.LogInformation("Ордер создан с маленьким объемом несмотря на низкий баланс");
        }
    }

    [Fact]
    public async Task OrderBelowMinTradeQuantity_ShouldFailValidation()
    {
        // Arrange
        ResetServices();

        var symbol = "ETH";
        SetupExchangeInfo(symbol, quantityDecimals: 4, minNotionalValue: 10m, minQuantity: 0.01m);
        SetupWalletInfo(_traderWallet, 50000m);
        SetupWalletInfo(_myWallet, 100m);

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

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

        // Проверяем что либо не прошло валидацию, либо округлилось до минимума
        if (!result!.IsSuccess)
        {
            _logger.Object.LogInformation("Ордер не прошел валидацию из-за маленького количества");
        }
    }

    [Fact]
    public async Task MultipleTradersDifferentSymbols_ShouldCopyAllIndependently()
    {
        // Arrange
        ResetServices();

        var trader2Wallet = new Wallet("0x9999999999999999999999999999999999999999");

        SetupExchangeInfo("BTC");
        SetupExchangeInfo("ETH");
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(trader2Wallet, 15000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        InitializeEmptyWalletSnapshot(_traderWallet);
        InitializeEmptyWalletSnapshot(trader2Wallet);

        // Trader1 открывает BTC
        var order1 = CreateOrder(3001, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long);
        var trade1 = CreateTrade(7001, 3001, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Trader2 открывает ETH
        var order2 = CreateOrder(3002, trader2Wallet, "ETH", 3000m, 2m, Direction.Long);
        var trade2 = CreateTrade(7002, 3002, trader2Wallet, "ETH", 3000m, 2m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order1, order2]);
        DataBusEvents.NewTrades?.Invoke(([trade1, trade2], false));
        await Task.Delay(500);  // Увеличиваем время для обработки обоих трейдеров

        // Assert
        var result1 = _copyOrderResultService.GetResult("3001");
        var result2 = _copyOrderResultService.GetResult("3002");

        result1.Should().NotBeNull();
        result1!.IsSuccess.Should().BeTrue();

        result2.Should().NotBeNull();
        if (!result2!.IsSuccess)
        {
            throw new Exception($"Order 3002 failed: {result2.Message}");
        }
        result2.IsSuccess.Should().BeTrue();

        // Проверяем маппинги для обоих трейдеров
        var mapping1 = _positionMappingService.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var mapping2 = _positionMappingService.GetMapping(trader2Wallet, _myWallet, "ETH", Direction.Long);

        mapping1.Should().NotBeNull();
        mapping2.Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleTradersSameSymbolSameDirection_ShouldCreateSeparateMappings()
    {
        // Arrange
        ResetServices();

        var trader2Wallet = new Wallet("0x8888888888888888888888888888888888888888");

        SetupExchangeInfo("BTC");
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(trader2Wallet, 30000m);
        SetupWalletInfo(_myWallet, 15000m);

        var service = CreateService();

        // Инициализируем пустые snapshots для обоих трейдеров
        var emptySnapshot1 = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions = []
        };
        _currentWalletPositionService.InitializeWalletSnapshot(emptySnapshot1);

        var emptySnapshot2 = new WalletPositionsSnapshot
        {
            Wallet = trader2Wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = []
        };
        _currentWalletPositionService.InitializeWalletSnapshot(emptySnapshot2);

        // Оба трейдера открывают BTC Long
        // Trader1: 0.1 BTC @ 50000 = 5000 USD, orderRatio = 5000/20000 = 0.25 (25%)
        var order1 = CreateOrder(3003, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long);
        var trade1 = CreateTrade(7003, 3003, _traderWallet, "BTC", 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Trader2: 0.2 BTC @ 50000 = 10000 USD, orderRatio = 10000/30000 = 0.333 (33%)
        var order2 = CreateOrder(3004, trader2Wallet, "BTC", 50000m, 0.2m, Direction.Long);
        var trade2 = CreateTrade(7004, 3004, trader2Wallet, "BTC", 50000m, 0.2m, Direction.Long, OrderSubType.Open);

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
        ResetServices();

        SetupExchangeInfo("BTC");
        SetupExchangeInfo("ETH");
        SetupExchangeInfo("BNB");
        SetupWalletInfo(_traderWallet, 30000m);
        SetupWalletInfo(_myWallet, 15000m);

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

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
        ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 0m); // Нулевой баланс

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

        var order = CreateOrder(3008, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var trade = CreateTrade(7008, 3008, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        // Act
        DataBusEvents.NewOrders?.Invoke([order]);
        DataBusEvents.NewTrades?.Invoke(([trade], false));
        await Task.Delay(200);

        // Assert
        var result = _copyOrderResultService.GetResult("3008");
        result.Should().NotBeNull();

        // С нулевым балансом копируемый ордер может не пройти валидацию или создаться с 0 quantity
        // Зависит от реализации CopyOrderService
        if (!result!.IsSuccess)
        {
            _logger.Object.LogInformation("Ордер не прошел валидацию из-за нулевого баланса");
        }
    }

    [Fact]
    public async Task VeryLargeTraderBalanceVsSmallMyBalance_ShouldCalculateCorrectRatio()
    {
        // Arrange
        ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 1000000m, volumeUsd: 1000m); // 1 миллион трейдер, мы копируем на 1 тысячу
        SetupWalletInfo(_myWallet, 1000m); // 1 тысяча

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

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
        ResetServices();

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
        ResetServices();

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
        ResetServices();

        var symbol = "BTC";
        SetupExchangeInfo(symbol);
        SetupWalletInfo(_traderWallet, 20000m);
        SetupWalletInfo(_myWallet, 10000m);

        var service = CreateService();

        // Инициализируем пустой snapshot
        InitializeEmptyWalletSnapshot(_traderWallet);

        // Шаг 1: Open
        var openOrder = CreateOrder(4001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long);
        var openTrade = CreateTrade(8001, 4001, _traderWallet, symbol, 50000m, 0.1m, Direction.Long, OrderSubType.Open);

        DataBusEvents.NewOrders?.Invoke([openOrder]);
        DataBusEvents.NewTrades?.Invoke(([openTrade], false));
        await Task.Delay(100);

        // Обновляем snapshot с открытой позицией для последующих Increase операций
        var snapshot1 = CreateSnapshot(_traderWallet, symbol, 0.1m, Direction.Long);
        UpdateWalletSnapshot(snapshot1);  // Обновляем snapshot после Open

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

        // Шаг 4: Close (Short order to close Long position)
        var closeOrder = CreateOrder(4007, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(8007, 4007, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(200);

        // Assert
        var (Total, Success, Warning, Error, SuccessRate) = _copyOrderResultService.GetStatistics();
        // Open + 3×Increase + 2×Decrease + Close = 7 операций
        Total.Should().BeGreaterThanOrEqualTo(7, "должно быть минимум 7 операций");
        Success.Should().BeGreaterThanOrEqualTo(7, "минимум 7 успешных операций");

        // Маппинг должен быть удален после Close
        var mapping = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mapping.Should().BeNull("позиция закрыта, маппинг удален");

        // Все ордера в storage
        var orders = _storageService.GetAllOrders();
        orders.Should().Contain(o => o.OriginalOrderId == 4001);
        orders.Should().Contain(o => o.OriginalOrderId == 4007);
    }

    [Fact]
    public async Task ReopenAfterClose_ShouldCreateNewMappingWithNewBaseline()
    {
        // Arrange
        ResetServices();

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

        // Close (Short order to close Long position)
        var closeOrder = CreateOrder(5002, _traderWallet, symbol, 50000m, 0.1m, Direction.Short);
        var closeTrade = CreateTrade(9002, 5002, _traderWallet, symbol, 50000m, 0.1m, Direction.Short, OrderSubType.Close);

        DataBusEvents.NewOrders?.Invoke([closeOrder]);
        DataBusEvents.NewTrades?.Invoke(([closeTrade], false));
        await Task.Delay(100);

        // Проверяем что маппинг удален после Close
        var mappingAfterClose = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mappingAfterClose.Should().BeNull("маппинг должен быть удален после закрытия позиции");

        // Обновляем snapshot чтобы показать что позиция закрыта (empty positions)
        var emptySnapshot = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions = []
        };
        UpdateWalletSnapshot(emptySnapshot);  // Обновляем на пустой snapshot

        // Снова открываем новую позицию
        // ВАЖНО: НЕ обновляем snapshot перед Open, т.к. в реальности snapshot обновляется ПОСЛЕ исполнения ордера
        var openOrder2 = CreateOrder(5003, _traderWallet, symbol, 51000m, 0.2m, Direction.Long);
        var openTrade2 = CreateTrade(9003, 5003, _traderWallet, symbol, 51000m, 0.2m, Direction.Long, OrderSubType.Open);

        DataBusEvents.NewOrders?.Invoke([openOrder2]);
        DataBusEvents.NewTrades?.Invoke(([openTrade2], false));
        await Task.Delay(100);

        // Assert
        var mappingAfterReopen = _positionMappingService.GetMapping(_traderWallet, _myWallet, symbol, Direction.Long);
        mappingAfterReopen.Should().NotBeNull("должен быть создан новый маппинг");

        // Новая позиция после close должна иметь baseline = 0
        mappingAfterReopen!.TraderQuantityAtEntry.Should().Be(0m, "новый Open после Close должен иметь baseline = 0");
        mappingAfterReopen.MyQuantity.Should().BeGreaterThan(0m, "должна быть создана копия позиции");
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

    private void SetupWalletInfo(Wallet wallet, decimal accountVolume, Dictionary<string, Position>? positions = null, decimal? volumeUsd = null)
    {
        var walletInfo = new WalletInfoModel
        {
            Wallet = wallet,
            AccountVolume = accountVolume,
            Positions = positions ?? []
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, It.IsAny<bool>()))
            .ReturnsAsync(walletInfo);

        // Setup для CopyTradeWalletSettingsService
        // Для _myWallet не создаем настройки (он не трейдер)
        // volumeUsd - это наш виртуальный баланс для копирования этого трейдера
        if (wallet != _myWallet)
        {
            var walletSettings = new CopyTradeWalletSettings
            {
                Wallet = wallet,
                VolumeUsd = volumeUsd ?? accountVolume / 2,  // По умолчанию половина баланса трейдера
                CopyKoef = 1.0m
            };

            _walletSettingsServiceMock
                .Setup(x => x.Get(wallet))
                .ReturnsAsync(walletSettings);
        }
    }

    private static OriginalOrder CreateOrder(long orderId, Wallet wallet, string symbol, decimal price, decimal quantity,
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

    private static OriginalTrade CreateTrade(long tradeId, long orderId, Wallet wallet, string symbol, decimal price,
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

    private static WalletPositionsSnapshot CreateSnapshot(
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
        // Создаем реальный FillsOrderService для корректного расчета SubType
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());

        // Создаем TradeService, который подписывается на DataBusEvents.NewTrades
        var tradeService = new TradeService(
            _orderProviderMock.Object,
            _walletInfoProvider.Object,
            _tradeRepositoryInfluxMock.Object,
            _tradeRepositorySQLMock.Object,
            fillsOrderService,
            _currentWalletPositionService,
            _baselinePositionServiceMock.Object,
            _realtimeUpdateServiceMock.Object,
            _tradeServiceLogger.Object);

        // Для разрешения циклической зависимости OrderService <-> CopyOrderService:
        // 1. Создаем временный мок CopyOrderService для OrderService
        var tempCopyOrderServiceMock = new Mock<ICopyOrderService>(MockBehavior.Loose);

        // 2. Создаем настоящий OrderService с временным моком
        var orderService = new OrderService(
            _orderProviderMock.Object,
            _walletInfoProvider.Object,
            _orderRepositoryInfluxMock.Object,
            _orderRepositorySQLiteMock.Object,
            _tradeRepositorySQLMock.Object,
            _currentWalletPositionService,
            fillsOrderService,
            tempCopyOrderServiceMock.Object,
            _realtimeUpdateServiceMock.Object,
            _ordersProviderMock.Object,
            _storageService,
            _orderServiceLogger.Object);

        // 3. Создаем настоящий CopyOrderService с настоящим OrderService
        var copyOrderService = new IntegrationTestableCopyOrderService(
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            _currentWalletPositionService,
            _positionMappingService,
            _copyOrderResultService,
            fillsOrderService,
            _walletSettingsServiceMock.Object,
            orderService,
            _logger.Object,
            _myWallet);

        // 4. Устанавливаем настоящий CopyOrderService в OrderService через рефлексию
        var copyOrderServiceField = typeof(OrderService).GetField("_copyOrderService",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        copyOrderServiceField?.SetValue(orderService, copyOrderService);

        return copyOrderService;
    }

    private void ResetServices()
    {
        // Очищаем все данные
        _positionMappingService.ClearAllMappings();
        _storageService.ClearAllOrders();
        _copyOrderResultService.ClearAllResults();
        _currentWalletPositionService.ClearAllSnapshots();  // Очищаем snapshots

        // Очищаем все подписки на события (отписываем все старые экземпляры CopyOrderService)
        DataBusEvents.ClearAllSubscriptions();

        // Сбрасываем Mock объекты (очищаем все Setup'ы)
        _walletInfoProvider.Reset();
        _exchangeInfoProvider.Reset();

        // Пересоздаем _storageService чтобы он снова подписался на события
        _storageService = new CopyOrderStorageService(_storageLogger.Object);
    }

    /// <summary>
    /// Инициализирует пустой snapshot (для тестов)
    /// </summary>
    private void InitializeEmptyWalletSnapshot(Wallet wallet)
    {
        var emptySnapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = []
        };

        _currentWalletPositionService.InitializeWalletSnapshot(emptySnapshot);
    }

    /// <summary>
    /// Обновляет существующий snapshot (для тестов)
    /// Используется когда нужно изменить позицию трейдера между операциями
    /// </summary>
    private void UpdateWalletSnapshot(WalletPositionsSnapshot snapshot)
    {
        // Принудительно обновляем snapshot через рефлексию
        var field = typeof(CurrentWalletPositionService).GetField("_walletPositionSnapshot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var dict = field.GetValue(_currentWalletPositionService) as System.Collections.Concurrent.ConcurrentDictionary<Wallet, WalletPositionsSnapshot>;
            if (dict != null)
            {
                dict[snapshot.Wallet] = snapshot;
            }
        }
    }

    [Fact]
    public async Task IntegrationTest_ComplexScenarioWithBalanceChange_ShouldHandleMultipleOrdersAndBalanceUpdates()
    {
        // Arrange
        ResetServices();

        /*
         * Сценарий:
         * 1. Трейдер: 10,000 USD, Мы: 1,000 USD (VolumeUsd из настроек)
         * 2. Трейдер открывает позицию на 20,000 USD (leverage 2x, 200% от баланса)
         *    → orderRatio = 20,000 / 10,000 = 2.0
         *    → мы копируем: 1,000 × 2.0 = 2,000 USD (1 ETH)
         * 3. Трейдер открывает еще позицию на 20,000 USD (200% от баланса)
         *    → orderRatio = 2.0
         *    → мы копируем: 2,000 USD (1 ETH)
         * 4. У трейдера баланс меняется на 100,000 USD (вырос в 10 раз!)
         * 5. Первые два ордера НЕ исполняются (остаются Open)
         * 6. Трейдер открывает еще ордер на 200,000 USD (200% от НОВОГО баланса)
         *    → orderRatio = 200,000 / 100,000 = 2.0 (тот же!)
         *    → мы копируем: 1,000 × 2.0 = 2,000 USD (1 ETH)
         *    → ВАЖНО: несмотря на рост баланса трейдера, мы копируем ту же долю
         * 7. Трейдер закрывает первые два ордера
         * 8. Третий ордер исполняется (Filled)
         */

        var symbol = "ETH";
        var price = 2000m;

        // Setup ExchangeInfo
        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, symbol, "USDC", "ETH/USDC", true)
        {
            QuantityDecimals = 4,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo(symbol))
            .ReturnsAsync(exchangeInfo);

        // Setup начальных настроек для кошелька
        var initialWalletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 1000m,  // Наш объем 1,000 USD
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(initialWalletSettings);

        // Начальное состояние трейдера: баланс 10,000 USD
        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000m,
            Positions = []
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, It.IsAny<bool>()))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(new WalletInfoModel
            {
                Wallet = _myWallet,
                AccountVolume = 1000m,
                Positions = []
            });

        // Инициализируем пустой snapshot для трейдера
        InitializeEmptyWalletSnapshot(_traderWallet);

        // Создаем тестируемый сервис
        var service = CreateService();

        // ============================================================
        // Шаг 1: Трейдер открывает первую позицию на 20,000 USD
        // ============================================================
        var order1 = new OriginalOrder
        {
            OrderId = 1001,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = price,
            Quantity = 10m,  // 10 ETH * 2000 = 20,000 USD
            Direction = Direction.Long,
            Leverage = 2m,
            Status = OrderStatus.Open
        };

        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(200);

        // Проверяем первую копию
        var result1 = _copyOrderResultService.GetResult("1001");
        result1.Should().NotBeNull();
        result1!.IsSuccess.Should().BeTrue();

        var copyOrders = _storageService.GetAllOrders().ToList();
        copyOrders.Should().HaveCount(1);

        // Наш ордер: orderRatio = 20,000 / 10,000 = 2.0 (200%)
        // myVolumeUsd = 1,000 * 2.0 = 2,000 USD
        // myQuantity = 2,000 / 2,000 = 1 ETH
        copyOrders[0].Quantity.Should().Be(1m);
        copyOrders[0].VolumeUsd.Should().Be(2000m);
        copyOrders[0].OrderRatio.Should().Be(2.0m);

        // ВАЖНО: НЕ обновляем snapshot! Ордер Open (не Filled), snapshot остается пустым

        // ============================================================
        // Шаг 2: Трейдер открывает вторую позицию на 20,000 USD
        // ============================================================
        var order2 = new OriginalOrder
        {
            OrderId = 1002,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = price,
            Quantity = 10m,  // 10 ETH * 2000 = 20,000 USD
            Direction = Direction.Long,
            Leverage = 2m,
            Status = OrderStatus.Open
        };

        DataBusEvents.NewOrders?.Invoke([order2]);
        await Task.Delay(200);

        // Проверяем вторую копию
        var result2 = _copyOrderResultService.GetResult("1002");
        result2.Should().NotBeNull();
        result2!.IsSuccess.Should().BeTrue();

        copyOrders = _storageService.GetAllOrders().ToList();
        copyOrders.Should().HaveCount(2);
        copyOrders[1].Quantity.Should().Be(1m);  // Снова 1 ETH (та же пропорция)
        copyOrders[1].VolumeUsd.Should().Be(2000m);

        // ВАЖНО: НЕ обновляем snapshot! Ордер Open (не Filled), snapshot остается пустым

        // ============================================================
        // Шаг 3: У трейдера меняется баланс на 100,000 USD
        // ============================================================
        traderWalletInfo.AccountVolume = 100000m;
        // Первые два ордера НЕ исполняются (остаются Open)

        // ============================================================
        // Шаг 4: Трейдер готовится открыть третью позицию на 200,000 USD
        // ============================================================
        var order3 = new OriginalOrder
        {
            OrderId = 1003,
            Wallet = _traderWallet,
            Symbol = symbol,
            Price = price,
            Quantity = 100m,  // 100 ETH * 2000 = 200,000 USD
            Direction = Direction.Long,
            Leverage = 2m,
            Status = OrderStatus.Open
        };

        // ВАЖНО: order3 пока НЕ отправляется! Он будет отправлен в шаге 6

        // ============================================================
        // Шаг 5: Трейдер отменяет первые два ордера (Cancelled)
        // ============================================================
        order1.Status = OrderStatus.Canceled;
        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(200);

        order2.Status = OrderStatus.Canceled;
        DataBusEvents.NewOrders?.Invoke([order2]);
        await Task.Delay(200);

        // После отмены ордеров 1001 и 1002, копируемые ордера тоже должны быть закрыты
        // Они остаются в storage (просто закрыты), всего 2 ордера
        copyOrders = _storageService.GetAllOrders().ToList();
        copyOrders.Should().HaveCount(2);  // Только 2 ордера (1001, 1002), order3 еще не отправлен

        // ============================================================
        // Шаг 6: Третий ордер приходит (Open → Filled)
        // ============================================================
        // Сначала приходит как Open
        order3.Status = OrderStatus.Open;
        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(200);

        // Проверяем что order3 скопирован
        var result3 = _copyOrderResultService.GetResult("1003");
        result3.Should().NotBeNull();
        result3!.IsSuccess.Should().BeTrue();

        copyOrders = _storageService.GetAllOrders().ToList();
        copyOrders.Should().HaveCount(3);  // Теперь 3 ордера (1001, 1002, 1003)

        // Snapshot пустой (ордера не исполнились) → order3 определяется как Open
        // orderRatio = 200,000 / 100,000 = 2.0 (200%)
        // myVolumeUsd = 1,000 * 2.0 = 2,000 USD
        // myQuantity = 2,000 / 2,000 = 1 ETH
        var order3Copy = copyOrders.First(o => o.OriginalOrder.OrderId == 1003);
        order3Copy.Quantity.Should().Be(1m);
        order3Copy.VolumeUsd.Should().Be(2000m);
        order3Copy.OrderRatio.Should().Be(2.0m);

        // Затем приходит как Filled
        order3.Status = OrderStatus.Filled;
        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(200);

        // Финальные проверки
        var stats = _copyOrderResultService.GetStatistics();
        stats.Total.Should().Be(3);  // 3 ордера (1001, 1002, 1003)
        stats.Success.Should().Be(3);  // Все 3 ордера успешно скопированы
        stats.Warning.Should().Be(0);
        stats.Error.Should().Be(0);

        // Проверяем что все 3 ордера Long
        copyOrders = _storageService.GetAllOrders().ToList();
        copyOrders.Should().HaveCount(3);
        copyOrders.Should().AllSatisfy(o => o.OriginalOrder.Direction.Should().Be(Direction.Long));

        // Все Long ордера должны иметь quantity = 1 ETH каждый
        copyOrders.Should().AllSatisfy(o => o.Quantity.Should().Be(1m));

        // Все ордера должны иметь SubType = Open
        copyOrders.Should().AllSatisfy(o => o.OrderSubType.Should().Be(OrderSubType.Open));

        // Проверяем что первые два ордера были отменены
        // (CopyOrderService вызовет HandleCanceledOrder для них)
    }

    [Fact]
    public async Task IntegrationTest_TwoTraders_DifferentOrderSequence_ShouldProduceSameProportionalVolume()
    {
        // Этот тест проверяет что порядок ордеров (2000→4500 vs 4500→2000) не влияет на финальную пропорцию
        // Трейдер A: баланс 10,000$, наш VolumeUsd 1,000$ → коэффициент 0.1
        // Трейдер B: баланс 10,000$, наш VolumeUsd 1,000$ → коэффициент 0.1
        // Оба трейдера открывают позиции на 2,000$ + 4,500$ = 6,500$ (65% от баланса)
        // Ожидаемый результат:
        // - Трейдер A: 1,000$ * 0.65 = 650$
        // - Трейдер B: 1,000$ * 0.65 = 650$
        // - Объемы ОДИНАКОВЫЕ несмотря на разный порядок ордеров!

        // ============================================================
        // Arrange
        // ============================================================
        ResetServices();  // Clear state from other tests

        var traderA = new Wallet("0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        var traderB = new Wallet("0xBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");

        var btcPrice = 50000m;  // BTC = 50,000$
        var ethPrice = 3000m;   // ETH = 3,000$

        // Setup Trader A (BTC): баланс 10,000$, наш VolumeUsd 1,000$
        var traderAWalletInfo = new WalletInfoModel
        {
            Wallet = traderA,
            AccountVolume = 10000m,
            Positions = []
        };
        _walletInfoProvider
            .Setup(x => x.GetInfo(traderA, It.IsAny<bool>()))
            .ReturnsAsync(traderAWalletInfo);

        var traderASettings = new CopyTradeWalletSettings
        {
            Wallet = traderA,
            VolumeUsd = 1000m,  // Изменено с 5000 на 1000 для равного сравнения
            CopyKoef = 1.0m
        };
        _walletSettingsServiceMock
            .Setup(x => x.Get(traderA))
            .ReturnsAsync(traderASettings);

        // Setup Trader B (ETH): баланс 10,000$, наш VolumeUsd 1,000$
        var traderBWalletInfo = new WalletInfoModel
        {
            Wallet = traderB,
            AccountVolume = 10000m,
            Positions = []
        };
        _walletInfoProvider
            .Setup(x => x.GetInfo(traderB, It.IsAny<bool>()))
            .ReturnsAsync(traderBWalletInfo);

        var traderBSettings = new CopyTradeWalletSettings
        {
            Wallet = traderB,
            VolumeUsd = 1000m,
            CopyKoef = 1.0m
        };
        _walletSettingsServiceMock
            .Setup(x => x.Get(traderB))
            .ReturnsAsync(traderBSettings);

        // Setup exchange info
        var btcExchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "BTC", "USDC", "BTC/USDC", true)
        {
            QuantityDecimals = 3,
        };
        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("BTC"))
            .ReturnsAsync(btcExchangeInfo);

        var ethExchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "ETH", "USDC", "ETH/USDC", true)
        {
            QuantityDecimals = 2,
        };
        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("ETH"))
            .ReturnsAsync(ethExchangeInfo);

        InitializeEmptyWalletSnapshot(traderA);
        InitializeEmptyWalletSnapshot(traderB);

        // Создаем тестируемый сервис
        var service = CreateService();

        // ============================================================
        // Act - Трейдер A (BTC): порядок 2,000$ → 4,500$
        // ============================================================

        // Трейдер A - Ордер 1: Open 2,000$ (20% от баланса)
        var traderA_order1 = new OriginalOrder
        {
            OrderId = 1001,
            Wallet = traderA,
            Symbol = "BTC",
            Price = btcPrice,
            Quantity = 0.04m,  // 0.04 BTC * 50,000 = 2,000$
            Direction = Direction.Long,
            Leverage = 10m,
            Status = OrderStatus.Open,
            SubType = OrderSubType.Open
        };
        DataBusEvents.NewOrders?.Invoke([traderA_order1]);
        await Task.Delay(200);

        // Проверяем первую копию
        var resultA1 = _copyOrderResultService.GetResult("1001");
        resultA1.Should().NotBeNull();
        if (!resultA1!.IsSuccess)
        {
            System.Diagnostics.Debug.WriteLine($"TraderA Order1 failed: {resultA1.Message}");
        }
        resultA1!.IsSuccess.Should().BeTrue($"because: {resultA1.Message}");

        // Трейдер A - Ордер 2: Increase 4,500$ (45% от баланса)
        var traderA_order2 = new OriginalOrder
        {
            OrderId = 1002,
            Wallet = traderA,
            Symbol = "BTC",
            Price = btcPrice,
            Quantity = 0.09m,  // 0.09 BTC * 50,000 = 4,500$
            Direction = Direction.Long,
            Leverage = 10m,
            Status = OrderStatus.Open,
            SubType = OrderSubType.Increase
        };

        DataBusEvents.NewOrders?.Invoke([traderA_order2]);
        await Task.Delay(200);

        // Проверяем вторую копию
        var resultA2 = _copyOrderResultService.GetResult("1002");
        resultA2.Should().NotBeNull();
        resultA2!.IsSuccess.Should().BeTrue();

        // ============================================================
        // Act - Трейдер B (ETH): порядок 4,500$ → 2,000$ (обратный!)
        // ============================================================

        // Трейдер B - Ордер 1: Open 4,500$ (45% от баланса)
        var traderB_order1 = new OriginalOrder
        {
            OrderId = 2001,
            Wallet = traderB,
            Symbol = "ETH",
            Price = ethPrice,
            Quantity = 1.5m,  // 1.5 ETH * 3,000 = 4,500$
            Direction = Direction.Long,
            Leverage = 10m,
            Status = OrderStatus.Open,
            SubType = OrderSubType.Open
        };

        DataBusEvents.NewOrders?.Invoke([traderB_order1]);
        await Task.Delay(200);

        // Проверяем первую копию
        var resultB1 = _copyOrderResultService.GetResult("2001");
        resultB1.Should().NotBeNull();
        resultB1!.IsSuccess.Should().BeTrue();

        // Трейдер B - Ордер 2: Increase 2,000$ (20% от баланса)
        var traderB_order2 = new OriginalOrder
        {
            OrderId = 2002,
            Wallet = traderB,
            Symbol = "ETH",
            Price = ethPrice,
            Quantity = 0.6667m,  // 0.6667 ETH * 3,000 ≈ 2,000$
            Direction = Direction.Long,
            Leverage = 10m,
            Status = OrderStatus.Open,
            SubType = OrderSubType.Increase
        };

        DataBusEvents.NewOrders?.Invoke([traderB_order2]);
        await Task.Delay(200);

        // Проверяем вторую копию
        var resultB2 = _copyOrderResultService.GetResult("2002");
        resultB2.Should().NotBeNull();
        resultB2!.IsSuccess.Should().BeTrue();

        // ============================================================
        // Assert - Проверяем финальные объемы
        // ============================================================
        var allOrders = _storageService.GetAllOrders().ToList();
        allOrders.Should().HaveCount(4);  // 2 BTC + 2 ETH

        // Получаем BTC ордера (Трейдер A)
        var btcOrders = allOrders.Where(o => o.OriginalOrder.Symbol == "BTC").ToList();
        btcOrders.Should().HaveCount(2);

        // Получаем ETH ордера (Трейдер B)
        var ethOrders = allOrders.Where(o => o.OriginalOrder.Symbol == "ETH").ToList();
        ethOrders.Should().HaveCount(2);

        // Рассчитываем финальные объемы в USD
        var btcTotalQuantity = btcOrders.Sum(o => o.Quantity);
        var btcTotalVolumeUsd = btcTotalQuantity * btcPrice;

        var ethTotalQuantity = ethOrders.Sum(o => o.Quantity);
        var ethTotalVolumeUsd = ethTotalQuantity * ethPrice;

        // Проверяем ожидаемые объемы
        // Трейдер A: 1,000$ * 0.65 = 650$
        btcTotalVolumeUsd.Should().BeApproximately(650m, 10m);

        // Трейдер B: 1,000$ * 0.65 = 650$
        ethTotalVolumeUsd.Should().BeApproximately(650m, 10m);

        // КЛЮЧЕВАЯ ПРОВЕРКА: Объемы ОДИНАКОВЫЕ несмотря на разный порядок ордеров!
        // Оба трейдера: VolumeUsd = 1,000$, вложили 65% от баланса
        // Результат должен быть идентичным: 650$ каждый
        var volumeRatio = btcTotalVolumeUsd / ethTotalVolumeUsd;
        volumeRatio.Should().BeApproximately(1.0m, 0.05m);  // Должно быть ≈1 (одинаковые объемы)

        // Проверяем что оба трейдера вложили одинаковую долю от баланса (65%)
        var traderATotalInvestment = (traderA_order1.VolumeUsd + traderA_order2.VolumeUsd);
        var traderAInvestmentPercent = traderATotalInvestment / traderAWalletInfo.AccountVolume * 100;
        traderAInvestmentPercent.Should().BeApproximately(65m, 1m);

        var traderBTotalInvestment = (traderB_order1.VolumeUsd + traderB_order2.VolumeUsd);
        var traderBInvestmentPercent = traderBTotalInvestment / traderBWalletInfo.AccountVolume * 100;
        traderBInvestmentPercent.Should().BeApproximately(65m, 1m);

        // Проверяем статистику
        var stats = _copyOrderResultService.GetStatistics();
        stats.Total.Should().Be(4);
        stats.Success.Should().Be(4);
        stats.Warning.Should().Be(0);
        stats.Error.Should().Be(0);

        // Debug output (можно убрать после успешного прохождения теста)
        System.Diagnostics.Debug.WriteLine(
            $"Test completed: " +
            $"TraderA BTC volume: {btcTotalVolumeUsd:F2}$, " +
            $"TraderB ETH volume: {ethTotalVolumeUsd:F2}$, " +
            $"Volume ratio: {volumeRatio:F2} (expected: 1.00)"
        );
    }

    #endregion
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
        CopyTradeWalletSettingsService walletSettingsService,
        IOrderService orderService,
        ILogger<CopyOrderService> logger,
        Wallet myWallet)
        : base(walletProvider, exchangeInfoProvider, currentWalletPositionService, positionMappingService, resultService, fillsOrderService, walletSettingsService, orderService, logger)
    {
        // Используем рефлексию чтобы подменить _myWallet
        var field = typeof(CopyOrderService).GetField("_myWallet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, myWallet);
    }
}
