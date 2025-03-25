using System.Data;
//using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data.Sql;

public class DapperContext
{
    private readonly string? _connectionString;

    public DapperContext(string connectionString)
    {
        _connectionString = connectionString;
    }
    public IDbConnection CreateConnection()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new InvalidOperationException("Connection string is not set");
        }
        return new SqliteConnection(_connectionString);
    }
}
