namespace CopyTrading.Settings;

public class PostgreSQLSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "copytrading";
    public string Username { get; set; } = "copytrading_user";
    public string Password { get; set; } = "copytrading_password";

    /// <summary>
    /// Builds connection string from individual properties if ConnectionString is empty
    /// </summary>
    public string GetConnectionString()
    {
        if (!string.IsNullOrEmpty(ConnectionString))
            return ConnectionString;

        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
    }
}
