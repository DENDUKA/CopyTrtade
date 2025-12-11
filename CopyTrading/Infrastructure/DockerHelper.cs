using System.Diagnostics;

namespace CopyTrading.Infrastructure;

/// <summary>
/// Helper class for managing Docker containers
/// </summary>
public static class DockerHelper
{
    private const string PostgresContainerName = "copytrading-postgres";
    private const int MaxRetries = 30;
    private const int RetryDelayMs = 1000;

    /// <summary>
    /// Ensures PostgreSQL container is running, starts it if necessary
    /// </summary>
    public static async Task EnsurePostgresRunning()
    {
        try
        {
            Console.WriteLine("[Docker] Checking PostgreSQL container status...");

            // Check if container exists and is running
            var isRunning = await IsContainerRunning(PostgresContainerName);

            if (isRunning)
            {
                Console.WriteLine("[Docker] PostgreSQL container is already running");
                return;
            }

            Console.WriteLine("[Docker] PostgreSQL container is not running, starting all containers ...");

            // Start container using docker-compose
            var startResult = await StartPostgresContainer();

            if (!startResult)
            {
                Console.WriteLine("[Docker] WARNING: Failed to start Docker containers. Application will try to connect anyway.");
                return;
            }

            Console.WriteLine("[Docker] Docker containers started, waiting for PostgreSQL to be ready...");

            // Wait for PostgreSQL to be ready
            var isReady = await WaitForPostgresReady();

            if (isReady)
            {
                Console.WriteLine("[Docker] PostgreSQL is ready and accepting connections");
            }
            else
            {
                Console.WriteLine("[Docker] WARNING: PostgreSQL container started but may not be fully ready yet");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Docker] ERROR: {ex.Message}. Application will continue anyway.");
        }
    }

    private static async Task<bool> IsContainerRunning(string containerName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"ps --filter name={containerName} --filter status=running --format {{{{.Names}}}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return !string.IsNullOrWhiteSpace(output) && output.Contains(containerName);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> StartPostgresContainer()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = "up -d",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForPostgresReady()
    {
        for (int i = 0; i < MaxRetries; i++)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"exec {PostgresContainerName} pg_isready -U copytrading_user -d copytrading",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    return true;
                }
            }
            catch
            {
                // Continue retrying
            }

            await Task.Delay(RetryDelayMs);
        }

        return false;
    }

    /// <summary>
    /// Check if Docker is available on the system
    /// </summary>
    public static async Task<bool> IsDockerAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
