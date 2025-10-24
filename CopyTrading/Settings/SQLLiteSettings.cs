namespace CopyTrading.Settings;

public static class SQLLiteSettings
{
    public static string Path = System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..",
        "SQLliteBD",
        "CopyTradingDB.db");
}
