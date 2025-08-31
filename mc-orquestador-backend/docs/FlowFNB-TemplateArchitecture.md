# Flow FNB v1.1 - Arquitectura de Templates y PostProcessing

## Flujo de Ejecución

### 1. **Datasets en Paralelo** (Task.WhenAll)

```
qCliente ──┐
qCuentas ──┼── Parallel Execution
rawDeudas ─┤
apiScore ──┤
apiProductosActivos ──┘
```

### 2. **Templates Concurrent Processing**

Los templates se pueden armar en paralelo, pero se seleccionan basado en las reglas:

#### Template motorDecision

- **Default**: `motor/motor-default.json`
- **Conditional**: `motor/motor-premium.json` (si `flags.esPremium == true && score.valor > 720`)

#### Template SAP

- **Default**: `sap/sap-fnb.json`
- **Conditional Rules**:
  - `sap/sap-fnb-premium.json` (si cuenta PREMIUM con saldo > 5000)
  - `sap/sap-fnb-empresarial.json` (si cuenta EMPRESARIAL)
  - `sap/sap-fnb-review.json` (si `score.fraude == true`)

### 3. **PostProcessing Conditional Execution**

#### Flujo de Payload en PostProcessing

```json
{
  "useTemplateResult": true,
  "templateSource": "motorDecision|sap",
  "mergeStrategy": "templateFirst"
}
```

**Significado**:

- `useTemplateResult: true` → El payload principal será el template armado (motor-default.json, sap-fnb-premium.json, etc.)
- `templateSource` → Especifica qué template usar como base
- `mergeStrategy: "templateFirst"` → Template data + additionalData (template tiene prioridad)

#### Ejemplo de Payload Final Enviado:

```json
{
  // ← Contenido del template (motor-premium.json o sap-fnb-premium.json)
  "decisionRules": [...],
  "customerSegment": "PREMIUM",
  "riskLevel": "LOW",

  // ← additionalData se fusiona
  "orchestratorContext": {
    "flowId": "12345",
    "executionId": "67890",
    "templateUsed": "motorDecision",
    "templatePath": "motor/motor-premium.json"
  },
  "enrichedCustomerProfile": {
    "dni": "12345678",
    "isPremium": true,
    "riskScore": 750
  }
}
```

## Endpoints PostProcessing

### 🎯 Motor Decision API

- **Condición**: `templateUsed == 'motorDecision'`
- **Template**: Contenido de `motor-default.json` o `motor-premium.json`
- **Endpoint**: `https://api.fnb.motor.com/v1/decision/execute`
- **Payload**: Template + metadata del cliente y contexto

### 🏢 SAP Integration API

- **Condición**: `templateUsed == 'sap'`
- **Template**: Contenido de `sap-fnb.json`, `sap-fnb-premium.json`, etc.
- **Endpoint**: `https://sap.fnb.com/api/v2/financial/process`
- **Payload**: Template + contexto financiero completo

### 🛡️ Fraud Validation API

- **Condición**: `score.fraude == true || score.valor < 500`
- **Template**: No usa template (`useTemplateResult: false`)
- **Payload**: Datos estáticos para validación antifraude

### 📧 Notification Service

- **Condición**: `always`
- **Template**: No usa template
- **Payload**: Resumen de ejecución y notificación

### 📋 Audit Trail Service

- **Condición**: `flags.esPremium == true || score.fraude == true`
- **Template**: Usa template para auditoría detallada
- **Payload**: Template + metadata de compliance

## Variables Disponibles en PostProcessing

### Datos de Input

- `{{dni}}`, `{{cuentaContrato}}`, `{{canal}}`, `{{transaccionId}}`, `{{pais}}`

### Datos de Datasets

- `{{cliente.nombre}}`, `{{cliente.email}}`
- `{{cuentaPrincipal.numero}}`, `{{cuentaPrincipal.tipo}}`, `{{cuentaPrincipal.saldo}}`
- `{{score.valor}}`, `{{score.fraude}}`, `{{score.modeloVersion}}`
- `{{productos.cantidad}}`, `{{productos.primeraCategoria}}`

### Datos Derivados

- `{{totales.deudaPendiente}}`
- `{{flags.tieneDeuda}}`, `{{flags.esPremium}}`

### Datos de Orchestrator

- `{{flowId}}`, `{{executionId}}`, `{{timestamp}}`
- `{{templateUsed}}`, `{{templatePath}}`

## Flujo Temporal

```
1. Datasets (paralelos) ──→ 2. Template Selection ──→ 3. Template Rendering
                                      ↓
4. PostProcessing Evaluation ──→ 5. Conditional Endpoint Execution
```

### Ejemplo de Ejecución:

1. **Cliente Premium con score alto**:

   - Templates: `motor-premium.json` + `sap-fnb-premium.json`
   - PostProcessing: Motor Decision API + SAP Integration + Audit Trail + Notification

2. **Cliente con fraude detectado**:

   - Templates: `motor-default.json` + `sap-fnb-review.json`
   - PostProcessing: Fraud Validation + SAP Integration + Audit Trail + Notification

3. **Cliente estándar**:
   - Templates: `motor-default.json` + `sap-fnb.json`
   - PostProcessing: Motor Decision API + SAP Integration + Notification

## Ventajas de esta Arquitectura

✅ **Performance**: Datasets y templates en paralelo
✅ **Flexibility**: Ejecución condicional de endpoints
✅ **Maintainability**: Templates reutilizables
✅ **Observability**: Trazabilidad completa con OpenTelemetry
✅ **Resilience**: Retry policies y fallbacks por endpoint
✅ **Compliance**: Auditoría granular y automática
