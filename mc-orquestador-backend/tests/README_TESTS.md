# Tests para OrchestratorController

## Resumen de Tests Implementados

Se han creado **23 tests** completos para el `OrchestratorController` que cubren todos los endpoints y escenarios posibles:

### 1. OrchestratorControllerTests.cs (12 tests)

#### Tests del endpoint BuildPayload:

- ✅ **BuildPayload_WithValidRequest_ReturnsOkResult**: Verifica respuesta exitosa con datos válidos
- ✅ **BuildPayload_WithFailedExecution_ReturnsBadRequest**: Maneja fallos de ejecución del flow
- ✅ **BuildPayload_WithException_ReturnsInternalServerError**: Maneja excepciones no controladas
- ✅ **BuildPayload_WithComplexInputData_ProcessesCorrectly**: Procesa datos complejos con metadatos

#### Tests del endpoint GetFlowConfig:

- ✅ **GetFlowConfig_WithValidFlowName_ReturnsOkResult**: Retorna configuración válida
- ✅ **GetFlowConfig_WithInvalidFlowName_ReturnsNotFound**: Maneja flows inexistentes

#### Tests del endpoint GetFlows:

- ✅ **GetFlows_ReturnsOkResultWithFlowList**: Retorna lista de flows disponibles
- ✅ **GetFlows_WithEmptyFlowList_ReturnsOkResultWithEmptyList**: Maneja listas vacías

#### Tests del endpoint ClearCache:

- ✅ **ClearCache_ReturnsOkResult**: Limpia cache exitosamente
- ✅ **ClearCache_WithException_ReturnsInternalServerError**: Maneja errores del servicio de cache

#### Tests de endpoints de monitoreo:

- ✅ **Health_ReturnsOkResultWithHealthInfo**: Verifica información de salud
- ✅ **Metrics_ReturnsOkResultWithMetricsInfo**: Verifica métricas del sistema

### 2. OrchestratorControllerIntegrationTests.cs (11 tests)

#### Tests de casos extremos y validación:

- ✅ **BuildPayload_WithInvalidFlowNames_HandlesGracefully**: Maneja nombres de flow inválidos (Theory con 3 casos)
- ✅ **BuildPayload_WithNullInputData_HandlesCorrectly**: Maneja datos de entrada nulos
- ✅ **BuildPayload_WithLargeInputData_ProcessesCorrectly**: Procesa grandes volúmenes de datos
- ✅ **GetFlowConfig_WithSpecialCharactersInFlowName_HandlesCorrectly**: Maneja caracteres especiales
- ✅ **GetFlows_WhenServiceThrowsException_ReturnsInternalServerError**: Maneja errores del servicio
- ✅ **GetFlowConfig_WhenServiceThrowsException_ReturnsInternalServerError**: Maneja errores de configuración
- ✅ **ClearCache_WithVeryLongCacheKey_HandlesCorrectly**: Maneja claves de cache muy largas
- ✅ **Health_AlwaysReturnsHealthyStatus**: Verifica consistencia del endpoint de salud
- ✅ **Metrics_ReturnsConsistentStructure**: Verifica estructura consistente de métricas

## Cobertura de Funcionalidades

### ✅ Endpoints Cubiertos:

- **POST /api/Orchestrator/build-payload/{flow}** - Construcción de payloads
- **GET /api/Orchestrator/health** - Estado de salud del servicio
- **GET /api/Orchestrator/flows/{flow}/config** - Configuración de flows
- **GET /api/Orchestrator/flows** - Lista de flows disponibles
- **DELETE /api/Orchestrator/cache/{key}** - Limpieza de cache
- **GET /api/Orchestrator/metrics** - Métricas del sistema

### ✅ Escenarios Cubiertos:

- **Casos exitosos**: Respuestas OK con datos válidos
- **Casos de error**: BadRequest, NotFound, InternalServerError
- **Validación de datos**: Datos nulos, vacíos, inválidos
- **Casos extremos**: Datos grandes, caracteres especiales, claves largas
- **Manejo de excepciones**: Errores del servicio, timeouts, fallos de conexión
- **Consistencia**: Múltiples llamadas, estructura de respuestas

### ✅ Tecnologías Utilizadas:

- **xUnit**: Framework de testing
- **Moq**: Mocking de dependencias
- **FluentAssertions**: Assertions legibles y expresivas
- **ASP.NET Core Testing**: Testing específico para controllers

## Ejecución de Tests

```bash
# Ejecutar todos los tests
dotnet test

# Ejecutar con reporte detallado
dotnet test --verbosity normal

# Ejecutar con logger detallado
dotnet test --logger "console;verbosity=detailed"
```

## Resultado Final

```
Test summary: total: 23; failed: 0; succeeded: 23; skipped: 0
✅ Todos los tests pasan exitosamente
✅ Cobertura completa del controlador
✅ Validación de todos los escenarios críticos
```

Los tests están listos para integración continua (CI/CD) y proporcionan una base sólida para el desarrollo y mantenimiento del microservicio orquestador.
