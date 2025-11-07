using Microsoft.AspNetCore.SignalR;
using CopyTrading.BlazorUI.Hubs;
using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Models.Enums.Order;

namespace CopyTrading.BlazorUI.Services;

/// <summary>
/// Сервис для отправки real-time обновлений в Blazor UI через SignalR
/// Подписывается на DataBusEvents и транслирует данные всем подключенным клиентам
/// </summary>
public class RealtimeUpdateService
{
    private readonly IHubContext<CopyTradingHub> _hubContext;
    private readonly ILogger<RealtimeUpdateService> _logger;

    public RealtimeUpdateService(
        IHubContext<CopyTradingHub> hubContext,
        ILogger<RealtimeUpdateService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        // Подписываемся на события из DataBusEvents
        SubscribeToEvents();

        _logger.LogInformation("RealtimeUpdateService запущен и подписан на DataBusEvents");
    }


    /// <summary>
    /// Обработчик новых трейдов
    /// </summary>
    public async Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) tradesData)
    {
        try
        {
            // Не отправляем снапшоты (начальная загрузка)
            if (tradesData.IsSnapshot)
            {
                _logger.LogDebug("Пропускаем snapshot трейдов");
                return;
            }

            _logger.LogInformation($"RealtimeUpdateService: Получено {tradesData.Trades.Length} новых трейдов");

            // Отправляем всем подключенным клиентам
            await _hubContext.Clients.All.SendAsync("ReceiveTrades", tradesData.Trades);

            _logger.LogDebug($"Отправлено {tradesData.Trades.Length} трейдов всем клиентам");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке трейдов через SignalR");
        }
    }

    /// <summary>
    /// Подписка на все события DataBusEvents
    /// </summary>
    private void SubscribeToEvents()
    {
        DataBusEvents.NewOrders += OnNewOrders;
        DataBusEvents.OrderFinished += OnOrderFinished;
    }

    /// <summary>
    /// Обработчик новых ордеров
    /// </summary>
    private async void OnNewOrders(OriginalOrder[] orders)
    {
        try
        {
            _logger.LogInformation($"RealtimeUpdateService: Получено {orders.Length} новых ордеров");

            // Отправляем всем подключенным клиентам
            await _hubContext.Clients.All.SendAsync("ReceiveOrders", orders);

            _logger.LogDebug($"Отправлено {orders.Length} ордеров всем клиентам");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке ордеров через SignalR");
        }
    }

    /// <summary>
    /// Обработчик завершенных ордеров
    /// </summary>
    private async void OnOrderFinished(OrderFills orderFills)
    {
        try
        {
            _logger.LogInformation($"RealtimeUpdateService: Ордер завершен - {orderFills.OriginalOrder.OrderId}");

            // Отправляем всем подключенным клиентам
            await _hubContext.Clients.All.SendAsync("ReceiveOrderFinished", orderFills);

            _logger.LogDebug($"Отправлено уведомление о завершении ордера {orderFills.OriginalOrder.OrderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке завершенного ордера через SignalR");
        }
    }

    /// <summary>
    /// Отправить обновление позиций (вызывается из CopyOrderService после изменения)
    /// </summary>
    public async Task SendPositionsUpdate()
    {
        try
        {
            _logger.LogDebug("Отправка обновления позиций всем клиентам");

            // Уведомляем клиентов что нужно обновить позиции
            await _hubContext.Clients.All.SendAsync("PositionsUpdated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке обновления позиций");
        }
    }
}
