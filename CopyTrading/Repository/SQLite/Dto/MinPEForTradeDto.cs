namespace CopyTrading.Repository.SQLite.Dto;

public class MinPEForTradeDto
{
    public long TradeId { get; set; }
    public string Symbol { get; set; }
    public string Wallet { get; set; }
    public DateTime Time { get; set; }
    public double AccountVolume { get; set; }
    public double Volume { get; set; }
    public double MinPE { get; set; }
    public double Spread { get; set; }
    public double DeltaTimeS { get; set; }
    public long OrderId { get; set; }
}