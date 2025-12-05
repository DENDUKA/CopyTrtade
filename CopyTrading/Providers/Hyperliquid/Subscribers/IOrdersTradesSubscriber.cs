using CopyTrading.Models.Values;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public interface IOrdersTradesSubscriber
{
    /// <summary>
    /// Статус подписок на ордера для каждого кошелька.
    /// Возвращает массив кортежей (Кошелек, Подписан).
    /// </summary>
    (Wallet, bool)[] OrdersSubscriptionStatus { get; }

    /// <summary>
    /// Статус подписок на трейды для каждого кошелька.
    /// Возвращает массив кортежей (Кошелек, Подписан).
    /// </summary>
    (Wallet, bool)[] TradesSubscriptionStatus { get; }

    /// <summary>
    /// Получает статус подписки для указанного кошелька.
    /// </summary>
    /// <param name="wallet">Кошелек для проверки</param>
    /// <param name="ordersSubscribed">true если подписка на ордера активна</param>
    /// <param name="tradesSubscribed">true если подписка на трейды активна</param>
    void GetSubscribeStatus(Wallet wallet, out bool ordersSubscribed, out bool tradesSubscribed);

    /// <summary>
    /// Подписывается на обновления новых ордеров для указанных кошельков.
    /// После успешной подписки автоматически загружает все открытые ордера.
    /// </summary>
    /// <param name="wallets">Массив кошельков для подписки</param>
    Task SubscribeToNewOrders(Wallet[] wallets);

    /// <summary>
    /// Подписывается на обновления сделок для указанных кошельков.
    /// </summary>
    /// <param name="wallets">Массив кошельков для подписки</param>
    Task SubscribeToTrades(Wallet[] wallets);
}
