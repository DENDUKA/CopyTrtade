using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface IPositionMappingService
{
    int GetMappingsCount { get; }

    PositionMapping? GetMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction);
    void SaveOrUpdateMapping(PositionMapping mapping);
    bool DeleteMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction);
    bool UpdateMyQuantity(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction, decimal newQuantity);
    IEnumerable<PositionMapping> GetMappingsByTrader(Wallet traderWallet, Wallet myWallet);
    IEnumerable<PositionMapping> GetAllMappings();
    void ClearAllMappings();
}
