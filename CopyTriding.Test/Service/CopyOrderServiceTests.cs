using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CryptoExchange.Net.SharedApis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTriding.Test.Service;

public class CopyOrderServiceTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider;
    private readonly Mock<IExchangeInfoProvider> _exchangeInfoProvider;
    private readonly Mock<ILogger<CopyOrderService>> _logger;

    public CopyOrderServiceTests()
    {
        _walletInfoProvider = new Mock<IWalletInfoProvider>(MockBehavior.Strict);
        _exchangeInfoProvider = new Mock<IExchangeInfoProvider>(MockBehavior.Strict);
        _logger = new Mock<ILogger<CopyOrderService>>();
    }

    [Fact]
    public async Task CreateCopyOrder_CalculatesCorrectQuantity_WhenTraderUsesSmallPercentage()
    {
        // Arrange
        var traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var myWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var originalOrder = new OriginalOrder
        {
            OrderId = 12345,
            Wallet = traderWallet,
            Symbol = "BTC",
            Price = 50000M,      // Цена BTC = $50,000
            Quantity = 0.02M,    // 0.02 BTC
            Direction = Direction.Long,
            Leverage = 5M,
            Status = OrderStatus.Open,
            // VolumeUsd = 0.02 * 50000 = $1,000
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = traderWallet,
            AccountVolume = 20000M,  // Баланс трейдера $20,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = myWallet,
            AccountVolume = 2000M,   // Мой баланс $2,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "BTC", "USDC", "BTC/USDC", true)
        {
            QuantityDecimals = 3,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("BTC"))
            .ReturnsAsync(exchangeInfo);

        // Используем рефлексию для тестирования приватного метода
        var service = new TestableCopyOrderService(
            null!,  // OrderService не используется в CreateCopyOrder
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            null!,  // CurrentWalletPositionService не используется в CreateCopyOrder
            null!,  // PositionMappingService не используется в CreateCopyOrder
            _logger.Object,
            myWallet);

        // Act
        var copyOrder = await service.TestCreateCopyOrder(originalOrder);

        // Assert
        // orderRatio = $1,000 / $20,000 = 0.05 (5% от счета трейдера)
        copyOrder.OrderRatio.Should().Be(0.05M);

        // myVolumeUsd = $2,000 * 0.05 = $100
        copyOrder.VolumeUsd.Should().BeApproximately(100M, 0.01M);

        // myQuantity = $100 / $50,000 = 0.002 BTC
        copyOrder.Quantity.Should().Be(0.002M);

        // Проверяем что сохранены правильные значения балансов
        copyOrder.MyPE.Should().Be(2000M);
        copyOrder.AccountPE.Should().Be(20000M);
    }

    [Fact]
    public async Task CreateCopyOrder_CalculatesCorrectQuantity_WhenTraderUsesHighLeverage()
    {
        // Arrange
        var traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var myWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var originalOrder = new OriginalOrder
        {
            OrderId = 67890,
            Wallet = traderWallet,
            Symbol = "ETH",
            Price = 3000M,       // Цена ETH = $3,000
            Quantity = 10M,      // 10 ETH
            Direction = Direction.Short,
            Leverage = 10M,
            Status = OrderStatus.Open,
            // VolumeUsd = 10 * 3000 = $30,000
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = traderWallet,
            AccountVolume = 10000M,  // Баланс трейдера $10,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = myWallet,
            AccountVolume = 5000M,   // Мой баланс $5,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "ETH", "USDC", "ETH/USDC", true)
        {
            QuantityDecimals = 4,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("ETH"))
            .ReturnsAsync(exchangeInfo);

        var service = new TestableCopyOrderService(
            null!,  // OrderService не используется в CreateCopyOrder
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            null!,  // CurrentWalletPositionService не используется в CreateCopyOrder
            null!,  // PositionMappingService не используется в CreateCopyOrder
            _logger.Object,
            myWallet);

        // Act
        var copyOrder = await service.TestCreateCopyOrder(originalOrder);

        // Assert
        // orderRatio = $30,000 / $10,000 = 3.0 (300% от счета трейдера - использует плечо x10)
        copyOrder.OrderRatio.Should().Be(3.0M);

        // myVolumeUsd = $5,000 * 3.0 = $15,000 (та же пропорция от моего счета)
        copyOrder.VolumeUsd.Should().BeApproximately(15000M, 0.01M);

        // myQuantity = $15,000 / $3,000 = 5 ETH
        copyOrder.Quantity.Should().Be(5M);

        // Маржа трейдера = $30,000 / 10 = $3,000 (30% от его счета)
        // Моя маржа должна быть = $15,000 / 10 = $1,500 (30% от моего счета) ✓
    }

    [Fact]
    public async Task CreateCopyOrder_RoundsQuantityCorrectly_BasedOnExchangeDecimals()
    {
        // Arrange
        var traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var myWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var originalOrder = new OriginalOrder
        {
            OrderId = 11111,
            Wallet = traderWallet,
            Symbol = "SOL",
            Price = 100M,        // Цена SOL = $100
            Quantity = 50M,      // 50 SOL
            Direction = Direction.Long,
            Leverage = 3M,
            Status = OrderStatus.Open,
            // VolumeUsd = 50 * 100 = $5,000
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = traderWallet,
            AccountVolume = 25000M,  // Баланс трейдера $25,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = myWallet,
            AccountVolume = 3000M,   // Мой баланс $3,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "SOL", "USDC", "SOL/USDC", true)
        {
            QuantityDecimals = 2,  // Только 2 знака после запятой
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("SOL"))
            .ReturnsAsync(exchangeInfo);

        var service = new TestableCopyOrderService(
            null!,  // OrderService не используется в CreateCopyOrder
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            null!,  // CurrentWalletPositionService не используется в CreateCopyOrder
            null!,  // PositionMappingService не используется в CreateCopyOrder
            _logger.Object,
            myWallet);

        // Act
        var copyOrder = await service.TestCreateCopyOrder(originalOrder);

        // Assert
        // orderRatio = $5,000 / $25,000 = 0.2 (20%)
        copyOrder.OrderRatio.Should().Be(0.2M);

        // myVolumeUsd = $3,000 * 0.2 = $600
        // myQuantity (до округления) = $600 / $100 = 6.0 SOL
        // myQuantity (после округления до 2 знаков) = 6.00 SOL
        copyOrder.Quantity.Should().Be(6.00M);

        // Проверяем что округление произошло до 2 знаков
        var decimalPlaces = BitConverter.GetBytes(decimal.GetBits(copyOrder.Quantity)[3])[2];
        decimalPlaces.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateCopyOrder_HandlesTinyPositions_WithProperRounding()
    {
        // Arrange
        var traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var myWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var originalOrder = new OriginalOrder
        {
            OrderId = 99999,
            Wallet = traderWallet,
            Symbol = "BTC",
            Price = 60000M,      // Цена BTC = $60,000
            Quantity = 0.5M,     // 0.5 BTC
            Direction = Direction.Long,
            Leverage = 2M,
            Status = OrderStatus.Open,
            // VolumeUsd = 0.5 * 60000 = $30,000
        };

        var traderWalletInfo = new WalletInfoModel
        {
            Wallet = traderWallet,
            AccountVolume = 100000M,  // Баланс трейдера $100,000
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        var myWalletInfo = new WalletInfoModel
        {
            Wallet = myWallet,
            AccountVolume = 500M,     // Мой баланс всего $500
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>(),
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(traderWallet, true))
            .ReturnsAsync(traderWalletInfo);

        _walletInfoProvider
            .Setup(x => x.GetInfo(myWallet, false))
            .ReturnsAsync(myWalletInfo);

        var exchangeInfo = new SharedFuturesSymbol(TradingMode.PerpetualLinear, "BTC", "USDC", "BTC/USDC", true)
        {
            QuantityDecimals = 5,
        };

        _exchangeInfoProvider
            .Setup(x => x.GetExchangeInfo("BTC"))
            .ReturnsAsync(exchangeInfo);

        var service = new TestableCopyOrderService(
            null!,  // OrderService не используется в CreateCopyOrder
            _walletInfoProvider.Object,
            _exchangeInfoProvider.Object,
            null!,  // CurrentWalletPositionService не используется в CreateCopyOrder
            null!,  // PositionMappingService не используется в CreateCopyOrder
            _logger.Object,
            myWallet);

        // Act
        var copyOrder = await service.TestCreateCopyOrder(originalOrder);

        // Assert
        // orderRatio = $30,000 / $100,000 = 0.3 (30%)
        copyOrder.OrderRatio.Should().Be(0.3M);

        // myVolumeUsd = $500 * 0.3 = $150
        copyOrder.VolumeUsd.Should().BeApproximately(150M, 0.01M);

        // myQuantity = $150 / $60,000 = 0.0025 BTC
        copyOrder.Quantity.Should().Be(0.0025M);
    }
}

// Тестовая обертка для доступа к приватным методам
public class TestableCopyOrderService : CopyOrderService
{
    private readonly Wallet _testMyWallet;

    public TestableCopyOrderService(
        OrderService orderService,
        IWalletInfoProvider walletProvider,
        IExchangeInfoProvider exchangeInfoProvider,
        CurrentWalletPositionService currentWalletPositionService,
        PositionMappingService positionMappingService,
        ILogger<CopyOrderService> logger,
        Wallet myWallet)
        : base(orderService, walletProvider, exchangeInfoProvider, currentWalletPositionService, positionMappingService, logger)
    {
        _testMyWallet = myWallet;
        // Используем рефлексию чтобы подменить _myWallet
        var field = typeof(CopyOrderService).GetField("_myWallet",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(this, myWallet);
    }

    public async Task<CopyOrderV2> TestCreateCopyOrder(OriginalOrder order)
    {
        var method = typeof(CopyOrderService).GetMethod("CreateCopyOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<CopyOrderV2>)method.Invoke(this, new object[] { order, OrderSubType.Open });
        return await task;
    }
}
