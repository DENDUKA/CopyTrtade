namespace CopyTrading.ProviderModels.WsOrder;

public class WsSubscriptionResponse
{
    public class Rootobject
    {
        public string channel { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string method { get; set; }
        public Subscription subscription { get; set; }
    }

    public class Subscription
    {
        public string type { get; set; }
        public string user { get; set; }
    }
}
