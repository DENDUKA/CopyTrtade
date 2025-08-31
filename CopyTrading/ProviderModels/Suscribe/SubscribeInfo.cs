using WatsonWebsocket;

namespace CopyTrading.ProviderModels.Suscribe;

public record SubscribeInfo
{
    public WatsonWsClient Client { get; set; }
    public SubscribeStatus Status { get; set; }
    public int RecconnectCount { get; set; }
    public CancellationTokenSource TokenSource { get; set; }
}