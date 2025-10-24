using Microsoft.AspNetCore.SignalR;
using CopyTrading.Services;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Orders;

namespace CopyTrading.BlazorUI.Hubs;

/// <summary>
/// SignalR Hub для real-time обновлений Copy Trading данных
/// </summary>
public class CopyTradingHub : Hub
{
    private readonly PositionMappingService _positionMappingService;
    private readonly CurrentWalletPositionService _walletPositionService;
    private readonly OrderService _orderService;
    private readonly ILogger<CopyTradingHub> _logger;

    public CopyTradingHub(
        PositionMappingService positionMappingService,
        CurrentWalletPositionService walletPositionService,
        OrderService orderService,
        ILogger<CopyTradingHub> logger)
    {
        _positionMappingService = positionMappingService;
        _walletPositionService = walletPositionService;
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Клиент подключился - отправляем текущее состояние
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Клиент подключился: {Context.ConnectionId}");

        // Отправляем начальные данные клиенту
        await SendInitialData();

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Клиент отключился
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Клиент отключился: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Клиент запрашивает все активные позиции
    /// </summary>
    public async Task<PositionMapping[]> GetAllPositions()
    {
        _logger.LogDebug("GetAllPositions вызван");
        return _positionMappingService.GetAllMappings().ToArray();
    }

    /// <summary>
    /// Клиент запрашивает позиции по конкретному символу
    /// </summary>
    public PositionMapping[] GetPositionsBySymbol(string symbol)
    {
        _logger.LogDebug($"GetPositionsBySymbol вызван: {symbol}");
        return _positionMappingService.GetAllMappings()
            .Where(p => p.Symbol == symbol)
            .ToArray();
    }

    /// <summary>
    /// Клиент запрашивает количество активных позиций
    /// </summary>
    public int GetPositionsCount()
    {
        return _positionMappingService.GetMappingsCount;
    }

    /// <summary>
    /// Отправить начальные данные при подключении
    /// </summary>
    private async Task SendInitialData()
    {
        try
        {
            // Отправляем текущие позиции
            var positions = _positionMappingService.GetAllMappings();
            await Clients.Caller.SendAsync("ReceiveInitialPositions", positions);

            _logger.LogInformation($"Отправлено {positions.Count()} позиций клиенту {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке начальных данных");
        }
    }
}
