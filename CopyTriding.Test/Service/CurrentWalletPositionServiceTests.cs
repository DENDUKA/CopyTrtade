using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
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
    private readonly Mock<ILogger<CurrentWalletPositionService>> _logger = new(MockBehavior.Strict);

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
}