using Microsoft.AspNetCore.SignalR;
using CopyTrading.Services;
using CopyTrading.Models.Models;

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
        try
        {
            _logger.LogDebug("GetAllPositions вызван");
            var positions = _positionMappingService.GetAllMappings().ToArray();
            _logger.LogDebug($"GetAllPositions возвращает {positions.Length} позиций");
            return positions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в GetAllPositions");
            throw;
        }
    }

    /// <summary>
    /// Клиент запрашивает позиции по конкретному символу
    /// </summary>
    public PositionMapping[] GetPositionsBySymbol(string symbol)
    {
        _logger.LogDebug($"GetPositionsBySymbol вызван: {symbol}");
        return [.. _positionMappingService.GetAllMappings().Where(p => p.Symbol == symbol)];
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
            _logger.LogInformation("Начинаем отправку начальных данных");

            // Отправляем текущие позиции
            var positions = _positionMappingService.GetAllMappings().ToArray();
            _logger.LogInformation($"Получено {positions.Length} позиций из сервиса");

            // Логируем каждую позицию для отладки
            foreach (var pos in positions)
            {
                _logger.LogDebug($"Позиция: {pos.Symbol} {pos.Direction}, Trader: {pos.TraderWallet?.ToString() ?? "null"}, My: {pos.MyWallet?.ToString() ?? "null"}");
            }

            _logger.LogInformation("Попытка сериализации и отправки позиций...");
            await Clients.Caller.SendAsync("ReceiveInitialPositions", positions);

            _logger.LogInformation($"Успешно отправлено {positions.Length} позиций клиенту {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке начальных данных");
            throw;
        }
    }
}
