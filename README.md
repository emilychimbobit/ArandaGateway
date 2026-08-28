# BIT-MINSUR-ARANDA-02

BIT Repository

## Estructura del Repositorio

| Carpeta | Propósito |
|---------|-----------|
| docs/ | Documentación, diagramas y manuales |
| src/frontend/ | Código frontend (web, PowerApps, Teams) |
| src/backend/ | APIs y servicios backend |
| src/ia/ | Agentes, funciones y modelos de IA |
| src/integration/ | Conectores y integración de datos |
| data/ | Scripts y esquemas de bases de datos |
| tests/ | Pruebas de QA y validación |

## Ramas

- `main` - Producción (protegida)
- `develop` - Desarrollo e integración
- `qa` - Pruebas de calidad

## Aranda Gateway API

La gateway se implementa como una Minimal API con .NET 10 en
`src/backend/api/ArandaGateway.Api`.

### Configuración local

Los secretos se administran con .NET User Secrets:

```powershell
dotnet user-secrets init --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Gateway:ApiKey" "API_KEY_EXCLUSIVA_DE_LA_GATEWAY" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Aranda:BaseUrl" "https://HOST/ASMSAPI/" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Aranda:ApiKey" "Bearer API_KEY" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
```

`Gateway:ApiKey` es la credencial independiente que APIM envía a la gateway
en `X-Api-Key`.

`Aranda:ApiKey` debe contener el valor completo enviado en
`X-Authorization`, incluido `Bearer`.

Los IDs no sensibles se configuran en la sección `Aranda` de
`appsettings.json`. `ProjectId = 1` y `AuthorId = 2` están confirmados.
Creación y anulación responderán `503` mientras falten los IDs de catálogo,
modelo y estados marcados con `null`.

### Ejecución y pruebas

```powershell
dotnet run --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet test .\ArandaGateway.slnx
```

Swagger UI está disponible en `http://localhost:5112/swagger`. Use
**Authorize** para configurar `X-Api-Key`. Mientras se define SSO, las
operaciones reciben el username en `X-Collaborator-Username`.

Los contratos y reglas pueden probarse localmente. La validación end-to-end
contra Aranda permanece pendiente hasta que Cloudflare permita solicitudes
server-to-server desde la gateway.
