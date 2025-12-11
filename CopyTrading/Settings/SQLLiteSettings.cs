namespace CopyTrading.Settings;

public static class SQLLiteSettings
{
    public static string Path
    {
        get
        {
            // Check if running in Docker container
            var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

            if (isDocker)
            {
                // Docker path
                return "/app/data/sqlite/CopyTradingDB.db";
            }
            else
            {
                // Local development path
                var dbPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..",
                    "SQLliteBD",
                    "CopyTradingDB.db");
                return System.IO.Path.GetFullPath(dbPath);
            }
        }
    }
}
