using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Orchestrator.Infrastructure.Database;

public interface IConnectionFactory
{
    IDbConnection CreateConnection(string connectionName);
    string GetConnectionString(string connectionName);
}

public class ConnectionFactory : IConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionFactory> _logger;

    public ConnectionFactory(IConfiguration configuration, ILogger<ConnectionFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IDbConnection CreateConnection(string connectionName)
    {
        var connectionString = GetConnectionString(connectionName);
        
        return connectionName.ToLowerInvariant() switch
        {
            var name when name.Contains("postgres") || name == "primary" || name == "audit" => 
                new NpgsqlConnection(connectionString),
            var name when name.Contains("sqlserver") || name.Contains("mssql") => 
                new SqlConnection(connectionString),
            var name when name.Contains("azuresql") || name.Contains("azure") => 
                new SqlConnection(connectionString),
            var name when name.Contains("sqlite") => 
                new SqliteConnection(connectionString),
            _ => throw new NotSupportedException($"Database provider for connection '{connectionName}' is not supported. " +
                                              $"Supported providers: PostgreSQL, SQL Server, Azure SQL, SQLite")
        };
    }

    public string GetConnectionString(string connectionName)
    {
        var connectionString = _configuration.GetConnectionString(connectionName);
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionName}' not found in configuration");
        }

        _logger.LogDebug("Retrieved connection string for {ConnectionName}", connectionName);
        return connectionString;
    }
}

public static class DatabaseProviderExtensions
{
    public static string GetProviderName(this string connectionName)
    {
        return connectionName.ToLowerInvariant() switch
        {
            var name when name.Contains("postgres") || name == "primary" || name == "audit" => "PostgreSQL",
            var name when name.Contains("sqlserver") || name.Contains("mssql") => "SQL Server",
            var name when name.Contains("azuresql") || name.Contains("azure") => "Azure SQL",
            var name when name.Contains("sqlite") => "SQLite",
            _ => "Unknown"
        };
    }

    public static bool SupportsSchema(this string connectionName)
    {
        return connectionName.ToLowerInvariant() switch
        {
            var name when name.Contains("sqlite") => false,
            _ => true
        };
    }

    public static string GetParameterPrefix(this string connectionName)
    {
        return connectionName.ToLowerInvariant() switch
        {
            var name when name.Contains("postgres") || name == "primary" || name == "audit" => "@",
            var name when name.Contains("sqlserver") || name.Contains("mssql") => "@",
            var name when name.Contains("azuresql") || name.Contains("azure") => "@",
            var name when name.Contains("sqlite") => "@",
            _ => "@"
        };
    }
}
