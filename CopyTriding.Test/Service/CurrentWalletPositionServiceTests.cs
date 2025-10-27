using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTriding.Test.Service;

public class CurrentWalletPositionServiceTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider = new(MockBehavior.Strict);
    private readonly Mock<ILogger<CurrentWalletPositionService>> _logger = new(MockBehavior.Loose);

    [Fact]
    public async Task CurrentLongPosInitialize_Add_Succsess()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot()
        {
            Wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00"),
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
            {
                new Position()
                {
                    Symbol = "BTC",
                    AverageEntryPrice = 50000,
                    Leverage = 10,
                    LiquidationPrice = 45000,
                    MarginUsage = 1000,
                    Quantity = 0.2M,
                    VolumeUsd = 10000,
                }
            }
        };

        var shortTrade = new OriginalTrade()
        {
            Wallet = snapshot.Wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 0.1M,
        };

        var longTrade = new OriginalTrade()
        {
            Wallet = snapshot.Wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 0.1M,
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var subType = await service.AddTrade(shortTrade);
        var currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        var currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Decrease);
        currentPositions.Quantity.Should().Be(0.1M);


        // Act
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Increase);
        currentPositions.Quantity.Should().Be(0.2M);


        // Act
        subType = await service.AddTrade(shortTrade);
        subType = await service.AddTrade(shortTrade);
        currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.FirstOrDefault(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Close);
        currentPositions.Should().BeNull();
    }

    [Fact]
    public async Task CurrentShortPosInitialize_Add_Succsess()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot()
        {
            Wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00"),
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
            {
                new Position()
                {
                    Symbol = "BTC",
                    AverageEntryPrice = 50000,
                    Leverage = 10,
                    LiquidationPrice = 45000,
                    MarginUsage = 1000,
                    Quantity = -0.2M,
                    VolumeUsd = 10000,
                }
            }
        };

        var shortTrade = new OriginalTrade()
        {
            Wallet = snapshot.Wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 0.1M,
        };

        var longTrade = new OriginalTrade()
        {
            Wallet = snapshot.Wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 0.1M,
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var subType = await service.AddTrade(shortTrade);
        var currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        var currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Increase);
        currentPositions.Quantity.Should().Be(-0.3M);


        // Act
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Decrease);
        currentPositions.Quantity.Should().Be(-0.2M);


        // Act
        subType = await service.AddTrade(longTrade);
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(snapshot.Wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.FirstOrDefault(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Close);
        currentPositions.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnOpen_WhenNoPositionExists()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        var order = new CopyTrading.Models.Models.Orders.OriginalOrder
        {
            Wallet = wallet,
            Symbol = "ETH",
            Direction = Direction.Long,
            Quantity = 10M,
            Price = 3000M,
            Leverage = 5M,
            Status = CopyTrading.Models.Models.Enums.Order.OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = await service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Open);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnIncrease_WhenSameDirectionPositionExists()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "SOL",
                    Quantity = 100M, // Positive = Long
                    AverageEntryPrice = 100M,
                    VolumeUsd = 10000M,
                    Leverage = 3
                }
            }
        };

        var order = new CopyTrading.Models.Models.Orders.OriginalOrder
        {
            Wallet = wallet,
            Symbol = "SOL",
            Direction = Direction.Long,
            Quantity = 50M,
            Price = 100M,
            Leverage = 3M,
            Status = CopyTrading.Models.Models.Enums.Order.OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = await service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnDecrease_WhenOppositeDirectionWithSmallerQuantity()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "AVAX",
                    Quantity = 100M,
                    AverageEntryPrice = 50M,
                    VolumeUsd = 5000M,
                    Leverage = 2
                }
            }
        };

        var order = new OriginalOrder
        {
            Wallet = wallet,
            Symbol = "AVAX",
            Direction = Direction.Short,
            Quantity = 30M, // Меньше чем открытая позиция
            Price = 50M,
            Leverage = 2M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = await service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnClose_WhenOppositeDirectionWithExactQuantity()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "LINK",
                    Quantity = -50M, // Short позиция отрицательная
                    AverageEntryPrice = 20M,
                    VolumeUsd = 1000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            Wallet = wallet,
            Symbol = "LINK",
            Direction = Direction.Long,
            Quantity = 50M, // Точно закрывает позицию
            Price = 20M,
            Leverage = 5M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = await service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Close);
    }

    [Fact]
    public async Task AddTrade_ShouldReturnOpen_AndAddPosition_WhenNoPositionExists()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var walletInfo = new WalletInfoModel
        {
            Wallet = wallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0,
            Positions = new Dictionary<string, Position>
            {
                ["BNB"] = new Position
                {
                    Symbol = "BNB",
                    Quantity = 10M, // Positive = Long
                    AverageEntryPrice = 300M,
                    VolumeUsd = 3000M,
                    Leverage = 2
                }
            },
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, false))
            .ReturnsAsync(walletInfo);

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        var trade = new OriginalTrade
        {
            Wallet = wallet,
            Symbol = "BNB",
            Direction = Direction.Long,
            Quantity = 10M,
            Price = 300M
            // VolumeUsd вычисляется автоматически = Quantity * Price
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var subType = await service.AddTrade(trade);

        // Assert
        subType.Should().Be(OrderSubType.Open);
        var currentSnapshot = await service.GetSnapshot(wallet);
        currentSnapshot.Positions.Should().HaveCount(1);
        currentSnapshot.Positions.First().Symbol.Should().Be("BNB");
    }

    [Fact]
    public async Task AddTrade_ShouldUpdateAveragePrice_WhenIncreasingPosition()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "ADA",
                    Quantity = 1000M, // Positive = Long
                    AverageEntryPrice = 0.5M,
                    VolumeUsd = 500M,
                    Leverage = 2
                }
            }
        };

        // Добавляем трейд по более высокой цене
        var trade = new OriginalTrade
        {
            Wallet = wallet,
            Symbol = "ADA",
            Direction = Direction.Long,
            Quantity = 1000M,
            Price = 0.6M
            // VolumeUsd = 600M вычисляется автоматически
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        await service.AddTrade(trade);

        // Assert
        var currentSnapshot = await service.GetSnapshot(wallet);
        var position = currentSnapshot.Positions.First(p => p.Symbol == "ADA");

        // Новая средняя цена = (500 + 600) / (1000 + 1000) = 1100 / 2000 = 0.55
        position.AverageEntryPrice.Should().BeApproximately(0.55M, 0.001M);
        position.Quantity.Should().Be(2000M);
    }

    [Fact]
    public async Task TryGetLeverage_ShouldReturnLeverage_WhenPositionExists()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "DOT",
                    Quantity = 100M, // Positive = Long
                    AverageEntryPrice = 10M,
                    VolumeUsd = 1000M,
                    Leverage = 7
                }
            }
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var leverage = await service.TryGetLeverage(wallet, "DOT");

        // Assert
        leverage.Should().NotBeNull();
        leverage.Should().Be(7);
    }

    [Fact]
    public async Task TryGetLeverage_ShouldReturnNull_WhenPositionDoesNotExist()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var leverage = await service.TryGetLeverage(wallet, "XRP");

        // Assert
        leverage.Should().BeNull();
    }

    [Fact]
    public async Task GetSnapshot_ShouldReturnCachedSnapshot_WhenAlreadyInitialized()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var originalTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = originalTimestamp,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "DOGE",
                    Quantity = 10000M, // Positive = Long
                    AverageEntryPrice = 0.1M,
                    VolumeUsd = 1000M,
                    Leverage = 3
                }
            }
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var retrievedSnapshot = await service.GetSnapshot(wallet);

        // Assert
        retrievedSnapshot.TimeStamp.Should().Be(originalTimestamp);
        retrievedSnapshot.Positions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSnapshot_ShouldFetchFromProvider_WhenNotInitialized()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var walletInfo = new WalletInfoModel
        {
            Wallet = wallet,
            AccountVolume = 5000M,
            TotalMarginUsed = 100M,
            Positions = new Dictionary<string, Position>
            {
                ["UNI"] = new Position
                {
                    Symbol = "UNI",
                    Quantity = -200M, // Negative = Short
                    AverageEntryPrice = 8M,
                    VolumeUsd = 1600M,
                    Leverage = 4
                }
            },
            TimeStamp = DateTime.UtcNow
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, true))
            .ReturnsAsync(walletInfo);

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        // Act
        var snapshot = await service.GetSnapshot(wallet);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Wallet.Should().Be(wallet);
        snapshot.Positions.Should().HaveCount(1);
        snapshot.Positions.First().Symbol.Should().Be("UNI");

        _walletInfoProvider.Verify(x => x.GetInfo(wallet, true), Times.Once);
    }

    [Fact]
    public async Task InitializeWalletSnapshot_ShouldNotReinitialize_WhenAlreadyExists()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var firstSnapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow.AddHours(-1),
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "MATIC",
                    Quantity = 500M, // Positive = Long
                    AverageEntryPrice = 1M,
                    VolumeUsd = 500M,
                    Leverage = 2
                }
            }
        };

        var secondSnapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        // Act
        service.InitializeWalletSnapshot(firstSnapshot);
        service.InitializeWalletSnapshot(secondSnapshot); // Попытка переинициализации

        // Assert
        var retrievedSnapshot = await service.GetSnapshot(wallet);
        retrievedSnapshot.Positions.Should().HaveCount(1); // Должна остаться первая версия
        retrievedSnapshot.Positions.First().Symbol.Should().Be("MATIC");
    }

    [Fact]
    public async Task AddTrade_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            _logger.Object);

        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "ATOM",
                    Quantity = 100M, // Positive = Long
                    AverageEntryPrice = 10M,
                    VolumeUsd = 1000M,
                    Leverage = 3
                }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        var trade = new OriginalTrade
        {
            Wallet = wallet,
            Symbol = "ATOM",
            Direction = Direction.Long,
            Quantity = 10M,
            Price = 10M
            // VolumeUsd = 100M вычисляется автоматически
        };

        // Act - симулируем параллельные трейды
        var tasks = Enumerable.Range(0, 5).Select(_ => service.AddTrade(trade));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().Be(OrderSubType.Increase));
        var currentSnapshot = await service.GetSnapshot(wallet);
        var position = currentSnapshot.Positions.First(p => p.Symbol == "ATOM");
        position.Quantity.Should().Be(150M); // 100 + 5 * 10
    }
}