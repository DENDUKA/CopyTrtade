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

    [Fact]
    public async Task ComplexTradingScenario_With100Trades_SingleCycle_ShouldHandleAllOperations()
    {
        // Arrange
        var wallet = new Wallet("0x1234567890123456789012345678901234567890");

        // Setup mock для случаев когда нужно получить позицию из provider (Open и Flip)
        var currentPrice = 50000M;

        _walletInfoProvider
            .Setup(x => x.GetInfo(wallet, false))
            .ReturnsAsync((Wallet w, bool b) =>
            {
                // Симулируем текущую позицию с актуальной ценой
                return new WalletInfoModel
                {
                    Wallet = w,
                    AccountVolume = 100000M,
                    TotalMarginUsed = 5000M,
                    Positions = new Dictionary<string, Position>
                    {
                        ["BTC"] = new Position
                        {
                            Symbol = "BTC",
                            Quantity = 0.1M, // Начальное значение, будет обновляться
                            AverageEntryPrice = currentPrice,
                            VolumeUsd = 0.1M * currentPrice,
                            Leverage = 10
                        }
                    },
                    TimeStamp = DateTime.UtcNow
                };
            });

        var service = new CurrentWalletPositionService(
            _walletInfoProvider.Object,
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
                Price = price
                // VolumeUsd вычисляется автоматически
            };

            var result = await service.AddTrade(trade);

            // Get current position for debugging
            var debugSnapshot = await service.GetSnapshot(wallet);
            var debugPosition = debugSnapshot.Positions.FirstOrDefault(p => p.Symbol == "BTC");
            var actualQty = debugPosition?.Quantity ?? 0M;

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
}