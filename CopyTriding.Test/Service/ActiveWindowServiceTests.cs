using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderTypeEnum = CopyTrading.Models.Models.Enums.Order.OrderType;

namespace CopyTrading.Test.Service;

public class ActiveWindowServiceTests
{
    private readonly Mock<IFillsOrderService> _fillsOrderServiceMock;
    private readonly Mock<ILogger<ActiveWindowService>> _loggerMock;
    private readonly ActiveWindowService _service;
    private readonly Wallet _wallet1 = new("0x1111111111111111111111111111111111111111");
    private readonly Wallet _wallet2 = new("0x2222222222222222222222222222222222222222");

    public ActiveWindowServiceTests()
    {
        _fillsOrderServiceMock = new Mock<IFillsOrderService>();
        _loggerMock = new Mock<ILogger<ActiveWindowService>>();
        _service = new ActiveWindowService(_fillsOrderServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetNearestOrders_ShouldReturnLongOrdersSortedByPriceDescending()
    {
        // Arrange - создаем Long ордера с разными ценами
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "BTC", Direction.Long, price: 50000, OrderStatus.Open),
            CreateOrderFills(2, _wallet1, "BTC", Direction.Long, price: 51000, OrderStatus.Open),
            CreateOrderFills(3, _wallet1, "BTC", Direction.Long, price: 49000, OrderStatus.Open),
            CreateOrderFills(4, _wallet1, "BTC", Direction.Long, price: 52000, OrderStatus.Open),
            CreateOrderFills(5, _wallet1, "BTC", Direction.Long, price: 48000, OrderStatus.Open),
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act
        var result = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 3);

        // Assert - Long ордера должны быть отсортированы от высокой цены к низкой
        result.Should().HaveCount(3);
        result[0].Price.Should().Be(52000); // Самая высокая цена
        result[1].Price.Should().Be(51000);
        result[2].Price.Should().Be(50000);
    }

    [Fact]
    public void GetNearestOrders_ShouldReturnShortOrdersSortedByPriceAscending()
    {
        // Arrange - создаем Short ордера с разными ценами
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "ETH", Direction.Short, price: 3000, OrderStatus.Open),
            CreateOrderFills(2, _wallet1, "ETH", Direction.Short, price: 2900, OrderStatus.Open),
            CreateOrderFills(3, _wallet1, "ETH", Direction.Short, price: 3100, OrderStatus.Open),
            CreateOrderFills(4, _wallet1, "ETH", Direction.Short, price: 2800, OrderStatus.Open),
            CreateOrderFills(5, _wallet1, "ETH", Direction.Short, price: 3200, OrderStatus.Open),
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "ETH"))
            .Returns(orders);

        // Act
        var result = _service.GetNearestOrders(_wallet1, "ETH", Direction.Short, count: 3);

        // Assert - Short ордера должны быть отсортированы от низкой цены к высокой
        result.Should().HaveCount(3);
        result[0].Price.Should().Be(2800); // Самая низкая цена
        result[1].Price.Should().Be(2900);
        result[2].Price.Should().Be(3000);
    }

    [Fact]
    public void GetNearestOrders_ShouldFilterByWalletAndSymbol()
    {
        // Arrange - создаем ордера для разных кошельков и символов
        var wallet1BtcOrders = new[]
        {
            CreateOrderFills(1, _wallet1, "BTC", Direction.Long, price: 50000, OrderStatus.Open),
            CreateOrderFills(2, _wallet1, "BTC", Direction.Long, price: 51000, OrderStatus.Open),
        };

        var wallet1EthOrders = new[]
        {
            CreateOrderFills(3, _wallet1, "ETH", Direction.Long, price: 3000, OrderStatus.Open),
        };

        var wallet2BtcOrders = new[]
        {
            CreateOrderFills(4, _wallet2, "BTC", Direction.Long, price: 52000, OrderStatus.Open),
        };

        // Setup для разных комбинаций wallet + symbol
        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(wallet1BtcOrders);

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "ETH"))
            .Returns(wallet1EthOrders);

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet2, "BTC"))
            .Returns(wallet2BtcOrders);

        // Act
        var wallet1BtcResult = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long);
        var wallet1EthResult = _service.GetNearestOrders(_wallet1, "ETH", Direction.Long);
        var wallet2BtcResult = _service.GetNearestOrders(_wallet2, "BTC", Direction.Long);

        // Assert - каждый запрос должен вернуть только свои ордера
        wallet1BtcResult.Should().HaveCount(2);
        wallet1BtcResult.Should().OnlyContain(o => o.Wallet == _wallet1 && o.Symbol == "BTC");

        wallet1EthResult.Should().HaveCount(1);
        wallet1EthResult.Should().OnlyContain(o => o.Wallet == _wallet1 && o.Symbol == "ETH");

        wallet2BtcResult.Should().HaveCount(1);
        wallet2BtcResult.Should().OnlyContain(o => o.Wallet == _wallet2 && o.Symbol == "BTC");
    }

    [Fact]
    public void GetNearestOrders_ShouldRespectCountParameter()
    {
        // Arrange - создаем 10 Long ордеров
        var orders = Enumerable.Range(1, 10)
            .Select(i => CreateOrderFills(i, _wallet1, "BTC", Direction.Long, price: 50000 + i * 100, OrderStatus.Open))
            .ToArray();

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act & Assert - тестируем разные значения count
        var result1 = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 1);
        result1.Should().HaveCount(1);

        var result3 = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 3);
        result3.Should().HaveCount(3);

        var result5 = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 5);
        result5.Should().HaveCount(5);

        var result10 = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 10);
        result10.Should().HaveCount(10);
    }

    [Fact]
    public void GetNearestOrders_ShouldReturnFewerOrders_WhenNotEnoughAvailable()
    {
        // Arrange - создаем только 2 Long ордера
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "BTC", Direction.Long, price: 50000, OrderStatus.Open),
            CreateOrderFills(2, _wallet1, "BTC", Direction.Long, price: 51000, OrderStatus.Open),
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act - запрашиваем 5 ордеров, но доступно только 2
        var result = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 5);

        // Assert - должно вернуться только 2 ордера
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetNearestOrders_ShouldReturnEmptyArray_WhenNoPendingOrders()
    {
        // Arrange - нет pending ордеров
        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(Array.Empty<OrderFills>());

        // Act
        var result = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetNearestOrders_ShouldReturnOnlyRequestedDirection()
    {
        // Arrange - создаем смесь Long и Short ордеров
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "BTC", Direction.Long, price: 50000, OrderStatus.Open),
            CreateOrderFills(2, _wallet1, "BTC", Direction.Short, price: 51000, OrderStatus.Open),
            CreateOrderFills(3, _wallet1, "BTC", Direction.Long, price: 49000, OrderStatus.Open),
            CreateOrderFills(4, _wallet1, "BTC", Direction.Short, price: 52000, OrderStatus.Open),
            CreateOrderFills(5, _wallet1, "BTC", Direction.Long, price: 48000, OrderStatus.Open),
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act
        var longResult = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 10);
        var shortResult = _service.GetNearestOrders(_wallet1, "BTC", Direction.Short, count: 10);

        // Assert
        longResult.Should().HaveCount(3);
        longResult.Should().OnlyContain(o => o.Direction == Direction.Long);

        shortResult.Should().HaveCount(2);
        shortResult.Should().OnlyContain(o => o.Direction == Direction.Short);
    }

    [Fact]
    public void GetNearestOrders_ShouldUseDefaultCount_WhenCountNotSpecified()
    {
        // Arrange - создаем 10 Long ордеров
        var orders = Enumerable.Range(1, 10)
            .Select(i => CreateOrderFills(i, _wallet1, "BTC", Direction.Long, price: 50000 + i * 100, OrderStatus.Open))
            .ToArray();

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act - не указываем count, должен использоваться дефолт (3)
        var result = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long);

        // Assert - должно вернуться 3 ордера (дефолтное значение)
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetNearestOrders_LongOrders_ShouldReturnOrdersClosestToExecution()
    {
        // Arrange - текущая цена ~50000, создаем Long лимит ордера
        // Long лимит ордера исполняются при падении цены
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "BTC", Direction.Long, price: 49900, OrderStatus.Open), // Ближайший к исполнению
            CreateOrderFills(2, _wallet1, "BTC", Direction.Long, price: 49800, OrderStatus.Open), // 2-й по близости
            CreateOrderFills(3, _wallet1, "BTC", Direction.Long, price: 49700, OrderStatus.Open), // 3-й по близости
            CreateOrderFills(4, _wallet1, "BTC", Direction.Long, price: 49000, OrderStatus.Open), // Дальше от исполнения
            CreateOrderFills(5, _wallet1, "BTC", Direction.Long, price: 48000, OrderStatus.Open), // Самый дальний
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "BTC"))
            .Returns(orders);

        // Act - получаем 3 ближайших Long ордера
        var result = _service.GetNearestOrders(_wallet1, "BTC", Direction.Long, count: 3);

        // Assert - должны вернуться ордера с самыми высокими ценами (ближайшие к исполнению при падении)
        result.Should().HaveCount(3);
        result[0].OrderId.Should().Be(1); // 49900
        result[1].OrderId.Should().Be(2); // 49800
        result[2].OrderId.Should().Be(3); // 49700
    }

    [Fact]
    public void GetNearestOrders_ShortOrders_ShouldReturnOrdersClosestToExecution()
    {
        // Arrange - текущая цена ~3000, создаем Short лимит ордера
        // Short лимит ордера исполняются при росте цены
        var orders = new[]
        {
            CreateOrderFills(1, _wallet1, "ETH", Direction.Short, price: 3100, OrderStatus.Open), // Ближайший к исполнению
            CreateOrderFills(2, _wallet1, "ETH", Direction.Short, price: 3200, OrderStatus.Open), // 2-й по близости
            CreateOrderFills(3, _wallet1, "ETH", Direction.Short, price: 3300, OrderStatus.Open), // 3-й по близости
            CreateOrderFills(4, _wallet1, "ETH", Direction.Short, price: 3500, OrderStatus.Open), // Дальше от исполнения
            CreateOrderFills(5, _wallet1, "ETH", Direction.Short, price: 4000, OrderStatus.Open), // Самый дальний
        };

        _fillsOrderServiceMock
            .Setup(x => x.GetPendingOrdersByWalletAndSymbol(_wallet1, "ETH"))
            .Returns(orders);

        // Act - получаем 3 ближайших Short ордера
        var result = _service.GetNearestOrders(_wallet1, "ETH", Direction.Short, count: 3);

        // Assert - должны вернуться ордера с самыми низкими ценами (ближайшие к исполнению при росте)
        result.Should().HaveCount(3);
        result[0].OrderId.Should().Be(1); // 3100
        result[1].OrderId.Should().Be(2); // 3200
        result[2].OrderId.Should().Be(3); // 3300
    }

    // Вспомогательный метод для создания тестового OrderFills
    private OrderFills CreateOrderFills(
        long orderId,
        Wallet wallet,
        string symbol,
        Direction direction,
        decimal price,
        OrderStatus status,
        decimal quantity = 1.0m)
    {
        var order = new OriginalOrder
        {
            OrderId = orderId,
            Wallet = wallet,
            Symbol = symbol,
            Price = price,
            Quantity = quantity,
            Leverage = 10,
            SubType = OrderSubType.Open,
            Direction = direction,
            Time = DateTime.UtcNow,
            Status = status,
            Type = OrderTypeEnum.Limit
        };

        return new OrderFills(order);
    }
}
