# Multicotizador MCBrokers — Requerimientos del Rediseño

> Documento de trabajo. Fuente de verdad para alcance, arquitectura y orden de implementación.
> Basado en `plan_multicotizador.md` (plan aprobado) y los documentos técnicos por aseguradora en `/Documentación/`.
> Última actualización: 2026-05-13.

---

## 1. Resumen ejecutivo

Rediseño completo del Multicotizador actual (.NET 5 + Angular 8 + MySQL) hacia una plataforma web (Razor Pages) y móvil (MAUI) que comparte una única API REST. Cinco aseguradoras integradas: **GNP, Quálitas, ANA, AXA DXN y AXA COL**. El núcleo del rediseño es un **catálogo maestro homologado administrable** que reemplaza la coincidencia por Levenshtein en tiempo real del sistema actual.

**Stack confirmado:**
- ASP.NET Core .NET 10 + Razor Pages
- Clean Architecture (Domain / Application / Infrastructure / Api / Web)
- SQL Server Azure (instancia `prod_macooley`)
- Azure Blob Storage (XMLs request/response, PDFs de pólizas, imports)
- Azure Key Vault (secretos por aseguradora)
- Google OAuth con restricción `hd=mcbrokers.com.mx`
- Application Insights + Serilog estructurado
- Despliegue en `multicotizador.azurewebsites.net` (slot staging + swap)
- App móvil futura: .NET MAUI sobre la misma API

**TDD estricto:** ningún código de lógica de negocio se escribe sin un test fallido previo que lo justifique (Red → Green → Refactor).

---

## 2. Actores y roles

| Actor | Descripción | Acceso |
|---|---|---|
| **Agente / Vendedor** | Cotiza y emite pólizas. | Web (Razor) + Móvil (MAUI). |
| **Administrador** | Gestiona aseguradoras, tarifas, agentes, catálogos, errores conocidos. | Web admin. |
| **Finanzas** | Consulta comisiones, claves de agente por aseguradora. | Web admin (vista restringida). |
| **Operaciones / Soporte** | Da de alta errores conocidos, supervisa salud de integraciones, revisa cola de homologación. | Web admin. |
| **Cliente final** | Receptor de la póliza emitida (PDF por correo). No accede al sistema. | — |

Restricción dura: el correo del agente debe pertenecer al dominio `@mcbrokers.com.mx`. Validación en Google OAuth (`hd=`) y defensa en profundidad en código.

---

## 3. Arquitectura

### 3.1 Estructura de solución (Clean Architecture)

```
McBrokers.Multicotizador.sln
│
├── src/
│   ├── McBrokers.SharedKernel/              # Result<T>, OperationOutcome, tipos comunes
│   ├── McBrokers.Domain/                    # Entidades, VOs, Domain Services, reglas puras
│   ├── McBrokers.Application/               # Casos de uso, puertos, DTOs
│   ├── McBrokers.Infrastructure/            # EF Core, SQL Server, Blob, Key Vault, Google OAuth
│   ├── McBrokers.Insurers.Abstractions/     # IInsurerAdapter, contratos comunes
│   ├── McBrokers.Insurers.Qualitas/         # WCF SOAP + mappers + parser de error
│   ├── McBrokers.Insurers.Gnp/              # HttpClient + XML body + parser
│   ├── McBrokers.Insurers.Ana/              # WCF SOAP (2 servicios) + mappers
│   ├── McBrokers.Insurers.AxaDxn/           # WCF + integración COPSIS para emisión
│   ├── McBrokers.Insurers.AxaCol/           # WCF + XML embebido en CDATA
│   ├── McBrokers.Api/                       # Endpoints REST v1
│   └── McBrokers.Web/                       # Razor Pages (admin + cotización web)
│
└── tests/
    ├── McBrokers.Domain.Tests/
    ├── McBrokers.Application.Tests/
    ├── McBrokers.Insurers.Qualitas.Tests/
    ├── McBrokers.Insurers.Gnp.Tests/
    ├── McBrokers.Insurers.Ana.Tests/
    ├── McBrokers.Insurers.AxaDxn.Tests/
    ├── McBrokers.Insurers.AxaCol.Tests/
    ├── McBrokers.Api.Tests/                 # WebApplicationFactory + Testcontainers
    └── McBrokers.E2E.Tests/                 # Cotización → emisión end-to-end (smoke staging)
```

### 3.2 Principios de diseño

- **Regla de dependencias:** Domain no referencia nada; Application → Domain; Infrastructure → Application + Domain; Api/Web → Application + Infrastructure. Los adapters de aseguradora dependen de `Insurers.Abstractions` (que vive en Application) y de `Infrastructure` para utilidades de red.
- **Anti-corruption layer por aseguradora:** cada `McBrokers.Insurers.{X}` traduce entre el modelo de dominio y los caprichos del WS de su aseguradora. Si AXA cambia su XML, solo se toca ese proyecto.
- **Puertos y adaptadores:** `IInsurerAdapter` con `QuoteAsync(...)` y `EmitAsync(...)`. Implementaciones por aseguradora inyectadas vía `IEnumerable<IInsurerAdapter>` para iterar.
- **Razor consume la API interna** vía `HttpClient` tipado. No llama directo a Application Layer. Esto garantiza que el contrato que usa Razor es el mismo que el móvil — ningún endpoint privilegiado.
- **Cotización asíncrona obligatoria:** el flujo síncrono actual no escala.
  - `POST /api/v1/quotations` → encola y devuelve `quotationId + correlationId` (HTTP 202).
  - Worker (`IHostedService` o Azure Function) cotiza contra cada aseguradora en paralelo.
  - Cliente hace `GET /api/v1/quotations/{id}` (polling con ETag) o se suscribe a SignalR (`/hubs/quotations`).
  - Cada `QuotationInsurerResult` se inserta a medida que llega; la UI los muestra incrementalmente.
- **Correlation ID en cada request:** header `X-Correlation-Id` propagado a logs, XML en Blob, BD y respuesta. Si el cliente no lo manda, se genera.
- **Configuración:** `appsettings.json` no contiene secretos. Key Vault inyecta credenciales por aseguradora vía `IOptions<InsurerCredentialOptions>` en runtime.
- **Observabilidad:** Application Insights + Serilog JSON estructurado. Métricas custom: `quote_attempt_total{insurer,outcome}`, `quote_latency_ms{insurer}`.

### 3.3 Diagrama de capas (texto)

```
┌─────────────────────────────────────────────────────────────┐
│   Razor Pages (Admin + Cotización web)   |   App móvil      │
└───────────────────────┬──────────────────┴───────┬──────────┘
                        │ HTTP/JSON (mismo contrato)│
                        └─────────────┬─────────────┘
                                      ▼
┌─────────────────────────────────────────────────────────────┐
│                    McBrokers.Api (v1)                       │
│  CorrelationId · ProblemDetails · Auth Google · Rate Limit  │
└───────────────────────┬─────────────────────────────────────┘
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              McBrokers.Application                          │
│   QuoteAcrossInsurers · EmitPolicy · ManageCatalog · …      │
└───────┬────────────────┬────────────────────────────┬───────┘
        ▼                ▼                            ▼
┌──────────────┐ ┌──────────────────┐ ┌─────────────────────────┐
│  Domain      │ │ Insurer Adapters │ │  Infrastructure         │
│  (puro)      │ │ Qua/GNP/ANA/AXA  │ │  SQL · Blob · KV · OAuth│
└──────────────┘ └──────────────────┘ └─────────────────────────┘
```

---

## 4. Modelo de datos (lógico)

### 4.1 Agentes y autorización

| Tabla | Columnas clave | Propósito |
|---|---|---|
| `Agent` | Id, Email (único, dominio `@mcbrokers.com.mx`), FullName, IsActive, Role (Admin/Agent/Finance) | Identidad autenticada por Google. |
| `AgentInsurerKey` | AgentId, InsurerId, ExternalAgentCode, CommissionPct, ValidFrom, ValidTo | Clave de agente por aseguradora para comisiones. |
| `AgentSession` | Id, AgentId, IssuedAt, LastSeenAt, Device, IpHash | Auditoría de sesiones (web y móvil). |
| `AuditLog` | Id, AgentId, Action, EntityType, EntityId, CorrelationId, PayloadJson, CreatedAt | Auditoría inmutable de acciones admin. |

### 4.2 Configuración de aseguradoras

| Tabla | Columnas clave | Propósito |
|---|---|---|
| `Insurer` | Id, Code (`QUA`/`GNP`/`ANA`/`AXA_DXN`/`AXA_COL`), Name, IsEnabled, Logo, DisplayOrder | Switch admin global. |
| `InsurerConfig` | InsurerId, Environment, EndpointUrl, BusinessNumber, AgentCode, KeyVaultSecretName, TimeoutSeconds, MaxRetries | Datos no sensibles. El secreto vive en KV. |
| `InsurerPackageMapping` | InsurerId, InternalPackageId, ExternalPackageCode, ExternalDescription | Ej. interno=AMPLIA → Qua="1", GNP="AMPLIA", AXA="1". |
| `InsurerCoverageDefault` | InsurerId, PackageId, CoverageCode, SumInsured, Deductible, IsMandatory | Coberturas estándar por paquete por aseguradora. |
| `InsurerTariffOverride` | InsurerId, EffectiveFrom, Field, Value, UpdatedByAgentId | Cambios de tarifa administrables. |

### 4.3 Catálogo maestro homologado (núcleo del rediseño)

| Tabla | Columnas clave | Propósito |
|---|---|---|
| `VehicleMaster` | Id (`MK-00001`), Year, MasterBrand, MasterModel, MasterVersion, BodyType, Transmission, Doors, Cylinders, IsActive | Una fila por "auto canónico". |
| `VehicleInsurerMapping` | Id, VehicleMasterId, InsurerId, ClaveAmis, InsurerBrandRaw, InsurerModelRaw, InsurerVersionRaw, ConfidenceScore, ReviewState (Approved/Pending/Rejected), ReviewedByAgentId, ReviewedAt | N filas por VehicleMaster (una por aseguradora). **Reemplaza el Levenshtein en runtime.** |
| `Brand` / `BrandSynonym` | Brand.Id, Name / BrandId, SynonymText, Source | Diccionario "Chevrolet"="GM"="GENERAL MOTORS". |
| `TransmissionSynonym` | Text (STD/AUT/MAN/AUTOMATICO/ESTANDAR/4VEL), Canonical | Normalización transmisión. |
| `CatalogImportBatch` | Id, Source, StartedAt, CompletedAt, RowsTotal, RowsAutoApproved, RowsPendingReview, ImportedByAgentId | Bitácora de cargas/ETL. |

**Decisión clave:** la homologación es explícita (offline, persistida) — no se hace fuzzy match en tiempo de cotización. El runtime busca por `(VehicleMasterId, InsurerId)` en O(1).

### 4.4 Cotizaciones y emisiones

| Tabla | Columnas clave | Propósito |
|---|---|---|
| `Quotation` | Id (Guid), CorrelationId, AgentId, VehicleMasterId, PackageId, PaymentMode, ValuationType, SumInsured, CustomerSnapshotJson, PostalCode, Status (Pending/Partial/Completed/Failed), CreatedAt | Cotización lógica con múltiples resultados. |
| `QuotationInsurerResult` | Id, QuotationId, InsurerId, Status (Succeeded/Failed/Timeout/InsurerDown/NotCovered), ErrorCategory (Technical/Business/InsurerDown), ErrorCode, ErrorMessageHuman, PremiumTotal, PremiumNet, Tax, Fees, LatencyMs, RequestBlobRef, ResponseBlobRef, CreatedAt | Una fila por aseguradora con resultado y referencia al XML en Blob. |
| `Emission` | Id, QuotationInsurerResultId, PolicyNumber, Status (Pending/Issued/Failed), PdfBlobRef, IssuedAt, EmittedByAgentId | La emisión cuelga del resultado específico. |
| `EmissionAttempt` | Id, EmissionId, AttemptNumber, Outcome, LatencyMs, ErrorCode, CreatedAt | Reintentos de emisión. |

### 4.5 Errores conocidos

| Tabla | Columnas clave | Propósito |
|---|---|---|
| `KnownInsurerError` | Id, InsurerId, ExternalCode (ej. Qua `0288`), ExternalMessagePattern, Category, HumanMessage (es-MX), SuggestedAction, AutoRetryStrategy | Mapeo de error crudo del proveedor a mensaje útil para el vendedor. Administrable. |

### 4.6 Geografía

| Tabla | Propósito |
|---|---|
| `PostalCode` | CP → Estado, Municipio, Colonias (un registro por colonia). |
| `State` / `Municipality` | Catálogos normalizados con códigos compatibles por aseguradora (ANA usa EdoMun de 5 dígitos). |
| `InsurerStateCodeMapping` | InsurerId, StateId, ExternalCode. |

### 4.7 Restricciones e índices destacados

- `Quotation` particionable por `CreatedAt` (mensual) — facilita archivo.
- Índice cubriente en `VehicleInsurerMapping(VehicleMasterId, InsurerId)` para resolución O(1) de `ClaveAmis`.
- Índice único en `Agent(Email)` con `WHERE Email LIKE '%@mcbrokers.com.mx'`.
- `Quotation.CorrelationId` con índice — pivote de grep cruzado entre logs, BD y blobs.

---

## 5. Plan de fases — orden de implementación

Escala: **S** ≈ 1 semana (40h), **M** ≈ 2–3 sem, **L** ≈ 4–6 sem, **XL** > 6 sem.

> **Orden acordado por el usuario (2026-05-11):**
> Fases 0 → 1 → 2 → 3 (**GNP** como primera aseguradora) → resto de aseguradoras (Quálitas, ANA, AXA DXN, AXA COL) → 5 → 6 → 7 (móvil).
> **Nota:** el plan original recomendaba Quálitas como primera. El usuario eligió GNP. Decisión registrada.

### Fase 0 — Cimientos (S–M)

- Repo + branching strategy + GitHub Actions (build + test + análisis estático).
- Bootstrap solución .NET 10 con todos los proyectos vacíos según §3.1.
- Provisionar Azure: App Service, SQL `prod_macooley`, Storage (contenedores `xml-requests`, `xml-responses`, `pdf-policies`, `imports`), Key Vault, Application Insights.
- Google OAuth con restricción `hd=mcbrokers.com.mx` + validación en código.
- Migración EF Core inicial con tablas de identidad/agentes.
- Pipeline de deploy con slot staging (`multicotizador-staging.azurewebsites.net`) y swap.
- Health checks `/health/live`, `/health/ready`.
- Dashboard inicial en Application Insights.

**Riesgo:** acceso/red a aseguradoras desde Azure (whitelist de IP por aseguradora). Validar antes de Fase 3.

### Fase 1 — Panel admin mínimo (M)

- Razor Pages Admin: CRUD Aseguradoras, CRUD Agentes, CRUD Roles.
- Configuración de aseguradora (endpoints, business number, agente externo, timeout, retries) — secretos solo se referencian (no se escriben en BD).
- Pantallas de tarifas / coberturas / paquetes editables.
- Auditoría: cada cambio queda en `AuditLog`.
- Tests E2E mínimos del panel.

### Fase 2 — Catálogo maestro homologado (L) — **fase crítica**

El éxito del rediseño depende de esta fase.

- ETL desde MySQL legado → SQL Azure aplicando la estrategia de `README_multicotizadorminero.md`:
  - Carga a staging tables.
  - Normalización + diccionario de sinónimos (STD/AUT/AC/4CIL).
  - Bloqueo por marca + modelo + tipo vehículo.
  - Token Set Ratio con umbral 95% → autoaprobación; resto → `ReviewState=Pending`.
- UI admin para revisión manual del 5–10% pendiente (workflow tipo cola).
- Importadores incrementales por aseguradora (catálogo del año nuevo) ejecutables por admin.
- API `GET /api/v1/catalog?year=…` que devuelve el catálogo maestro completo del año (modelo minero: front filtra local).
- Plan de pruebas: validar que los AMIS resueltos del catálogo nuevo coinciden con los del sistema actual para muestra ≥ 500 vehículos cotizados en últimos 6 meses.

**Riesgo:** calidad de datos legados. **Mitigación:** la app puede arrancar con catálogo parcial; los vehículos sin homologación se marcan como "no cotiza este vehículo" con mensaje claro.

### Fase 3 — Primera aseguradora: **GNP** (M)

Decisión del usuario: GNP es la primera. Validamos el flujo punta a punta con esta integración.

- Implementar `IInsurerAdapter` para GNP (HttpClient + XML body + parser).
- Caso de uso `QuoteAcrossInsurers` con solo GNP habilitada.
- Encolado asíncrono (Azure Service Bus o `IHostedService` con cola en BD para arrancar).
- SignalR hub `/hubs/quotations` para progreso en vivo.
- Pantalla Razor de wizard de cotización (vendedor) consumiendo la API.
- Persistencia de XML req/res en Blob con `correlationId` en metadata.
- Contract tests con XMLs grabados.
- Cargar errores conocidos de GNP a `KnownInsurerError` (incluida la lógica de 3 intentos / 5 s documentada).

**Adiciones UX (feedback de usuarios, 2026-05-13) — prioridad: cotizar > emitir > styling:**

- **Header del asesor (cross-cutting admin + vendor)**: cabecera superior derecha en el `_Layout` compartido con foto circular (placeholder por ahora), nombre completo del asesor y **clave de asesor**. La clave es un campo nuevo `Agent.AgentCode` (varchar corto único, opcional) — clave interna MCBrokers que Finanzas usa para pagar comisiones. Distinta de `AgentInsurerKey.ExternalAgentCode` (per aseguradora). Captura desde el admin de agentes; los agentes recién auto-provisionados por OAuth quedan sin clave hasta que admin la asigne. Estilo mínimo funcional — frontend + marketing lo refinarán después.
- **Fallback "No encuentro el vehículo"** en el wizard de cotización: cuando los selectores no surfan el vehículo que el vendedor busca, un botón abre un buscador de texto libre sobre `VehicleMaster`. Filtrado **permisivo** por aseguradoras seleccionadas (devuelve vehículos con `VehicleInsurerMapping.ReviewState=Approved` para *al menos una* de las aseguradoras activas, marcando por resultado cuáles tienen mapping). Sin coincidencias → mensaje fijo "Contactar a la aseguradora para obtener la equivalencia" (la respuesta se canaliza al workflow de homologación de Fase 2). Implementación inicial con `LIKE` multi-token; FTS / Token Set Ratio se evalúa solo si la cobertura no alcanza con tráfico real. No introduce fuzzy-match runtime AMIS — el AMIS se sigue resolviendo por la homologación explícita.

### Fase 4 — Resto de aseguradoras (L)

Una por sprint en este orden:

1. **Quálitas** — WCF SOAP + parser de error (incluido el 0288 ya conocido).
2. **ANA** — WCF SOAP, dos servicios.
3. **AXA COL** — WCF + XML embebido en CDATA.
4. **AXA DXN** — WCF + integración COPSIS para emisión (al final por dependencia externa).

Por cada una:
- Adapter + contract tests con XMLs grabados.
- Mapeo de paquetes / coberturas / formas de pago / estados.
- Parser de errores + carga a `KnownInsurerError`.
- Tablero "panorama por aseguradora" en Application Insights.

### Fase 5 — Emisión, impresión y archivo (M)

- Caso de uso `EmitPolicy` que toma un `QuotationInsurerResult`.
- Adapter de emisión por aseguradora (lógica diferenciada para AXA DXN vía COPSIS).
- Descarga del PDF y subida a Blob (SAS URL para entrega segura).
- Reintentos con backoff (especialmente GNP).
- Envío de PDF al cliente por correo (cuenta institucional — **no** `multicotizador@mcb.uno`).
- Auditoría completa de la emisión.

### Fase 6 — Manejo de errores estructurado y observabilidad (S–M)

Transversal pero se cierra al final.

- Cada `QuotationInsurerResult` debe tener `ErrorCategory` + `HumanMessage`.
- UI vendedor muestra siempre causa + acción sugerida.
- Dashboard ops: tasa de éxito por aseguradora, latencia P50/P95, top 10 errores.
- Alertas (Application Insights) por aseguradora bajo umbral de éxito.

### Fase 7 — App móvil MAUI (L–XL)

Detalle en §7.

### Fase 8 — Retiro del sistema actual (S)

- Migración de pólizas históricas (solo metadata + Blob para PDFs ya emitidos).
- Operación dual (vendedores tienen ambos en lectura).
- Apagado del Angular 8 + API .NET 5.
- Revocación de credenciales del repo legado.

### Estimación

| Fase | Complejidad | Notas |
|---|---|---|
| 0 — Cimientos | S–M | Cuello de botella: red hacia aseguradoras desde Azure |
| 1 — Admin | M | |
| 2 — Catálogo | L | Riesgo más alto |
| 3 — GNP | M | Aprendizaje del patrón |
| 4 — Resto aseguradoras | L | 4 aseguradoras × M ≈ L |
| 5 — Emisión | M | |
| 6 — Errores/obs | S–M | Transversal |
| 7 — Móvil | L–XL | Empieza cuando 3–5 estén estables |
| 8 — Retiro | S | |

Total Fases 0–5: ~4 meses dev senior full-time, o 6–7 meses en bloques de 4 h diarios. Móvil suma 2–3 meses adicionales.

---

## 6. Migración del sistema actual

### 6.1 Migrar / preservar (con transformación)

| Origen | Destino | Cómo |
|---|---|---|
| Configuración de aseguradoras (endpoints, business numbers, claves de agente) | `Insurer`, `InsurerConfig`, Key Vault | Lectura única desde MySQL legado + carga manual al admin. |
| Datos productivos (contactos, agentes, claves, comisiones) | `Agent`, `AgentInsurerKey` | ETL one-shot. Emails normalizados a `@mcbrokers.com.mx`. |
| Catálogo legado (55K marcas + 179K desc + 732K versiones) | `VehicleMaster` + `VehicleInsurerMapping` | ETL con estrategia minero. Bulk insert a staging y match con confianza. |
| Pólizas históricas emitidas | `Emission` (metadata) + Blob (PDFs) | Migración incremental por año. No re-emitir, solo archivar. |
| XMLs históricos req/res | Blob `xml-archive/{year}/{month}/…` + índice en BD | Subida masiva con `correlationId` sintético si no existe. |
| Mapeos paquetes ↔ códigos, formas de pago, coberturas | `InsurerPackageMapping`, `InsurerCoverageDefault`, `KnownInsurerError` | Carga desde los `.md` en `/Documentación/` — son fuente de verdad. |
| Catálogo CP/estados/municipios | `PostalCode`, `State`, `Municipality`, `InsurerStateCodeMapping` | ETL con validación contra SEPOMEX. |

### 6.2 Reescribir 100%

| Componente actual | Decisión |
|---|---|
| `CotizacionNegocio.cs` (6.974 líneas) | Reescribir. Se desintegra en 5 adapters + casos de uso Application + Domain Services. Ningún copy-paste. |
| `Capa_Acceso_Datos` | EF Core con repos por agregado. Los SPs como `sp_get_versiones` desaparecen. |
| Construcción de XML/JSON por aseguradora | Reescribir desde los `.md` de documentación como contrato externo (no desde el código). |
| Frontend Angular 8 (`autos.component.*`) | Reescribir en Razor Pages. Wizard de cotización + resultados + emisión. |
| Manejo de errores (`hasError`/`txtError`) | `ProblemDetails` (RFC 7807) + `ErrorCategory` estructurada. |
| Persistencia de XMLs a disco local | `IBlobStore` con `correlationId`. Disco local desaparece. |
| Logging `log4net` | Serilog → Application Insights, JSON estructurado, propagación de correlation. |
| Auth JWT con secret en `appsettings.json` | Google OAuth + cookies HttpOnly (Razor) + JWT firmado HMAC (móvil) con refresh tokens. |
| Servicio de correo `multicotizador@mcb.uno` | Cuenta institucional + Microsoft Graph (si MCBrokers tiene M365) o SendGrid. |

### 6.3 Referencia (input, no migración)

- Los 4 `.md` en `/Documentación/` (GNP, ANA, AXA, Quálitas) son el contrato externo. Especificación de los adapters nuevos. Mantener actualizados.
- `README_multicotizadorminero.md` — estrategia maestra de catálogos. Implementar tal cual.
- `MCB DB Model.pdf` — apoyo histórico únicamente. El modelo nuevo (§4) lo reemplaza.

### 6.4 Descartar

- Solución `Arquitectura_duranm.sln` (queda read-only en repo legado).
- `TokenGmail.json`, `credentials.json`, `appsettings.json` con secretos. **Rotar TODAS las credenciales** antes/durante el rediseño.
- Carpetas `bin/`, `obj/`, `Templates.zip` versionadas — limpieza al pasar al repo nuevo.

---

## 7. App móvil (Fase 7 — diferida)

### 7.1 Veredicto

Viable y recomendable, **pero no antes de que las Fases 0–5 estén productivas y estables**. Razones:

- Misma API REST → cero duplicación en backend.
- El esquema asíncrono (`POST /quotations 202` + polling/SignalR) es **necesario** para móvil (4G y llamadas largas).
- Catálogo unificado por año encaja con patrón "descargar y filtrar local".

### 7.2 Stack: .NET MAUI

Recomendado por alineación de stack. Permite reusar `McBrokers.Domain` y un cliente HTTP tipado generado desde OpenAPI (NSwag/Refit).

```
McBrokers.Mobile.sln
├── McBrokers.Mobile.App/       # MAUI shell, vistas XAML, navegación
├── McBrokers.Mobile.Core/      # ViewModels, servicios, cache offline
└── McBrokers.Mobile.ApiClient/ # Cliente HTTP generado de OpenAPI
```

### 7.3 Endpoints adicionales para móvil

Sobre la API v1 (compartida con web):

| Endpoint | Web | Móvil | Propósito |
|---|---|---|---|
| `POST /api/v1/auth/google/web` | ✓ | – | Flujo web con cookie HttpOnly. |
| `POST /api/v1/auth/google/mobile` (PKCE) | – | ✓ | Flujo nativo OAuth con PKCE, devuelve access + refresh. |
| `POST /api/v1/auth/refresh` | – | ✓ | Rotación de tokens. |
| `GET/POST /api/v1/quotations` | ✓ | ✓ | Compartido. |
| `GET /api/v1/quotations/{id}` (ETag) | ✓ | ✓ | Polling eficiente. |
| `WS /hubs/quotations` (SignalR) | ✓ | ✓ | Móvil cae a polling si la red bloquea WS. |
| `GET /api/v1/catalog/sync?year=&since={etag}` | – | ✓ | Sincronización delta para uso offline. |
| `POST /api/v1/devices/register` | – | ✓ | Token FCM/APNs para push. |
| `POST /api/v1/devices/unregister` | – | ✓ | Logout/desinstalación. |
| `GET /api/v1/agent/profile` | ✓ | ✓ | Datos del agente + claves. |
| `GET /api/v1/emissions/{id}/pdf` | ✓ | ✓ | Variante móvil con `?as=download-url` (SAS de 5 min). |
| `POST /api/v1/emissions/{id}/share` | ✓ | ✓ | Correo al cliente. |
| `GET /api/v1/app/version-check` | – | ✓ | Forzar actualización. |
| `POST /api/v1/diagnostics/crash` | – | ✓ | Stack trace + correlation ID. |
| `GET /api/v1/dictionaries/*` | ✓ | ✓ | Móvil cachea agresivo. |
| `POST /api/v1/feedback` | – | ✓ | Feedback de vendedor. |
| `/api/v1/admin/*` | ✓ | – | Móvil no expone admin. |

**Resumen:** 6 endpoints nuevos puramente móvil + 1 variante.

### 7.4 Operativo

- Tokens: access 15 min, refresh 30 días con rotación. Refresh en Keychain (iOS) / Keystore (Android).
- Almacenamiento local: SQLite (`sqlite-net-pcl`) para catálogo offline. Sin PII de clientes.
- Push: FCM (Android) + APNs (iOS) vía Azure Notification Hubs.
- Modo offline: lectura de catálogo + borrador de cotización; envío diferido al volver conexión.
- Stores: publicación manual. Versionado semántico. App Center o GitHub Actions con fastlane para builds firmados, sin automatizar el upload.
- Política de privacidad y términos: requeridos. Vivirán en `multicotizador.mcbrokers.com.mx/legal`.
- Cumplimiento LFPDPPP: la cotización móvil pide solo CP + año/marca/versión. Captura PII completa del cliente se hace al emitir (puede preferirse web).
- Versionado API: móvil siempre `/api/v1/`. Cambios incompatibles → `/api/v2/`. Dos versiones mayores vivas en paralelo.

### 7.5 Sub-fases

- **7a (M):** Skeleton MAUI + auth + lectura de catálogo + ver cotizaciones existentes (read-only).
- **7b (M):** Crear cotización + ver resultados (SignalR + polling).
- **7c (M):** Emisión + descarga PDF + compartir.
- **7d (S):** Push + version-check + diagnostics.
- **7e (S):** Hardening, accesibilidad, pruebas con vendedores en campo, builds firmados.

---

## 8. Principios de ingeniería aplicables

- **TDD estricto** en Domain y Application: Red → Green → Refactor. No se escribe lógica de negocio sin test fallido previo.
- **Clean Architecture:** ver `docs-engineering/CleanArchitecture.md`.
- **SOLID + Clean Code:** ver `docs-engineering/SOLID.md`, `docs-engineering/CleanCode.md`.
- **YAGNI estricto:** no código "por si acaso"; ver `docs-engineering/CodeSimplicity.md`.
- **Testing pyramid:** mayoría unit (Domain/Application), contract tests por aseguradora (XMLs grabados), integration con Testcontainers, E2E mínimos en staging.
- **Sin secretos en repo:** Key Vault para todo. Rotación previa al go-live.

---

## 9. Riesgos a vigilar

| Riesgo | Fase | Mitigación |
|---|---|---|
| Calidad del catálogo legado | 2 | Workflow de revisión manual + UI admin para los pendientes. App arranca con catálogo parcial. |
| Red Azure → aseguradoras (whitelist IP) | 0 | Validar conectividad y whitelisting antes de iniciar Fase 3. |
| Dependencia COPSIS para AXA DXN | 4 | Dejar AXA DXN al final del orden de aseguradoras. |
| Credenciales del sistema actual en el repo legado | Transversal | Rotar TODAS antes/durante el rediseño. |
| Cambios de contrato externo de aseguradora durante el desarrollo | 3-4 | Anti-corruption layer aislado por proyecto. Cambios solo tocan ese adapter. |

---

## 10. Decisiones registradas

| Fecha | Decisión |
|---|---|
| 2026-05-11 | Stack: ASP.NET Core .NET 10 + Razor + SQL Azure + Google OAuth + Azure Blob + Key Vault. |
| 2026-05-11 | Homologación de catálogo **explícita** (offline, persistida). No Levenshtein en runtime. |
| 2026-05-11 | Cotización asíncrona obligatoria (POST 202 + worker + polling/SignalR). |
| 2026-05-11 | Razor consume la API interna por HTTP, no Application directo. Móvil usará el mismo contrato. |
| 2026-05-11 | Orden de aseguradoras: **GNP** primero (Fase 3), luego Quálitas, ANA, AXA COL, AXA DXN. (Diferencia con plan original que sugería Quálitas como primera.) |
| 2026-05-11 | App móvil = MAUI, después de Fases 0–5 estables. |
| 2026-05-11 | TDD estricto: ninguna lógica de negocio sin test previo. |
| 2026-05-13 | `Agent.AgentCode` (clave interna MCBrokers para comisiones) — nuevo campo opcional, único cuando se asigna. Distinto de `AgentInsurerKey.ExternalAgentCode`. Capturado por admin; OAuth no lo provisiona. |
| 2026-05-13 | Fallback de búsqueda de vehículo en wizard: filtro **permisivo** por aseguradoras activas (devuelve vehículos con mapping `Approved` para al menos una). LIKE multi-token como implementación inicial. |

---

> **Estado:** plan aprobado, pendiente aprobar el árbol concreto de archivos antes de ejecutar `dotnet new`.
