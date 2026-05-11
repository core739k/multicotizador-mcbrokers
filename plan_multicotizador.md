● Plan de Acción — Rediseño del Multicotizador MCBrokers

  Stack confirmado: ASP.NET Core .NET 10 + Razor Pages + SQL Server Azure (prod_macooley) + Clean Architecture + Google OAuth @mcbrokers.com.mx + Azure Blob para XMLs/PDFs + tabla maestra de homologación
  administrable. Despliegue en multicotizador.azurewebsites.net. Aseguradoras: GNP, Quálitas, ANA, AXA (DXN y COL).

  ---
  1. Arquitectura propuesta

  1.1 Estructura de solución (Clean Architecture)

  McBrokers.Multicotizador.sln
  │
  ├── src/
  │   ├── McBrokers.SharedKernel/                  # Result<T>, OperationOutcome, tipos comunes
  │   ├── McBrokers.Domain/                        # Entidades, VOs, Domain Services, reglas puras
  │   │   ├── Agents/                              # Agent, AgentKey, Role
  │   │   ├── Catalog/                             # VehicleMaster, VehicleInsurerMapping, Brand, Year, BodyType, Transmission (VOs)
  │   │   ├── Quotations/                          # Quotation, QuotationLine, Coverage, Package
  │   │   ├── Emissions/                           # Emission, Policy
  │   │   ├── Insurers/                            # InsurerCode (enum/VO), InsurerConfig
  │   │   ├── Geo/                                 # PostalCode, State, Municipality
  │   │   └── Errors/                              # ErrorCategory (Technical | Business | InsurerDown), DomainError
  │   │
  │   ├── McBrokers.Application/                   # Casos de uso, puertos, DTOs
  │   │   ├── Quotations/                          # QuoteAcrossInsurers, GetQuotationStatus, ListAgentQuotations
  │   │   ├── Emissions/                           # EmitPolicy, GetPolicyPdf
  │   │   ├── Catalog/                             # SearchVehicleMaster, UpsertMapping, ImportLegacyCatalog
  │   │   ├── Admin/                               # ToggleInsurer, UpdateTariff, ManageAgents
  │   │   ├── Auth/                                # ResolveAgentFromGoogleToken
  │   │   ├── Ports/                               # IInsurerAdapter, IBlobStore, ICatalogRepository, IAgentRepository, IClock, INotificationSender
  │   │   └── Errors/                              # ApplicationError, mapeo a HTTP problem details
  │   │
  │   ├── McBrokers.Infrastructure/                # EF Core, SQL Server, Blob, Key Vault, Google OAuth
  │   │   ├── Persistence/                         # AppDbContext, configuraciones EF, migraciones, repos
  │   │   ├── Blob/                                # AzureBlobStore (XMLs req/res, PDFs)
  │   │   ├── Secrets/                             # KeyVaultProvider
  │   │   ├── Identity/                            # GoogleOAuthHandler con restricción de dominio
  │   │   └── Logging/                             # Serilog → Application Insights, enrichers con correlation ID
  │   │
  │   ├── McBrokers.Insurers.Abstractions/         # IInsurerAdapter, InsurerQuoteRequest/Response, InsurerError
  │   ├── McBrokers.Insurers.Qualitas/             # Cliente WCF SOAP + mappers + parser de error
  │   ├── McBrokers.Insurers.Gnp/                  # HttpClient + XML body + parser
  │   ├── McBrokers.Insurers.Ana/                  # Cliente WCF SOAP (2 servicios) + mappers
  │   ├── McBrokers.Insurers.AxaDxn/               # Cliente WCF + integración COPSIS para emisión
  │   ├── McBrokers.Insurers.AxaCol/               # Cliente WCF + XML embebido en CDATA
  │   │
  │   ├── McBrokers.Api/                           # Endpoints REST (consumidos por Razor y por móvil)
  │   │   ├── Endpoints/                           # Minimal APIs o Controllers
  │   │   ├── Filters/                             # CorrelationIdFilter, ProblemDetails, AgentClaimsFilter
  │   │   ├── Versioning/                          # /api/v1
  │   │   └── OpenApi/
  │   │
  │   └── McBrokers.Web/                           # Razor Pages (panel admin + cotización web)
  │       ├── Pages/Admin/                         # Aseguradoras, Tarifas, Catálogos, Agentes
  │       ├── Pages/Cotizacion/                    # Wizard de cotización para vendedor
  │       └── ViewModels/
  │
  └── tests/
      ├── McBrokers.Domain.Tests/                  # Unit tests de reglas (rápidos)
      ├── McBrokers.Application.Tests/             # Casos de uso con mocks de puertos
      ├── McBrokers.Insurers.{X}.Tests/            # Contract tests por aseguradora (XMLs grabados de respuesta)
      ├── McBrokers.Api.Tests/                     # WebApplicationFactory + Testcontainers (SQL Server)
      └── McBrokers.E2E.Tests/                     # Flujos cotización→emisión end-to-end (smoke en staging)

  1.2 Principios de diseño aplicados

  - Regla de dependencias: Domain no referencia nada; Application referencia Domain; Infrastructure referencia Application y Domain; Api/Web referencian Application e Infrastructure.
  - Anti-corruption layer por aseguradora: cada McBrokers.Insurers.{X} traduce entre el modelo de dominio y los caprichos del WS de su aseguradora. Si mañana AXA cambia su XML, sólo se toca ese proyecto.
  - Puertos y adaptadores: IInsurerAdapter con un único método QuoteAsync(QuoteRequest, CancellationToken) y EmitAsync(EmitRequest, …) — implementaciones por aseguradora se inyectan vía DI con
  IEnumerable<IInsurerAdapter> para iterar.
  - Razor Pages consume la API interna por HttpClient tipado (no llama directamente al Application Layer). Esto garantiza que el contrato que usa Razor es exactamente el mismo que el móvil — ningún endpoint
  privilegiado.
  - Cotización asíncrona obligatoria (decisión clave): el flujo síncrono actual no escala. Esquema:
    a. POST /api/v1/quotations → encola y devuelve quotationId + correlationId inmediatamente (HTTP 202).
    b. Worker (McBrokers.Worker hospedado o Azure Function) ejecuta cotización contra cada aseguradora en paralelo.
    c. Cliente hace GET /api/v1/quotations/{id} (polling) o se suscribe a SignalR (/hubs/quotations) para actualizaciones en vivo.
    d. Cada QuotationInsurerResult se inserta a medida que llega. La UI los va mostrando.
  - Correlation ID en cada request: header X-Correlation-Id → propagado a logs, a XML guardado en Blob, a entradas en BD, a respuesta. Si el cliente no lo manda, se genera.
  - Configuración: appsettings.json no contiene secretos; Key Vault inyecta credenciales por aseguradora vía IOptions<InsurerCredentialOptions> a tiempo de ejecución.
  - Observabilidad: Application Insights + Serilog estructurado. Métricas custom: quote_attempt_total{insurer=...,outcome=...}, quote_latency_ms{insurer=...}.

  1.3 Diagrama de capas (texto)

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

  ---
  2. Modelo de datos principal

  Esquema lógico (SQL Server). Detalles de columnas representativos — el diseño fino se hará durante la fase de catálogo.

  2.1 Agentes y autorización

  ┌─────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────┬───────────────────────────────────────────────────────────────────────┐
  │      Tabla      │                                        Columnas clave                                        │                               Propósito                               │
  ├─────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤
  │ Agent           │ Id, Email (único, dominio @mcbrokers.com.mx), FullName, IsActive, Role (Admin/Agent/Finance) │ Identidad del agente, autenticada por Google.                         │
  ├─────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤
  │ AgentInsurerKey │ AgentId FK, InsurerId FK, ExternalAgentCode, CommissionPct, ValidFrom, ValidTo               │ Clave de agente por aseguradora para comisiones (vista por Finanzas). │
  ├─────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤
  │ AgentSession    │ Id, AgentId, IssuedAt, LastSeenAt, Device, IpHash                                            │ Auditoría de sesiones (web y móvil).                                  │
  ├─────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤
  │ AuditLog        │ Id, AgentId, Action, EntityType, EntityId, CorrelationId, PayloadJson, CreatedAt             │ Auditoría inmutable de acciones admin.                                │
  └─────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────┴───────────────────────────────────────────────────────────────────────┘

  2.2 Configuración de aseguradoras

  Tabla: Insurer
  Columnas clave: Id, Code (QUA/GNP/ANA/AXA_DXN/AXA_COL), Name, IsEnabled, Logo, DisplayOrder
  Propósito: Switch admin para activar/desactivar globalmente.
  ────────────────────────────────────────
  Tabla: InsurerConfig
  Columnas clave: InsurerId, Environment (Prod/Staging), EndpointUrl, BusinessNumber, AgentCode, KeyVaultSecretName (apunta a Key Vault), TimeoutSeconds, MaxRetries
  Propósito: Datos no-sensibles; el secreto vive en Key Vault.
  ────────────────────────────────────────
  Tabla: InsurerPackageMapping
  Columnas clave: InsurerId, InternalPackageId, ExternalPackageCode, ExternalDescription
  Propósito: Ej. interno=AMPLIA → Qua="1", GNP="AMPLIA", AXA="1".
  ────────────────────────────────────────
  Tabla: InsurerCoverageDefault
  Columnas clave: InsurerId, PackageId, CoverageCode, SumInsured, Deductible, IsMandatory
  Propósito: Coberturas estándar por paquete por aseguradora.
  ────────────────────────────────────────
  Tabla: InsurerTariffOverride
  Columnas clave: InsurerId, EffectiveFrom, Field, Value, UpdatedByAgentId
  Propósito: Cambios de tarifa administrables (lo que pidieron mantener).

  2.3 Catálogo maestro homologado (núcleo del rediseño)

  Tabla: VehicleMaster
  Columnas clave: Id (MK-00001), Year, MasterBrand, MasterModel, MasterVersion, BodyType, Transmission (STD/AUT/CVT), Doors, Cylinders, IsActive
  Propósito: Una fila por "auto canónico" (year + marca + modelo + versión canónica).
  ────────────────────────────────────────
  Tabla: VehicleInsurerMapping
  Columnas clave: Id, VehicleMasterId FK, InsurerId FK, ClaveAmis, InsurerBrandRaw, InsurerModelRaw, InsurerVersionRaw, ConfidenceScore, ReviewState (Approved/Pending/Rejected), ReviewedByAgentId, ReviewedAt
  Propósito: N filas por VehicleMaster: una por aseguradora. Esta es la tabla que reemplaza el Levenshtein en tiempo real.
  ────────────────────────────────────────
  Tabla: Brand / BrandSynonym
  Columnas clave: Brand.Id, Name / BrandId, SynonymText, Source
  Propósito: Diccionario "Chevrolet"="GENERAL MOTORS"="GM" administrable.
  ────────────────────────────────────────
  Tabla: TransmissionSynonym
  Columnas clave: Text (STD/AUT/MAN/AUTOMATICO/ESTANDAR/4VEL), Canonical
  Propósito: Normalización del problema "estándar vs automático".
  ────────────────────────────────────────
  Tabla: CatalogImportBatch
  Columnas clave: Id, Source, StartedAt, CompletedAt, RowsTotal, RowsAutoApproved, RowsPendingReview, ImportedByAgentId
  Propósito: Bitácora de cargas / ETL desde la BD del sistema actual y desde catálogos nuevos de aseguradoras.

  2.4 Cotizaciones y emisiones

  Tabla: Quotation
  Columnas clave: Id (Guid), CorrelationId, AgentId, VehicleMasterId, PackageId, PaymentMode, ValuationType, SumInsured, CustomerSnapshotJson, PostalCode, Status (Pending/Partial/Completed/Failed), CreatedAt
  Propósito: Cotización lógica (puede dar múltiples resultados por aseguradora).
  ────────────────────────────────────────
  Tabla: QuotationInsurerResult
  Columnas clave: Id, QuotationId, InsurerId, Status (Succeeded/Failed/Timeout/InsurerDown/NotCovered), ErrorCategory (Technical/Business/InsurerDown), ErrorCode, ErrorMessageHuman, PremiumTotal, PremiumNet,
    Tax, Fees, LatencyMs, RequestBlobRef, ResponseBlobRef, CreatedAt
  Propósito: Una fila por aseguradora con resultado individual y referencia al XML en Blob.
  ────────────────────────────────────────
  Tabla: Emission
  Columnas clave: Id, QuotationInsurerResultId, PolicyNumber, Status (Pending/Issued/Failed), PdfBlobRef, IssuedAt, EmittedByAgentId
  Propósito: La emisión cuelga del resultado específico (porque sólo se emite con una aseguradora).
  ────────────────────────────────────────
  Tabla: EmissionAttempt
  Columnas clave: Id, EmissionId, AttemptNumber, Outcome, LatencyMs, ErrorCode, CreatedAt
  Propósito: Trazabilidad de reintentos de emisión.

  2.5 Catálogo de errores conocido

  Tabla: KnownInsurerError
  Columnas clave: Id, InsurerId, ExternalCode (ej. Qua 0288), ExternalMessagePattern, Category, HumanMessage (es-MX), SuggestedAction, AutoRetryStrategy
  Propósito: Mapeo del error crudo del proveedor a mensaje útil para el vendedor. Administrable: Operaciones agrega entradas a medida que aparecen errores nuevos.

  2.6 Geografía

  ┌─────────────────────────┬────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │          Tabla          │                                                   Propósito                                                    │
  ├─────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ PostalCode              │ CP → Estado, Municipio, Colonias (un registro por colonia). Reutiliza el dataset del sistema actual (saneado). │
  ├─────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ State / Municipality    │ Catálogos normalizados con códigos compatibles con cada aseguradora (ej. ANA usa EdoMun de 5 dígitos).         │
  ├─────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ InsurerStateCodeMapping │ InsurerId, StateId, ExternalCode                                                                               │
  └─────────────────────────┴────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

  2.7 Restricciones e índices destacados

  - Quotation particionable por CreatedAt (mensual) — facilita archivo.
  - Índice cubriente en VehicleInsurerMapping(VehicleMasterId, InsurerId) para resolución directa O(1) de claveAMIS.
  - Índice único en Agent(Email) con WHERE Email LIKE '%@mcbrokers.com.mx'.
  - Quotation.CorrelationId con índice; usado para grep cruzado entre logs, BD y blobs.

  ---
  3. Plan de fases con estimación de complejidad

  Escala: S (1 semana ≈ 40h), M (2–3 sem), L (4–6 sem), XL (>6 sem). Asume un dev senior dedicado; ajustar a la realidad del equipo.

  Fase 0 — Cimientos (S–M)

  Habilitar el "esqueleto vivo" para que el resto se construya encima.
  - Crear repo, branching strategy, GitHub Actions con build + test + análisis estático.
  - Bootstrap solución .NET 10 con todos los proyectos vacíos según §1.1.
  - Provisionar Azure: App Service, SQL prod_macooley, Storage (contenedores xml-requests, xml-responses, pdf-policies, imports), Key Vault, Application Insights.
  - Configurar Google OAuth con restricción hd=mcbrokers.com.mx y validación de dominio en código (defensa en profundidad).
  - Migración EF Core inicial con tablas de identidad/agentes.
  - Pipeline de deploy con slot de staging (multicotizador-staging.azurewebsites.net) y swap.
  - Health checks (/health/live, /health/ready).
  - Dashboard inicial en Application Insights.

  Riesgo: acceso/red a aseguradoras desde Azure (whitelist de IP por aseguradora). Validar antes de Fase 4.

  Fase 1 — Panel admin mínimo (M)

  - Razor Pages Admin: CRUD Aseguradoras, CRUD Agentes, CRUD Roles.
  - Pantalla de configuración de aseguradora (endpoints, business number, agente externo, timeout, retries) — secretos sólo se referencian.
  - Pantallas de tarifas/coberturas/paquetes editables.
  - Auditoría: cada cambio queda en AuditLog.
  - Tests E2E mínimos del panel.

  Fase 2 — Catálogo maestro homologado (L) — fase crítica

  El éxito del rediseño depende de esta fase.
  - ETL desde MySQL legado → SQL Azure aplicando la estrategia descrita en README_multicotizadorminero.md:
    - Carga a staging tables.
    - Normalización + diccionario de sinónimos (STD/AUT/AC/4CIL).
    - Bloqueo por marca + modelo + tipo vehículo.
    - Token Set Ratio con umbral 95% → autoaprobación; lo demás → ReviewState=Pending.
  - UI admin para revisión manual del 5–10% pendiente (workflow tipo cola).
  - Importadores incrementales por aseguradora (catálogo del año nuevo) que el admin pueda disparar.
  - API GET /api/v1/catalog?year=… que devuelva el catálogo maestro completo del año (modelo minero: front filtra local).
  - Plan de pruebas: validar que los AMIS resueltos del catálogo nuevo coinciden con los del sistema actual para una muestra representativa (≥ 500 vehículos cotizados en últimos 6 meses).

  Riesgo: calidad de datos legados. Mitigación: la app móvil/web puede arrancar con catálogo parcial; las aseguradoras sin homologación se marcan como "no cotiza este vehículo" con mensaje claro.

  Fase 3 — Cotización con la primera aseguradora (M)

  Elegir la menos compleja para validar el flujo punta a punta. Recomiendo Qualitas (mejor documentada, control sobre el reintento 0288 ya conocido).
  - Implementar IInsurerAdapter para Qualitas con clientes WCF generados.
  - Caso de uso QuoteAcrossInsurers con sólo Qualitas habilitada.
  - Encolado asíncrono (Azure Service Bus o IHostedService con cola en BD para empezar).
  - SignalR hub /hubs/quotations para progreso en vivo.
  - Pantalla Razor de wizard de cotización (vendedor) consumiendo la API.
  - Persistencia de XML req/res en Blob con correlationId en metadata.
  - Contract tests con XMLs grabados.

  Fase 4 — Resto de aseguradoras (L)

  Una por sprint, en orden propuesto: GNP → ANA → AXA COL → AXA DXN (esta última al final por dependencia con COPSIS).
  - Por cada una: adapter + contract tests + mapeo de paquetes/coberturas/formas de pago/estados desde §2.2 + parser de errores conocidos cargado en KnownInsurerError.
  - Tablero "panorama por aseguradora" en Application Insights.

  Fase 5 — Emisión, impresión y archivo (M)

  - Caso de uso EmitPolicy que toma un QuotationInsurerResult.
  - Adapter de emisión por aseguradora (lógica diferenciada para AXA DXN vía COPSIS).
  - Descarga del PDF y subida a Blob (con SAS URL para entrega segura).
  - Reintentos con backoff (especialmente GNP, que ya tiene 3 intentos / 5 s).
  - Envío de PDF por correo al cliente (mismo flujo Gmail OAuth pero con cuenta institucional, no la actual multicotizador@mcb.uno).
  - Auditoría completa de la emisión.

  Fase 6 — Manejo de errores estructurado y observabilidad (S–M)

  Transversal pero se cierra al final.
  - Validar que cada QuotationInsurerResult tenga ErrorCategory + HumanMessage.
  - UI vendedor muestra siempre causa + acción sugerida (ej. "Quálitas: vehículo no válido para uso particular — intenta cambiar a comercial").
  - Dashboard ops: tasa de éxito por aseguradora, latencia P50/P95, top 10 errores por aseguradora.
  - Alertas (Application Insights) cuando una aseguradora cae por debajo de cierto umbral de éxito.

  Fase 7 — App móvil (L–XL)

  Detalle en §5.

  Fase 8 — Retiro del sistema actual (S)

  - Migración de pólizas históricas (sólo metadata + apuntar a Blob para PDFs ya emitidos).
  - Período de operación dual (vendedores tienen acceso a ambos en lectura).
  - Apagado del Angular 8 + API .NET 5.
  - Revocación de credenciales que estaban en el repo legado.

  Estimación total (orden de magnitud)

  ┌─────────────────────────┬─────────────┬───────────────────────────────────────────────────────┐
  │          Fase           │ Complejidad │                         Notas                         │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 0 — Cimientos           │ S–M         │ Cuello de botella: red hacia aseguradoras desde Azure │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 1 — Admin               │ M           │                                                       │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 2 — Catálogo            │ L           │ Riesgo más alto — definir antes de Fase 3             │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 3 — Primera aseguradora │ M           │ Aprendizaje del patrón                                │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 4 — Resto aseguradoras  │ L           │ 4 aseguradoras × M ≈ L total                          │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 5 — Emisión             │ M           │                                                       │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 6 — Errores/obs         │ S–M         │ Transversal                                           │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 7 — Móvil               │ L–XL        │ Empieza cuando Fases 3–5 estén estables               │
  ├─────────────────────────┼─────────────┼───────────────────────────────────────────────────────┤
  │ 8 — Retiro              │ S           │                                                       │
  └─────────────────────────┴─────────────┴───────────────────────────────────────────────────────┘

  Total realista para llegar a producción usable (Fases 0–5): ~4 meses con un senior dedicado tiempo completo, o 6–7 meses con un senior trabajando en bloques de 4 h diarios (tu modo habitual). La app móvil
  suma otros 2–3 meses (depende de MAUI vs Flutter).

  ---
  4. Qué migrar del sistema actual y qué reescribir

  4.1 Migrar / preservar (con transformación)

  Origen: Configuración de aseguradoras (endpoints, business numbers, claves de agente)
  Destino: Insurer, InsurerConfig, Key Vault
  Cómo: Lectura única desde MySQL legado, validación y carga manual al admin.
  ────────────────────────────────────────
  Origen: Datos productivos: contactos, agentes, claves de agente, comisiones
  Destino: Agent, AgentInsurerKey
  Cómo: ETL one-shot. Emails se normalizan a @mcbrokers.com.mx.
  ────────────────────────────────────────
  Origen: Catálogo de vehículos legado (55K marcas + 179K desc + 732K versiones)
  Destino: VehicleMaster + VehicleInsurerMapping
  Cómo: ETL con estrategia minero. Bulk insert a staging y luego match con confianza.
  ────────────────────────────────────────
  Origen: Pólizas históricas emitidas
  Destino: Emission (sólo metadata) + Blob (PDFs ya emitidos)
  Cómo: Migración incremental por año. No re-emitir, sólo archivar.
  ────────────────────────────────────────
  Origen: XMLs históricos req/res
  Destino: Azure Blob xml-archive/{year}/{month}/… con índice en BD para búsqueda
  Cómo: Subida masiva, con correlationId sintético si no existe.
  ────────────────────────────────────────
  Origen: Mapeos documentados: paquetes ↔ códigos aseguradora, formas de pago, coberturas
  Destino: InsurerPackageMapping, InsurerCoverageDefault, KnownInsurerError
  Cómo: Carga desde los .md ya generados en /Documentación — son fuente de verdad.
  ────────────────────────────────────────
  Origen: Catálogo CP/estados/municipios
  Destino: PostalCode, State, Municipality, InsurerStateCodeMapping
  Cómo: ETL con validación contra catálogos oficiales (SEPOMEX).

  4.2 Reescribir (no migrar lógica)

  Componente actual: CotizacionNegocio.cs (6.974 líneas)
  Decisión: Reescribir 100%. Se desintegra en: 5 adapters (uno por aseguradora) + casos de uso de Application + Domain Services. Ningún copy-paste.
  ────────────────────────────────────────
  Componente actual: Capa_Acceso_Datos
  Decisión: Reescribir. EF Core con repos por agregado. Los SPs como sp_get_versiones desaparecen (el catálogo nuevo no los necesita).
  ────────────────────────────────────────
  Componente actual: Construcción de XML/JSON por aseguradora
  Decisión: Reescribir desde los .md de documentación como contrato externo (no desde el código). Esto fuerza a entender el contrato y a tener tests basados en él.
  ────────────────────────────────────────
  Componente actual: Frontend Angular 8 (autos.component.*)
  Decisión: Reescribir en Razor Pages. Wizard de cotización + listado de resultados + emisión.
  ────────────────────────────────────────
  Componente actual: Manejo de errores (hasError/txtError)
  Decisión: Reescribir como ProblemDetails (RFC 7807) + ErrorCategory estructurada. Vendedor SIEMPRE ve causa y acción sugerida.
  ────────────────────────────────────────
  Componente actual: Persistencia de XMLs a disco local
  Decisión: Reescribir como IBlobStore con correlationId. Disco local deja de existir.
  ────────────────────────────────────────
  Componente actual: Logging con log4net
  Decisión: Reescribir con Serilog → Application Insights, JSON estructurado, correlation propagation.
  ────────────────────────────────────────
  Componente actual: Autenticación JWT con secret en appsettings.json
  Decisión: Reescribir con Google OAuth + cookies HttpOnly para Razor y JWT firmado/HMAC para móvil (con refresh tokens).
  ────────────────────────────────────────
  Componente actual: Servicio de correo (Gmail OAuth multicotizador@mcb.uno)
  Decisión: Reescribir con cuenta institucional + Microsoft Graph (recomendable si MCBrokers tiene M365) o SendGrid.

  4.3 Tomar como referencia (no migrar, no reescribir — usar como input)

  - Los 4 documentos .md en /Documentación/ (GNP, ANA, AXA, Quálitas) son el contrato externo. Sirven como especificación para los adapters nuevos. Mantenerlos actualizados.
  - README_multicotizadorminero.md es la estrategia maestra de catálogos. Implementar tal como está descrita.
  - MCB DB Model.pdf sólo como apoyo de comprensión histórica; el modelo nuevo (§2) lo reemplaza.

  4.4 Descartar

  - Toda la solución Arquitectura_duranm.sln (queda en read-only en repo legado).
  - TokenGmail.json, credentials.json, appsettings.json con secretos — rotar TODAS las credenciales antes/durante el rediseño.
  - Carpetas bin/, obj/, Templates.zip versionadas — limpieza al pasar al repo nuevo.

  ---
  5. App móvil — factibilidad y arquitectura

  5.1 Veredicto de factibilidad

  Viable y recomendable, pero NO antes de que Fases 0–5 estén productivas y estables. Razones:
  - Misma API REST → cero código duplicado en backend.
  - El esquema asíncrono (POST /quotations 202 + polling/SignalR) es además necesario para móvil; cualquier flujo síncrono se rompe en redes 4G donde una llamada de 60 s falla.
  - Catálogo unificado del año (modelo minero) encaja con el patrón móvil de "descargar el catálogo del año al iniciar y filtrar local".

  5.2 Stack móvil recomendado

  Opciones evaluadas:

  Opción: .NET MAUI (recomendado)
  Pros: Mismo lenguaje y modelos del backend; el equipo ya domina .NET; comparte Domain/Application via NuGet
  Contras: UI nativa-aceptable pero no excelente; menos talento disponible que Flutter
  ────────────────────────────────────────
  Opción: Flutter
  Pros: Excelente UX, hot reload, gran comunidad
  Contras: Dart nuevo para el equipo; sin compartir modelos con backend
  ────────────────────────────────────────
  Opción: React Native
  Pros: Talento abundante
  Contras: Otro stack para mantener
  ────────────────────────────────────────
  Opción: Nativo iOS + Android
  Pros: Mejor UX
  Contras: Doble esfuerzo, doble timeline

  Recomendación: .NET MAUI por alineación de stack. Permite reusar McBrokers.Domain y un cliente HTTP tipado generado desde el OpenAPI de la API. Estructura:

  McBrokers.Mobile.sln
  ├── McBrokers.Mobile.App/         # MAUI shell, vistas XAML, navegación
  ├── McBrokers.Mobile.Core/        # ViewModels, servicios, cache offline
  └── McBrokers.Mobile.ApiClient/   # Cliente HTTP generado de OpenAPI (NSwag/Refit)

  5.3 Endpoints adicionales (mobile-specific) vs los que ya usa el web

  La API es una sola con versión /api/v1/, pero hay endpoints que sólo el móvil necesita o que requieren variantes:

  Endpoint: POST /api/v1/auth/google/web
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: –
  Razón: Flujo web con cookie HttpOnly
  ────────────────────────────────────────
  Endpoint: POST /api/v1/auth/google/mobile (PKCE)
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Flujo nativo OAuth con PKCE, devuelve access + refresh token
  ────────────────────────────────────────
  Endpoint: POST /api/v1/auth/refresh
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Rotación de tokens (web usa cookie)
  ────────────────────────────────────────
  Endpoint: GET /api/v1/quotations / POST /api/v1/quotations
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Compartido
  ────────────────────────────────────────
  Endpoint: GET /api/v1/quotations/{id} (con If-None-Match/ETag)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Polling eficiente
  ────────────────────────────────────────
  Endpoint: WS /hubs/quotations (SignalR)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Móvil cae a polling si la red bloquea WS
  ────────────────────────────────────────
  Endpoint: GET /api/v1/catalog/sync?year=YYYY&since={etag}
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Nuevo: sincronización delta del catálogo maestro para uso offline. Devuelve sólo cambios desde since. Permite que la app filtre marca/modelo/versión sin red.
  ────────────────────────────────────────
  Endpoint: POST /api/v1/devices/register
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Nuevo: registra token FCM/APNs para push notifications (notificar resultado de cotización cuando lleguen todas las aseguradoras).
  ────────────────────────────────────────
  Endpoint: POST /api/v1/devices/unregister
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Logout/desinstalación.
  ────────────────────────────────────────
  Endpoint: GET /api/v1/agent/profile
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Datos del agente + claves de aseguradora.
  ────────────────────────────────────────
  Endpoint: GET /api/v1/emissions/{id}/pdf (devuelve SAS URL temporal de Blob)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Variante móvil: query ?as=download-url para que el móvil reciba URL firmada de 5 min y la abra con visor nativo.
  ────────────────────────────────────────
  Endpoint: POST /api/v1/emissions/{id}/share (correo al cliente)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón:
  ────────────────────────────────────────
  Endpoint: GET /api/v1/app/version-check?platform=ios|android&current=X.Y.Z
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Nuevo: devuelve {minSupported, latest, forceUpdate, releaseNotes}. Permite forzar actualización al haber cambios incompatibles.
  ────────────────────────────────────────
  Endpoint: POST /api/v1/diagnostics/crash
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Nuevo: recibe stack trace + correlation ID del móvil para Application Insights.
  ────────────────────────────────────────
  Endpoint: GET /api/v1/dictionaries/* (paquetes, formas de pago, valuaciones, estados, CPs)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: ✓
  Razón: Móvil los cachea agresivamente.
  ────────────────────────────────────────
  Endpoint: POST /api/v1/feedback
  ¿Web lo usa?: –
  ¿Móvil lo usa?: ✓
  Razón: Captura feedback de vendedor desde la app (canal directo para detectar errores que el log no captura).
  ────────────────────────────────────────
  Endpoint: Endpoints admin (/api/v1/admin/*)
  ¿Web lo usa?: ✓
  ¿Móvil lo usa?: –
  Razón: El móvil no expone panel admin; sólo cotización + emisión + consulta.

  Resumen: 6 endpoints nuevos puramente para móvil (auth/mobile, refresh, catalog/sync delta, devices/register, devices/unregister, app/version-check, diagnostics/crash) y 1 variante
  (emissions/{id}/pdf?as=download-url).

  5.4 Consideraciones operativas móvil

  - Tokens: access token 15 min, refresh token 30 días con rotación. Refresh tokens guardados en Keychain (iOS) / Keystore (Android).
  - Almacenamiento local: SQLite vía sqlite-net-pcl para catálogo offline; sólo metadatos no-sensibles (sin PII de clientes).
  - Push notifications: Firebase Cloud Messaging (Android) + Apple Push Notification service (iOS), orquestadas desde Azure Notification Hubs.
  - Modo offline: lectura de catálogo + borrador de cotización; envío diferido al volver conexión.
  - App Store / Play Store: publicación manual como definiste. Versionado semántico, App Center o GitHub Actions con fastlane para builds firmados — sin automatizar el upload.
  - Política de privacidad y términos: requeridos para ambas tiendas; vivirán en multicotizador.mcbrokers.com.mx/legal.
  - Cumplimiento: si la app maneja PII de clientes (RFC, fecha de nacimiento, domicilio), evaluar requisitos de LFPDPPP. Idealmente, la cotización móvil pide sólo CP + año/marca/versión y la captura de datos
  completos del cliente se hace al emitir (donde el flujo puede usar el web por seguridad si así se prefiere).
  - Versionado de API: el móvil siempre apunta a /api/v1/; cambios incompatibles → /api/v2/. Política de soporte: las dos últimas versiones mayores quedan vivas mientras haya apps en producción usándolas.

  5.5 Fase móvil — sub-plan

  - 7a (M): Skeleton MAUI + auth + lectura de catálogo + ver cotizaciones existentes (read-only).
  - 7b (M): Crear cotización + ver resultados (SignalR + polling).
  - 7c (M): Emisión + descarga PDF + compartir.
  - 7d (S): Push notifications + version-check + diagnostics.
  - 7e (S): Hardening, accesibilidad, pruebas con vendedores en campo, builds firmados para Stores.

  ---
  Resumen ejecutivo

  Bloque: Arquitectura
  Decisión clave: Clean Architecture con anti-corruption layer por aseguradora. Razor Pages y app móvil consumen exactamente la misma API REST. Cotización asíncrona vía colas + SignalR.
  ────────────────────────────────────────
  Bloque: Datos
  Decisión clave: Catálogo maestro homologado explícito (no Levenshtein). Quotation con N QuotationInsurerResult paralelos. KnownInsurerError administrable para mapear errores crudos a mensajes accionables.
  ────────────────────────────────────────
  Bloque: Fases
  Decisión clave: 0 Cimientos → 1 Admin → 2 Catálogo (crítica) → 3 Primera aseguradora → 4 Resto → 5 Emisión → 6 Errores/Obs → 7 Móvil → 8 Retiro.
  ────────────────────────────────────────
  Bloque: Migración
  Decisión clave: Migrar datos y configuración; reescribir 100% de la lógica. Documentos .md por aseguradora son la especificación.
  ────────────────────────────────────────
  Bloque: Móvil
  Decisión clave: MAUI sobre la misma API. 7 endpoints adicionales específicos (auth/mobile, refresh, catalog/sync, devices register/unregister, version-check, diagnostics) + 1 variante (PDF como SAS URL).
  ────────────────────────────────────────
  Bloque: Riesgos a vigilar
  Decisión clave: Calidad del catálogo legado (Fase 2), red Azure→aseguradoras (Fase 0), dependencia COPSIS para AXA DXN (Fase 4), rotación de credenciales del sistema actual antes de iniciar.

  Plan listo para refinar. Cuando indiques, paso a la siguiente etapa (diseño detallado de una fase, contratos OpenAPI, o el modelo SQL en DDL).

✻ Brewed for 3m 32s

※ recap: Diagnostiqué el multicotizador MCBrokers actual (.NET 5/Angular 8/MySQL) y entregué el plan de rediseño en .NET 10 + Razor + SQL Azure con Clean Architecture, catálogo homologado y app móvil MAUI.
  Siguiente paso: indicar qué fase profundizar (diseño detallado, OpenAPI o DDL del modelo SQL). (disable recaps in /config)
