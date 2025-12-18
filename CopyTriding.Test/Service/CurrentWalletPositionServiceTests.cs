using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTrading.Test.Service;

public class CurrentWalletPositionServiceTests
{
    private readonly Mock<IWalletInfoProvider> _walletInfoProvider = new(MockBehavior.Strict);
    private readonly Mock<ILogger<CurrentWalletPositionService>> _logger = new(MockBehavior.Loose);
    private readonly CopyOrderStorageService _storageService = new(Mock.Of<ILogger<CopyOrderStorageService>>());

    [Fact]
    public async Task CurrentLongPosInitialize_Add_Succsess()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
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
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 0.1M,
            IsFuture = true,
        };

        var longTrade = new OriginalTrade()
        {
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 0.1M,
            IsFuture = true,
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var subType = await service.AddTrade(shortTrade);
        var currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        var currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Decrease);
        currentPositions.Quantity.Should().Be(0.1M);


        // Act
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Increase);
        currentPositions.Quantity.Should().Be(0.2M);


        // Act
        subType = await service.AddTrade(shortTrade);
        subType = await service.AddTrade(shortTrade);
        currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.FirstOrDefault(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Close);
        currentPositions.Should().BeNull();
    }

    [Fact]
    public async Task CurrentShortPosInitialize_Add_Succsess()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
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
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 0.1M,
            IsFuture = true,
        };

        var longTrade = new OriginalTrade()
        {
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 0.1M,
            IsFuture = true,
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var subType = await service.AddTrade(shortTrade);
        var currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        var currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Increase);
        currentPositions.Quantity.Should().Be(-0.3M);


        // Act
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.First(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Decrease);
        currentPositions.Quantity.Should().Be(-0.2M);


        // Act
        subType = await service.AddTrade(longTrade);
        subType = await service.AddTrade(longTrade);
        currentSnapshot = await service.GetSnapshot(wallet);

        //Assert
        currentPositions = currentSnapshot.Positions.FirstOrDefault(x => x.Symbol == shortTrade.Symbol);
        subType.Should().Be(OrderSubType.Close);
        currentPositions.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnOpen_WhenNoPositionExists()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        var order = new OriginalOrder
        {
            Wallet = wallet,
            Symbol = "ETH",
            Direction = Direction.Long,
            Quantity = 10M,
            Price = 3000M,
            Leverage = 5M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Open);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnIncrease_WhenSameDirectionPositionExists()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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

        var order = new OriginalOrder
        {
            Wallet = wallet,
            Symbol = "SOL",
            Direction = Direction.Long,
            Quantity = 50M,
            Price = 100M,
            Leverage = 3M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var orderSubType = service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnDecrease_WhenOppositeDirectionWithSmallerQuantity()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
        var orderSubType = service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public async Task GetOrderSubType_ShouldReturnClose_WhenOppositeDirectionWithExactQuantity()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
        var orderSubType = service.GetOrderSubType(order);

        // Assert
        orderSubType.Should().Be(OrderSubType.Close);
    }

    [Fact]
    public async Task AddTrade_ShouldReturnOpen_AndAddPosition_WhenNoPositionExists()
    {
        // Arrange
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        // Mock для GetInfo - возвращаем WalletInfoModel с позицией BNB
        var walletInfo = new WalletInfoModel
        {
            Wallet = wallet,
            AccountVolume = 10000M,
            TotalMarginUsed = 0M,
            Positions = new Dictionary<string, Position>
            {
                ["BNB"] = new Position
                {
                    Symbol = "BNB",
                    Quantity = 10M,
                    AverageEntryPrice = 300M,
                    VolumeUsd = 3000M
                }
            }
        };

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, false))
            .ReturnsAsync(walletInfo);

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
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
            Price = 300M,
            IsFuture = true,
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
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
            Price = 0.6M,
            IsFuture = true,
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
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        var originalTimestamp = DateTime.UtcNow.AddMinutes(-10);

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
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
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
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
        var wallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

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
            Price = 10M,
            IsFuture = true,
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

    [Fact]
    public async Task ComplexTradingScenario_With100Trades_SingleCycle_ShouldHandleAllOperations()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");

        // Setup mock для случаев когда нужно получить позицию из provider (Open и Flip)
        var currentPrice = 50000M;
        var currentBtcPosition = 0M;
        var currentBtcAvgPrice = 50000M;

        // Динамический mock для GetInfo - возвращает актуальную позицию
        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, false))
            .ReturnsAsync(() => new WalletInfoModel
            {
                Wallet = wallet,
                AccountVolume = 100000M,
                TotalMarginUsed = 0M,
                Positions = currentBtcPosition != 0M
                    ? new Dictionary<string, Position>
                    {
                        ["BTC"] = new Position
                        {
                            Symbol = "BTC",
                            Quantity = currentBtcPosition,
                            AverageEntryPrice = currentBtcAvgPrice,
                            VolumeUsd = currentBtcPosition * currentBtcAvgPrice
                        }
                    }
                    : new Dictionary<string, Position>()
            });

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>()),
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        service.InitializeWalletSnapshot(snapshot);

        var tradeLog = new List<(int TradeNum, Direction Direction, decimal Quantity, decimal Price, OrderSubType Expected, decimal ExpectedPosition, string Description)>();

        // ==================== PHASE 1: OPEN & INCREASE LONG ====================
        // Trade 1: Open BTC Long 0.1 @ 50000
        currentPrice = 50000M;
        currentBtcPosition = 0.1M; // Устанавливаем начальную позицию для первого трейда
        currentBtcAvgPrice = 50000M;
        tradeLog.Add((1, Direction.Long, 0.1M, currentPrice, OrderSubType.Open, 0.1M, "Open BTC Long"));

        // Trade 2-10: Increase BTC Long постепенно (9 трейдов)
        for (int i = 2; i <= 10; i++)
        {
            currentPrice += 100M;
            var qty = 0.05M;
            var expectedPos = 0.1M + (i - 1) * qty;
            tradeLog.Add((i, Direction.Long, qty, currentPrice, OrderSubType.Increase, expectedPos, $"Increase BTC Long #{i - 1}"));
        }
        // После Trade 10: Position = 0.1 + 9*0.05 = 0.55 BTC Long

        // Trade 11-20: Продолжаем увеличивать (10 трейдов)
        for (int i = 11; i <= 20; i++)
        {
            currentPrice += 100M;
            var qty = 0.03M;
            var expectedPos = 0.55M + (i - 10) * qty;
            tradeLog.Add((i, Direction.Long, qty, currentPrice, OrderSubType.Increase, expectedPos, $"Increase BTC Long #{i}"));
        }
        // После Trade 20: Position = 0.55 + 10*0.03 = 0.85 BTC Long

        // ==================== PHASE 2: PARTIAL DECREASE ====================
        // Trade 21-30: Частично закрываем (10 трейдов)
        for (int i = 21; i <= 30; i++)
        {
            currentPrice += 50M;
            var qty = 0.05M;
            var expectedPos = 0.85M - (i - 20) * qty;
            tradeLog.Add((i, Direction.Short, qty, currentPrice, OrderSubType.Decrease, expectedPos, $"Decrease BTC Long #{i - 20}"));
        }
        // После Trade 30: Position = 0.85 - 10*0.05 = 0.35 BTC Long

        // ==================== PHASE 3: INCREASE AGAIN ====================
        // Trade 31-40: Снова увеличиваем (10 трейдов)
        for (int i = 31; i <= 40; i++)
        {
            currentPrice += 80M;
            var qty = 0.04M;
            var expectedPos = 0.35M + (i - 30) * qty;
            tradeLog.Add((i, Direction.Long, qty, currentPrice, OrderSubType.Increase, expectedPos, $"Increase BTC Long again #{i - 30}"));
        }
        // После Trade 40: Position = 0.35 + 10*0.04 = 0.75 BTC Long

        // ==================== PHASE 4: CLOSE COMPLETELY ====================
        // Trade 41-49: Закрываем постепенно (9 трейдов)
        for (int i = 41; i <= 49; i++)
        {
            currentPrice += 100M;
            var qty = 0.075M;
            var expectedPos = 0.75M - (i - 40) * qty;
            tradeLog.Add((i, Direction.Short, qty, currentPrice, OrderSubType.Decrease, expectedPos, $"Closing BTC Long #{i - 40}"));
        }
        // После Trade 49: Position = 0.75 - 9*0.075 = 0.075 BTC Long

        // ==================== PHASE 5: MORE OPERATIONS TO REACH 100 TRADES ====================
        // Trade 50-69: Continue increasing (20 трейдов)
        for (int i = 50; i <= 69; i++)
        {
            currentPrice += 90M;
            var qty = 0.025M;
            var expectedPos = 0.075M + (i - 49) * qty;
            tradeLog.Add((i, Direction.Long, qty, currentPrice, OrderSubType.Increase, expectedPos, $"Increase BTC Long #{i - 40}"));
        }
        // После Trade 69: Position = 0.075 + 20*0.025 = 0.575 BTC Long

        // Trade 70-89: Decrease gradually (20 трейдов)
        for (int i = 70; i <= 89; i++)
        {
            currentPrice += 60M;
            var qty = 0.025M;
            var expectedPos = 0.575M - (i - 69) * qty;
            tradeLog.Add((i, Direction.Short, qty, currentPrice, OrderSubType.Decrease, expectedPos, $"Decrease BTC Long #{i - 60}"));
        }
        // После Trade 89: Position = 0.575 - 20*0.025 = 0.075 BTC Long

        // Trade 90-99: Small increases and decreases (10 трейдов)
        for (int i = 90; i <= 94; i++)
        {
            currentPrice += 40M;
            var qty = 0.01M;
            var expectedPos = 0.075M + (i - 89) * qty;
            tradeLog.Add((i, Direction.Long, qty, currentPrice, OrderSubType.Increase, expectedPos, $"Small increase #{i - 89}"));
        }
        // После Trade 94: Position = 0.075 + 5*0.01 = 0.125 BTC Long

        for (int i = 95; i <= 99; i++)
        {
            currentPrice += 30M;
            var qty = 0.025M;
            var expectedPos = 0.125M - (i - 94) * qty;
            tradeLog.Add((i, Direction.Short, qty, currentPrice, OrderSubType.Decrease, expectedPos, $"Small decrease #{i - 94}"));
        }
        // После Trade 99: Position = 0.125 - 5*0.025 = 0 BTC

        // Trade 100: Final Close (if any remaining)
        currentPrice += 50M;
        // Проверяем остаток после Trade 99
        // 0.125 - 5*0.025 = 0.125 - 0.125 = 0, так что Trade 100 не нужен
        // Но нужно сделать 100 трейдов, поэтому добавим еще один Increase перед последним Close

        // Корректируем: Trade 95-98 закрывают до 0.025, Trade 99 до 0.01, Trade 100 закрывает полностью
        tradeLog.RemoveAt(tradeLog.Count - 1); // Убираем последний Decrease (был Trade 99 с 0.025)
        // Trade 99: Decrease до 0.01
        currentPrice += 30M;
        tradeLog.Add((99, Direction.Short, 0.015M, currentPrice, OrderSubType.Decrease, 0.01M, "Decrease to 0.01"));

        // Trade 100: Final Close
        currentPrice += 50M;
        tradeLog.Add((100, Direction.Short, 0.01M, currentPrice, OrderSubType.Close, 0M, "Final Close - complete"));

        // Act & Assert - execute all 100 trades
        for (int i = 0; i < tradeLog.Count; i++)
        {
            var (tradeNum, direction, quantity, price, expected, expectedPosition, description) = tradeLog[i];

            var trade = new OriginalTrade
            {
                Wallet = wallet,
                Symbol = "BTC",
                Direction = direction,
                Quantity = quantity,
                Price = price,
                IsFuture = true,
                // VolumeUsd вычисляется автоматически
            };

            var result = await service.AddTrade(trade);

            // Get current position for debugging
            var debugSnapshot = await service.GetSnapshot(wallet);
            var debugPosition = debugSnapshot.Positions.FirstOrDefault(p => p.Symbol == "BTC");
            var actualQty = debugPosition?.Quantity ?? 0M;

            // Обновляем позицию для mock
            currentBtcPosition = actualQty;
            if (debugPosition != null)
            {
                currentBtcAvgPrice = debugPosition.AverageEntryPrice;
            }

            // Assert OrderSubType
            result.Should().Be(expected,
                $"Trade #{tradeNum}: {description} | Direction={direction}, " +
                $"Quantity={quantity}, Price={price} | Expected={expected}, Got={result} | " +
                $"ActualPosition={actualQty}, ExpectedPosition={expectedPosition}");

            // Verify position after trade (except for Close which removes position)
            var currentSnapshot = await service.GetSnapshot(wallet);
            if (expected == OrderSubType.Close && Math.Abs(expectedPosition) < 0.001M)
            {
                currentSnapshot.Positions.Should().BeEmpty($"Trade #{tradeNum}: Position should be closed");
            }
            else if (expected != OrderSubType.Close || Math.Abs(expectedPosition) > 0.001M)
            {
                var position = currentSnapshot.Positions.FirstOrDefault(p => p.Symbol == "BTC");
                position.Should().NotBeNull($"Trade #{tradeNum}: Position should exist");

                // Проверяем Quantity для всех трейдов
                position!.Quantity.Should().Be(expectedPosition,
                    $"Trade #{tradeNum}: {description} | Expected position {expectedPosition}, Got {position.Quantity}");
            }
        }

        // Final assertions - verify all positions are closed
        var finalSnapshot = await service.GetSnapshot(wallet);
        finalSnapshot.Positions.Should().BeEmpty("All positions should be closed at the end of the scenario");

        // Verify we processed exactly 100 trades
        tradeLog.Count.Should().Be(100, "Should have processed exactly 100 trades");
    }

    #region GetOrderSubType Tests with Corner Cases

    [Fact]
    public void GetOrderSubType_NoPositionNoPendingOrders_ShouldReturnOpen()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Open);
    }

    [Fact]
    public void GetOrderSubType_LongPositionLongOrder_ShouldReturnIncrease()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M, // Добавляем еще 5
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public void GetOrderSubType_ShortPositionShortOrder_ShouldReturnIncrease()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = -10M, // Short 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 5M, // Добавляем еще Short 5
            Price = 49000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public void GetOrderSubType_LongPositionPartialShortOrder_ShouldReturnDecrease()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 5M, // Частично закрываем (10 - 5 = 5 остается Long)
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public void GetOrderSubType_LongPositionExactShortOrder_ShouldReturnClose()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 10M, // Точно закрываем (10 - 10 = 0)
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Close);
    }

    [Fact]
    public void GetOrderSubType_LongPositionOversizeShortOrder_ShouldReturnFlip()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 15M, // Закрываем и переворачиваем (10 - 15 = -5, Short)
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Flip);
    }

    [Fact]
    public void GetOrderSubType_ShortPositionOversizeLongOrder_ShouldReturnFlip()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = -10M, // Short 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 15M, // Закрываем и переворачиваем (-10 + 15 = 5, Long)
            Price = 49000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Flip);
    }

    [Fact]
    public void GetOrderSubType_WithPendingLongOrders_ShouldIncludeInCalculation()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Real Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        // Добавляем pending ордер Long
        var pendingOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M, // Pending Long 5
            Price = 52000M, // Цена выше - исполнится раньше при падении
            Status = OrderStatus.Open
        };

        var newOrder = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 12M, // Short 12
            Price = 51000M, // Цена между pending и real
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingOrder]);
        var result = service.GetOrderSubType(newOrder);

        // Assert
        // Потенциальная позиция = 10 (real) + 5 (pending Long выше цены) = 15
        // Short 12 → 15 - 12 = 3 (остается Long) → Decrease
        result.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public void GetOrderSubType_ShortOrder_ShouldConsiderAllLongPendingOrders()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Real Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        // Добавляем pending Long ордера с разными ценами
        var pendingOrder1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 3M,
            Price = 49000M, // Ниже текущей цены
            Status = OrderStatus.Open
        };

        var pendingOrder2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 7M,
            Price = 52000M, // Выше текущей цены
            Status = OrderStatus.Open
        };

        var newShortOrder = new OriginalOrder
        {
            OrderId = 3,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 15M, // Short 15
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingOrder1, pendingOrder2]);
        var result = service.GetOrderSubType(newShortOrder);

        // Assert
        // Для Short ордера: учитываем ВСЕ Long pending (независимо от цены)
        // Потенциальная позиция = 10 (real) + 3 + 7 = 20
        // Short 15 → 20 - 15 = 5 (остается Long) → Decrease
        result.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public void GetOrderSubType_ShortOrder_ShouldConsiderOnlyLowerPriceShortPending()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = -10M, // Real Short 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        // Pending Short ордера с разными ценами
        var pendingShort1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 3M,
            Price = 49000M, // Ниже → исполнится раньше при росте
            Status = OrderStatus.Open
        };

        var pendingShort2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 5M,
            Price = 52000M, // Выше → НЕ исполнится раньше
            Status = OrderStatus.Open
        };

        var newShortOrder = new OriginalOrder
        {
            OrderId = 3,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 5M,
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingShort1, pendingShort2]);
        var result = service.GetOrderSubType(newShortOrder);

        // Assert
        // Для Short по цене 51000: учитываем только Short с ценой < 51000 (это pendingShort1)
        // Потенциальная позиция = -10 (real) - 3 (pending Short 49000) = -13
        // Short 5 → -13 - 5 = -18 (увеличиваем Short) → Increase
        result.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public void GetOrderSubType_LongOrder_ShouldConsiderOnlyHigherPriceLongPending()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M, // Real Long 10
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        // Pending Long ордера с разными ценами
        var pendingLong1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 3M,
            Price = 52000M, // Выше → исполнится раньше при падении
            Status = OrderStatus.Open
        };

        var pendingLong2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M,
            Price = 49000M, // Ниже → НЕ исполнится раньше
            Status = OrderStatus.Open
        };

        var newLongOrder = new OriginalOrder
        {
            OrderId = 3,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M,
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingLong1, pendingLong2]);
        var result = service.GetOrderSubType(newLongOrder);

        // Assert
        // Для Long по цене 51000: учитываем только Long с ценой > 51000 (это pendingLong1)
        // Потенциальная позиция = 10 (real) + 3 (pending Long 52000) = 13
        // Long 5 → 13 + 5 = 18 (увеличиваем Long) → Increase
        result.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public void GetOrderSubType_ShouldExcludeOrderWithSameId()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10M,
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500000M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M,
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([order]); // Добавляем в pending
        var result = service.GetOrderSubType(order); // Пересчитываем для того же ордера

        // Assert
        // Должен исключить сам себя из расчета
        // Потенциальная позиция = 10 (real) + 0 (сам себя не учитываем) = 10
        // Long 5 → 10 + 5 = 15 (увеличиваем) → Increase
        result.Should().Be(OrderSubType.Increase);
    }

    [Fact]
    public void GetOrderSubType_NoRealPositionButPendingOrders_ShouldCalculateCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>() // Нет real позиции
        };

        // Pending Long ордера
        var pendingLong1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M,
            Price = 52000M,
            Status = OrderStatus.Open
        };

        var pendingLong2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 10M,
            Price = 51000M,
            Status = OrderStatus.Open
        };

        var newShortOrder = new OriginalOrder
        {
            OrderId = 3,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 12M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingLong1, pendingLong2]);
        var result = service.GetOrderSubType(newShortOrder);

        // Assert
        // Real = 0, Pending = 5 + 10 = 15 (ВСЕ Long учитываются для Short)
        // Потенциальная позиция = 0 + 15 = 15
        // Short 12 → 15 - 12 = 3 (остается Long) → Decrease
        result.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public void GetOrderSubType_FlipDueToPendingOrders_NotRealPosition()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 5M, // Real Long 5 (небольшая)
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 250000M,
                    Leverage = 5
                }
            }
        };

        // Pending Long ордера
        var pendingLong = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 30M, // Большой pending Long
            Price = 52000M,
            Status = OrderStatus.Open
        };

        var newShortOrder = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 40M, // Большой Short
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingLong]);
        var result = service.GetOrderSubType(newShortOrder);

        // Assert
        // Потенциальная позиция = 5 (real) + 30 (pending Long) = 35
        // Short 40 → 35 - 40 = -5 (переворачиваем в Short) → Flip
        result.Should().Be(OrderSubType.Flip);
    }

    [Fact]
    public void GetOrderSubType_MultiplePendingBothDirections_ShouldCalculateCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 100M, // Real Long 100
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 5000000M,
                    Leverage = 5
                }
            }
        };

        // Pending Long и Short ордера
        var pendingLong1 = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 20M,
            Price = 52000M,
            Status = OrderStatus.Open
        };

        var pendingLong2 = new OriginalOrder
        {
            OrderId = 2,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 30M,
            Price = 51500M,
            Status = OrderStatus.Open
        };

        var pendingShort1 = new OriginalOrder
        {
            OrderId = 3,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 40M,
            Price = 50500M, // Ниже цены нового ордера
            Status = OrderStatus.Open
        };

        var newShortOrder = new OriginalOrder
        {
            OrderId = 4,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 80M,
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        fillsOrderService.OnNewOrders([pendingLong1, pendingLong2, pendingShort1]);
        var result = service.GetOrderSubType(newShortOrder);

        // Assert
        // Для Short по цене 51000:
        // - Учитываем ВСЕ Long pending: 20 + 30 = 50
        // - Учитываем Short с ценой < 51000: 40
        // Потенциальная = 100 + 50 - 40 = 110
        // Short 80 → 110 - 80 = 30 (остается Long) → Decrease
        result.Should().Be(OrderSubType.Decrease);
    }

    [Fact]
    public void GetOrderSubType_ZeroQuantityInPosition_ShouldTreatAsNoPosition()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 0M, // Zero position
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 0M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 5M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        result.Should().Be(OrderSubType.Open);
    }

    [Fact]
    public void GetOrderSubType_VerySmallQuantityDifference_ShouldReturnClose()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");
        var fillsOrderService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
            fillsOrderService,
            _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position
                {
                    Symbol = "BTC",
                    Quantity = 10.0001M, // Long 10.0001
                    AverageEntryPrice = 50000M,
                    VolumeUsd = 500005M,
                    Leverage = 5
                }
            }
        };

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 10M, // Почти точно закрывает (остается 0.0001)
            Price = 51000M,
            Status = OrderStatus.Open
        };

        // Act
        service.InitializeWalletSnapshot(snapshot);
        var result = service.GetOrderSubType(order);

        // Assert
        // Разница < 0.001 → должен быть Close
        result.Should().Be(OrderSubType.Close);
    }

    #endregion

    #region CalculatePotentialPosition() Tests

    [Fact]
    public void CalculatePotentialPosition_NoRealPosition_NoPendingOrders_ShouldReturnZero()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        service.InitializeWalletSnapshot(snapshot);

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(order);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculatePotentialPosition_LongRealPosition_NoPendingOrders_ShouldReturnRealPosition()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(order);

        // Assert
        result.Should().Be(10M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShortRealPosition_NoPendingOrders_ShouldReturnNegativePosition()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = -10M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        var order = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(order);

        // Assert
        result.Should().Be(-10M);
    }

    [Fact]
    public void CalculatePotentialPosition_LongOrder_ShouldOnlyConsiderLongPendingWithHigherPrice()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 10M, Price = 51000M, Status = OrderStatus.Open }, // Выше - учитывается
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 5M, Price = 52000M, Status = OrderStatus.Open },  // Выше - учитывается
            new OriginalOrder { OrderId = 4, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 3M, Price = 50000M, Status = OrderStatus.Open },  // Равна - НЕ учитывается
            new OriginalOrder { OrderId = 5, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 2M, Price = 49000M, Status = OrderStatus.Open }   // Ниже - НЕ учитывается
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + Pending (OrderId=2: +10, OrderId=3: +5) = 115
        result.Should().Be(115M);
    }

    [Fact]
    public void CalculatePotentialPosition_LongOrder_ShouldNotConsiderShortPending()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера (Short - не должны учитываться для Long ордера)
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 20M, Price = 55000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 10M, Price = 52000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Short pending не учитываются для Long ордера
        result.Should().Be(100M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShortOrder_ShouldConsiderAllLongPending()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending Long ордера (все должны учитываться для Short ордера)
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 10M, Price = 55000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 5M, Price = 50000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 4, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 3M, Price = 45000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 20M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + All Long pending: +10 +5 +3 = 118
        result.Should().Be(118M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShortOrder_ShouldOnlyConsiderShortPendingWithLowerPrice()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending Short ордера
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 10M, Price = 48000M, Status = OrderStatus.Open }, // Ниже - учитывается
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 5M, Price = 49000M, Status = OrderStatus.Open },  // Ниже - учитывается
            new OriginalOrder { OrderId = 4, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 3M, Price = 50000M, Status = OrderStatus.Open },  // Равна - НЕ учитывается
            new OriginalOrder { OrderId = 5, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 2M, Price = 51000M, Status = OrderStatus.Open }   // Выше - НЕ учитывается
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 20M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + Pending Short (OrderId=2: -10, OrderId=3: -5) = 85
        result.Should().Be(85M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShortOrder_WithLongAndShortPending_ShouldCalculateCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера: Long (все учитываются) + Short (только с ценой < 50000)
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 10M, Price = 55000M, Status = OrderStatus.Open },   // Long - учитывается
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 5M, Price = 45000M, Status = OrderStatus.Open },    // Long - учитывается
            new OriginalOrder { OrderId = 4, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 8M, Price = 48000M, Status = OrderStatus.Open },   // Short < 50k - учитывается
            new OriginalOrder { OrderId = 5, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 3M, Price = 49000M, Status = OrderStatus.Open },   // Short < 50k - учитывается
            new OriginalOrder { OrderId = 6, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 7M, Price = 51000M, Status = OrderStatus.Open }    // Short >= 50k - НЕ учитывается
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 20M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + Long pending: +10 +5 + Short pending < 50k: -8 -3 = 104
        result.Should().Be(104M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShouldExcludeOrderWithSameId()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера включая сам текущий ордер
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 1, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 999M, Price = 51000M, Status = OrderStatus.Open }, // Сам ордер - НЕ учитывается
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 10M, Price = 52000M, Status = OrderStatus.Open }   // Учитывается
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 999M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + Only OrderId=2: +10 (OrderId=1 исключен)
        result.Should().Be(110M);
    }

    [Fact]
    public void CalculatePotentialPosition_NoRealPosition_WithPendingOrders_ShouldCalculateCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>()
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера при отсутствии реальной позиции
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 5M, Price = 51000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 3M, Price = 52000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 0 + Pending: +5 +3 = 8
        result.Should().Be(8M);
    }

    [Fact]
    public void CalculatePotentialPosition_DifferentSymbol_ShouldNotAffectCalculation()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M },
                new Position { Symbol = "ETH", Quantity = 200M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending ордера для разных символов
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 10M, Price = 51000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "ETH", Direction = Direction.Long, Quantity = 20M, Price = 2100M, Status = OrderStatus.Open } // Другой символ
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 1M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Только BTC: Real 100 + Pending BTC: +10 = 110 (ETH не учитывается)
        result.Should().Be(110M);
    }

    [Fact]
    public void CalculatePotentialPosition_LongPositionWithShortPending_ShouldDecrease()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending Short ордера для Long ордера (не учитываются)
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 30M, Price = 55000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        // Short ордер - должен учитывать Short pending с меньшей ценой
        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 20M,
            Price = 60000M, // Short pending с ценой 55000 < 60000 учитывается
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 100 + Short pending (price 55000 < 60000): -30 = 70
        result.Should().Be(70M);
    }

    [Fact]
    public void CalculatePotentialPosition_ShortPositionWithLongPending_ShouldDecrease()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = -100M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Pending Long ордера
        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 30M, Price = 45000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        // Short ордер - учитывает ВСЕ Long pending
        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 20M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: -100 + All Long pending: +30 = -70
        result.Should().Be(-70M);
    }

    [Fact]
    public void CalculatePotentialPosition_ComplexScenario_WithMultiplePendingOrders()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 50M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        // Комплексная смесь pending ордеров
        var pendingOrders = new[]
        {
            // Long ордера
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 5M, Price = 52000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 3, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 3M, Price = 51000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 4, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 2M, Price = 49000M, Status = OrderStatus.Open },
            // Short ордера
            new OriginalOrder { OrderId = 5, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 7M, Price = 48000M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 6, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 4M, Price = 49500M, Status = OrderStatus.Open },
            new OriginalOrder { OrderId = 7, Wallet = wallet, Symbol = "BTC", Direction = Direction.Short, Quantity = 6M, Price = 51000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            Quantity = 10M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        // Real: 50
        // Long pending (все): +5 +3 +2 = +10
        // Short pending < 50000: -7 (48k) -4 (49.5k) = -11
        // Short pending >= 50000: НЕ учитываются (51k)
        // Total: 50 + 10 - 11 = 49
        result.Should().Be(49M);
    }

    [Fact]
    public void CalculatePotentialPosition_VerySmallQuantities_ShouldHandleCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 0.0001M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 0.00005M, Price = 51000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 0.00001M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        result.Should().Be(0.00015M);
    }

    [Fact]
    public void CalculatePotentialPosition_VeryLargeQuantities_ShouldHandleCorrectly()
    {
        // Arrange
        var wallet = new Wallet("0xecb63caa47c7c4e77f60f1ce858cf28dc2b82b00");
        var fillsService = new FillsOrderService(Mock.Of<ILogger<FillsOrderService>>());
        var service = new CurrentWalletPositionService(_walletInfoProvider.Object, fillsService, _logger.Object);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            TimeStamp = DateTime.UtcNow,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 1000000M }
            }
        };

        service.InitializeWalletSnapshot(snapshot);

        var pendingOrders = new[]
        {
            new OriginalOrder { OrderId = 2, Wallet = wallet, Symbol = "BTC", Direction = Direction.Long, Quantity = 500000M, Price = 51000M, Status = OrderStatus.Open }
        };

        fillsService.OnNewOrders(pendingOrders);

        var currentOrder = new OriginalOrder
        {
            OrderId = 1,
            Wallet = wallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            Quantity = 100000M,
            Price = 50000M,
            Status = OrderStatus.Open
        };

        // Act
        var result = service.CalculatePotentialPosition(currentOrder);

        // Assert
        result.Should().Be(1500000M);
    }

    #endregion
}