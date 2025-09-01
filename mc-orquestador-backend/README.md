# Orchestrator Microservice

A .NET 8 microservice that processes external flow configurations to resolve data from SQL/HTTP sources, applies business rules, and renders JSON payloads using templates. Features hot-reload capabilities for flows and templates without requiring redeployment.

## Architecture

- **Flow Loader**: Manages external flow configurations with file watching and caching
- **Dataset Executors**: Execute SQL queries (Dapper) and HTTP requests (HttpClient + Polly)
- **Redis Cache**: Caches dataset results with configurable TTL
- **Context Builder**: Builds evaluation context from inputs, datasets, and mappings
- **Derived Evaluator**: Calculates derived fields using expressions and collection operations
- **Template System**: Rule-based template selection and Scriban rendering
- **Validation**: Ensures required fields are present before rendering
- **Trace Logging**: Comprehensive audit trail and performance metrics

## Tech Stack

- **.NET 8** - ASP.NET Core Minimal APIs
- **Dapper** - SQL data access with Npgsql provider
- **StackExchange.Redis** - Redis caching
- **Scriban** - Template engine for JSON rendering
- **Polly** - HTTP resilience and retry policies
- **Serilog** - Structured logging
- **OpenTelemetry** - Observability and tracing

## Quick Start

### Using Docker Compose (Recommended)

```bash
# Clone and navigate to project
cd mc-orquestador-backend

# Start all services (orchestrator, postgres, redis)
docker-compose up -d

# Check health
curl http://localhost:8080/health
```

### Manual Setup

```bash
# Restore packages
dotnet restore src/Orchestrator.csproj

# Update connection strings in appsettings.json
# Ensure PostgreSQL and Redis are running

# Run the application
dotnet run --project src/Orchestrator.csproj
```

## Configuration

### Environment Variables

- `EXTERNAL_FLOWS_PATH`: Path to flow configuration files (default: `/config/Flows`)
- `EXTERNAL_TEMPLATES_PATH`: Path to template files (default: `/config/Templates`)
- `EXTERNAL_CACHE_SECONDS`: Cache TTL for files (default: `30`)

### Connection Strings

```json
{
  "ConnectionStrings": {
    "primary": "Host=localhost;Database=primary;Username=postgres;Password=password",
    "audit": "Host=localhost;Database=audit;Username=postgres;Password=password"
  },
  "Redis": {
    "Configuration": "localhost:6379"
  }
}
```

## API Endpoints

### Main Orchestrator Endpoint

```http
POST /build-payload/{flow}
Content-Type: application/json

{
  "dni": "12345678",
  "cuentaContrato": "001-XYZ",
  "canal": "OV",
  "transaccionId": 999,
  "pais": "PE"
}
```

**Response:**

```json
{
  "template": "sap/sap-fnb.json",
  "payload": {
    "motorDecision": { ... },
    "sap": { ... }
  },
  "debug": {
    "datasets": { ... },
    "derived": { ... }
  }
}
```

### Utility Endpoints

- `GET /health` - Health check
- `GET /flows/{flow}/config` - View flow configuration
- `DELETE /cache/{key}` - Clear cache entry
- `GET /metrics` - Application metrics

## Flow Configuration

Flows are JSON files defining data sources, business logic, and template selection rules:

```json
{
  "name": "FlowFNB",
  "inputs": ["dni", "cuentaContrato", "canal", "transaccionId", "pais"],
  "datasets": [
    {
      "name": "qCliente",
      "type": "sql",
      "database": "primary",
      "from": "clientes as c",
      "select": ["c.email as emailCliente"],
      "where": [{ "field": "c.dni", "op": "=", "valueFrom": "dni" }],
      "result": { "mode": "single" },
      "cache": { "enabled": true, "ttlSec": 1800, "key": "cliente:{dni}" }
    }
  ],
  "mapping": {
    "cliente.email": "qCliente.emailCliente"
  },
  "derived": {
    "flags.tieneDeuda": { "expr": "totales.deudaPendiente > 0" }
  },
  "templates": {
    "sap": {
      "default": "sap/sap-fnb.json",
      "rules": [
        {
          "if": [{ "path": "flags.esPremium", "op": "=", "value": true }],
          "template": "sap/sap-fnb-premium.json"
        }
      ]
    }
  }
}
```

## Dataset Types

### SQL Datasets

```json
{
  "name": "qCuentas",
  "type": "sql",
  "database": "primary",
  "from": "cuentas as c",
  "select": ["c.numero_cuenta", "c.saldo_actual"],
  "joins": [
    {
      "type": "innerJoin",
      "table": "productos as p",
      "on": "c.producto_id = p.id"
    }
  ],
  "where": [
    { "field": "c.dni", "op": "=", "valueFrom": "dni" },
    { "field": "c.estado", "op": "in", "value": ["activa", "preferente"] }
  ],
  "orderBy": ["c.fecha_apertura desc"],
  "limit": 10
}
```

### Raw SQL Datasets

```json
{
  "name": "rawDeudas",
  "type": "rawSql",
  "database": "primary",
  "sql": "SELECT d.id, d.monto FROM deudas d WHERE d.dni = @dni",
  "params": { "dni": "{dni}" }
}
```

### HTTP Datasets

```json
{
  "name": "apiScore",
  "type": "http",
  "http": {
    "method": "GET",
    "url": "https://api.example.com/score?dni={{dni}}",
    "headers": { "x-api-key": "{{ENV.API_KEY}}" },
    "timeoutMs": 1500,
    "retry": { "maxAttempts": 2, "backoffMs": 200 }
  },
  "extract": {
    "valor": "data.score",
    "fraude": "data.flags.fraud"
  }
}
```

## Templates

Templates use Scriban syntax with metadata for validation:

```json
{
  "_meta": {"required": ["cliente.nombre", "transaccionId"]},
  "Solicitud": {
    "Cliente": {
      "Nombre": "{{ cliente.nombre }}",
      "Score": "{{ score.valor }}"
    },
    "Cuentas": [
      {{ for c in cuentas -}}
      {
        "Numero": "{{ c.numeroCuenta }}",
        "Saldo": "{{ c.saldoActual }}"
      }{{ if !for.last }},{{ end }}
      {{- end }}
    ]
  }
}
```

## Hot-Reload

Files are monitored using `IChangeToken`. Updates to flows or templates automatically invalidate cache:

- Flow changes reload configuration
- Template changes recompile Scriban templates
- No application restart required

## Caching Strategy

### Dataset Caching (Redis)

- Cache keys support placeholders: `cliente:{dni}`
- Configurable TTL per dataset
- Automatic cache invalidation

### File Caching (Memory)

- Flow configurations cached with file watching
- Template compilation cached with file watching
- Configurable cache duration

## Testing

```bash
# Run unit tests
dotnet test tests/Orchestrator.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Sample Test Request

```bash
curl -X POST http://localhost:8080/build-payload/FNB \
  -H "Content-Type: application/json" \
  -d '{
    "dni": "12345678",
    "cuentaContrato": "001-123456",
    "canal": "OV",
    "transaccionId": 999,
    "pais": "PE"
  }'
```

## Monitoring & Observability

### Structured Logging

- Request/response logging
- Dataset execution metrics
- Template selection traces
- Error tracking with context

### OpenTelemetry Integration

- Distributed tracing
- Performance metrics
- Custom spans for dataset operations

### Health Checks

- Application health endpoint
- Dependency health monitoring
- Redis and database connectivity

## Production Deployment

### Docker Production

```bash
# Build image
docker build -t orchestrator:latest .

# Run with external volumes
docker run -d \
  -p 8080:8080 \
  -v /host/flows:/config/Flows \
  -v /host/templates:/config/Templates \
  -e ConnectionStrings__primary="Host=prod-db;Database=primary;..." \
  -e Redis__Configuration="redis-cluster:6379" \
  orchestrator:latest
```

### Environment Variables for Production

```bash
EXTERNAL_FLOWS_PATH=/config/Flows
EXTERNAL_TEMPLATES_PATH=/config/Templates
EXTERNAL_CACHE_SECONDS=15
ASPNETCORE_ENVIRONMENT=Production
```

## Performance Considerations

- **Dataset Coalescing**: Future enhancement to group similar SQL queries
- **Connection Pooling**: Configured for high-throughput scenarios
- **Redis Clustering**: Supports Redis cluster for scalability
- **Template Compilation**: Cached compiled templates for performance
- **Async Operations**: Full async/await throughout the pipeline

## Security

- **Input Validation**: Comprehensive validation of inputs and configurations
- **Secret Management**: Environment variable injection for API keys
- **Data Redaction**: Automatic PII redaction in logs
- **SQL Injection Prevention**: Parameterized queries via Dapper

## Troubleshooting

### Common Issues

1. **Flow Not Found**: Check file exists in `EXTERNAL_FLOWS_PATH`
2. **Template Errors**: Validate Scriban syntax and required fields
3. **Cache Issues**: Check Redis connectivity and configuration
4. **Database Errors**: Verify connection strings and database schema

### Debugging

Enable debug logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Orchestrator": "Debug"
    }
  }
}
```

## Contributing

1. Follow .NET coding standards
2. Add unit tests for new features
3. Update documentation for API changes
4. Ensure all tests pass before submitting PRs

## License

MIT License - see LICENSE file for details
