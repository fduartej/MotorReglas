# Ejecución Paralela de Datasets

## Resumen de Cambios

Se ha implementado la **ejecución paralela de datasets** en el orquestador para mejorar significativamente el performance de los flujos.

## Problema Anterior

Anteriormente, los datasets se ejecutaban de forma **secuencial** usando un `foreach` loop:

```csharp
foreach (var dataset in flowConfig.Datasets)
{
    // Cada dataset esperaba a que terminara el anterior
    result = await _queryExecutor.ExecuteDatasetAsync(dataset, context);
}
```

## Solución Implementada

Ahora los datasets se ejecutan en **paralelo** usando `Task.WhenAll`:

```csharp
var datasetTasks = flowConfig.Datasets.Select(async dataset =>
{
    // Cada dataset se ejecuta independientemente
    // ...
});

var completedDatasets = await Task.WhenAll(datasetTasks);
```

## Beneficios

### Performance Mejorado

- **Reducción significativa del tiempo total** de ejecución del flujo
- Los datasets SQL e HTTP se ejecutan simultáneamente
- Solo se espera por el dataset más lento, no por la suma de todos

### Ejemplo con Flujo FNB

Con el flujo FNB que tiene 5 datasets:

**Antes (Secuencial):**

```
qCliente (200ms) → qCuentas (150ms) → rawDeudas (100ms) → apiScore (300ms) → apiProductosActivos (250ms)
Total: 1000ms
```

**Ahora (Paralelo):**

```
qCliente (200ms) ┐
qCuentas (150ms) ├── Todos ejecutándose al mismo tiempo
rawDeudas (100ms)├──
apiScore (300ms) ├── ← El más lento determina el tiempo total
apiProductosActivos (250ms) ┘
Total: 300ms (66% más rápido)
```

## Características Mantenidas

✅ **Caché**: Se mantiene la funcionalidad de caché para cada dataset  
✅ **Manejo de Errores**: Los errores en un dataset no afectan a los otros  
✅ **Logging**: Logs mejorados con timing individual por dataset  
✅ **Telemetría**: Compatible con OpenTelemetry para monitoreo

## Logging Mejorado

Se han agregado logs específicos para monitorear la ejecución paralela:

```csharp
// Al iniciar la ejecución paralela
_logger.LogInformation("Starting parallel execution of {DatasetCount} datasets for flow {FlowName}")

// Para cada dataset individual
_logger.LogDebug("Starting dataset {DatasetName} of type {DatasetType}")
_logger.LogDebug("Dataset {DatasetName} completed in {ElapsedMs}ms")

// Al completar todos los datasets
_logger.LogInformation("All {DatasetCount} datasets completed for flow {FlowName}")
```

## Consideraciones Técnicas

### Thread Safety

- Los ejecutores SQL y HTTP son thread-safe
- Cada dataset mantiene su propio contexto de ejecución
- El caché Redis es thread-safe

### Manejo de Recursos

- Las conexiones a base de datos se manejan de forma segura
- Los timeouts HTTP se respetan individualmente
- La presión de memoria se mantiene controlada

### Dependencias

- Los datasets son independientes entre sí en la fase de ejecución
- Las dependencias se resuelven en fases posteriores (mapping, derived fields)

## Monitoreo y Observabilidad

Con OpenTelemetry implementado, se puede observar:

- **Métricas**: Duración de cada dataset individual
- **Trazas**: Spans paralelos para cada dataset
- **Logs**: Timing detallado de la ejecución paralela

## Archivos Modificados

- `src/Features/FlowProcessing/Services/FlowOrchestrator.cs`
  - Refactorización del método `ExecuteFlowAsync`
  - Implementación de ejecución paralela con `Task.WhenAll`
  - Logging mejorado con timing individual

## Pruebas

Para probar la funcionalidad:

1. **Iniciar el servidor**: `dotnet run --project src/Orchestrator.csproj`
2. **Hacer llamada al endpoint**: `POST /api/orchestrator/build-payload/FNB`
3. **Revisar logs**: Verificar los tiempos de ejecución paralela
4. **Monitorear métricas**: En Azure Application Insights (si está configurado)

## Configuración del Flujo

No se requieren cambios en los archivos de configuración JSON de los flujos. La ejecución paralela es transparente y automática.

**Ejemplo con flow-FNB.json:**

```json
{
  "datasets": [
    {"name": "qCliente", "type": "sql", ...},
    {"name": "qCuentas", "type": "sql", ...},
    {"name": "rawDeudas", "type": "rawSql", ...},
    {"name": "apiScore", "type": "http", ...},
    {"name": "apiProductosActivos", "type": "http", ...}
  ]
}
```

Todos estos datasets ahora se ejecutan en paralelo automáticamente.
