using CopyTrading.BlazorUI.Services;
using CopyTrading.DataEvents;
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

[Collection("Sequential")]
public class CopyOrderServiceTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider;
    private readonly Mock<IExchangeInfoProvider> _exchangeInfoProvider;
    private readonly Mock<CopyTradeWalletSettingsService> _walletSettingsServiceMock;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly PositionMappingService _positionMappingService;
    private readonly CopyOrderResultService _copyOrderResultService;
    private CopyOrderStorageService _storageService;

    // Loggers
    private readonly Mock<ILogger<CopyOrderService>> _logger;
    private readonly Mock<ILogger<CopyOrderResultService>> _resultLogger;
    private readonly Mock<ILogger<CopyOrderStorageService>> _storageLogger;
    private readonly Mock<ILogger<PositionMappingService>> _mappingLogger;
    private readonly Mock<ILogger<CurrentWalletPositionService>> _positionLogger;
    private readonly Mock<ILogger<TradeService>> _tradeServiceLogger;
    private readonly Mock<ILogger<OrderService>> _orderServiceLogger;

    // Mocks для TradeService и OrderService
    private readonly FillsOrderService _fillsOrderService;
    private readonly Mock<OrdersTradesSubscriber> _orderProviderMock;
    private readonly Mock<OrderBookSubscriber> _orderBookProviderMock;
    private readonly Mock<TradeRepositoryInflux> _tradeRepositoryInfluxMock;
    private readonly Mock<TradeRepositoreySQL> _tradeRepositorySQLMock;
    private readonly Mock<RealtimeUpdateService> _realtimeUpdateServiceMock;
    private readonly Mock<Repository.Influx.OrderRepository> _orderRepositoryInfluxMock;
    private readonly Mock<Repository.SQLite.OrderRepository> _orderRepositorySQLiteMock;

    private readonly Wallet _traderWallet = new("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
    private readonly Wallet _myWallet = new("0x1234567890abcdef1234567890abcdef12345678");

    public CopyOrderServiceTests()
    {
        _walletInfoProvider = new Mock<IWalletInfoProvider>(MockBehavior.Loose);
        _exchangeInfoProvider = new Mock<IExchangeInfoProvider>(MockBehavior.Loose);

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

        // Инициализация моков для TradeService и OrderService
        var ordersProviderMock = new Mock<Providers.Hyperliquid.Providers.OrdersProvider>(MockBehavior.Loose, Mock.Of<ILogger<Providers.Hyperliquid.Providers.OrdersProvider>>());
        _orderProviderMock = new Mock<OrdersTradesSubscriber>(
            MockBehavior.Loose,
            Mock.Of<ILogger<OrdersTradesSubscriber>>(),
            ordersProviderMock.Object,
            _fillsOrderService);
        _orderBookProviderMock = new Mock<OrderBookSubscriber>(MockBehavior.Loose, Mock.Of<ILogger<OrderBookSubscriber>>());
        _tradeRepositoryInfluxMock = new Mock<TradeRepositoryInflux>(MockBehavior.Loose, Mock.Of<ILogger<TradeRepositoryInflux>>());
        _tradeRepositorySQLMock = new Mock<TradeRepositoreySQL>(MockBehavior.Loose, Mock.Of<ILogger<TradeRepositoreySQL>>());
        _realtimeUpdateServiceMock = new Mock<RealtimeUpdateService>(MockBehavior.Loose,
            Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<BlazorUI.Hubs.CopyTradingHub>>(),
            Mock.Of<ILogger<RealtimeUpdateService>>());
        _orderRepositoryInfluxMock = new Mock<Repository.Influx.OrderRepository>(MockBehavior.Loose, Mock.Of<ILogger<Repository.Influx.OrderRepository>>());
        _orderRepositorySQLiteMock = new Mock<Repository.SQLite.OrderRepository>(MockBehavior.Loose, Mock.Of<ILogger<Repository.SQLite.OrderRepository>>());

        // Создаем реальные сервисы
        _copyOrderResultService = new CopyOrderResultService(_resultLogger.Object);
        _storageService = new CopyOrderStorageService(_storageLogger.Object);
        _positionMappingService = new PositionMappingService(_mappingLogger.Object);

        // Создаем реальный FillsOrderService и сохраняем его в поле класса
        _fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());

        _currentWalletPositionService = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _fillsOrderService,
            _positionLogger.Object);
    }

    private void CreateServices()
    {
        // Создаем TradeService
        var tradeService = new TradeService(
            _orderProviderMock.Object,
            _orderBookProviderMock.Object,
            _walletInfoProvider.Object,
            _tradeRepositoryInfluxMock.Object,
            _tradeRepositorySQLMock.Object,
            _fillsOrderService,
            _currentWalletPositionService,
            _realtimeUpdateServiceMock.Object,
            _tradeServiceLogger.Object);

        // Создаем CopyOrderService
        var copyOrderService = new CopyOrderService(
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            _currentWalletPositionService,
            _positionMappingService,
            _copyOrderResultService,
            _fillsOrderService,
            _walletSettingsServiceMock.Object,
            _logger.Object);

        // Используем рефлексию чтобы установить _myWallet
        var field = typeof(CopyOrderService).GetField("_myWallet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(copyOrderService, _myWallet);

        // Создаем OrderService (подписывается на DataBusEvents.NewOrders)
        var orderService = new OrderService(
            _orderProviderMock.Object,
            _walletInfoProvider.Object,
            _orderRepositoryInfluxMock.Object,
            _exchangeInfoProvider.Object,
            _orderRepositorySQLiteMock.Object,
            _tradeRepositorySQLMock.Object,
            _currentWalletPositionService,
            _fillsOrderService,
            copyOrderService,
            _realtimeUpdateServiceMock.Object,
            _orderServiceLogger.Object);
    }

    private void ResetServices()
    {
        // Очищаем все данные
        _positionMappingService.ClearAllMappings();
        _storageService.ClearAllOrders();
        _copyOrderResultService.ClearAllResults();
        _currentWalletPositionService.ClearAllSnapshots();

        // Очищаем все подписки на события
        DataBusEvents.ClearAllSubscriptions();

        // Сбрасываем Mock объекты
        _walletInfoProvider.Reset();
        _exchangeInfoProvider.Reset();

        // Пересоздаем _storageService
        _storageService = new CopyOrderStorageService(_storageLogger.Object);
    }

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

    [Fact]
    public async Task CreateCopyOrder_CalculatesCorrectQuantity_WhenTraderUsesSmallPercentage()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var originalOrder = new OriginalOrder
        {
            OrderId = 12345,
            Wallet = _traderWallet,
            Symbol = "BTC",
            Price = 50000M,
            Quantity = 0.02M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 20000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 2000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "BTC", "USDC", "BTC/USDC", true)
        {
            QuantityDecimals = 3,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("BTC"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 2000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем сервисы
        CreateServices();

        // Act - отправляем ордер через DataBusEvents
        DataBusEvents.NewOrders?.Invoke([originalOrder]);

        // Даем время на обработку
        await Task.Delay(100);

        // Assert
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(1);

        var copyOrder = allOrders.First();
        copyOrder.OrderRatio.Should().Be(0.05M);
        copyOrder.VolumeUsd.Should().BeApproximately(100M, 0.01M);
        copyOrder.Quantity.Should().Be(0.002M);
        copyOrder.MyPE.Should().Be(2000M);
        copyOrder.AccountPE.Should().Be(20000M);
    }

    [Fact]
    public async Task CreateCopyOrder_CalculatesCorrectQuantity_WhenTraderUsesHighLeverage()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var originalOrder = new OriginalOrder
        {
            OrderId = 67890,
            Wallet = _traderWallet,
            Symbol = "ETH",
            Price = 3000M,
            Quantity = 10M,
            Direction = Direction.Short,
            Leverage = 10M,
            Status = OrderStatus.Open,
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 5000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "ETH", "USDC", "ETH/USDC", true)
        {
            QuantityDecimals = 4,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("ETH"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 5000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем сервисы
        CreateServices();

        // Act
        DataBusEvents.NewOrders?.Invoke([originalOrder]);
        await Task.Delay(100);

        // Assert
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(1);

        var copyOrder = allOrders.First();
        copyOrder.OrderRatio.Should().Be(3.0M);
        copyOrder.VolumeUsd.Should().BeApproximately(15000M, 0.01M);
        copyOrder.Quantity.Should().Be(5M);
    }

    [Fact]
    public async Task CreateCopyOrder_RoundsQuantityCorrectly_BasedOnExchangeDecimals()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var originalOrder = new OriginalOrder
        {
            OrderId = 11111,
            Wallet = _traderWallet,
            Symbol = "SOL",
            Price = 100M,
            Quantity = 50M,
            Direction = Direction.Long,
            Leverage = 3M,
            Status = OrderStatus.Open,
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 25000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 3000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "SOL", "USDC", "SOL/USDC", true)
        {
            QuantityDecimals = 2,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("SOL"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 3000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем сервисы
        CreateServices();

        // Act
        DataBusEvents.NewOrders?.Invoke([originalOrder]);
        await Task.Delay(100);

        // Assert
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(1);

        var copyOrder = allOrders.First();
        copyOrder.OrderRatio.Should().Be(0.2M);
        copyOrder.Quantity.Should().Be(6.00M);
    }

    [Fact]
    public async Task CreateCopyOrder_HandlesTinyPositions_WithProperRounding()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var originalOrder = new OriginalOrder
        {
            OrderId = 99999,
            Wallet = _traderWallet,
            Symbol = "BTC",
            Price = 60000M,
            Quantity = 0.5M,
            Direction = Direction.Long,
            Leverage = 2M,
            Status = OrderStatus.Open,
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 100000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 500M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "BTC", "USDC", "BTC/USDC", true)
        {
            QuantityDecimals = 5,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("BTC"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 500m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем сервисы
        CreateServices();

        // Act
        DataBusEvents.NewOrders?.Invoke([originalOrder]);
        await Task.Delay(100);

        // Assert
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(1);

        var copyOrder = allOrders.First();
        copyOrder.OrderRatio.Should().Be(0.3M);
        copyOrder.VolumeUsd.Should().BeApproximately(150M, 0.01M);
        copyOrder.Quantity.Should().Be(0.0025M);
    }

    [Fact]
    public async Task CreateCopyOrder_MultiplePendingOrders_ShouldCreateAllCopyOrders()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 1000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        // Настраиваем моки для всех возможных вызовов GetInfo
        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, false))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, true))
            .ReturnsAsync(myWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "TEST", "USDC", "TEST/USDC", true)
        {
            QuantityDecimals = 2,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("TEST"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 1000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем 4 оригинальных ордера
        var order1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 2M,
            Quantity = 50M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order3 = new OriginalOrder
        {
            OrderId = 3,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 4M,
            Quantity = 25M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order4 = new OriginalOrder
        {
            OrderId = 4,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 5M,
            Quantity = 170M,
            Direction = Direction.Short,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        // Создаем сервисы
        CreateServices();

        // Инициализируем пустой snapshot для трейдера (позиций еще нет)
        InitializeEmptyWalletSnapshot(_traderWallet);
        // Инициализируем пустой snapshot для моего кошелька
        InitializeEmptyWalletSnapshot(_myWallet);

        // Act - отправляем первый ордер через DataBusEvents
        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(100);

        // Сразу после размещения полностью исполняем первый ордер
        var trade1 = new OriginalTrade
        {
            TradeId = 1001,
            OrderId = order1.OrderId,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            TimeStamp = DateTime.UtcNow,
            IsFuture = true,
        };

        // Обновляем мок провайдера, чтобы он вернул позицию после исполнения трейда
        var traderWalletInfoWithPosition = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>
            {
                ["TEST"] = new Position
                {
                    Symbol = "TEST",
                    Quantity = 100M, // Положительное значение = Long
                    AverageEntryPrice = 1M,
                    Leverage = 5,
                    VolumeUsd = 100M,
                    MarginUsage = 20M
                }
            },
            TimeStamp = DateTime.UtcNow
        };

        // Используем callback вместо It.IsAny для избежания ошибки с expression tree
        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfoWithPosition);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, false))
            .ReturnsAsync(traderWalletInfoWithPosition);

        // Отправляем трейд для исполнения ордера (order1 уже Open)
        DataBusEvents.NewTrades?.Invoke(([trade1], false));
        await Task.Delay(50);

        // Обновляем статус ордера на Filled (в реальности это сделает FillsOrderService)
        order1.Status = OrderStatus.Filled;

        // Проверяем что CurrentWalletPositionService обновил snapshot после исполнения первого ордера
        var traderSnapshot = await _currentWalletPositionService.GetSnapshot(_traderWallet);
        traderSnapshot.Should().NotBeNull("snapshot должен существовать после исполнения трейда");
        traderSnapshot.Positions.Should().HaveCount(1, "должна быть одна позиция");

        var position = traderSnapshot.Positions.First();
        position.Symbol.Should().Be("TEST");
        position.Quantity.Should().Be(100M, "позиция должна быть 100 монет после исполнения order1");
        position.Direction.Should().Be(Direction.Long);

        // Отправляем остальные ордера (они остаются pending)
        DataBusEvents.NewOrders?.Invoke([order2]);
        await Task.Delay(100);

        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(100);

        // Теперь можем отправить order4 - благодаря учету pending ордеров он должен быть Decrease, а не Flip
        DataBusEvents.NewOrders?.Invoke([order4]);
        await Task.Delay(100);

        // Assert - проверяем основные результаты
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(4, "должно быть создано 4 копируемых ордера");

        // Проверяем что все ордера созданы
        allOrders.Should().Contain(o => o.OriginalOrderId == 1, "order1 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 2, "order2 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 3, "order3 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 4, "order4 должен быть скопирован");

        // Проверяем направления
        var copyOrder1 = allOrders.First(o => o.OriginalOrderId == 1);
        copyOrder1.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder2 = allOrders.First(o => o.OriginalOrderId == 2);
        copyOrder2.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder3 = allOrders.First(o => o.OriginalOrderId == 3);
        copyOrder3.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder4 = allOrders.First(o => o.OriginalOrderId == 4);
        copyOrder4.OriginalOrder.Direction.Should().Be(Direction.Short);
        copyOrder4.OrderSubType.Should().Be(OrderSubType.Decrease, "order4 должен быть Decrease т.к. учитываются pending ордера");
    }

    [Fact]
    public async Task CreateCopyOrder_MultiplePendingOrders_ShouldCreateAllCopyOrders_LastCloseOrder()
    {
        // Arrange
        ResetServices();
        InitializeEmptyWalletSnapshot(_traderWallet);

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = _myWallet,
            AccountVolume = 1000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        // Настраиваем моки для всех возможных вызовов GetInfo
        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, false))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, true))
            .ReturnsAsync(myWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "TEST", "USDC", "TEST/USDC", true)
        {
            QuantityDecimals = 2,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("TEST"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 1000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем 4 оригинальных ордера
        var order1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 2M,
            Quantity = 50M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order3 = new OriginalOrder
        {
            OrderId = 3,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 4M,
            Quantity = 25M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order4 = new OriginalOrder
        {
            OrderId = 4,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 5M,
            Quantity = 175M,
            Direction = Direction.Short,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        // Создаем сервисы
        CreateServices();

        // Инициализируем пустой snapshot для трейдера (позиций еще нет)
        InitializeEmptyWalletSnapshot(_traderWallet);
        // Инициализируем пустой snapshot для моего кошелька
        InitializeEmptyWalletSnapshot(_myWallet);

        // Act - отправляем первый ордер через DataBusEvents
        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(100);

        // Сразу после размещения полностью исполняем первый ордер
        var trade1 = new OriginalTrade
        {
            TradeId = 1001,
            OrderId = order1.OrderId,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            TimeStamp = DateTime.UtcNow,
            IsFuture = true,
        };

        // Обновляем мок провайдера, чтобы он вернул позицию после исполнения трейда
        var traderWalletInfoWithPosition = new WalletInfoModel
        {
            Wallet = _traderWallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>
            {
                ["TEST"] = new Position
                {
                    Symbol = "TEST",
                    Quantity = 100M, // Положительное значение = Long
                    AverageEntryPrice = 1M,
                    Leverage = 5,
                    VolumeUsd = 100M,
                    MarginUsage = 20M
                }
            },
            TimeStamp = DateTime.UtcNow
        };

        // Используем callback вместо It.IsAny для избежания ошибки с expression tree
        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, true))
            .ReturnsAsync(traderWalletInfoWithPosition);

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, false))
            .ReturnsAsync(traderWalletInfoWithPosition);

        // Отправляем трейд для исполнения ордера (order1 уже Open)
        DataBusEvents.NewTrades?.Invoke(([trade1], false));
        await Task.Delay(50);

        // Обновляем статус ордера на Filled (в реальности это сделает FillsOrderService)
        order1.Status = OrderStatus.Filled;

        // Проверяем что CurrentWalletPositionService обновил snapshot после исполнения первого ордера
        var traderSnapshot = await _currentWalletPositionService.GetSnapshot(_traderWallet);
        traderSnapshot.Should().NotBeNull("snapshot должен существовать после исполнения трейда");
        traderSnapshot.Positions.Should().HaveCount(1, "должна быть одна позиция");

        var position = traderSnapshot.Positions.First();
        position.Symbol.Should().Be("TEST");
        position.Quantity.Should().Be(100M, "позиция должна быть 100 монет после исполнения order1");
        position.Direction.Should().Be(Direction.Long);

        // Отправляем остальные ордера (они остаются pending)
        DataBusEvents.NewOrders?.Invoke([order2]);
        await Task.Delay(100);

        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(100);

        // Теперь можем отправить order4 - благодаря учету pending ордеров он должен быть Decrease, а не Flip
        DataBusEvents.NewOrders?.Invoke([order4]);
        await Task.Delay(100);

        // Assert - проверяем основные результаты
        var allOrders = _storageService.GetAllOrders();
        allOrders.Should().HaveCount(4, "должно быть создано 4 копируемых ордера");

        // Проверяем что все ордера созданы
        allOrders.Should().Contain(o => o.OriginalOrderId == 1, "order1 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 2, "order2 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 3, "order3 должен быть скопирован");
        allOrders.Should().Contain(o => o.OriginalOrderId == 4, "order4 должен быть скопирован");

        // Проверяем направления
        var copyOrder1 = allOrders.First(o => o.OriginalOrderId == 1);
        copyOrder1.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder2 = allOrders.First(o => o.OriginalOrderId == 2);
        copyOrder2.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder3 = allOrders.First(o => o.OriginalOrderId == 3);
        copyOrder3.OriginalOrder.Direction.Should().Be(Direction.Long);

        var copyOrder4 = allOrders.First(o => o.OriginalOrderId == 4);
        copyOrder4.OriginalOrder.Direction.Should().Be(Direction.Short);
        copyOrder4.OrderSubType.Should().Be(OrderSubType.Close, "order4 должен быть Close т.к. учитываются pending ордера");
    }

    [Fact]
    public async Task CreateCopyOrder_MultiplePendingOrders_DecreaseBecomesCloseAfterCancel()
    {
        // Arrange
        ResetServices();

        // Используем переменные для хранения текущего состояния кошельков
        // чтобы моки возвращали актуальное состояние
        var traderWalletPositions = new Dictionary<string, Position>();
        var myWalletPositions = new Dictionary<string, Position>();

        _walletInfoProvider
            .Setup(x => x.GetInfo(_traderWallet, It.IsAny<bool>()))
            .ReturnsAsync(() => new WalletInfoModel
            {
                Wallet = _traderWallet,
                AccountVolume = 10000M,
                TotalMarginUsed = 0,
                Positions = new Dictionary<string, Position>(traderWalletPositions),
                TimeStamp = DateTime.UtcNow
            });

        _walletInfoProvider
            .Setup(x => x.GetInfo(_myWallet, It.IsAny<bool>()))
            .ReturnsAsync(() => new WalletInfoModel
            {
                Wallet = _myWallet,
                AccountVolume = 1000M,
                TotalMarginUsed = 0,
                Positions = new Dictionary<string, Position>(myWalletPositions),
                TimeStamp = DateTime.UtcNow
            });

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "TEST", "USDC", "TEST/USDC", true)
        {
            QuantityDecimals = 2,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("TEST"))
            .ReturnsAsync(exchangeInfo);

        var walletSettings = new CopyTradeWalletSettings
        {
            Wallet = _traderWallet,
            VolumeUsd = 1000m,
            CopyKoef = 1.0m
        };

        _walletSettingsServiceMock
            .Setup(x => x.Get(_traderWallet))
            .ReturnsAsync(walletSettings);

        // Создаем сервисы
        CreateServices();

        // Инициализируем пустой snapshot для трейдера (позиций еще нет)
        InitializeEmptyWalletSnapshot(_traderWallet);
        // Инициализируем пустой snapshot для моего кошелька
        InitializeEmptyWalletSnapshot(_myWallet);

        // Создаем 4 оригинальных ордера
        var order1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 2M,
            Quantity = 50M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order3 = new OriginalOrder
        {
            OrderId = 3,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 4M,
            Quantity = 25M,
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        var order4 = new OriginalOrder
        {
            OrderId = 4,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 5M,
            Quantity = 150M, // Изменено с 170/175 на 150
            Direction = Direction.Short,
            Leverage = 5M,
            Status = OrderStatus.Open,
        };

        // Создаем сервисы
        CreateServices();

        // Инициализируем пустой snapshot для трейдера (позиций еще нет)
        InitializeEmptyWalletSnapshot(_traderWallet);
        // Инициализируем пустой snapshot для моего кошелька
        InitializeEmptyWalletSnapshot(_myWallet);

        // Act - отправляем первый ордер через DataBusEvents
        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(100);

        // Сразу после размещения полностью исполняем первый ордер
        var trade1 = new OriginalTrade
        {
            TradeId = 1001,
            OrderId = order1.OrderId,
            Wallet = _traderWallet,
            Symbol = "TEST",
            Price = 1M,
            Quantity = 100M,
            Direction = Direction.Long,
            TimeStamp = DateTime.UtcNow,
            IsFuture = true,
        };

        // Отправляем трейд для исполнения ордера (order1 уже Open)
        DataBusEvents.NewTrades?.Invoke(([trade1], false));
        await Task.Delay(50);

        // Обновляем статус ордера на Filled и отправляем событие чтобы FillsOrderService узнал об изменении
        order1.Status = OrderStatus.Filled;
        DataBusEvents.NewOrders?.Invoke([order1]);
        await Task.Delay(50);

        // Вручную обновляем snapshot после исполнения трейда
        // (в реальности snapshot обновляется через AddTrade в CurrentWalletPositionService)
        var traderSnapshot = await _currentWalletPositionService.GetSnapshot(_traderWallet);
        traderSnapshot.Positions.Add(new Position
        {
            Symbol = "TEST",
            Quantity = 100M, // Положительное значение = Long
            AverageEntryPrice = 1M,
            Leverage = 5,
            VolumeUsd = 100M,
            MarginUsage = 20M
        });

        // Проверяем что snapshot содержит позицию
        var snapshotAfterOrder1 = await _currentWalletPositionService.GetSnapshot(_traderWallet);
        snapshotAfterOrder1.Positions.Should().ContainSingle(p => p.Symbol == "TEST" && p.Quantity == 100,
            "после исполнения order1 должна быть позиция Long 100");

        // Отправляем остальные ордера (они остаются pending)
        DataBusEvents.NewOrders?.Invoke([order2]);
        await Task.Delay(100);

        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(100);

        DataBusEvents.NewOrders?.Invoke([order4]);
        await Task.Delay(100);

        // Assert часть 1 - проверяем что order4 изначально Decrease
        // Сначала проверим что есть в FillsOrderService для диагностики
        var allPending = _fillsOrderService.GetPendingOrdersByWalletAndSymbol(_traderWallet, "TEST");
        // Должно быть: order2, order3, order4 (order1 filled)

        // Проверяем SubType у OriginalOrder через FillsOrderService
        var orderFills4Before = _fillsOrderService.GetOrderFillsByOrderId(4);
        orderFills4Before.Should().NotBeNull("order4 должен быть в FillsOrderService");
        orderFills4Before!.OriginalOrder.SubType.Should().Be(OrderSubType.Decrease,
            "order4 должен быть Decrease т.к. 100 (real) + 50 (order2) + 25 (order3) - 150 (order4) = 25 (остается Long)");

        // Отменяем order3
        order3.Status = OrderStatus.Canceled;
        DataBusEvents.NewOrders?.Invoke([order3]);
        await Task.Delay(100);

        // Assert часть 2 - проверяем что order3 был отменен
        var openOrdersAfterCancel = _fillsOrderService.GetOpenOrdersByWallet(_traderWallet);
        openOrdersAfterCancel.Should().HaveCount(2, "после отмены order3 должно остаться только 2 открытых ордера (order2 и order4)");
        openOrdersAfterCancel.Should().NotContain(o => o.OriginalOrder.OrderId == 3, "order3 не должен быть в открытых ордерах");

        // Assert часть 3 - проверяем что SubType для order4 автоматически изменился с Decrease на Close
        // После отмены order3: реальная позиция = 100, pending = order2 (50) + order4 (-150)
        // Потенциальная позиция = 100 + 50 - 150 = 0 → order4 должен стать Close
        var orderFills4After = _fillsOrderService.GetOrderFillsByOrderId(4);
        orderFills4After.Should().NotBeNull("order4 должен быть в FillsOrderService");
        orderFills4After!.OriginalOrder.SubType.Should().Be(OrderSubType.Close,
            "SubType для order4 должен автоматически измениться с Decrease на Close после отмены order3, " +
            "т.к. потенциальная позиция стала 100 + 50 - 150 = 0");

        // Дополнительная проверка: order2 должен остаться без изменений
        var orderFills2After = _fillsOrderService.GetOrderFillsByOrderId(2);
        orderFills2After.Should().NotBeNull("order2 должен быть в FillsOrderService");
        orderFills2After!.OriginalOrder.SubType.Should().Be(OrderSubType.Increase,
            "SubType для order2 не должен измениться, он остается Increase");
    }
}
