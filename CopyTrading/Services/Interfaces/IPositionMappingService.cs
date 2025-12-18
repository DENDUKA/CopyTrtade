using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис управления маппингами позиций между трейдером и копирующим кошельком.
///
/// ОСНОВНЫЕ ОБЯЗАННОСТИ:
/// 1. Хранение связей между позициями трейдера и вашими скопированными позициями
/// 2. Отслеживание пропорций (PositionRatio) для корректного масштабирования операций
/// 3. Сохранение "базовой линии" (TraderQuantityAtEntry) для определения момента полного закрытия
/// 4. Управление жизненным циклом маппингов (создание, обновление, удаление)
///
/// КЛЮЧЕВЫЕ КОНЦЕПЦИИ:
///
/// PositionRatio (Коэффициент позиции):
/// - Рассчитывается при первом открытии: MyQuantity / TraderQuantity
/// - Сохраняется и используется для всех последующих операций Increase/Decrease
/// - Гарантирует пропорциональность изменений позиции
/// - Пример: Ratio=0.1 означает что ваша позиция = 10% от позиции трейдера
///
/// TraderQuantityAtEntry (Базовая линия):
/// - Позиция трейдера ДО первого Increase, который вы скопировали
/// - Устанавливается один раз при создании маппинга и НЕ изменяется
/// - Используется для определения момента полного закрытия:
///   * Если трейдер уходит НИЖЕ базовой линии → закрываем ВСЮ вашу позицию
///   * Если трейдер ВЫШЕ базовой → частично закрываем пропорционально
///
/// MyQuantity (Ваше количество):
/// - Текущее количество в вашей позиции
/// - Обновляется при каждом Increase/Decrease
/// - Используется для расчета объема при частичном закрытии
///
/// АРХИТЕКТУРНЫЕ ОСОБЕННОСТИ:
/// - In-memory хранилище (ConcurrentDictionary) без персистентности
/// - Потокобезопасность через ConcurrentDictionary
/// - Ключ маппинга: "{TraderWallet}_{MyWallet}_{Symbol}_{Direction}"
/// - Одна позиция = один маппинг (раздельно для Long и Short)
/// - Данные теряются при перезапуске приложения
///
/// ЖИЗНЕННЫЙ ЦИКЛ МАППИНГА:
///
/// 1. СОЗДАНИЕ (Open или первый Increase без маппинга):
///    - CopyOrderService создает новый PositionMapping
///    - Рассчитывает PositionRatio на основе текущих балансов
///    - Устанавливает TraderQuantityAtEntry = позиция трейдера ДО increase
///    - Сохраняет через SaveOrUpdateMapping()
///
/// 2. ОБНОВЛЕНИЕ (Increase/Decrease с существующим маппингом):
///    - GetMapping() возвращает существующий маппинг
///    - CopyOrderService рассчитывает новое MyQuantity
///    - Обновляет через SaveOrUpdateMapping() или UpdateMyQuantity()
///    - PositionRatio и TraderQuantityAtEntry остаются неизменными
///
/// 3. УДАЛЕНИЕ (Close или Decrease ниже базовой линии):
///    - Вызывается DeleteMapping() при полном закрытии позиции
///    - Маппинг удаляется из памяти
///    - При новом входе создается новый маппинг с новыми параметрами
///
/// ИСПОЛЬЗОВАНИЕ:
/// - CopyOrderService - основной потребитель для всех операций копирования
/// - При обработке Open/Increase/Decrease/Close ордеров
/// - Для проверки наличия открытых позиций перед копированием
/// - Для расчета объемов при масштабировании операций трейдера
///
/// ВАЖНО:
/// - Сервис НЕ персистентный - данные теряются при перезапуске
/// - НЕ отслеживает реальные позиции на бирже (только математическая модель)
/// - НЕ проверяет корректность данных (это делает CopyOrderService)
/// - Один Symbol+Direction может иметь несколько маппингов для разных пар TraderWallet+MyWallet
/// </summary>
public interface IPositionMappingService
{
    int GetMappingsCount { get; }

    Task<PositionMapping?> GetMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction);

    Task SaveOrUpdateMapping(PositionMapping mapping);

    Task<bool> DeleteMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction);

    Task<bool> UpdateMyQuantity(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction, decimal newQuantity);

    Task<IEnumerable<PositionMapping>> GetMappingsByTrader(Wallet traderWallet, Wallet myWallet);

    Task<IEnumerable<PositionMapping>> GetAllMappings();

    Task ClearAllMappings();
}
