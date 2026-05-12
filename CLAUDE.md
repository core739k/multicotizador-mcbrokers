# CLAUDE.md — Multicotizador MCBrokers (rediseño)

Instrucciones locales para Claude Code al trabajar en este proyecto.
Leer junto con `REQUIREMENTS.md` (alcance completo y modelo de datos) **antes** de cualquier acción.

---

## Alcance actual

**Solo aplicación web. Fases 0–6 del plan.**

La app móvil (Fase 7) está fuera del alcance hasta que el usuario lo apruebe explícitamente, una vez la web esté estable en producción. **No mencionar ni planificar móvil hasta entonces.**

---

## Orden de implementación (acordado)

1. **Fase 0** — Cimientos (esqueleto vivo, CI/CD, Azure, OAuth).
2. **Fase 1** — Panel admin mínimo (Razor).
3. **Fase 2** — Catálogo maestro homologado (crítica).
4. **Fase 3** — Primera aseguradora: **GNP**.
5. **Fase 4** — Resto en este orden: Quálitas → ANA → AXA COL → AXA DXN.
6. **Fase 5** — Emisión, PDF, archivo.
7. **Fase 6** — Manejo de errores estructurado y observabilidad.

---

## Disciplina de desarrollo

- **TDD estricto.** Ninguna lógica de negocio sin test fallido previo. Red → Green → Refactor.
- Los proyectos arrancan **vacíos** (sin `Class1.cs` ni `UnitTest1.cs`). Cada archivo nace por necesidad.
- Antes de crear cualquier clase, escribir el test que la motiva.
- Commits convencionales: `feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`.
- Un commit por tarea lógica completada.

---

## Stack y herramientas

- .NET 10 (SDK 10.0.202 — fijado en `global.json`)
- Razor Pages + ASP.NET Core Minimal APIs
- SQL Server Azure (`prod_macooley`)
- Azure Blob Storage (xml-requests, xml-responses, pdf-policies, imports)
- Azure Key Vault (secretos)
- Google OAuth (hd=mcbrokers.com.mx)
- Application Insights + Serilog
- xUnit + (FluentAssertions / Moq cuando se agreguen)

---

## Estructura

```
src/
  McBrokers.SharedKernel/         Result<T>, OperationOutcome, tipos comunes
  McBrokers.Domain/               Entidades y reglas puras
  McBrokers.Application/          Casos de uso + puertos
  McBrokers.Infrastructure/       EF Core, Blob, Key Vault, Identity
  McBrokers.Insurers.Abstractions/ IInsurerAdapter + contratos
  McBrokers.Insurers.Gnp/         Adapter GNP (Fase 3 — primero)
  McBrokers.Insurers.Qualitas/    Adapter Quálitas (Fase 4)
  McBrokers.Insurers.Ana/         Adapter ANA (Fase 4)
  McBrokers.Insurers.AxaDxn/      Adapter AXA DXN (Fase 4 — último, COPSIS)
  McBrokers.Insurers.AxaCol/      Adapter AXA COL (Fase 4)
  McBrokers.Api/                  Endpoints REST (consumidos por Web)
  McBrokers.Web/                  Razor Pages (admin + cotización)

tests/
  McBrokers.Domain.Tests/
  McBrokers.Application.Tests/
  McBrokers.Insurers.{X}.Tests/   Contract tests con XMLs grabados
  McBrokers.Api.Tests/            WebApplicationFactory + Testcontainers
  McBrokers.E2E.Tests/            Smoke en staging
```

**Regla de dependencias** (verificada por compilación):
- Domain → SharedKernel.
- Application → Domain, SharedKernel, Insurers.Abstractions.
- Insurers.Abstractions → Domain, SharedKernel.
- Insurers.{Gnp,Qualitas,Ana,AxaDxn,AxaCol} → Insurers.Abstractions, SharedKernel.
- Infrastructure → Application, Domain, SharedKernel.
- Api → Application, Infrastructure, todos los Insurers.*, SharedKernel.
- **Web → SharedKernel** únicamente. Consume Api por HTTP (mismo contrato que tendría móvil).

**Worker de cotización asíncrona:** por ahora dentro de `McBrokers.Api` como `IHostedService`. Se extrae a proyecto separado solo cuando la escala lo justifique.

---

## Comandos frecuentes

> Solución gestionada con formato `.slnx` (XML, nuevo en .NET 10).

```bash
# Build completo
dotnet build McBrokers.Multicotizador.slnx

# Tests (todos)
dotnet test McBrokers.Multicotizador.slnx

# Tests de un proyecto
dotnet test tests/McBrokers.Domain.Tests

# Watch tests (Red→Green→Refactor cómodo)
dotnet watch test --project tests/McBrokers.Domain.Tests

# Correr Api en local
dotnet run --project src/McBrokers.Api

# Correr Web en local
dotnet run --project src/McBrokers.Web

# Crear migración EF Core (cuando arranque Fase 0)
# dotnet ef migrations add <NombreMigracion> --project src/McBrokers.Infrastructure --startup-project src/McBrokers.Api

# Formato
dotnet format McBrokers.Multicotizador.slnx
```

---

## Configuración central

- `global.json` — pin del SDK .NET 10.
- `Directory.Build.props` — Nullable, ImplicitUsings, LangVersion=latest, TreatWarningsAsErrors=true, AnalysisLevel=latest.
- `Directory.Packages.props` — Central Package Management. Agregar versiones aquí, **nunca** en csproj individuales.
- `.editorconfig` — file-scoped namespaces, var preferido, async methods terminan en `Async`.

---

## Decisiones arquitectónicas no obvias

- **Admin Razor Pages usan Application Layer directamente.** REQUIREMENTS.md §3.2 dice que Razor consume la Api por HTTP, pero la razón es que móvil comparta el contrato — y móvil **no expone admin** (§7.3). Aplicar el indirección API → Application para admin es plumbing sin beneficio. Las páginas `/Admin/*` resuelven `CreateInsurer`, `ListAgents`, etc. vía DI. Para cotización/emisión (F3+), donde móvil sí participa, Razor llamará a Api por HttpClient.
- **Api también expone `/api/v1/admin/*`** con la misma policy `RequireAdmin`. Si en el futuro se necesita exponer admin a otro cliente, ya está. La Web no lo consume.
- **Worker de cotización asíncrona vive dentro de `McBrokers.Api`** como `IHostedService` por ahora — se extrae a proyecto separado solo cuando la escala lo justifique.

---

## Restricciones / decisiones

- **Sin secretos en repo.** Todo a Key Vault. `appsettings.*.Development.json` queda gitignored si trae cualquier secreto.
- **Homologación explícita** del catálogo (offline, persistida). No Levenshtein en runtime.
- **Cotización asíncrona obligatoria** (POST 202 + worker + polling/SignalR).
- **Razor consume Api por HTTP**, no por referencia de proyecto.
- **No Docker** por ahora. Deploy directo a Azure App Service.
- **Auditoría inmutable** de cambios admin en `AuditLog`.
- **CorrelationId** propagado en todo: header, logs, blobs, BD.
- **AXA COL deshabilitada por negocio.** MCBrokers solo opera AXA DXN. El adapter `McBrokers.Insurers.AxaCol` se mantiene compilando, pero `InsurersSeed` lo siembra con `IsEnabled=false` y el admin no debería habilitarlo sin instrucción explícita del negocio. REQUIREMENTS.md lista las 5 aseguradoras por completitud; el set operativo real es 4.

---

## Cuando algo no calza con lo anterior

Pausar y consultar antes de inventar. Las decisiones de arquitectura ya tomadas en `REQUIREMENTS.md` no se replantean sin pedirlo.
