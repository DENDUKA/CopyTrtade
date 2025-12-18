using CopyTrading.Models.Models;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICopyOrderResultService
{
    Task SaveSuccess(string originalOrderId, Wallet traderWallet, string symbol, string copyOrderId);
    Task SaveFailure(string originalOrderId, Wallet traderWallet, string symbol, string errorMessage);
    Task SaveWarning(string originalOrderId, Wallet traderWallet, string symbol, string warningMessage);
    Task<CopyOrderResult?> GetResult(string originalOrderId);
    Task<IEnumerable<CopyOrderResult>> GetAllResults();
    Task<(int Total, int Success, int Warning, int Error, double SuccessRate)> GetStatistics();
    Task ClearAllResults();
}
