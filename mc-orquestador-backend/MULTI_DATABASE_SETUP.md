# Multi-Database Support

## Overview

The Orchestrator now supports multiple database providers:

- **PostgreSQL** (existing, default)
- **SQL Server**
- **Azure SQL**
- **SQLite**

## Configuration

Database connections are configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "primary": "Host=localhost;Database=orchestrator;Username=user;Password=pass",
    "audit": "Host=localhost;Database=audit;Username=user;Password=pass",
    "sqlserver": "Server=localhost;Database=orchestrator;Integrated Security=true;TrustServerCertificate=true",
    "azuresql": "Server=your-server.database.windows.net;Database=orchestrator;User Id=user;Password=pass;Encrypt=true",
    "sqlite": "Data Source=orchestrator.db"
  }
}
```

## Usage in Flow Configuration

### Default Connection (PostgreSQL)

```json
{
  "name": "defaultDataset",
  "type": "rawSQL",
  "sql": "SELECT * FROM users",
  "result": { "mode": "multiple" }
}
```

### Specific Database Provider

```json
{
  "name": "sqlServerDataset",
  "type": "rawSQL",
  "sql": "SELECT * FROM users",
  "connectionName": "sqlserver",
  "result": { "mode": "multiple" }
}
```

## Supported Connection Names

| Connection Name | Database Provider | Description               |
| --------------- | ----------------- | ------------------------- |
| `primary`       | PostgreSQL        | Default primary database  |
| `audit`         | PostgreSQL        | Audit database            |
| `sqlserver`     | SQL Server        | Local SQL Server instance |
| `azuresql`      | Azure SQL         | Azure SQL Database        |
| `sqlite`        | SQLite            | Local SQLite database     |

## HTTP Fallback with Multi-Database

When HTTP datasets have fallback configured, they can specify which database to use:

```json
{
  "name": "apiDataset",
  "type": "HTTP",
  "http": {
    "url": "https://api.example.com/data",
    "method": "GET"
  },
  "onFailure": {
    "fallbackConfig": {
      "sql": "SELECT * FROM fallback_data WHERE id = @id",
      "connectionName": "sqlserver"
    }
  },
  "params": {
    "id": "{{inputs.userId}}"
  }
}
```

## Implementation Details

### ConnectionFactory

- `IConnectionFactory` interface provides abstraction over database providers
- Automatically detects provider based on connection string
- Supports connection string validation
- Handles provider-specific initialization

### Database Providers

- **PostgreSQL**: Uses `Npgsql` package
- **SQL Server**: Uses `Microsoft.Data.SqlClient` package
- **Azure SQL**: Uses `Microsoft.Data.SqlClient` package
- **SQLite**: Uses `Microsoft.Data.Sqlite` package

### QueryExecutor Updates

- Updated to use `IConnectionFactory` instead of direct `IDbConnection`
- Supports connection name parameter for dataset execution
- Maintains backward compatibility with existing flows

## Testing

Use the provided `test-multidatabase.json` flow to test all database providers:

```bash
curl -X POST http://localhost:5000/api/orchestrator/execute \
  -H "Content-Type: application/json" \
  -d @test-multidatabase.json
```

## Notes

- Connection strings must be properly configured for each database provider
- SQL syntax may vary between providers - ensure queries are compatible
- SQLite databases are created automatically if they don't exist
- Azure SQL requires proper firewall rules and authentication setup
