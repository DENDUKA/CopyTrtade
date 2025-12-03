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

    #region WillOrderCloseBelowBaseline() Tests

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenOrderNotFound()
    {
        // Arrange
        _fillsOrderServiceMock
            .Setup(x => x.GetOrderFillsByOrderId(123))
            .Returns((OrderFills?)null);

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(123);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenNoBaseline()
    {
        // Arrange
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 100m, 50000m);
        SetupOrderFills(order);

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenCurrentPositionNotFound()
    {
        // Arrange
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);

        var snapshot = new WalletPositionsSnapshot
        {
            Wallet = _wallet1,
            Positions = new List<Position>() // Позиция не найдена
        };

        _currentWalletPositionServiceMock
            .Setup(x => x.GetSnapshot(_wallet1))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenLongPositionAndLongOrder()
    {
        // Arrange - Long позиция + Long ордер = увеличение позиции
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m); // Long позиция

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenShortPositionAndShortOrder()
    {
        // Arrange - Short позиция + Short ордер = увеличение позиции
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -110m); // Short позиция

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenPositionStaysAboveBaseline_Long()
    {
        // Arrange - СЛУЧАЙ 1: Наш и предшествующие ордера НЕ заходят в baseline
        // Long: potential=120, new=110, baseline=100 → AboveBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 120m);
        SetupPotentialPosition(order, 120m); // Potential = 120 (без текущего ордера)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenPositionStaysAboveBaseline_Short()
    {
        // Arrange - СЛУЧАЙ 1: Наш и предшествующие ордера НЕ заходят в baseline
        // Short: potential=-120, new=-110, baseline=-100 → AboveBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -120m);
        SetupPotentialPosition(order, -120m); // Potential = -120 (без текущего ордера)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderPartiallyClosesIntoBaseline_Long()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline
        // Long: potential=110, new=90, baseline=100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 20m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m);
        SetupPotentialPosition(order, 110m); // Potential = 110, after order = 90

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderClosesToZero_Long()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline (закрывает всю позицию)
        // Long: potential=110, new=0, baseline=100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 110m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m);
        SetupPotentialPosition(order, 110m); // Potential = 110, after order = 0

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderReversesDirection_Long()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline (меняет направление)
        // Long: potential=110, new=-10, baseline=100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 120m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m);
        SetupPotentialPosition(order, 110m); // Potential = 110, after order = -10

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderPartiallyClosesIntoBaseline_Short()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline
        // Short: potential=-110, new=-90, baseline=-100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 20m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -110m);
        SetupPotentialPosition(order, -110m); // Potential = -110, after order = -90

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderClosesToZero_Short()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline (закрывает всю позицию)
        // Short: potential=-110, new=0, baseline=-100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 110m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -110m);
        SetupPotentialPosition(order, -110m); // Potential = -110, after order = 0

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenOrderReversesDirection_Short()
    {
        // Arrange - СЛУЧАЙ 2: Именно наш ордер пересекает baseline (меняет направление)
        // Short: potential=-110, new=10, baseline=-100 → CrossesBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 120m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -110m);
        SetupPotentialPosition(order, -110m); // Potential = -110, after order = 10

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAlreadyBelowBaseline_WhenPotentialAlreadyBelowBaseline_Long()
    {
        // Arrange - СЛУЧАЙ 3: Baseline пересечена до нашего ордера
        // Long: potential=90, new=80, baseline=100 → AlreadyBelowBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 100m);
        SetupPotentialPosition(order, 90m); // Potential = 90 (уже ниже baseline)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AlreadyBelowBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAlreadyBelowBaseline_WhenPotentialAlreadyBelowBaseline_Short()
    {
        // Arrange - СЛУЧАЙ 3: Baseline пересечена до нашего ордера
        // Short: potential=-90, new=-80, baseline=-100 → AlreadyBelowBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -100m);
        SetupPotentialPosition(order, -90m); // Potential = -90 (уже ниже baseline по модулю)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AlreadyBelowBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAlreadyBelowBaseline_WhenPotentialIsZero()
    {
        // Arrange - СЛУЧАЙ 3: Pending ордера уже закрыли всю позицию
        // Long: potential=0, new=-10, baseline=100 → AlreadyBelowBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 100m);
        SetupPotentialPosition(order, 0m); // Potential = 0 (pending ордера уже закрыли позицию)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AlreadyBelowBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAlreadyBelowBaseline_WhenPotentialReversedDirection()
    {
        // Arrange - СЛУЧАЙ 3: Pending ордера уже развернули позицию
        // Long: potential=-10, new=-20, baseline=100 → AlreadyBelowBaseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 100m);
        SetupPotentialPosition(order, -10m); // Potential = -10 (pending ордера уже развернули)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AlreadyBelowBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnAboveBaseline_WhenExactlyAtBaseline()
    {
        // Arrange - Граничный случай: позиция = baseline
        // Long: potential=110, new=100 (exactly at baseline), baseline=100
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m);
        SetupPotentialPosition(order, 110m); // Potential = 110, after order = 100 (exactly at baseline)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        // Позиция = baseline считается "не ниже baseline", поэтому AboveBaseline
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldReturnCrossesBaseline_WhenJustBelowBaseline()
    {
        // Arrange - Граничный случай: позиция чуть ниже baseline
        // Long: potential=110, new=99.99, baseline=100
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 10.01m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 110m);
        SetupPotentialPosition(order, 110m); // Potential = 110, after order = 99.99

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldHandleSmallDecimalValues()
    {
        // Arrange - Тест с очень малыми значениями
        await InitializeBaselinePosition(_wallet1, "BTC", 0.001m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 0.0005m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 0.0012m);
        SetupPotentialPosition(order, 0.0012m); // Potential = 0.0012, after order = 0.0007 (ниже baseline 0.001)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.CrossesBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldHandleLargePositions()
    {
        // Arrange - Тест с очень большими значениями
        await InitializeBaselinePosition(_wallet1, "BTC", 1000000m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 100000m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 1100000m);
        SetupPotentialPosition(order, 1100000m); // Potential = 1100000, after order = 1000000 (exactly at baseline)

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldWorkWithMultiplePendingOrders_Long()
    {
        // Arrange - СЛУЧАЙ 1 с pending ордерами
        // Есть pending ордера, но итоговая позиция все еще выше baseline
        await InitializeBaselinePosition(_wallet1, "BTC", 100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Short, 5m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", 130m);
        // Pending ордера уже забрали 10, поэтому potential = 120
        SetupPotentialPosition(order, 120m); // Potential = 120, after order = 115

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
    }

    [Fact]
    public async Task WillOrderCloseBelowBaseline_ShouldWorkWithMultiplePendingOrders_Short()
    {
        // Arrange - СЛУЧАЙ 1 с pending ордерами
        // Есть pending ордера, но итоговая позиция все еще выше baseline
        await InitializeBaselinePosition(_wallet1, "BTC", -100m);
        var order = CreateOrder(1, _wallet1, "BTC", Direction.Long, 5m, 50000m);
        SetupOrderFills(order);
        SetupCurrentPosition(_wallet1, "BTC", -130m);
        // Pending ордера уже забрали 10, поэтому potential = -120
        SetupPotentialPosition(order, -120m); // Potential = -120, after order = -115

        // Act
        var result = await _service.WillOrderCloseBelowBaseline(order.OrderId);

        // Assert
        result.Should().Be(BaselineCheckResult.AboveBaseline);
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

    private OriginalOrder CreateOrder(long orderId, Wallet wallet, string symbol, Direction direction, decimal quantity, decimal price)
    {
        return new OriginalOrder
        {
            OrderId = orderId,
            Wallet = wallet,
            Symbol = symbol,
            Direction = direction,
            Quantity = quantity,
            Price = price,
            Time = DateTime.UtcNow
        };
    }

    private void SetupOrderFills(OriginalOrder order)
    {
        var orderFills = new OrderFills(order);
        _fillsOrderServiceMock
            .Setup(x => x.GetOrderFillsByOrderId(order.OrderId))
            .Returns(orderFills);
    }

    private void SetupCurrentPosition(Wallet wallet, string symbol, decimal quantity)
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
            .Setup(x => x.GetSnapshot(wallet))
            .ReturnsAsync(snapshot);
    }

    private void SetupPotentialPosition(OriginalOrder order, decimal potentialQuantity)
    {
        _currentWalletPositionServiceMock
            .Setup(x => x.CalculatePotentialPosition(order))
            .Returns(potentialQuantity);
    }

    #endregion
}
