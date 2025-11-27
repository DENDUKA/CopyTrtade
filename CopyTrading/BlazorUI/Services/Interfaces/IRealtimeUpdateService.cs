using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;

namespace CopyTrading.BlazorUI.Services.Interfaces;

public interface IRealtimeUpdateService
{
    Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) tradesData);
    Task OnNewOrders(OriginalOrder[] orders);
    Task SendPositionsUpdate();
}
