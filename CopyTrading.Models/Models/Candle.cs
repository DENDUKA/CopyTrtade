namespace CopyTrading.Models.Models;

public class Candle
{
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public string Symbol { get; set; }
    public decimal Volume { get; set; }
    public DateTime Time { get; set; }
    public int Interval { get; set; }
}
