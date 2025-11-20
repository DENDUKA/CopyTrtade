using CopyTrading.Repository.SQLite;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class DatabaseController(
    LogRepository _logRepository,
    ILogger<DatabaseController> _logger) : ControllerBase
{
    [HttpPost("ClearLogs")]
    public async Task<IActionResult> ClearLogs()
    {
        try
        {
            _logger.LogInformation("Запрос на очистку логов...");
            await _logRepository.ClearAll();
            _logger.LogInformation("Логи успешно очищены");
            return Ok(new { success = true, message = "Логи успешно очищены" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке логов");
            return StatusCode(500, new { success = false, message = $"Ошибка: {ex.Message}" });
        }
    }
}
