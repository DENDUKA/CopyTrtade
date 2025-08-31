using CopyTrading.Models.Enums;
using System.Text.Json.Serialization;

namespace CopyTrading.ProviderModels.FetchOrder;

public class FetchOrderRootModel
{
    public FetchOrderModel order { get; set; }
    [JsonConverter(typeof(OrderStatusJsonConverter))]
    public OrderStatus status { get; set; }
    public long statusTimestamp { get; set; }
}
