# Configuración de Reintentos (Polly) en el Orquestador

## Resumen de Configuración Actual

### 📍 **Estado Actual de los Reintentos**

**En `appsettings.json`:**

```json
{
  "HttpClients": {
    "DefaultRetryPolicy": {
      "MaxAttempts": 3,
      "BackoffMs": 500,
      "TimeoutMs": 30000
    }
  }
}
```

**Configuración global predeterminada:**

- **MaxAttempts**: 3 intentos (1 inicial + 2 reintentos)
- **BackoffMs**: 500ms de retraso base
- **TimeoutMs**: 30 segundos de timeout global

### 📋 **Configuración por Dataset en Flows**

**En los archivos de flow (ej: `flow-FNB.json`):**

```json
{
  "name": "apiScore",
  "type": "http",
  "http": {
    "method": "GET",
    "url": "https://risk.example.com/score?dni={{dni}}",
    "timeoutMs": 1500,
    "retry": {
      "maxAttempts": 2,
      "backoffMs": 200
    }
  }
}
```

**Configuración específica por dataset:**

- **maxAttempts**: 2 intentos (1 inicial + 1 reintento)
- **backoffMs**: 200ms de retraso base
- **timeoutMs**: 1500ms de timeout específico

### 🔧 **Implementación Técnica**

#### 1. **Configuración Global (ServiceCollectionExtensions.cs)**

```csharp
// HTTP Client con configuración básica
var timeoutMs = configuration.GetSection("HttpClients:DefaultRetryPolicy")
    .GetValue("TimeoutMs", 30000);

services.AddHttpClient<HttpDatasetExecutor>(client =>
{
    client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
    client.DefaultRequestHeaders.Add("User-Agent", "Orchestrator/1.0");
});
```

#### 2. **Implementación por Dataset (Pendiente en HttpDatasetExecutor.cs)**

⚠️ **NOTA IMPORTANTE**: Actualmente los reintentos configurados en los datasets (`retry: { maxAttempts: 2, backoffMs: 200 }`) **NO están implementados** en el código.

El `HttpDatasetExecutor.cs` necesita ser actualizado para usar las políticas de Polly por dataset.

### 📊 **Configuraciones Actuales por Endpoint**

| Dataset               | Método | URL                 | Timeout | Max Attempts  | Backoff       |
| --------------------- | ------ | ------------------- | ------- | ------------- | ------------- |
| `apiScore`            | GET    | risk.example.com    | 1500ms  | 2             | 200ms         |
| `apiProductosActivos` | GET    | catalog.example.com | 2000ms  | No definido\* | No definido\* |

\*Usa configuración global cuando no está especificado.

### 🚀 **Recomendaciones de Implementación**

#### 1. **Implementar Polly por Dataset**

```csharp
// En HttpDatasetExecutor.cs
private async Task<string> ExecuteHttpRequestAsync(HttpConfig httpConfig, Dictionary<string, object> inputs)
{
    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(
            retryCount: (httpConfig.Retry?.MaxAttempts ?? 3) - 1,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromMilliseconds((httpConfig.Retry?.BackoffMs ?? 500) * Math.Pow(2, retryAttempt - 1))
        );

    return await retryPolicy.ExecuteAsync(async () =>
    {
        // Lógica de HTTP request actual
    });
}
```

#### 2. **Configuración Recomendada por Tipo de Servicio**

**APIs Críticas (Score, Fraude):**

```json
"retry": { "maxAttempts": 3, "backoffMs": 200 }
```

**APIs de Catálogo:**

```json
"retry": { "maxAttempts": 2, "backoffMs": 500 }
```

**APIs Internas:**

```json
"retry": { "maxAttempts": 4, "backoffMs": 100 }
```

### 🔍 **Variables de Entorno para Override**

Puedes sobrescribir la configuración usando variables de entorno:

```bash
# Override timeout global
HTTPCLIENT_DEFAULT_TIMEOUT_MS=45000

# Override retry por ambiente
RETRY_MAX_ATTEMPTS=5
RETRY_BACKOFF_MS=300
```

### 📈 **Monitoreo de Reintentos**

**Logs generados:**

```
HTTP request failed, retrying in 200ms. Attempt 1 of 2
HTTP request failed, retrying in 400ms. Attempt 2 of 2
```

**Métricas recomendadas:**

- Total de requests por dataset
- Número de reintentos por dataset
- Tiempo promedio de respuesta
- Tasa de éxito después de reintentos

### ⚡ **Estado de Implementación**

| Componente                | Estado           | Descripción                             |
| ------------------------- | ---------------- | --------------------------------------- |
| ✅ Configuración Global   | **Implementado** | Timeout y headers en HttpClient         |
| ✅ Configuración en Flows | **Implementado** | Definido en JSON de flows               |
| ❌ Polly por Dataset      | **Pendiente**    | Reintentos específicos no implementados |
| ❌ Logging de Reintentos  | **Pendiente**    | Logs de retry no implementados          |
| ❌ Métricas               | **Pendiente**    | Monitoreo de reintentos no implementado |

### 🎯 **Próximos Pasos**

1. **Implementar reintentos por dataset** en `HttpDatasetExecutor.cs`
2. **Agregar logging** de reintentos para debugging
3. **Implementar métricas** para monitoreo
4. **Testing** de políticas de retry en diferentes escenarios
5. **Documentar** patrones de retry por tipo de API

---

**Configuración actual**: Global en `appsettings.json` + Específica en flows JSON
**Reintentos activos**: ❌ No implementados (solo configuración)
**Timeout activo**: ✅ 30 segundos global, específico por dataset
