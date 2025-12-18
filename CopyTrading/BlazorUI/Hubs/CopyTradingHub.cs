using Microsoft.AspNetCore.SignalR;
using CopyTrading.Services.Interfaces;
using CopyTrading.Models.Models;
using System.Threading.Tasks;

namespace CopyTrading.BlazorUI.Hubs;

/// <summary>
/// SignalR Hub для real-time обновлений Copy Trading данных
/// </summary>
public class CopyTradingHub(
    IPositionMappingService positionMappingService,
    ICurrentWalletPositionService walletPositionService,
    IOrderService orderService,
    ILogger<CopyTradingHub> logger) : Hub
{

    /// <summary>
    /// Клиент подключился - отправляем текущее состояние
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation($"Клиент подключился: {Context.ConnectionId}");

        // Отправляем начальные данные клиенту
        await SendInitialData();

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Клиент отключился
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation($"Клиент отключился: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Клиент запрашивает все активные позиции
    /// </summary>
    public async Task<PositionMapping[]> GetAllPositions()
    {
        try
        {
            logger.LogDebug("GetAllPositions вызван");
            var positions = (await positionMappingService.GetAllMappings()).ToArray();
            logger.LogDebug($"GetAllPositions возвращает {positions.Length} позиций");
            return positions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в GetAllPositions");
            throw;
        }
    }

    /// <summary>
    /// Клиент запрашивает позиции по конкретному символу
    /// </summary>
    public async Task<PositionMapping[]> GetPositionsBySymbol(string symbol)
    {
        logger.LogDebug($"GetPositionsBySymbol вызван: {symbol}");
        var positions = await positionMappingService.GetAllMappings();

        return [.. positions.Where(p => p.Symbol == symbol)];
    }

    /// <summary>
    /// Клиент запрашивает количество активных позиций
    /// </summary>
    public int GetPositionsCount()
    {
        return positionMappingService.GetMappingsCount;
    }

    /// <summary>
    /// Отправить начальные данные при подключении
    /// </summary>
    private async Task SendInitialData()
    {
        try
        {
            logger.LogInformation("Начинаем отправку начальных данных");

            // Отправляем текущие позиции
            var positions = (await positionMappingService.GetAllMappings()).ToArray();
            logger.LogInformation($"Получено {positions.Length} позиций из сервиса");

            // Логируем каждую позицию для отладки
            foreach (var pos in positions)
            {
                logger.LogDebug($"Позиция: {pos.Symbol} {pos.Direction}, Trader: {pos.TraderWallet?.ToString() ?? "null"}, My: {pos.MyWallet?.ToString() ?? "null"}");
            }

            logger.LogInformation("Попытка сериализации и отправки позиций...");
            await Clients.Caller.SendAsync("ReceiveInitialPositions", positions);

            logger.LogInformation($"Успешно отправлено {positions.Length} позиций клиенту {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при отправке начальных данных");
            throw;
        }
    }
}
