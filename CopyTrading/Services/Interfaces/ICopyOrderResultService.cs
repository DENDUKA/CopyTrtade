using CopyTrading.Models.Models;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICopyOrderResultService
{
    void SaveSuccess(string originalOrderId, Wallet traderWallet, string symbol, string copyOrderId);
    void SaveFailure(string originalOrderId, Wallet traderWallet, string symbol, string errorMessage);
    void SaveWarning(string originalOrderId, Wallet traderWallet, string symbol, string warningMessage);
    CopyOrderResult? GetResult(string originalOrderId);
    IEnumerable<CopyOrderResult> GetAllResults();
    (int Total, int Success, int Warning, int Error, double SuccessRate) GetStatistics();
    void ClearAllResults();
}
