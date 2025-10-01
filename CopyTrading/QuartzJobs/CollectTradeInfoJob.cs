using CopyTrading.Repository.SQLite;
using CopyTrading.Services.Interfaces;
using Quartz;

namespace CopyTrading.QuartzJobs;

public class CollectTradeInfoJob : IJob
{
    private readonly TradeRepository _tradeRepository;
    private readonly IWalletInfoProvider _walletInfoProvider;
    private readonly ILogger<CollectTradeInfoJob> _logger;
    public CollectTradeInfoJob(
        TradeRepository tradeRepository,
        IWalletInfoProvider walletInfoProvider,
        ILogger<CollectTradeInfoJob> logger)
    {
        _tradeRepository = tradeRepository;
        _walletInfoProvider = walletInfoProvider;
        _logger = logger;
    }

    //Тут надо забирать из БД все записи у которых нет Direction и заполнять из отдельного запроса GetFills
    public async Task Execute(IJobExecutionContext context)
    {
        /*
        var trades = await _tradeRepository.MinPerpEquityForTradesQuery(new MinPerpEquityForTradesQuery
        {
            SubTypes = [OrderSubType.None, OrderSubType.Increase]
        });

        var wallets = trades.Select(x => x.Wallet).Distinct().ToArray();        

        foreach (var wallet in wallets)
        {
            var historycalTrades = await _walletInfoProvider.GetHistoricalTrades(wallet);
            //var historicalOrders = await _walletInfoProvider.GetHistoricalOrders(wallet)

            var tradesForWallet = trades.Where(x => x.Wallet == wallet).ToArray();

            var historycalTradesDict = historycalTrades.ToDictionary(ht => ht.TradeId);

            var notFoundCount = 0;

            var tradesForUpdate = new List<MinPEForTradeDto>();

            foreach (var trade in tradesForWallet)
            {
                if (historycalTradesDict.TryGetValue(trade.TradeId, out var historicalTrade))
                {
                    trade.SubType = historicalTrade.SubType;
                    trade.OrderId = historicalTrade.OrderId;
                    trade.Time = historicalTrade.TimeStamp;

                    tradesForUpdate.Add(trade);
                }
                else
                {
                    notFoundCount++;
                }
            }

            await _tradeRepository.WriteMinPeForTrade(tradesForUpdate.ToArray());

            _logger.LogInformation($"CollectTradeInfoJob {wallet} Обновлено {tradesForUpdate.Count}\nНе обновлено {notFoundCount}");
        }
        */
    }
}