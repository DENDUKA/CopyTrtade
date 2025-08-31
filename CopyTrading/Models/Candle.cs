
namespace CopyTrading.Models;

public class Candle
{
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public string Symbol { get; set; }
    public double Volume { get; set; }
    public DateTime Time { get; set; }
    public int Interval { get; set; }
}
