# Sistema de PostProcessing

El sistema de PostProcessing permite invocar endpoints REST después de que se completen los datasets de un flow, utilizando los resultados del template generado.

## Características Principales

### 🔄 Modos de Ejecución

- **Sequential**: Ejecuta endpoints uno tras otro
- **Conditional**: Ejecuta endpoints basado en condiciones
- **Parallel**: Ejecuta todos los endpoints en paralelo

### 🎯 Ejecución Condicional

Utiliza expresiones para determinar qué endpoints ejecutar:

```json
"executeIf": "templateUsed == 'motordecision'"
"executeIf": "input.monto > 1000"
"executeIf": "always"
```

### 📊 Auditoría Granular

Control detallado de logging por endpoint:

```json
"audit": {
  "enabled": true,
  "logRequest": true,
  "logResponse": true,
  "logHeaders": false
}
```

### 🛡️ Manejo de Errores

Estrategias flexibles por endpoint:

- **retry**: Reintenta con backoff
- **failfast**: Falla inmediatamente
- **ignore**: Continúa sin fallar

### 🔀 Mapeo de Respuestas

Extrae datos específicos de las respuestas:

```json
"responseMapping": {
  "decision": "$.result.decision",
  "confidence": "$.result.confidence"
}
```

## Configuración

### Estructura Básica

```json
{
  "postProcessing": {
    "executionMode": "conditional|sequential|parallel",
    "endpoints": [
      {
        "id": "unique-endpoint-id",
        "name": "Endpoint Description",
        "executeIf": "condition",
        "endpoint": {
          /* configuración HTTP */
        },
        "payload": {
          /* configuración de payload */
        },
        "responseMapping": {
          /* mapeo de respuesta */
        },
        "errorHandling": {
          /* manejo de errores */
        },
        "audit": {
          /* configuración de auditoría */
        }
      }
    ]
  }
}
```

### Configuración de Endpoint

```json
"endpoint": {
  "url": "https://api.example.com/endpoint",
  "method": "POST|GET|PUT|DELETE",
  "timeout": 60,
  "headers": {
    "Content-Type": "application/json",
    "Authorization": "Bearer {{config.token}}"
  }
}
```

### Configuración de Payload

```json
"payload": {
  "useTemplateResult": true,  // Usa el resultado del template
  "additionalData": {         // Datos adicionales
    "flowId": "{{flowId}}",
    "timestamp": "{{timestamp}}"
  }
}
```

O payload estático:

```json
"payload": {
  "useTemplateResult": false,
  "staticPayload": {
    "type": "notification",
    "message": "Process completed"
  }
}
```

### Variables Disponibles

- `{{flowId}}` - ID del flow
- `{{executionId}}` - ID de la ejecución
- `{{timestamp}}` - Timestamp actual
- `{{templateUsed}}` - Template utilizado
- `{{config.variableName}}` - Variables de configuración

## Ejemplos

### Ejemplo 1: Motor de Decisión Condicional

```json
{
  "postProcessing": {
    "executionMode": "conditional",
    "endpoints": [
      {
        "id": "motor-decision",
        "executeIf": "templateUsed == 'motordecision'",
        "endpoint": {
          "url": "https://api.motordecision.com/v1/execute",
          "method": "POST"
        },
        "payload": {
          "useTemplateResult": true
        },
        "errorHandling": {
          "strategy": "retry",
          "maxRetries": 3
        }
      }
    ]
  }
}
```

### Ejemplo 2: Secuencial con Múltiples APIs

```json
{
  "postProcessing": {
    "executionMode": "sequential",
    "endpoints": [
      {
        "id": "process-data",
        "endpoint": {
          "url": "https://api.process.com/data",
          "method": "POST"
        },
        "payload": {
          "useTemplateResult": true
        }
      },
      {
        "id": "send-notification",
        "endpoint": {
          "url": "https://api.notifications.com/send",
          "method": "POST"
        },
        "payload": {
          "useTemplateResult": false,
          "staticPayload": {
            "message": "Processing completed"
          }
        }
      }
    ]
  }
}
```

## Integración con OpenTelemetry

El sistema incluye telemetría completa:

- **Métricas**: Contadores de éxito/error por endpoint
- **Trazas**: Seguimiento distribuido de invocaciones
- **Logs**: Auditoría detallada configurable

## Compatibilidad

✅ **Backwards Compatible**: Los flows existentes siguen funcionando sin cambios
✅ **Opcional**: PostProcessing es completamente opcional
✅ **Incremental**: Se puede agregar gradualmente a flows existentes

## Archivos de Ejemplo

- `examples/postprocessing-flow-config.json` - Configuración completa con ejecución condicional
- `examples/simple-postprocessing-flow-config.json` - Configuración secuencial básica

## Arquitectura

```
FlowOrchestrator
├── Parallel Dataset Execution (Task.WhenAll)
├── Template Processing
└── PostProcessing Execution
    ├── Sequential Mode
    ├── Conditional Mode (con evaluación de expresiones)
    └── Parallel Mode
        ├── HTTP Invocations
        ├── Response Mapping
        ├── Error Handling
        └── Auditing + OpenTelemetry
```
