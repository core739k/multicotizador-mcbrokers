# AXA DXN — importador de catálogo: pausado

**Estado**: pausado a la espera de documentación técnica de AXA.
**Fecha**: 2026-05-18.

## Qué está construido

Todo el pipeline funciona y los tests pasan (49 tests propios verde).

| Capa | Componente | Path |
|---|---|---|
| Domain/Mapping | `AxaDxnCatalogSoapBuilder` (RPC/encoded, Axis 1.x) | `src/McBrokers.Insurers.AxaDxn/Mapping/Catalog/` |
| Domain/Mapping | `AxaDxnCatalogResponseParser` (doble parse + entities + SOAP Fault) | `src/McBrokers.Insurers.AxaDxn/Mapping/Catalog/` |
| Application | `IAxaDxnCatalogClient`, `AxaDxnCatalogSettings`, `RunAxaDxnCatalogImport` | `src/McBrokers.Application/Catalog/Importers/` |
| Infrastructure | `AxaDxnCatalogHttpClient` (Basic Auth, response body logging, 120s timeout) | `src/McBrokers.Infrastructure/InsurerCatalogs/` |
| Web | `/Admin/Catalog/Import` (Razor + overlay JS) | `src/McBrokers.Web/Pages/Admin/Catalog/` |
| DI | `RunAxaDxnCatalogImport` + `IAxaDxnCatalogClient` + `IImportInsurerCatalog` + `AxaDxnCatalogSettings` | `src/McBrokers.Infrastructure/DependencyInjection.cs` |

El flujo en runtime:

1. POST `/Admin/Catalog/Import` (handler `OnPostAxaDxn`) → invoca `RunAxaDxnCatalogImport.ExecuteAsync(insurerId)`.
2. Orquestador dispara 4 llamadas SOAP a `https://serviciosweb.axa.com.mx:9104/EmisionPolizasWS/services/SolicitudPolizasService` — `Marca`+`Submarca` por cada `Tarifa` y `TarifaPickup`.
3. Filtra `idTipoVehiculo ∈ {22, 3, 81, 7, 24}`, expande filas a `[currentYear-1, currentYear]`, delega a `ImportInsurerCatalog` con `IsSourceOfTruth=true`.

## El bloqueo

El WS responde **HTTP 500 SOAP Fault** al instante:

```xml
<faultcode>soapenv:Server.generalException</faultcode>
<faultstring>LA TARIFA NO EXISTE PARA EL USUARIO PROPORCIONADO</faultstring>
```

Combinación que estamos enviando (idéntica a la que el legacy usa hoy en producción contra el WS de cotización):

| Campo | Valor | Origen |
|---|---|---|
| Endpoint | `…/EmisionPolizasWS/services/SolicitudPolizasService` | hardcoded en `RunAxaDxnCatalogImport.FallbackEndpoint` |
| Usuario | `MXS00102308A` | `AxaDxnConfig.Usuario` |
| Tarifa | `TB7144` (autos) / `TB7204` (pickup) | `AxaDxnConfig.Tarifa` / `.TarifaPickup` |
| SOAP shape | RPC/encoded (Axis 1.x): `xmlns:xsi/xsd` + `soapenv:encodingStyle` + `xsi:type="xsd:string"` | `AxaDxnCatalogSoapBuilder` |

Lo descartado en el diagnóstico:

- **No es la forma del SOAP**: Axis deserializó el body sin quejarse de la forma — el fault es de aplicación (`Server.generalException`, no `Client`).
- **No es el endpoint**: corregido en commit `e343966` (antes apuntaba a `FlotillasService` por leer `InsurerConfig.EndpointUrl`); ahora apunta al WS correcto del catálogo.
- **No es la tarifa**: query directa a la BD MySQL legacy (`tbl_conf_aseg_axa`) confirmó que `TB7144`/`TB7204` son las tarifas que el legacy usa hoy y funciona contra el WS de cotización.
- **No es el password**: aunque conviene revalidar — el password del legacy es `bepren6cl&R%taswaw#L` (20 chars). Si el de SQL Server difiere, podría ser causa, pero si el password estuviera mal AXA respondería 401, no un fault de "tarifa no existe".

## Hipótesis principal

El usuario `MXS00102308A` con tarifa `TB7144` tiene permisos sobre `FlotillasService` (cotización) pero **no** sobre `SolicitudPolizasService` (catálogo + emisión). Cada WS de Axis 1.x mantiene su propia lista de "tarifas autorizadas por usuario".

## Qué falta para reanudar

Confirmar con AXA cualquiera de:

1. ¿Es necesaria una cuenta diferente (`Usuario`/`Password`) para `getCatalogosPorTarifaYNombre`?
2. ¿O hay que solicitar a AXA habilitar la tarifa `TB7144`/`TB7204` para `MXS00102308A` en `SolicitudPolizasService`?
3. ¿O AXA expone un endpoint distinto al de la emisión para consultar catálogos?

Hasta tener respuesta, el botón **Importar catálogo (AXA DXN)** en `/Admin/Catalog/Import` queda visible pero no se debe disparar (no rompe nada — solo escribe un batch fallido en `CatalogImportBatch`).

## Para reanudar el debug

Logs persistentes en `logs/web-AAAAMMDD.log` (file sink Serilog desde `8e8a5f5`). Buscar:

- `AXA DXN CATALOG REQUEST tarifa=…` — el body completo enviado.
- `AXA DXN catálogo respondió HTTP 500 … Body:` — el SOAP Fault de AXA (truncado a 2 KB).

Logging del request body es a nivel `Information` (debería bajarse a `Debug` o quitarse cuando el flujo esté estable).

## Commits del módulo (en orden)

| Commit | Qué |
|---|---|
| `f6fb5af` | Parser SOAP (TDD) |
| `52a6941` | Builder SOAP envelope (TDD) |
| `b3d7d68` | Orquestador con 4 llamadas + filtros + expansión por año (TDD) |
| `0a79a08` | Cliente HTTP/SOAP en Infrastructure (TDD) |
| `b8a63b2` | Página `/Admin/Catalog/Import` |
| `b1e4aa5` | DI wire-up |
| `e343966` | Fix endpoint (era el de cotización, no catálogo) |
| `feb317d` | Fix SOAP RPC/encoded (xsi/xsd + encodingStyle + xsi:type) |
| `abf7e66` | Quitar `beforeunload` del overlay |
| `e8491cd` | Loggear request body para diagnóstico |
| `8e8a5f5` | File sink Serilog → `logs/{app}-{date}.log` |
