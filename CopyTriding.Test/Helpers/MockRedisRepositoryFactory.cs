using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using Moq;

namespace CopyTrading.Test.Helpers;

/// <summary>
/// Фабрика для создания mock IRedisRepository с in-memory хранилищем
/// </summary>
public static class MockRedisRepositoryFactory
{
    /// <summary>
    /// Создает mock IRedisRepository со всеми необходимыми setup
    /// </summary>
    public static (Mock<IRedisRepository> Mock, MockRedisStorage Storage) Create()
    {
        var storage = new MockRedisStorage();
        var mock = new Mock<IRedisRepository>();

        SetupOrders(mock, storage);
        SetupPendingTrades(mock, storage);
        SetupOrderErrors(mock, storage);
        SetupPositionMappings(mock, storage);
        SetupBaselinePositions(mock, storage);
        SetupCopyOrders(mock, storage);
        SetupCopyOrderResults(mock, storage);
        SetupWalletSnapshots(mock, storage);

        return (mock, storage);
    }

    private static void SetupOrders(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveOrder(It.IsAny<OrderFills>()))
            .Returns((OrderFills order) =>
            {
                storage.Orders[order.OriginalOrder.OrderId] = order;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetOrder(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.Orders.TryGetValue(orderId, out var order);
                return Task.FromResult(order);
            });

        mock.Setup(x => x.LoadAllOrders())
            .Returns(() => Task.FromResult(new Dictionary<long, OrderFills>(storage.Orders)));

        mock.Setup(x => x.DeleteOrder(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.Orders.Remove(orderId);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.DeleteOrders(It.IsAny<IEnumerable<long>>()))
            .Returns((IEnumerable<long> orderIds) =>
            {
                foreach (var id in orderIds)
                    storage.Orders.Remove(id);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetPendingOrdersByWalletAndSymbol(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string wallet, string symbol) =>
            {
                var pendingOrders = storage.Orders.Values
                    .Where(o => o.OriginalOrder.Wallet.Value == wallet &&
                                o.OriginalOrder.Symbol == symbol &&
                                (o.OriginalOrder.Status == OrderStatus.Open ||
                                 o.OriginalOrder.Status == OrderStatus.Triggered))
                    .ToArray();
                return Task.FromResult(pendingOrders);
            });

        mock.Setup(x => x.UpdateOrderSubType(It.IsAny<long>(), It.IsAny<OrderSubType>()))
            .Returns((long orderId, OrderSubType newSubType) =>
            {
                if (storage.Orders.TryGetValue(orderId, out var order))
                {
                    order.OriginalOrder.SubType = newSubType;
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            });
    }

    private static void SetupPendingTrades(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SavePendingTrade(It.IsAny<OriginalTrade>()))
            .Returns((OriginalTrade trade) =>
            {
                storage.PendingTrades[trade.TradeId] = trade;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetPendingTrade(It.IsAny<long>()))
            .Returns((long tradeId) =>
            {
                storage.PendingTrades.TryGetValue(tradeId, out var trade);
                return Task.FromResult(trade);
            });

        mock.Setup(x => x.LoadAllPendingTrades())
            .Returns(() => Task.FromResult(new Dictionary<long, OriginalTrade>(storage.PendingTrades)));

        mock.Setup(x => x.DeletePendingTrade(It.IsAny<long>()))
            .Returns((long tradeId) =>
            {
                storage.PendingTrades.Remove(tradeId);
                return Task.CompletedTask;
            });
    }

    private static void SetupOrderErrors(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveOrderError(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long orderId, string error) =>
            {
                storage.OrderErrors[orderId] = error;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetOrderError(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.OrderErrors.TryGetValue(orderId, out var error);
                return Task.FromResult(error);
            });

        mock.Setup(x => x.DeleteOrderError(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.OrderErrors.Remove(orderId);
                return Task.CompletedTask;
            });
    }

    private static void SetupPositionMappings(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SavePositionMapping(It.IsAny<PositionMapping>()))
            .Returns((PositionMapping mapping) =>
            {
                storage.PositionMappings[mapping.GetKey()] = mapping;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetPositionMapping(It.IsAny<string>()))
            .Returns((string key) =>
            {
                storage.PositionMappings.TryGetValue(key, out var mapping);
                return Task.FromResult(mapping);
            });

        mock.Setup(x => x.DeletePositionMapping(It.IsAny<string>()))
            .Returns((string key) =>
            {
                storage.PositionMappings.Remove(key);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.LoadAllPositionMappings())
            .Returns(() => Task.FromResult(new Dictionary<string, PositionMapping>(storage.PositionMappings)));

        mock.Setup(x => x.GetMappingsByTrader(It.IsAny<Wallet>()))
            .Returns((Wallet wallet) =>
            {
                var mappings = storage.PositionMappings.Values
                    .Where(m => m.TraderWallet.Equals(wallet))
                    .ToArray();
                return Task.FromResult(mappings);
            });
    }

    private static void SetupBaselinePositions(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveBaselinePosition(It.IsAny<Wallet>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .Returns((Wallet wallet, string symbol, decimal quantity) =>
            {
                var key = $"{wallet.Value}_{symbol}";
                storage.BaselinePositions[key] = quantity;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetBaselinePosition(It.IsAny<Wallet>(), It.IsAny<string>()))
            .Returns((Wallet wallet, string symbol) =>
            {
                var key = $"{wallet.Value}_{symbol}";
                storage.BaselinePositions.TryGetValue(key, out var quantity);
                return Task.FromResult<decimal?>(quantity == 0 ? null : quantity);
            });

        mock.Setup(x => x.DeleteBaselinePosition(It.IsAny<Wallet>(), It.IsAny<string>()))
            .Returns((Wallet wallet, string symbol) =>
            {
                var key = $"{wallet.Value}_{symbol}";
                storage.BaselinePositions.Remove(key);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.LoadAllBaselinePositions())
            .Returns(() => Task.FromResult(new Dictionary<string, decimal>(storage.BaselinePositions)));
    }

    private static void SetupCopyOrders(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveCopyOrder(It.IsAny<CopyOrderV2>()))
            .Returns((CopyOrderV2 order) =>
            {
                storage.CopyOrders[order.OrderId] = order;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetCopyOrder(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.CopyOrders.TryGetValue(orderId, out var order);
                return Task.FromResult(order);
            });

        mock.Setup(x => x.LoadAllCopyOrders())
            .Returns(() => Task.FromResult(new Dictionary<long, CopyOrderV2>(storage.CopyOrders)));

        mock.Setup(x => x.DeleteCopyOrder(It.IsAny<long>()))
            .Returns((long orderId) =>
            {
                storage.CopyOrders.Remove(orderId);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.DeleteCopyOrders(It.IsAny<IEnumerable<long>>()))
            .Returns((IEnumerable<long> orderIds) =>
            {
                foreach (var id in orderIds)
                    storage.CopyOrders.Remove(id);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetCopyOrdersByOriginalId(It.IsAny<long>()))
            .Returns((long originalOrderId) =>
            {
                var orders = storage.CopyOrders.Values
                    .Where(o => o.OriginalOrderId == originalOrderId)
                    .ToArray();
                return Task.FromResult(orders);
            });

        mock.Setup(x => x.GetCopyOrdersByWalletAndSymbol(It.IsAny<Wallet>(), It.IsAny<string>()))
            .Returns((Wallet wallet, string symbol) =>
            {
                var orders = storage.CopyOrders.Values
                    .Where(o => o.OriginalOrder.Wallet.Equals(wallet) && o.OriginalOrder.Symbol == symbol)
                    .ToArray();
                return Task.FromResult(orders);
            });
    }

    private static void SetupCopyOrderResults(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveCopyOrderResult(It.IsAny<CopyOrderResult>()))
            .Returns((CopyOrderResult result) =>
            {
                storage.CopyOrderResults[result.OriginalOrderId] = result;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetCopyOrderResult(It.IsAny<string>()))
            .Returns((string originalOrderId) =>
            {
                storage.CopyOrderResults.TryGetValue(originalOrderId, out var result);
                return Task.FromResult(result);
            });

        mock.Setup(x => x.DeleteCopyOrderResult(It.IsAny<string>()))
            .Returns((string originalOrderId) =>
            {
                storage.CopyOrderResults.Remove(originalOrderId);
                return Task.CompletedTask;
            });

        mock.Setup(x => x.LoadAllCopyOrderResults())
            .Returns(() => Task.FromResult(new Dictionary<string, CopyOrderResult>(storage.CopyOrderResults)));

        mock.Setup(x => x.DeleteCopyOrderResults(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> originalOrderIds) =>
            {
                foreach (var id in originalOrderIds)
                    storage.CopyOrderResults.Remove(id);
                return Task.CompletedTask;
            });
    }

    private static void SetupWalletSnapshots(Mock<IRedisRepository> mock, MockRedisStorage storage)
    {
        mock.Setup(x => x.SaveWalletSnapshot(It.IsAny<WalletPositionsSnapshot>()))
            .Returns((WalletPositionsSnapshot snapshot) =>
            {
                storage.WalletSnapshots[snapshot.Wallet.Value] = snapshot;
                return Task.CompletedTask;
            });

        mock.Setup(x => x.GetWalletSnapshot(It.IsAny<Wallet>()))
            .Returns((Wallet wallet) =>
            {
                storage.WalletSnapshots.TryGetValue(wallet.Value, out var snapshot);
                return Task.FromResult(snapshot);
            });
    }
}

/// <summary>
/// In-memory хранилище для mock Redis repository
/// </summary>
public class MockRedisStorage
{
    public Dictionary<long, OrderFills> Orders { get; } = new();
    public Dictionary<long, OriginalTrade> PendingTrades { get; } = new();
    public Dictionary<long, string> OrderErrors { get; } = new();
    public Dictionary<string, PositionMapping> PositionMappings { get; } = new();
    public Dictionary<string, decimal> BaselinePositions { get; } = new();
    public Dictionary<long, CopyOrderV2> CopyOrders { get; } = new();
    public Dictionary<string, CopyOrderResult> CopyOrderResults { get; } = new();
    public Dictionary<string, WalletPositionsSnapshot> WalletSnapshots { get; } = new();
}
