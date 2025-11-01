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

        // Snapshot ПОСЛЕ increase показывает 130 (80 + 50)
        var snapshotAfterIncrease2 = new WalletPositionsSnapshot
        {
            Wallet = _traderWallet,
            TimeStamp = DateTime.UtcNow,
            Positions =
            [
                new Position
                {
                    Symbol = symbol,
                    Quantity = 130m, // 80 + 50 = 130
                    AverageEntryPrice = 300m,
                    Leverage = 5
                }
            ]
        };

        _currentWalletPositionService.UpdateSnapshot(snapshotAfterIncrease2);

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
