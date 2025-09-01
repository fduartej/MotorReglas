using Dapper;
using System.Data;
using System.Text;
using Orchestrator;
using Orchestrator.Infrastructure.Database;

namespace Orchestrator.Features.DatasetExecution.Sql;

public class QueryExecutor
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<QueryExecutor> _logger;

    public QueryExecutor(IConnectionFactory connectionFactory, ILogger<QueryExecutor> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<object> ExecuteDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs)
    {
        return await ExecuteDatasetAsync(dataset, inputs, "primary");
    }

    public async Task<object> ExecuteDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs, string connectionName)
    {
        try
        {
            return dataset.Type.ToLower() switch
            {
                "sql" => await ExecuteSqlDatasetAsync(dataset, inputs, connectionName),
                "rawsql" => await ExecuteRawSqlDatasetAsync(dataset, inputs, connectionName),
                _ => throw new ArgumentException($"Unsupported dataset type: {dataset.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing dataset {DatasetName}", dataset.Name);
            throw;
        }
    }

    private async Task<object> ExecuteSqlDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs, string connectionName)
    {
        var (sql, parameters) = BuildSqlQuery(dataset, inputs);
        
        _logger.LogDebug("Executing SQL: {Sql} with parameters: {@Parameters}", sql, parameters);

        using var connection = _connectionFactory.CreateConnection(connectionName);
        
        if (dataset.Result.Mode.ToLower() == "single")
        {
            var result = await connection.QueryFirstOrDefaultAsync(sql, parameters);
            return result ?? new object();
        }
        else
        {
            var results = await connection.QueryAsync(sql, parameters);
            return results.ToList();
        }
    }

    private async Task<object> ExecuteRawSqlDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs, string connectionName)
    {
        if (string.IsNullOrEmpty(dataset.Sql))
            throw new ArgumentException("Raw SQL dataset requires Sql property");

        var sql = dataset.Sql;
        var parameters = new DynamicParameters();

        // Replace placeholders and build parameters
        if (dataset.Params != null)
        {
            foreach (var param in dataset.Params)
            {
                var value = ReplacePlaceholders(param.Value, inputs);
                parameters.Add(param.Key, value);
            }
        }

        _logger.LogDebug("Executing Raw SQL: {Sql} with parameters: {@Parameters}", sql, parameters);

        using var connection = _connectionFactory.CreateConnection(connectionName);

        if (dataset.Result.Mode.ToLower() == "single")
        {
            var result = await connection.QueryFirstOrDefaultAsync(sql, parameters);
            return result ?? new object();
        }
        else
        {
            var results = await connection.QueryAsync(sql, parameters);
            return results.ToList();
        }
    }

    private static (string Sql, DynamicParameters Parameters) BuildSqlQuery(DatasetConfig dataset, Dictionary<string, object> inputs)
    {
        var sql = new StringBuilder();
        var parameters = new DynamicParameters();

        BuildSelectClause(sql, dataset);
        BuildFromClause(sql, dataset);
        BuildJoinClauses(sql, dataset);
        BuildWhereClause(sql, dataset, inputs, parameters);
        BuildOrderByClause(sql, dataset);
        BuildLimitClause(sql, dataset);

        return (sql.ToString(), parameters);
    }

    private static void BuildSelectClause(StringBuilder sql, DatasetConfig dataset)
    {
        sql.Append("SELECT ");
        var selectFields = new List<string>();
        
        if (dataset.Select != null && dataset.Select.Count > 0)
        {
            selectFields.AddRange(dataset.Select);
        }
        
        if (dataset.SelectDerived != null && dataset.SelectDerived.Count > 0)
        {
            selectFields.AddRange(dataset.SelectDerived);
        }

        if (selectFields.Count == 0)
        {
            selectFields.Add("*");
        }

        sql.Append(string.Join(", ", selectFields));
    }

    private static void BuildFromClause(StringBuilder sql, DatasetConfig dataset)
    {
        sql.Append($" FROM {dataset.From}");
    }

    private static void BuildJoinClauses(StringBuilder sql, DatasetConfig dataset)
    {
        if (dataset.Joins != null && dataset.Joins.Count > 0)
        {
            foreach (var join in dataset.Joins)
            {
                var joinType = join.Type.ToUpper() switch
                {
                    "INNERJOIN" => "INNER JOIN",
                    "LEFTJOIN" => "LEFT JOIN",
                    "RIGHTJOIN" => "RIGHT JOIN",
                    "FULLJOIN" => "FULL JOIN",
                    _ => "INNER JOIN"
                };
                
                sql.Append($" {joinType} {join.Table} ON {join.On}");
            }
        }
    }

    private static void BuildWhereClause(StringBuilder sql, DatasetConfig dataset, Dictionary<string, object> inputs, DynamicParameters parameters)
    {
        if (dataset.Where != null && dataset.Where.Count > 0)
        {
            sql.Append(" WHERE ");
            var whereClauses = new List<string>();

            foreach (var where in dataset.Where)
            {
                var clause = BuildSingleWhereClause(where, inputs, parameters);
                whereClauses.Add(clause);
            }

            sql.Append(string.Join(" AND ", whereClauses));
        }
    }

    private static string BuildSingleWhereClause(WhereConfig where, Dictionary<string, object> inputs, DynamicParameters parameters)
    {
        var paramName = $"p{parameters.ParameterNames.Count()}";
        object? paramValue;

        if (!string.IsNullOrEmpty(where.ValueFrom))
        {
            // Get value from inputs
            if (inputs.TryGetValue(where.ValueFrom, out var inputValue))
            {
                paramValue = inputValue;
            }
            else
            {
                throw new ArgumentException($"Input parameter '{where.ValueFrom}' not found");
            }
        }
        else
        {
            paramValue = where.Value;
        }

        var clause = where.Op.ToLower() switch
        {
            "=" => $"{where.Field} = @{paramName}",
            "!=" or "<>" => $"{where.Field} != @{paramName}",
            ">" => $"{where.Field} > @{paramName}",
            ">=" => $"{where.Field} >= @{paramName}",
            "<" => $"{where.Field} < @{paramName}",
            "<=" => $"{where.Field} <= @{paramName}",
            "like" => $"{where.Field} LIKE @{paramName}",
            "in" => BuildInClause(where.Field, paramValue, paramName),
            _ => throw new ArgumentException($"Unsupported operator: {where.Op}")
        };

        if (where.Op.ToLower() != "in")
        {
            parameters.Add(paramName, paramValue);
        }
        else
        {
            // Handle IN clause parameters separately
            if (paramValue is IEnumerable<object> enumerable)
            {
                var values = enumerable.ToArray();
                for (int i = 0; i < values.Length; i++)
                {
                    parameters.Add($"{paramName}_{i}", values[i]);
                }
            }
        }

        return clause;
    }

    private static void BuildOrderByClause(StringBuilder sql, DatasetConfig dataset)
    {
        if (dataset.OrderBy != null && dataset.OrderBy.Count > 0)
        {
            sql.Append($" ORDER BY {string.Join(", ", dataset.OrderBy)}");
        }
    }

    private static void BuildLimitClause(StringBuilder sql, DatasetConfig dataset)
    {
        if (dataset.Limit.HasValue)
        {
            sql.Append($" LIMIT {dataset.Limit.Value}");
        }
    }

    private static string BuildInClause(string field, object? value, string paramName)
    {
        if (value is not IEnumerable<object> enumerable)
        {
            throw new ArgumentException("IN operator requires array value");
        }

        var values = enumerable.ToArray();
        if (values.Length == 0)
        {
            return "1=0"; // Always false
        }

        var paramNames = values.Select((_, i) => $"@{paramName}_{i}");
        return $"{field} IN ({string.Join(", ", paramNames)})";
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, object> inputs)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;
        foreach (var input in inputs)
        {
            result = result.Replace($"{{{input.Key}}}", input.Value?.ToString() ?? "");
        }
        return result;
    }
}
