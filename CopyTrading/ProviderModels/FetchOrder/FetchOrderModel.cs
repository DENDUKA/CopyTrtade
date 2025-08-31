namespace CopyTrading.ProviderModels.FetchOrder;

public class FetchOrderModel
{
    public string coin { get; set; }
    public string side { get; set; }
    public string limitPx { get; set; }
    public string sz { get; set; }
   // public int oid { get; set; }
    public long timestamp { get; set; }
    public string triggerCondition { get; set; }
    public bool isTrigger { get; set; }
    public string triggerPx { get; set; }
    public object[] children { get; set; }
    public bool isPositionTpsl { get; set; }
    public bool reduceOnly { get; set; }
    public string orderType { get; set; }
    public string origSz { get; set; }
    public string tif { get; set; }
    public object cloid { get; set; }
}
