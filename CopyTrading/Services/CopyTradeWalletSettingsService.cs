using CopyTrading.Models.Models;
using CopyTrading.Models.Values;
using CopyTrading.Repository.SQLite;
using CopyTrading.Settings;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

/// <summary>
/// Хранит настройки копитрейда по каждому Wallet.
/// Пока поддерживается только объём в USD.
/// </summary>
public class CopyTradeWalletSettingsService
{
    private readonly ILogger<CopyTradeWalletSettingsService> _logger;
    private readonly WalletSettingsRepository _repository;
    private readonly ConcurrentDictionary<Wallet, CopyTradeWalletSettings> _settings = new();

    public CopyTradeWalletSettingsService(
        WalletSettingsRepository repository,
        ILogger<CopyTradeWalletSettingsService> logger)
    {
        _repository = repository;
        _logger = logger;

        Initialize().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Создать или обновить настройку для кошелька.
    /// </summary>
    public async Task<CopyTradeWalletSettings> CreateOrUpdate(CopyTradeWalletSettings settings)
    {
        Validate(settings);

        var updated = _settings.AddOrUpdate(
            settings.Wallet,
            _ => settings,
            (_, existing) => Apply(existing, settings));

        await Upsert(updated);
        _logger.LogInformation("[CopyTradeVolumePerWalletService] {Wallet} VolumeUsd={VolumeUsd} CopyKoef={CopyKoef}", settings.Wallet, settings.VolumeUsd, settings.CopyKoef);
        return updated;
    }

    /// <summary>
    /// Получить настройку по кошельку. Возвращает null, если не найдено.
    /// </summary>
    public virtual async Task<CopyTradeWalletSettings?> Get(Wallet wallet)
    {
        if (wallet is null) throw new ArgumentNullException(nameof(wallet));

        if (_settings.TryGetValue(wallet, out var value))
            return value;

        try
        {
            var fromDb = await _repository.Get(wallet);
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
    /// Получить все текущие настройки.
    /// </summary>
    public Task<IEnumerable<CopyTradeWalletSettings>> GetAll()
    {
        return Task.FromResult<IEnumerable<CopyTradeWalletSettings>>(_settings.Values.ToArray());
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

    private async Task Initialize()
    {
        try
        {
            var all = await _repository.GetAll();
            foreach (var s in all) _settings[s.Wallet] = s;
            _logger.LogInformation("[CopyTradeVolumePerWalletService] Загружено {Count} настроек из БД", _settings.Count);

            // Сидирование дефолтов для TrackedWallets
            var created = 0;
            foreach (var wallet in WalletSettings.TrackedWallets)
            {
                if (_settings.ContainsKey(wallet)) continue;

                var settings = new CopyTradeWalletSettings
                {
                    Wallet = wallet,
                    VolumeUsd = 0m,
                    CopyKoef = 1.0m
                };
                await _repository.Upsert(settings);
                _settings[wallet] = settings;
                created++;
            }
            if (created > 0)
                _logger.LogInformation("[CopyTradeVolumePerWalletService] Создано {Created} дефолтных настроек для TrackedWallets", created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка инициализации сервиса");
        }
    }

    #region Helpers
    private static void Validate(CopyTradeWalletSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        if (settings.Wallet is null) throw new ArgumentNullException(nameof(settings.Wallet));
        if (settings.VolumeUsd < 0) throw new ArgumentOutOfRangeException(nameof(settings.VolumeUsd), "VolumeUsd не может быть отрицательным");
        if (settings.CopyKoef <= 0) throw new ArgumentOutOfRangeException(nameof(settings.CopyKoef), "CopyKoef должен быть > 0");
    }

    private static CopyTradeWalletSettings Apply(CopyTradeWalletSettings target, CopyTradeWalletSettings source)
    {
        target.VolumeUsd = source.VolumeUsd;
        target.CopyKoef = source.CopyKoef;
        return target;
    }

    private async Task Upsert(CopyTradeWalletSettings settings)
    {
        try
        {
            await _repository.Upsert(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CopyTradeVolumePerWalletService] Ошибка сохранения настройки в БД для {Wallet}", settings.Wallet);
            throw;
        }
    }
    #endregion
}
