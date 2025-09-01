using Microsoft.Data.SqlClient;
using System.Data;

namespace Infrastructure.Data.Sql;

public class SqlRepository
{
    public readonly string _connectionString;

    public SqlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new InvalidOperationException("Connection string is not set");
        }
        return new SqlConnection(_connectionString);
    }

}
