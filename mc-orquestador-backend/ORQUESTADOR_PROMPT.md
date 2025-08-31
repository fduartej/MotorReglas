Orchestrator (.NET 8, Dapper, Redis, Scriban) with External Flows/Templates + Hot-Reload
Goal
Build a microservice that, from external config files, resolves data (SQL/HTTP), caches with Redis, builds a context via mapping/derived, selects a template by rules, renders JSON (Scriban), and returns the payload (object, not string). No redeploy required when flows/templates change.

Tech
.NET 8, ASP.NET Core Minimal APIs

Dapper (SQL), provider via DI (Npgsql / SqlClient)

StackExchange.Redis

HttpClientFactory + Polly (retry/timeout)

Scriban (templating)

System.Text.Json

OpenTelemetry

External folders (runtime, hot-reload)
EXTERNAL_FLOWS_PATH → default /config/Flows

EXTERNAL_TEMPLATES_PATH → default /config/Templates

EXTERNAL_CACHE_SECONDS → default 30

Files are outside the code and mounted as volumes in containers. Use file watchers to invalidate in-memory cache when a file changes.

Project structure

/src
  /Flows                   # external (mounted). example file included for dev
    flow-FNB.json
  /Templates               # external (mounted). example files included for dev
    /motor
      motor-default.json
      motor-premium.json
    /sap
      sap-fnb.json
      sap-fnb-premium.json
      sap-fnb-empresarial.json

  Program.cs
  AppSettings.cs
  Contracts.cs
  Extensions.cs

  Services/
    FlowLoader.cs            # FS + IMemoryCache + IChangeToken (watch)
    QueryExecutor.cs         # SQL builder + rawSql (Dapper)
    HttpDatasetExecutor.cs   # HTTP datasets (extract/resultPaths)
    RedisCache.cs
    ContextBuilder.cs        # mapping + collections
    DerivedEvaluator.cs      # sumCollectionField + expr
    TemplateSelector.cs
    TemplateRenderer.cs      # FS + compiled cache + watch (Scriban)
    Validator.cs             # _meta.required
    TraceLogger.cs

/tests
  Orchestrator.Tests.csproj

dotnet add package Dapper
dotnet add package Npgsql                # o Microsoft.Data.SqlClient
dotnet add package StackExchange.Redis
dotnet add package Scriban
dotnet add package Polly.Extensions.Http
dotnet add package Serilog.AspNetCore
dotnet add package Microsoft.Extensions.FileProviders.Physical

settings

{
  "ExternalConfig": {
    "FlowsPath": "/config/Flows",
    "TemplatesPath": "/config/Templates",
    "CacheSeconds": 30
  },
  "ConnectionStrings": {
    "primary": "Host=localhost;Database=primary;Username=...;Password=...",
    "audit": "Host=localhost;Database=audit;Username=...;Password=..."
  },
  "Redis": { "Configuration": "localhost:6379" }
}

DI wiring (Program.cs hints)
Bind ExternalConfig, IMemoryCache, Redis connection multiplexer.

IFlowsFileProvider / ITemplatesFileProvider → PhysicalFileProvider with paths from ENV > appsettings.

Typed HttpClient with Polly (timeout + retry jitter).

Register services listed above.

Endpoint
POST /build-payload/{flow}

Body (example):
{ "dni":"12345678","cuentaContrato":"001-XYZ","canal":"OV","transaccionId":999,"pais":"PE" }

Response: { template, payload, debug: { datasets, derived } }

Flow config format (EXTERNAL) — full example
File: /config/Flows/flow-FNB.json

{
  "name": "FlowFNB",
  "version": "1.0.0",
  "inputs": ["dni", "cuentaContrato", "canal", "transaccionId", "pais"],

  "settings": {
    "failFast": false,
    "timezone": "America/Lima"
  },

  "datasets": [
    {
      "name": "qCliente",
      "type": "sql",
      "database": "primary",
      "from": "clientes as c",
      "select": ["c.email as emailCliente"],
      "selectDerived": ["CONCAT(c.nombre,' ',c.apellido) as nombreCompleto"],
      "joins": [
        { "type": "leftJoin", "table": "direcciones as d", "on": "c.id = d.cliente_id" },
        { "type": "leftJoin", "table": "ciudades as ciu", "on": "d.ciudad_id = ciu.id" },
        { "type": "leftJoin", "table": "provincias as p", "on": "ciu.provincia_id = p.id" }
      ],
      "where": [
        { "field": "c.dni", "op": "=", "valueFrom": "dni" },
        { "field": "d.tipo", "op": "=", "value": "principal" }
      ],
      "orderBy": ["c.id asc"],
      "result": { "mode": "single" },
      "cache": { "enabled": true, "ttlSec": 1800, "key": "cliente:{dni}" }
    },

    {
      "name": "qCuentas",
      "type": "sql",
      "database": "primary",
      "from": "cuentas as c",
      "select": [
        "c.numero_cuenta as numeroCuenta",
        "c.tipo_cuenta as tipoCuenta",
        "c.saldo_actual as saldoActual",
        "p.nombre as nombreProducto",
        "cat.nombre as categoriaProducto",
        "s.codigo as codigoSucursal",
        "s.nombre as nombreSucursal"
      ],
      "joins": [
        { "type": "innerJoin", "table": "productos as p", "on": "c.producto_id = p.id" },
        { "type": "leftJoin",  "table": "categorias_producto as cat", "on": "p.categoria_id = cat.id" },
        { "type": "innerJoin", "table": "sucursales as s", "on": "c.sucursal_id = s.id" }
      ],
      "where": [
        { "field": "c.dni", "op": "=", "valueFrom": "dni" },
        { "field": "c.estado", "op": "in", "value": ["activa", "preferente"] }
      ],
      "orderBy": ["c.fecha_apertura desc"],
      "limit": 10,
      "result": { "mode": "array" },
      "cache": { "enabled": true, "ttlSec": 900, "key": "cuentas:{dni}" }
    },

    {
      "name": "rawDeudas",
      "type": "rawSql",
      "database": "primary",
      "sql": "SELECT d.id, d.monto, d.estado, d.fecha FROM deudas d WHERE d.dni = @dni AND d.estado IN ('PEND','VENC') ORDER BY d.fecha DESC",
      "params": { "dni": "{dni}" },
      "result": { "mode": "array" },
      "cache": { "enabled": true, "ttlSec": 300, "key": "deudas:{dni}" }
    },

    {
      "name": "apiScore",
      "type": "http",
      "method": "GET",
      "url": "https://risk.example.com/score?dni={{dni}}&country={{pais}}",
      "headers": {
        "x-api-key": "{{ENV.RISK_API_KEY}}",
        "accept": "application/json"
      },
      "timeoutMs": 1500,
      "retry": { "maxAttempts": 2, "backoffMs": 200 },
      "result": { "mode": "single" },
      "resultPath": "$",
      "extract": {
        "valor": "data.score",
        "banderaFraude": "data.flags.fraud",
        "modeloVersion": "meta.modelVersion"
      },
      "cache": { "enabled": true, "ttlSec": 600, "key": "score:{dni}:{pais}" }
    },

    {
      "name": "apiProductosActivos",
      "type": "http",
      "method": "GET",
      "url": "https://catalog.example.com/customers/{{dni}}/products",
      "headers": { "authorization": "Bearer {{ENV.CATALOG_TOKEN}}" },
      "timeoutMs": 2000,
      "result": { "mode": "array" },
      "resultPath": "items",
      "resultPaths": [
        { "as": "primeraCategoria", "path": "0.category" },
        { "as": "cantidadProductos", "path": "length" }
      ],
      "cache": { "enabled": true, "ttlSec": 300, "key": "productos:{dni}" }
    }
  ],

  "collections": {
    "cuentas":   { "from": "qCuentas" },
    "deudas":    { "from": "rawDeudas" },
    "productos": { "from": "apiProductosActivos" }
  },

  "mapping": {
    "cliente.nombre":              "qCliente.nombreCompleto",
    "cliente.email":               "qCliente.emailCliente",
    "score.valor":                 "apiScore.valor",
    "score.fraude":                "apiScore.banderaFraude",
    "score.modeloVersion":         "apiScore.modeloVersion",
    "productos.primeraCategoria":  "apiProductosActivos.primeraCategoria",
    "productos.cantidad":          "apiProductosActivos.cantidadProductos",
    "cuentaPrincipal.numero":      "qCuentas.0.numeroCuenta",
    "cuentaPrincipal.tipo":        "qCuentas.0.tipoCuenta",
    "cuentaPrincipal.saldo":       "qCuentas.0.saldoActual"
  },

  "derived": {
    "totales.deudaPendiente": { "sumCollectionField": { "collection": "deudas", "field": "monto" } },
    "flags.tieneDeuda":       { "expr": "totales.deudaPendiente > 0" },
    "flags.esPremium":        { "expr": "cuentas[0].categoriaProducto == 'PREMIUM' && cuentas[0].saldoActual > 5000" }
  },

  "templates": {
    "engine": "scriban",

    "motorDecision": {
      "default": "Templates/motor/motor-default.json",
      "rules": [
        {
          "if": [
            { "path": "flags.esPremium", "op": "=", "value": true },
            { "path": "score.valor", "op": ">", "value": 720 }
          ],
          "template": "Templates/motor/motor-premium.json"
        }
      ]
    },

    "sap": {
      "default": "Templates/sap/sap-fnb.json",
      "rules": [
        {
          "if": [
            { "path": "cuentas.0.categoriaProducto", "op": "=", "value": "PREMIUM" },
            { "path": "cuentas.0.saldoActual", "op": ">", "value": 5000 }
          ],
          "template": "Templates/sap/sap-fnb-premium.json"
        },
        {
          "if": [{ "path": "cuentas.0.tipoCuenta", "op": "=", "value": "EMPRESARIAL" }],
          "template": "Templates/sap/sap-fnb-empresarial.json"
        },
        {
          "if": [{ "path": "score.fraude", "op": "=", "value": true }],
          "template": "Templates/sap/sap-fnb-review.json"
        }
      ]
    }
  },

  "audit": {
    "trace": {
      "enabled": true,
      "fields": ["inputs", "datasets", "derived", "mappingContext", "chosenTemplates", "payloads"]
    }
  }
}

Template examples (EXTERNAL)
/config/Templates/sap/sap-fnb.json

{
  "_meta": { "required": ["cliente.nombre", "transaccionId"] },

  "Solicitud": {
    "Cliente": {
      "Nombre": "{{ cliente.nombre }}",
      "Email": "{{ cliente.email }}",
      "Score": "{{ score.valor }}"
    },
    "Cuentas": [
      {{ for c in cuentas -}}
      {
        "Numero": "{{ c.numeroCuenta }}",
        "Tipo": "{{ c.tipoCuenta }}",
        "Saldo": "{{ c.saldoActual }}",
        "Producto": "{{ c.nombreProducto }}",
        "Categoria": "{{ c.categoriaProducto }}",
        "Sucursal": { "Codigo": "{{ c.codigoSucursal }}", "Nombre": "{{ c.nombreSucursal }}" }
      }{{ if !for.last }},{{ end }}
      {{- end }}
    ],
    "Deudas": [
      {{ for d in deudas -}}
      { "Id": "{{ d.id }}", "Monto": "{{ d.monto }}", "Estado": "{{ d.estado }}" }{{ if !for.last }},{{ end }}
      {{- end }}
    ],
    "TransaccionId": "{{ transaccionId }}"
  }
}

/config/Templates/sap/sap-fnb-premium.json

{
  "_meta": { "required": ["cliente.nombre", "transaccionId"] },

  "SolicitudPremium": {
    "Cliente": { "Nombre": "{{ cliente.nombre }}", "Score": "{{ score.valor }}" },
    "Resumen": {
      "CuentasActivas": "{{ cuentas.size }}",
      "PrimeraCuenta": {
        "Numero": "{{ cuentas[0].numeroCuenta }}",
        "Tipo": "{{ cuentas[0].tipoCuenta }}",
        "Saldo": "{{ cuentas[0].saldoActual }}"
      },
      "DeudasPendientes": "{{ deudas.size }}"
    },
    "TransaccionId": "{{ transaccionId }}"
  }
}

Contracts (generate records/interfaces)

public record FlowConfig(
  string Name,
  string Version,
  string[] Inputs,
  FlowSettings Settings,
  List<DatasetConfig> Datasets,
  Dictionary<string,string> Mapping,
  Dictionary<string,CollectionBinding> Collections,
  Dictionary<string,DerivedItem> Derived,
  TemplateSection Templates,
  AuditConfig Audit
);
public record FlowSettings(bool FailFast, string Timezone);

public record DatasetConfig(
  string Name, string Type, string? Database,
  string? From, List<string>? Select, List<string>? SelectDerived,
  List<JoinConfig>? Joins, List<WhereConfig>? Where, List<string>? OrderBy, int? Limit,
  ResultConfig Result, CacheConfig? Cache,
  string? Sql, Dictionary<string,string>? Params,                 // rawSql
  HttpConfig? Http, Dictionary<string,string>? Extract, List<ResultPath>? ResultPaths // http
);
public record JoinConfig(string Type, string Table, string On);
public record WhereConfig(string Field, string Op, object? Value, string? ValueFrom);
public record ResultConfig(string Mode); // "single" | "array"
public record CacheConfig(bool Enabled, int TtlSec, string Key);

public record HttpConfig(string Method, string Url, Dictionary<string,string>? Headers,
                         int? TimeoutMs, RetryConfig? Retry, string? BodyTemplate, string? ResultPath);
public record RetryConfig(int MaxAttempts, int BackoffMs);
public record ResultPath(string As, string Path);

public record CollectionBinding(string From);
public record DerivedItem(SumCollectionField? SumCollectionField, string? Expr);
public record SumCollectionField(string Collection, string Field);

public record TemplateSection(TemplateGroup MotorDecision, TemplateGroup Sap);
public record TemplateGroup(string Default, List<TemplateRule>? Rules);
public record TemplateRule(List<TemplateCond> If, string Template);
public record TemplateCond(string Path, string Op, object? Value);

public record AuditConfig(TraceConfig Trace);
public record TraceConfig(bool Enabled, string[] Fields);
public record PayloadInput(string dni, string? cuentaContrato, string? canal, long? transaccionId, string? pais);

Services behavior (what to implement)
FlowLoader
Read /config/Flows/flow-{name}.json. Validate; cache in IMemoryCache with TTL and IChangeToken (watch file). ENV overrides paths.

QueryExecutor

type=="sql": build SQL (FROM/JOINS/WHERE/ORDER/LIMIT) with params; Dapper query; return single row or array.

type=="rawSql": use Sql and Params, replace {input} placeholders, bind as named params in Dapper.

Respect Result.Mode.

(V2) Group by signature database+from+joins+where to coalesce selects.

HttpDatasetExecutor

Build URL/headers/body via Scriban from inputs and ENV.*.

HttpClientFactory + Polly (timeout/retry).

Parse JSON; apply ResultPath (path or $ for root).

If extract present, project aliases; if resultPaths present, project listed fields.

Return object or array per Result.Mode.

RedisCache

If cache.enabled, form key substituting {placeholders} from inputs; set/get whole dataset result with TTL.

ContextBuilder

Start with inputs → ctx.

For each dataset result → ctx.datasets[name] = obj|array.

Apply mapping (deep set) from dataset.field (supports index 0, .length via pre-read).

Bind collections (ctx[alias] = datasets[from]).

DerivedEvaluator

sumCollectionField: sum over ctx[collection] field.

expr: mini-evaluator supporting > < >= <= == != && || + - * /, [] index, .size/.length.

Store at target path (deep set).

TemplateSelector

Evaluate rules in order with getByPath(ctx, Path) and operators = != > >= < <= in. First match wins; else default.

Validator

Read template JSON, inspect _meta.required[] paths; return 400 with { missing: [...] } if any null/undefined.

TemplateRenderer

Load template file string; compile with Scriban; cache compiled by path with IChangeToken.

Render with context; parse result string to object and return.

TraceLogger

Log traceId, flow, timings, inputs, datasets, derived, chosen template, payload. Redact secrets.

Acceptance criteria
Flows/Templates are external; updates are reflected without redeploy (file watcher + TTL).

Datasets support sql, rawSql, http; result.mode handles single/array.

HTTP datasets support extract and resultPaths to expose multiple values.

Redis caches dataset results; cache keys accept placeholders.

Collections bind arrays for iteration in templates.

Derived fields work (sum + expr) and can be used in mapping/rules.

Template selection by rules; _meta.required validated.

Response returns { template, payload, debug: { datasets, derived } }.

Unit tests: template selection, required validation, mapping, derived, http extract, redis hit/miss.

Dev & Docker
Local dev can read from src/Flows and src/Templates; prod mounts volumes:

-v /host/flows:/config/Flows

-v /host/templates:/config/Templates

ENV:

EXTERNAL_FLOWS_PATH=/config/Flows

EXTERNAL_TEMPLATES_PATH=/config/Templates

EXTERNAL_CACHE_SECONDS=15