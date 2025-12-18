using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using CopyTrading.Test.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTrading.Test.Service;

/// <summary>
/// Тесты для автоматической очистки памяти в CopyOrderStorageService
/// </summary>
public class CopyOrderStorageServiceCleanupTests
{
    private readonly Mock<ILogger<CopyOrderStorageService>> _loggerMock;
    private readonly CopyOrderStorageService _service;
    private readonly MockRedisStorage _storage;

    public CopyOrderStorageServiceCleanupTests()
    {
        _loggerMock = new Mock<ILogger<CopyOrderStorageService>>();
        var (redisMock, storage) = MockRedisRepositoryFactory.Create();
        _storage = storage;

        _service = new CopyOrderStorageService(redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void CleanupShouldNotRunWhenOrdersCountBelowThreshold()
    {
        // Arrange - добавляем 100 ордеров (меньше порога 2000)
        for (int i = 0; i < 100; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-i));
            _service.AddOrder(copyOrder);
        }

        var ordersCountBefore = _service.GetAllOrders().Length;

        // Act - добавляем новый ордер (должен триггернуть проверку очистки)
        var newOrder = CreateCopyOrder(9999, OrderStatus.Open, DateTime.Now);
        _service.AddOrder(newOrder);

        // Assert - количество не должно измениться (кроме одного добавленного)
        var ordersCountAfter = _service.GetAllOrders().Length;
        ordersCountAfter.Should().Be(ordersCountBefore + 1);
        ordersCountAfter.Should().Be(101);
    }

    [Fact]
    public void CleanupShouldRemoveOldOrdersWhenThresholdExceeded()
    {
        // Arrange - напрямую добавляем 2000 ордеров в словарь (имитируем накопление данных)
        for (int i = 0; i < 2000; i++)
        {
            var status = i % 3 == 0 ? OrderStatus.Filled :
                        i % 3 == 1 ? OrderStatus.Canceled :
                        OrderStatus.Open;
            var copyOrder = CreateCopyOrder(i, status, DateTime.Now.AddMinutes(-2000 + i));

            // Напрямую добавляем в словарь (без триггера очистки)
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        _storage.CopyOrders.Count.Should().Be(2000);

        // Act - добавляем новый ордер через AddOrder, который ДОЛЖЕН запустить очистку
        // (2000 в словаре + 1 добавляемый = 2001 > порог 2000)
        var newOrder = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder);

        // Assert - количество должно быть около 500-501 (целевое значение после очистки)
        var ordersCount = _service.GetAllOrders().Length;
        ordersCount.Should().BeLessThanOrEqualTo(501); // 500 + 1 новый ордер
        ordersCount.Should().BeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public void CleanupShouldRemoveOldestOrdersFirst()
    {
        // Arrange - добавляем 2000 ордеров с разным временем
        var oldestOrderId = 0L;
        var middleOrderId = 1000L;
        var newestOrderId = 1999L;

        for (int i = 0; i < 2000; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-2000 + i));

            // Напрямую добавляем в словарь
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        _storage.CopyOrders.Count.Should().Be(2000);

        // Act - добавляем новый ордер через AddOrder, который запустит очистку
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder1); // 2001 - не триггерит очистку

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddMinutes(2));
        _service.AddOrder(newOrder2); // 2002 - триггерит очистку

        // Assert - самые старые ордера должны быть удалены
        var allOrders = _service.GetAllOrders();
        allOrders.Should().NotContain(o => o.OrderId == oldestOrderId, "самый старый ордер должен быть удален");
        allOrders.Should().NotContain(o => o.OrderId == middleOrderId, "средние ордера должны быть удалены");

        // Самые новые ордера должны остаться
        allOrders.Should().Contain(o => o.OrderId == newestOrderId, "самый новый ордер должен остаться");
        allOrders.Should().Contain(o => o.OrderId == 10000, "первый добавленный ордер должен остаться");
        allOrders.Should().Contain(o => o.OrderId == 10001, "второй добавленный ордер должен остаться");
    }

    [Fact]
    public void CleanupShouldRemoveAllOrderTypes()
    {
        // Arrange - добавляем 2000 ордеров разных статусов (все должны быть удалены при превышении лимита)
        for (int i = 0; i < 2000; i++)
        {
            OrderStatus status;
            if (i % 3 == 0)
            {
                status = OrderStatus.Open;
            }
            else if (i % 3 == 1)
            {
                status = OrderStatus.Filled;
            }
            else
            {
                status = OrderStatus.Canceled;
            }

            var copyOrder = CreateCopyOrder(i, status, DateTime.Now.AddMinutes(-2000 + i));
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        _service.GetAllOrders().Length.Should().Be(2000);

        // Act - добавляем 2 ордера для триггера очистки
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder1); // 2001 - не триггерит

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddMinutes(2));
        _service.AddOrder(newOrder2); // 2002 - триггерит очистку

        // Assert - ордера всех статусов должны быть удалены (старые)
        var ordersAfter = _service.GetAllOrders();
        ordersAfter.Length.Should().BeLessThanOrEqualTo(501);

        // Проверяем что остались только новые ордера (ID >= 1500), т.к. удалено ~1500 самых старых
        var oldOrdersRemaining = ordersAfter.Count(o => o.OrderId < 1500);
        oldOrdersRemaining.Should().Be(0, "старые ордера должны быть удалены независимо от статуса");
    }

    [Fact]
    public void CleanupShouldLogCorrectInformation()
    {
        // Arrange - добавляем 2000 ордеров
        for (int i = 0; i < 2000; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-i));
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        // Act - добавляем 2 ордера, второй должен запустить очистку
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now);
        _service.AddOrder(newOrder1); // 2001

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddSeconds(1));
        _service.AddOrder(newOrder2); // 2002 - триггерит очистку

        // Assert - проверяем что логирование было вызвано
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Начинаем очистку старых ордеров")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "должно быть залогировано начало очистки");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Очистка завершена")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "должно быть залогировано завершение очистки");
    }

    [Fact]
    public void CleanupShouldReduceToTargetCount()
    {
        // Arrange - добавляем ровно 2000 ордеров
        for (int i = 0; i < 2000; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-2000 + i));
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        _storage.CopyOrders.Count.Should().Be(2000);

        // Act - добавляем 2 ордера, второй должен запустить очистку
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder1); // 2001 - не триггерит

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddMinutes(2));
        _service.AddOrder(newOrder2); // 2002 - триггерит очистку

        // Assert - должно остаться ровно 500-501 ордер (500 целевых + 0-1 новых)
        var ordersCount = _service.GetAllOrders().Length;
        ordersCount.Should().BeInRange(500, 501);
    }

    [Fact]
    public void MultipleCleanupsShouldWorkCorrectly()
    {
        // Arrange - добавляем 2000 ордеров
        for (int i = 0; i < 2000; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(-2000 + i));
            GetCopyOrdersDict().TryAdd(copyOrder.OrderId, copyOrder);
        }

        // Act 1 - первая очистка
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Open, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder1); // 2001

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddMinutes(2));
        _service.AddOrder(newOrder2); // 2002 - триггерит первую очистку

        var countAfterFirstCleanup = _service.GetAllOrders().Length;
        countAfterFirstCleanup.Should().BeInRange(500, 501);

        // Добавляем еще 1500 ордеров (чтобы снова превысить порог)
        for (int i = 20000; i < 21500; i++)
        {
            var copyOrder = CreateCopyOrder(i, OrderStatus.Filled, DateTime.Now.AddMinutes(3 + (i - 20000)));
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        // Act 2 - вторая очистка
        var newOrder3 = CreateCopyOrder(30000, OrderStatus.Open, DateTime.Now.AddMinutes(10000));
        _service.AddOrder(newOrder3); // 2001

        var newOrder4 = CreateCopyOrder(30001, OrderStatus.Open, DateTime.Now.AddMinutes(10001));
        _service.AddOrder(newOrder4); // 2002 - триггерит вторую очистку

        // Assert - снова должно быть ~500-501
        var countAfterSecondCleanup = _service.GetAllOrders().Length;
        countAfterSecondCleanup.Should().BeInRange(500, 501);
    }

    [Fact]
    public void GetStatisticsShouldWorkAfterCleanup()
    {
        // Arrange - добавляем 2000 ордеров разных статусов
        for (int i = 0; i < 2000; i++)
        {
            var status = i % 2 == 0 ? OrderStatus.Filled : OrderStatus.Open;
            var copyOrder = CreateCopyOrder(i, status, DateTime.Now.AddMinutes(-2000 + i));
            _storage.CopyOrders[copyOrder.OrderId] = copyOrder;
        }

        // Act - добавляем 2 ордера, второй запустит очистку
        var newOrder1 = CreateCopyOrder(10000, OrderStatus.Canceled, DateTime.Now.AddMinutes(1));
        _service.AddOrder(newOrder1); // 2001

        var newOrder2 = CreateCopyOrder(10001, OrderStatus.Open, DateTime.Now.AddMinutes(2));
        _service.AddOrder(newOrder2); // 2002 - триггерит очистку

        // Assert - статистика должна работать корректно
        var stats = _service.GetStatistics();
        stats["Total"].Should().BeInRange(500, 501);
        stats["Total"].Should().Be(stats["Open"] + stats["Filled"] + stats["Canceled"] +
                                   stats["Triggered"] + stats["Rejected"] + stats["MarginCanceled"]);
    }

    // Вспомогательный метод для создания тестового CopyOrderV2
    private static CopyOrderV2 CreateCopyOrder(long orderId, OrderStatus status, DateTime time)
    {
        var originalOrder = new OriginalOrder
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

        return new CopyOrderV2
        {
            OrderId = orderId,
            OriginalOrderId = orderId,
            OriginalOrder = originalOrder,
            OrderSubType = OrderSubType.Open,
            OrderRatio = 0.1m,
            MyPE = 10000,
            AccountPE = 100000,
            Quantity = 0.01m
        };
    }

}
