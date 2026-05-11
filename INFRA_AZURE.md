# Provisioning de infraestructura — acciones del usuario

Lo que **Claude no puede hacer** desde código en este repo. Son acciones que el usuario debe ejecutar (portal de Azure, gcloud / Google Cloud Console, o Azure CLI) para que la aplicación funcione end-to-end.

Marcar como hechas con `[x]` a medida que se completan.

> Convención: el grupo de recursos se llama `rg-mcbrokers-multicotizador`, en región `Mexico Central` (`mexicocentral`). Ajustar si MCBrokers tiene política distinta.

---

## 1. Google Cloud — Cliente OAuth 2.0

- [ ] Crear proyecto en Google Cloud Console: **MCBrokers Multicotizador**.
- [ ] Habilitar **People API** (mínimo necesario para email + nombre).
- [ ] Configurar **OAuth consent screen** como tipo **Internal** (restringe a la Workspace de mcbrokers.com.mx). Si no aparece "Internal", confirmar que la Workspace está enlazada al proyecto.
- [ ] Crear **OAuth 2.0 Client ID** tipo **Web application**.
  - URIs de redirección autorizadas:
    - `https://multicotizador.azurewebsites.net/signin-google`
    - `https://multicotizador-staging.azurewebsites.net/signin-google`
    - `https://localhost:5001/signin-google` (desarrollo local)
- [ ] Guardar **Client ID** y **Client Secret**. Irán a Key Vault, **no** al repo.

---

## 2. Azure — Grupo de recursos

```powershell
az group create `
  --name rg-mcbrokers-multicotizador `
  --location mexicocentral
```

---

## 3. Azure — SQL Server + base `prod_macooley`

- [ ] Crear **Azure SQL Server** (logical server).
- [ ] Configurar firewall: permitir Azure services + IP del equipo dev.
- [ ] Crear base de datos `prod_macooley` (tier inicial sugerido: **GP_S_Gen5_2** serverless, autopause 60 min).
- [ ] Crear segunda base `staging_macooley` para slot staging.
- [ ] Guardar **connection string** en Key Vault (no en appsettings).

```powershell
$SqlAdminPwd = Read-Host -AsSecureString "SQL admin password"

az sql server create `
  --name sql-mcbrokers-multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --location mexicocentral `
  --admin-user mcbadmin `
  --admin-password $($SqlAdminPwd | ConvertFrom-SecureString -AsPlainText)

az sql server firewall-rule create `
  --resource-group rg-mcbrokers-multicotizador `
  --server sql-mcbrokers-multicotizador `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0

az sql db create `
  --resource-group rg-mcbrokers-multicotizador `
  --server sql-mcbrokers-multicotizador `
  --name prod_macooley `
  --edition GeneralPurpose `
  --family Gen5 `
  --capacity 2 `
  --compute-model Serverless `
  --auto-pause-delay 60
```

---

## 4. Azure — Storage Account + contenedores Blob

- [ ] Crear **Storage Account** estándar.
- [ ] Crear contenedores privados:
  - `xml-requests` — cuerpos XML enviados a aseguradoras.
  - `xml-responses` — cuerpos XML recibidos.
  - `pdf-policies` — PDFs de pólizas emitidas.
  - `imports` — archivos para ETL de catálogo legado.
- [ ] Habilitar **versioning** y **soft delete (14 días)** en blobs.
- [ ] Guardar la connection string en Key Vault.

```powershell
az storage account create `
  --name stmcbmulticotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --location mexicocentral `
  --sku Standard_LRS `
  --kind StorageV2 `
  --allow-blob-public-access false

foreach ($c in @('xml-requests','xml-responses','pdf-policies','imports')) {
  az storage container create `
    --account-name stmcbmulticotizador `
    --name $c `
    --auth-mode login
}
```

---

## 5. Azure — Key Vault

- [ ] Crear Key Vault: `kv-mcbrokers-multicotizador`.
- [ ] Asignar **Key Vault Secrets User** a la identidad managed del App Service (paso 6).
- [ ] Cargar los secretos:

| Secreto | Origen |
|---|---|
| `ConnectionStrings--Default` | Connection string a `prod_macooley` |
| `Authentication--Google--ClientId` | Google OAuth Client ID |
| `Authentication--Google--ClientSecret` | Google OAuth Client Secret |
| `Storage--ConnectionString` | Storage account connection string |
| `Insurers--Gnp--ApiUser` | (Fase 3) usuario API GNP |
| `Insurers--Gnp--ApiPassword` | (Fase 3) password API GNP |

```powershell
az keyvault create `
  --name kv-mcbrokers-multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --location mexicocentral `
  --enable-rbac-authorization true

az keyvault secret set `
  --vault-name kv-mcbrokers-multicotizador `
  --name "ConnectionStrings--Default" `
  --value "Server=tcp:sql-mcbrokers-multicotizador.database.windows.net,1433;Database=prod_macooley;Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True;"
```

---

## 6. Azure — App Service + slot staging

- [ ] App Service Plan **B1** (o superior). Linux/Windows: **Windows** (.NET 10 LTS-equivalent runtime).
- [ ] Web App `multicotizador`.
- [ ] Slot deployment `staging` (`multicotizador-staging`).
- [ ] Habilitar **Managed Identity** (system-assigned) en ambos.
- [ ] Configurar **Key Vault references** en App Settings:
  - `ConnectionStrings__Default` → `@Microsoft.KeyVault(SecretUri=...)`
  - `Authentication__Google__ClientId` → `@Microsoft.KeyVault(...)`
  - `Authentication__Google__ClientSecret` → `@Microsoft.KeyVault(...)`
- [ ] Configurar Application Insights connection string (paso 7) en App Setting `APPLICATIONINSIGHTS_CONNECTION_STRING`.

```powershell
az appservice plan create `
  --name asp-mcbrokers-multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --sku B1

az webapp create `
  --name multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --plan asp-mcbrokers-multicotizador `
  --runtime "DOTNET:10.0"

az webapp deployment slot create `
  --name multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --slot staging

az webapp identity assign `
  --name multicotizador `
  --resource-group rg-mcbrokers-multicotizador

az webapp identity assign `
  --name multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --slot staging
```

---

## 7. Azure — Application Insights

- [ ] Crear recurso Application Insights basado en Workspace.
- [ ] Conectar al App Service.
- [ ] Verificar que `/health/live` y `/health/ready` aparezcan en availability tests (opcional, configurar a futuro).

```powershell
az monitor app-insights component create `
  --app ai-mcbrokers-multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --location mexicocentral `
  --kind web `
  --workspace "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.OperationalInsights/workspaces/<workspace>"
```

---

## 8. Red — Whitelist de IP en aseguradoras

**Acción manual con cada proveedor.** Cada aseguradora típicamente requiere IP outbound conocida.

- [ ] Asignar **NAT Gateway** o **outbound IPs estáticas** al App Service.
- [ ] Solicitar whitelisting de esa IP a:
  - GNP (Fase 3)
  - Quálitas (Fase 4)
  - ANA (Fase 4)
  - AXA DXN (Fase 4 — además incluye COPSIS)
  - AXA COL (Fase 4)
- [ ] Documentar IP autorizada por aseguradora en `Documentación/INFRA_NETWORK.md` (a crear).

```powershell
# Ver outbound IPs actuales del App Service
az webapp show `
  --name multicotizador `
  --resource-group rg-mcbrokers-multicotizador `
  --query outboundIpAddresses
```

---

## 9. Pipeline de despliegue (GitHub Actions)

- [ ] Crear **Service Principal** o **Federated Credential (OIDC)** con permiso de despliegue al Resource Group.
- [ ] Guardar credenciales como secret en el repo: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (o `AZURE_CREDENTIALS` JSON).
- [ ] El workflow de deploy (a crear en F1) hace: build → publish → push a slot `staging` → smoke test → swap.

---

## 10. Verificación local antes de F1

- [ ] Variable de entorno `MCBROKERS_DB_CONNECTION` apuntando a LocalDB o a `prod_macooley` (solo en máquina del dev):
  ```powershell
  $env:MCBROKERS_DB_CONNECTION = "Server=(localdb)\mssqllocaldb;Database=McBrokers.Multicotizador;Trusted_Connection=True;"
  ```
- [ ] Ejecutar la migración inicial localmente:
  ```powershell
  dotnet ef database update --project src/McBrokers.Infrastructure --startup-project src/McBrokers.Api
  ```
- [ ] Confirmar que aparece la tabla `Agents` con el índice `UX_Agents_Email` filtrado por `LIKE '%@mcbrokers.com.mx'`.

---

## Lo que NO va aquí

- **Rotación de credenciales del sistema actual** — se hace antes de apagar el legado (ver REQUIREMENTS.md §6.4).
- **Migración de datos productivos** — Fase 8.
