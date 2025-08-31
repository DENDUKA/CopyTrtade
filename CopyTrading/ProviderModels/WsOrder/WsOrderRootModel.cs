namespace CopyTrading.ProviderModels.WsOrder;

public class WsOrderRootModel
{
    public string channel { get; set; }
    public WsOrderModel[] data { get; set; }
}