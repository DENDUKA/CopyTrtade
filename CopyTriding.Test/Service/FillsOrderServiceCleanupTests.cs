using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CopyTrading.Test.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Threading.Tasks;

namespace CopyTrading.Test.Service;

public class FillsOrderServiceCleanupTests
{
    private readonly Mock<ILogger<FillsOrderService>> _loggerMock;
    private readonly FillsOrderService _service;
    private readonly MockRedisStorage _storage;

    public FillsOrderServiceCleanupTests()
    {
        _loggerMock = new Mock<ILogger<FillsOrderService>>();
        var (redisMock, storage) = MockRedisRepositoryFactory.Create();
        _storage = storage;
        _service = new FillsOrderService(_loggerMock.Object, redisMock.Object);
    }

    [Fact]
    public async Task CleanupShouldNotRunWhenOrdersCountBelowThreshold()
    {
        // Arrange - добавляем 100 завершенных ордеров (меньше 2000)
        for (int i = 0; i < 100; i++)
        {
            var order = CreateOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-i));
            _service.OnNewOrders([order]);
        }

        var ordersCountBefore = (await _service.GetAllOrderFills()).Length;

        // Act - вызываем OnNewOrders для триггера очистки
        var newOrder = CreateOrder(9999, OrderStatus.Open, DateTime.Now);
        _service.OnNewOrders([newOrder]);

        // Assert - количество не должно измениться (кроме одного добавленного)
        var ordersCountAfter = (await _service.GetAllOrderFills()).Length;
        ordersCountAfter.Should().Be(ordersCountBefore + 1);
    }

    [Fact]
    public async Task CleanupShouldRemoveOldCompletedOrdersWhenThresholdExceeded()
    {
        // Arrange - добавляем 2100 завершенных ордеров (больше 2000)
        for (int i = 0; i < 2100; i++)
        {
            var status = i % 2 == 0 ? OrderStatus.Filled : OrderStatus.Canceled;
            var order = CreateOrder(i, status, DateTime.Now.AddMinutes(-i));

            // Напрямую вызываем обработку без триггера очистки
            var orders = new[] { order };
            foreach (var o in orders)
            {
                var orderFills = new OrderFills(o);
                GetOrdersDict().TryAdd(o.OrderId, orderFills);
            }
        }

        GetOrdersDict().Count.Should().Be(2100);

        // Act - добавляем новый ордер, который должен запустить очистку
        var newOrder = CreateOrder(10000, OrderStatus.Open, DateTime.Now);
        _service.OnNewOrders([newOrder]);

        // Assert - количество должно быть около 500 (целевое значение после очистки)
        var ordersCount = (await _service.GetAllOrderFills()).Length;
        ordersCount.Should().BeLessThanOrEqualTo(501); // 500 + 1 новый открытый ордер
    }

    [Fact]
    public async Task CleanupShouldRemoveOldestOrdersFirst()
    {
        // Arrange - добавляем 2100 завершенных ордеров с разным временем
        var oldestOrderId = 100L;
        var newestOrderId = 2000L;

        for (int i = 0; i < 2100; i++)
        {
            var order = CreateOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-2100 + i));

            // Напрямую добавляем в словарь
            var orderFills = new OrderFills(order);
            GetOrdersDict().TryAdd(order.OrderId, orderFills);
        }

        // Act - триггерим очистку
        var newOrder = CreateOrder(10000, OrderStatus.Open, DateTime.Now);
        _service.OnNewOrders([newOrder]);

        // Assert - самые старые ордера должны быть удалены
        (await _service.GetOrderFillsByOrderId(oldestOrderId)).Should().BeNull();

        // Самые новые ордера должны остаться
        (await _service.GetOrderFillsByOrderId(newestOrderId)).Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupShouldNotRemoveOpenOrders()
    {
        // Arrange - добавляем 1500 завершенных и 600 открытых ордеров
        for (int i = 0; i < 1500; i++)
        {
            var order = CreateOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-i));
            var orderFills = new OrderFills(order);
            GetOrdersDict().TryAdd(order.OrderId, orderFills);
        }

        for (int i = 1500; i < 2100; i++)
        {
            var order = CreateOrder(i, OrderStatus.Open, DateTime.Now.AddMinutes(-i));
            var orderFills = new OrderFills(order);
            GetOrdersDict().TryAdd(order.OrderId, orderFills);
        }

        var openOrdersBefore = (await _service.GetAllOrderFills())
            .Count(o => o.OriginalOrder.Status == OrderStatus.Open);

        openOrdersBefore.Should().Be(600);

        // Act - триггерим очистку
        var newOrder = CreateOrder(10000, OrderStatus.Open, DateTime.Now);
        _service.OnNewOrders([newOrder]);

        // Assert - открытые ордера не должны быть удалены
        var openOrdersAfter = (await _service.GetAllOrderFills())
            .Count(o => o.OriginalOrder.Status == OrderStatus.Open);

        openOrdersAfter.Should().Be(601); // 600 + 1 новый
    }

    // Вспомогательный метод для создания тестового ордера
    private static OriginalOrder CreateOrder(long orderId, OrderStatus status, DateTime time)
    {
        return new OriginalOrder
        {
            OrderId = orderId,
            Wallet = new Wallet("0x1234567890123456789012345678901234567890"),
            Symbol = "BTCUSDT",
            Price = 50000,
            Quantity = 0.1m,
            Leverage = 10,
            SubType = OrderSubType.Open,
            Direction = Direction.Long,
            Time = time,
            Status = status
        };
    }

    // Используем MockRedisStorage для доступа к ордерам
    private Dictionary<long, OrderFills> GetOrdersDict()
    {
        return _storage.Orders;
    }
}
