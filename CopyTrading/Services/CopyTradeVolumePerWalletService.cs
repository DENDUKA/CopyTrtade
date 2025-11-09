using CopyTrading.Models.Models;
using CopyTrading.Models.Values;
using CopyTrading.Repository.SQLite;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CopyTrading.Services;

/// <summary>
/// Хранит настройки копитрейда по каждому Wallet.
/// Пока поддерживается только объём в USD.
/// </summary>
public class CopyTradeVolumePerWalletService
{
    private readonly ILogger<CopyTradeVolumePerWalletService> _logger;
    private readonly WalletSettingsRepository _repository;
    private readonly ConcurrentDictionary<Wallet, CopyTradeWalletSettings> _settings = new();

    public CopyTradeVolumePerWalletService(
        WalletSettingsRepository repository,
        ILogger<CopyTradeVolumePerWalletService> logger)
    {
        _repository = repository;
        _logger = logger;

        // Предзагрузка настроек из БД (опционально). Ошибки логируем, но не валим приложение.
        try
        {
            var settings = _repository.GetAll().GetAwaiter().GetResult();
            foreach (var walletSetting in settings)
            {
                _settings[walletSetting.Wallet] = walletSetting;
            }
            _logger.LogInformation("[CopyTradeVolumePerWalletService] Загружено {Count} настроек из БД", _settings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка загрузки настроек из БД при старте");
        }
    }

    /// <summary>
    /// Создать или обновить настройку для кошелька.
    /// </summary>
    public async Task<CopyTradeWalletSettings> CreateOrUpdate(CopyTradeWalletSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (settings.Wallet is null) throw new ArgumentNullException(nameof(settings.Wallet));
        if (settings.VolumeUsd < 0) throw new ArgumentOutOfRangeException(nameof(settings.VolumeUsd), "VolumeUsd не может быть отрицательным");
        if (settings.CopyKoef <= 0) throw new ArgumentOutOfRangeException(nameof(settings.CopyKoef), "CopyKoef должен быть > 0");

        var updated = _settings.AddOrUpdate(
            settings.Wallet,
            addValueFactory: _ => settings,
            updateValueFactory: (_, existing) =>
            {
                existing.VolumeUsd = settings.VolumeUsd;
                existing.CopyKoef = settings.CopyKoef;
                return existing;
            });

        try
        {
            await _repository.Upsert(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка сохранения настройки в БД для {Wallet}", settings.Wallet);
        }

        _logger.LogInformation("[CopyTradeVolumePerWalletService] {Wallet} VolumeUsd={VolumeUsd} CopyKoef={CopyKoef}", settings.Wallet, settings.VolumeUsd, settings.CopyKoef);
        return updated;
    }

    /// <summary>
    /// Получить настройку по кошельку. Возвращает null, если не найдено.
    /// </summary>
    public CopyTradeWalletSettings? Get(Wallet wallet)
    {
        if (wallet is null) throw new ArgumentNullException(nameof(wallet));

        if (_settings.TryGetValue(wallet, out var value))
            return value;

        // Попытка ленивой загрузки из БД
        try
        {
            var fromDb = _repository.Get(wallet).GetAwaiter().GetResult();
            if (fromDb is not null)
            {
                _settings[wallet] = fromDb;
                return fromDb;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка чтения настройки из БД для {Wallet}", wallet);
        }

        return null;
    }

    /// <summary>
    /// Попытаться получить настройку без исключений.
    /// </summary>
    public bool TryGet(Wallet wallet, out CopyTradeWalletSettings settings)
        => _settings.TryGetValue(wallet, out settings!);

    /// <summary>
    /// Получить все текущие настройки.
    /// </summary>
    public async Task<IEnumerable<CopyTradeWalletSettings>> GetAll()
    {
        if (_settings.Count == 0)
        {
            try
            {
                var all = await _repository.GetAll();
                foreach (var s in all) _settings[s.Wallet] = s;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка загрузки GetAll из БД");
            }
        }
        return _settings.Values.ToArray();
    }

    public async Task<bool> Delete(CopyTradeWalletSettings settings)
    {
        if (settings is null || settings.Wallet is null) throw new ArgumentNullException(nameof(settings));
        var removed = _settings.TryRemove(settings.Wallet, out _);
        try
        {
            var dbRemoved = await _repository.Delete(settings.Wallet);
            return removed || dbRemoved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка удаления настройки из БД для {Wallet}", settings.Wallet);
            throw;
        }
    }
}
