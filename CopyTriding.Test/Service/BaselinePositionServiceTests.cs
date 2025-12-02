using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTrading.Test.Service;

public class BaselinePositionServiceTests
{
    private readonly Mock<ICurrentWalletPositionService> _currentWalletPositionServiceMock;
    private readonly Mock<IFillsOrderService> _fillsOrderServiceMock;
    private readonly Mock<ILogger<BaselinePositionService>> _loggerMock;
    private readonly BaselinePositionService _service;
    private readonly Wallet _wallet1 = new("0x1111111111111111111111111111111111111111");
    private readonly Wallet _wallet2 = new("0x2222222222222222222222222222222222222222");

    public BaselinePositionServiceTests()
    {
        _currentWalletPositionServiceMock = new Mock<ICurrentWalletPositionService>();
        _fillsOrderServiceMock = new Mock<IFillsOrderService>();
        _loggerMock = new Mock<ILogger<BaselinePositionService>>();
        _service = new BaselinePositionService(_currentWalletPositionServiceMock.Object, _fillsOrderServiceMock.Object, _loggerMock.Object);
    }

    #region Start() Tests

    [Fact]
    public async Task Start_ShouldInitializeBaselinePositions_ForAllWallets()
    {
        // Arrange
        var wallets = new[] { _wallet1, _wallet2 };
        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(wallets);

        var snapshot1 = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10.5m },
                new Position { Symbol = "ETH", Quantity = -20.3m }
            }
        };

        var snapshot2 = new WalletPositionsSnapshot
        {
            Wallet = _wallet2,
            Positions = new List<Position>
            {
                new Position { Symbol = "SOL", Quantity = 100m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot1);

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet2))
            .ReturnsAsync(snapshot2);

        // Act
        await _service.Start();

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(10.5m);
        _service.GetBaselinePosition(_wallet1, "ETH").Should().Be(-20.3m);
        _service.GetBaselinePosition(_wallet2, "SOL").Should().Be(100m);
    }

    [Fact]
    public async Task Start_ShouldSkipWallet_WhenSnapshotIsNull()
    {
        // Arrange
        var wallets = new[] { _wallet1 };
        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(wallets);

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync((WalletPositionsSnapshot?)null);

        // Act
        await _service.Start();

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0);
    }

    [Fact]
    public async Task Start_ShouldSkipWallet_WhenSnapshotPositionsIsNull()
    {
        // Arrange
        var wallets = new[] { _wallet1 };
        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(wallets);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = null
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.Start();

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0);
    }

    [Fact]
    public async Task Start_ShouldContinueWithOtherWallets_WhenOneWalletThrowsException()
    {
        // Arrange
        var wallets = new[] { _wallet1, _wallet2 };
        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(wallets);

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ThrowsAsync(new Exception("Network error"));

        var snapshot2 = new WalletPositionsSnapshot
        {
            Wallet = _wallet2,
            Positions = new List<Position>
            {
                new Position { Symbol = "SOL", Quantity = 100m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet2))
            .ReturnsAsync(snapshot2);

        // Act
        await _service.Start();

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0);
        _service.GetBaselinePosition(_wallet2, "SOL").Should().Be(100m);
    }

    #endregion

    #region OnNewTrades() Tests

    [Fact]
    public async Task OnNewTrades_ShouldSkipProcessing_WhenIsSnapshot()
    {
        // Arrange
        var trades = new[]
        {
            CreateTrade(_wallet1, "BTC", 1m)
        };

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: true));

        // Assert
        _currentWalletPositionServiceMock.Verify(x => x.GetSnapshot(It.IsAny<Wallet>()), Times.Never);
    }

    [Fact]
    public async Task OnNewTrades_ShouldRemoveBaselinePosition_WhenCurrentPositionIsNull()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>() // Позиция не найдена
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0);
    }

    [Fact]
    public async Task OnNewTrades_ShouldRemoveBaselinePosition_WhenCurrentQuantityIsZero()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 0m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0);
    }

    [Fact]
    public async Task OnNewTrades_ShouldUpdateBaselinePosition_WhenPositionDecreases()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 8m } // Уменьшилась с 10 до 8
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(8m);
    }

    [Fact]
    public async Task OnNewTrades_ShouldUpdateBaselinePosition_WhenShortPositionDecreases()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", -10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = -8m } // Уменьшилась с -10 до -8 (по модулю)
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(-8m);
    }

    [Fact]
    public async Task OnNewTrades_ShouldNotUpdateBaselinePosition_WhenPositionIncreases()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 12m } // Увеличилась с 10 до 12
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(10m); // Не изменилась
    }

    [Fact]
    public async Task OnNewTrades_ShouldNotCreateNewBaselinePosition_WhenBaselineNotFound()
    {
        // Arrange
        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 5m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(0); // Не создана
    }

    [Fact]
    public async Task OnNewTrades_ShouldProcessMultipleTradesForDifferentPairs()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);
        await InitializeBaselinePosition(_wallet1, "ETH", 20m);
        await InitializeBaselinePosition(_wallet2, "SOL", 100m);

        var trades = new[]
        {
            CreateTrade(_wallet1, "BTC", 1m),
            CreateTrade(_wallet1, "ETH", 2m),
            CreateTrade(_wallet2, "SOL", 3m)
        };

        var snapshot1 = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 8m },  // Уменьшилась
                new Position { Symbol = "ETH", Quantity = 22m }  // Увеличилась
            }
        };

        var snapshot2 = new WalletPositionsSnapshot
        {
            Wallet = _wallet2,
            Positions = new List<Position>
            {
                new Position { Symbol = "SOL", Quantity = 95m }  // Уменьшилась
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot1);

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet2))
            .ReturnsAsync(snapshot2);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(8m);   // Обновлена
        _service.GetBaselinePosition(_wallet1, "ETH").Should().Be(20m);  // Не изменилась
        _service.GetBaselinePosition(_wallet2, "SOL").Should().Be(95m);  // Обновлена
    }

    [Fact]
    public async Task OnNewTrades_ShouldNotUpdateBaselinePosition_WhenPositionRemainsTheSame()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10m } // Осталась такой же
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(10m); // Не изменилась
    }

    [Fact]
    public async Task OnNewTrades_ShouldContinueProcessing_WhenOneTradeGroupThrowsException()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);
        await InitializeBaselinePosition(_wallet2, "ETH", 20m);

        var trades = new[]
        {
            CreateTrade(_wallet1, "BTC", 1m),
            CreateTrade(_wallet2, "ETH", 2m)
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ThrowsAsync(new Exception("Network error"));

        var snapshot2 = new WalletPositionsSnapshot
        {
            Wallet = _wallet2,
            Positions = new List<Position>
            {
                new Position { Symbol = "ETH", Quantity = 18m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet2))
            .ReturnsAsync(snapshot2);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(10m);  // Не изменилась из-за ошибки
        _service.GetBaselinePosition(_wallet2, "ETH").Should().Be(18m);  // Обновлена
    }

    [Fact]
    public async Task OnNewTrades_ShouldHandlePositionChangeFromLongToShort()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = -5m } // Изменилась с long 10 на short -5
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        // Объем уменьшился (с 10 до 5 по модулю), поэтому должна обновиться
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(-5m);
    }

    [Fact]
    public async Task OnNewTrades_ShouldHandlePositionChangeFromShortToLong()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", -10m);

        var trades = new[] { CreateTrade(_wallet1, "BTC", 1m) };
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 5m } // Изменилась с short -10 на long 5
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        await _service.OnNewTrades((trades, IsSnapshot: false));

        // Assert
        // Объем уменьшился (с 10 до 5 по модулю), поэтому должна обновиться
        _service.GetBaselinePosition(_wallet1, "BTC").Should().Be(5m);
    }

    #endregion

    #region GetBaselinePosition() Tests

    [Fact]
    public async Task GetBaselinePosition_ShouldReturnPosition_WhenExists()
    {
        // Arrange
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(new[] { _wallet1 });

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        await _service.Start();

        // Act
        var result = _service.GetBaselinePosition(_wallet1, "BTC");

        // Assert
        result.Should().Be(10m);
    }

    [Fact]
    public void GetBaselinePosition_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = _service.GetBaselinePosition(_wallet1, "BTC");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetBaselinePosition_ShouldReturnNull_ForDifferentWallet()
    {
        // Arrange
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(new[] { _wallet1 });

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        await _service.Start();

        // Act
        var result = _service.GetBaselinePosition(_wallet2, "BTC");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetBaselinePosition_ShouldReturnNull_ForDifferentSymbol()
    {
        // Arrange
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>
            {
                new Position { Symbol = "BTC", Quantity = 10m }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(new[] { _wallet1 });

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        await _service.Start();

        // Act
        var result = _service.GetBaselinePosition(_wallet1, "ETH");

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private OriginalTrade CreateTrade(Wallet wallet, string symbol, decimal quantity)
    {
        return new OriginalTrade
        {
            Wallet = wallet,
            Symbol = symbol,
            Quantity = quantity,
            Price = 50000m,
            Direction = quantity > 0 ? Direction.Long : Direction.Short,
            TimeStamp = DateTime.UtcNow,
            TradeId = Random.Shared.Next(1, 1000000),
            OrderId = Random.Shared.Next(1, 1000000),
            IsFuture = true
        };
    }

    private async Task InitializeBaselinePosition(Wallet wallet, string symbol, decimal quantity)
    {
        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = wallet,
            Positions = new List<Position>
            {
                new Position { Symbol = symbol, Quantity = quantity }
            }
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetAllWallets())
            .Returns(new[] { wallet });

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(wallet))
            .ReturnsAsync(snapshot);

        await _service.Start();

        // Reset mocks after initialization
        _currentWalletPositionServiceMock.Reset();
    }

    #endregion
}
