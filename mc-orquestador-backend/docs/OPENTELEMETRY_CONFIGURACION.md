# Configuración de OpenTelemetry con Azure Monitor

## 📊 **Configuración Implementada**

### **1. Configuración en `appsettings.json`**

```json
{
  "OpenTelemetry": {
    "ServiceName": "mc-orquestador-backend"
  },
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=5445e5fe-c76a-4f17-b65b-981f8a4f447e;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=f86f8726-55db-4edc-bc65-36bba3d8c76d"
  }
}
```

### **2. Instrumentación Automática**

✅ **ASP.NET Core**: Requests HTTP entrantes, middleware, controllers
✅ **HttpClient**: Llamadas HTTP salientes (APIs externas, datasets HTTP)
✅ **SQL Client**: Consultas a PostgreSQL (datasets SQL)
✅ **Runtime .NET**: GC, memoria, threads, excepciones

### **3. Instrumentación Personalizada**

#### **📈 Métricas Personalizadas (OrchestratorMetrics)**

| Métrica                                            | Tipo      | Descripción                        |
| -------------------------------------------------- | --------- | ---------------------------------- |
| `orchestrator_flow_executions_total`               | Counter   | Total de ejecuciones de flows      |
| `orchestrator_dataset_executions_total`            | Counter   | Total de ejecuciones de datasets   |
| `orchestrator_template_renderings_total`           | Counter   | Total de renderizados de templates |
| `orchestrator_cache_hits_total`                    | Counter   | Total de cache hits                |
| `orchestrator_cache_misses_total`                  | Counter   | Total de cache misses              |
| `orchestrator_flow_execution_duration_seconds`     | Histogram | Duración de ejecución de flows     |
| `orchestrator_dataset_execution_duration_seconds`  | Histogram | Duración de ejecución de datasets  |
| `orchestrator_template_rendering_duration_seconds` | Histogram | Duración de renderizado            |
| `orchestrator_payload_size_bytes`                  | Histogram | Tamaño de payloads generados       |

#### **🔍 Trazas Personalizadas (OrchestratorActivitySource)**

| Actividad                        | Componente         | Tags                              |
| -------------------------------- | ------------------ | --------------------------------- |
| `Flow.Execute.{flowName}`        | flow-processing    | flow.name, orchestrator.component |
| `Dataset.Execute.{datasetName}`  | dataset-execution  | dataset.name, dataset.type        |
| `Template.Render.{templateName}` | template-rendering | template.name                     |
| `Cache.{operation}`              | cache              | cache.operation, cache.key        |

### **4. Configuración Avanzada**

#### **Filtros Implementados**

- **Health Checks**: Excluidos de trazas para reducir ruido
- **Excepciones**: Capturadas automáticamente en trazas
- **Contexto de Servicio**: Namespace, instancia, ambiente

#### **Fallback para Desarrollo**

Si no hay Application Insights ConnectionString:

- ✅ **Exporta a consola** para debugging local
- ✅ **Mantiene instrumentación** completa
- ✅ **No falla el startup** del servicio

### **5. Ejemplo de Uso en Código**

#### **Métricas:**

```csharp
public class FlowOrchestrator
{
    private readonly OrchestratorMetrics _metrics;

    public async Task<OrchestratorResponse> ExecuteFlowAsync(string flowName, Dictionary<string, object> inputData)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Lógica de ejecución...
            var result = await ProcessFlow(flowName, inputData);

            // Registrar métricas de éxito
            _metrics.IncrementFlowExecution(flowName, "success");
            _metrics.RecordFlowExecutionDuration(flowName, stopwatch.Elapsed.TotalSeconds, "success");
            _metrics.RecordPayloadSize(flowName, CalculatePayloadSize(result));

            return result;
        }
        catch (Exception ex)
        {
            // Registrar métricas de error
            _metrics.IncrementFlowExecution(flowName, "error");
            _metrics.RecordFlowExecutionDuration(flowName, stopwatch.Elapsed.TotalSeconds, "error");
            throw;
        }
    }
}
```

#### **Trazas:**

```csharp
public async Task<object> ExecuteDatasetAsync(DatasetConfig dataset, Dictionary<string, object> inputs)
{
    using var activity = OrchestratorActivitySource.StartDatasetExecution(dataset.Name, dataset.Type);
    activity?.AddContextInfo(inputs);

    try
    {
        var result = await ProcessDataset(dataset, inputs);
        activity?.SetSuccess();
        return result;
    }
    catch (Exception ex)
    {
        activity?.SetError(ex);
        throw;
    }
}
```

### **6. Visualización en Azure Application Insights**

#### **📊 Dashboards Automáticos**

- **Application Map**: Dependencias entre servicios
- **Performance**: Latencias de requests y dependencias
- **Failures**: Errores y excepciones
- **Metrics**: Métricas personalizadas del orquestador

#### **🔍 Queries de Ejemplo (KQL)**

**Flows más ejecutados:**

```kql
customMetrics
| where name == "orchestrator_flow_executions_total"
| summarize executions = sum(value) by tostring(customDimensions.flow_name)
| order by executions desc
```

**Duración promedio por flow:**

```kql
customMetrics
| where name == "orchestrator_flow_execution_duration_seconds"
| summarize avg_duration = avg(value) by tostring(customDimensions.flow_name)
| order by avg_duration desc
```

**Trazas de flows con errores:**

```kql
dependencies
| where name startswith "Flow.Execute"
| where success == false
| project timestamp, name, duration, resultCode, customDimensions
```

### **7. Variables de Entorno para Configuración**

```bash
# Desarrollo sin Application Insights
APPLICATIONINSIGHTS__CONNECTIONSTRING=""

# Producción con Application Insights completo
APPLICATIONINSIGHTS__CONNECTIONSTRING="InstrumentationKey=...;IngestionEndpoint=..."

# Override del nombre del servicio
OPENTELEMETRY__SERVICENAME="mc-orquestador-backend-prod"
```

### **8. Beneficios de la Implementación**

✅ **Observabilidad Completa**: Trazas, métricas y logs correlacionados
✅ **Monitoreo de Performance**: Latencias y throughput por flow/dataset
✅ **Detección de Errores**: Alertas automáticas en Application Insights
✅ **Análisis de Dependencias**: Visualización de llamadas a APIs externas
✅ **Métricas de Negocio**: Ejecuciones por flow, tamaños de payload
✅ **Debugging Avanzado**: Trazas distribuidas con contexto completo

### **9. Alertas Recomendadas**

| Alerta          | Condición           | Acción                    |
| --------------- | ------------------- | ------------------------- |
| Flow Failures   | > 5% error rate     | Notificación inmediata    |
| High Latency    | > 10s flow duration | Escalamiento              |
| Cache Miss Rate | > 80% miss rate     | Revisión de configuración |
| Payload Size    | > 1MB average       | Optimización de templates |

---

**🚀 Status**: ✅ Implementado y funcionando
**🔧 Configuración**: Automática con fallback para desarrollo
**📊 Métricas**: 9 métricas personalizadas implementadas
**🔍 Trazas**: ActivitySource personalizado para todos los componentes
