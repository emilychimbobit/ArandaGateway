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
dotnet user-secrets set "Aranda:BaseUrl" "https://HOST/ASMSAPI/" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Aranda:ApiKey" "Bearer API_KEY" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Aranda:SubscriptionKey" "SUBSCRIPTION_KEY" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet user-secrets set "Aranda:AuthCookie" "AuthCookieASMS=VALOR" --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
```

La salida hacia Aranda necesita **tres** credenciales, no una. Faltando
cualquiera la respuesta es `502` con `errorCode` `ARANDA_401`:

| Secreto | Encabezado | Quién lo exige |
| --- | --- | --- |
| `Aranda:ApiKey` | `X-Authorization` | Aranda |
| `Aranda:SubscriptionKey` | `Ocp-Apim-Subscription-Key` | Azure API Management |
| `Aranda:AuthCookie` | `Cookie` | Aranda (sesión `AuthCookieASMS`) |

`SubscriptionKey` solo hace falta cuando `Aranda:BaseUrl` apunta a APIM
(`https://apim-servicios.azure-api.net/fcintgestionaranda/v1`) y no directo a
Aranda; sin ella APIM responde 401 antes de enrutar.

`AuthCookie` es una **cookie de sesión y caduca**. Aranda devuelve 401 con la
página de IIS "You do not have permission to view this directory or page."
cuando falta o venció, aunque el token de `ApiKey` siga vigente. No se versiona
en el repositorio.

#### Cómo se mantiene viva la sesión

Aranda usa expiración deslizante: devuelve un `Set-Cookie` renovado en cada
respuesta y lo que mata la sesión es la **inactividad**, no el tiempo
transcurrido. Se midió una sesión muerta tras unos 10 minutos sin tráfico.

El gateway hace dos cosas para que no caduque:

- `ArandaSessionCookieHandler` guarda la cookie de cada respuesta y la usa en
  la siguiente. `Aranda:AuthCookie` es solo la **semilla** del primer request.
- `ArandaSessionKeepAliveService` consulta Aranda cada
  `Aranda:SessionKeepAliveMinutes` minutos (5 por omisión, `0` desactiva) para
  reiniciar el contador aunque no haya tráfico de usuarios.

#### Renovar la sesión sin reiniciar (`/admin/aranda-session`)

La cookie caduca en pocos minutos y un despliegue completo tarda más que eso,
así que renovarla por configuración obliga a coordinar el cambio en una ventana
muy corta. Para soporte hay una ruta que la instala en caliente:

```bash
curl -X PUT "https://HOST/admin/aranda-session" \
  -H "Content-Type: application/json" \
  -d '{"cookie":"AuthCookieASMS=VALOR"}'

curl "https://HOST/admin/aranda-session"
```

El `PUT` reemplaza la sesión en memoria y el `GET` informa si hay sesión y
cuándo se renovó, sin devolver nunca el valor de la cookie. Tras instalarla, el
latido la mantiene viva mientras el proceso siga arriba.

Esto no reemplaza a `Aranda:AuthCookie`, que sigue siendo la semilla del
arranque; evita el redespliegue cuando la sesión muere en caliente.

**Riesgo asumido.** Estas rutas son anónimas, igual que el resto de la gateway,
y el App Service responde desde internet: se comprobó llamando a
`https://ase-gestionaranda-dev.azurewebsites.net/health` sin pasar por APIM. En
consecuencia, cualquiera que conozca la URL puede instalar la cookie con la que
la gateway opera contra Aranda, o dejarla inoperativa enviando una inválida. Es
distinto del resto de los endpoints anónimos, que solo leen con una credencial
fija: esta ruta **cambia con qué credencial actúa el servicio**.

La mitigación pendiente es restringir `/admin/*` por IP con las reglas de acceso
del App Service, o reponer una clave de autorización. Ver
[docs/deuda-tecnica.md](docs/deuda-tecnica.md).

#### Reintentos y el desafío de Cloudflare

Aranda está detrás de Cloudflare, que de forma intermitente responde `403` con
`Cf-Mitigated: challenge` incluso a peticiones idénticas que funcionaron un
momento antes. APIM no lo evita: reenvía los encabezados del cliente al
backend, así que Cloudflare sigue evaluando la petición. Se observó el desafío
con el `User-Agent` de curl y también, esporádicamente, con el del gateway.

`ArandaRetryHandler` lo absorbe con hasta tres intentos (esperas de 500 ms y
1500 ms). Qué se reintenta depende de la operación, para no crear duplicados:

| Operación | Se reintenta ante |
| --- | --- |
| Consultas (usuario, ticket, búsqueda, CMDB) | Desafío, `429`, `5xx` y fallos de red |
| Crear, actualizar, adjuntar | Solo desafío y `429` |

La diferencia importa: el desafío y el `429` se resuelven **en el borde**, sin
llegar a Aranda, así que repetir una creación no duplica nada. Un `5xx` o un
tiempo de espera agotado pudieron ejecutarse en Aranda, de modo que ahí las
escrituras no se repiten y el error se propaga.

El reintento consume el tiempo de `Aranda:TimeoutSeconds`, que acota el total.

Lo definitivo para el desafío está del lado de la plataforma, no del gateway:
que la política de APIM normalice el `User-Agent` hacia el backend y que se
excluya del desafío la ruta `/ASMSAPI/api/v9/*` para el origen de APIM.

#### Resumen

Con lo anterior la sesión no caduca mientras el gateway esté arriba. Sigue haciendo
falta una cookie **válida** en dos casos: al arrancar el proceso, y tras una
parada larga. Renovarla es manual: sacarla de una petición autenticada (por
ejemplo desde Postman) y actualizar el secreto. La solución definitiva es que
el gateway inicie sesión por su cuenta, y eso requiere que publiquen la
operación de autenticación de Aranda en APIM, hoy ausente del spec.

La gateway no valida credenciales de entrada: sus endpoints son anónimos y el
control de acceso queda delegado a APIM y a la red del App Service.

`Aranda:ApiKey` debe contener el valor completo enviado en
`X-Authorization`, incluido `Bearer`.

Los IDs no sensibles se configuran en la sección `Aranda` de
`appsettings.json`. `ProjectId = 1` y `AuthorId = 2` están confirmados.
Creación y anulación responderán `503` mientras falten los IDs de catálogo,
modelo y estados marcados con `null`.

Los catálogos del requerimiento fijo se validaron contra Aranda el 1 de
septiembre de 2026: servicio `Por categorizar` (`51`), categoría
`Ticket creado por bot` (`988`), impacto `BAJO` (`1`), urgencia `BAJO` (`4`),
grupo `Mesa de Ayuda` (`2`), modelo (`17`), estado inicial `Registrado` (`59`)
y estado de cancelación `Cancelado` (`61`). Este servicio solo está disponible
para requerimientos (`itemType = 4`), no para incidentes (`itemType = 1`).

La creación exige `RegistryTypeId` y `UnitId`. Para QA se configuraron
provisionalmente `Correo` (`4608`) y `Minsur Lima` (`5875`), validados mediante
la prueba controlada `RF-56025`. Estos valores deben confirmarse con el equipo
de Aranda antes de promover la solución a producción. En una actualización,
Aranda exige `RegistryTypeId` y acepta `UnitId = 0` para conservar la sede
existente.

#### Parche temporal: usuario fijo (`Aranda:UserOverride`)

La API de usuarios de Aranda todavía no es accesible desde el gateway, así que
la resolución del colaborador está reemplazada por un usuario fijo por dominio,
configurado en la sección `Aranda:UserOverride` de `appsettings.json`:

| Dominio | Id | Usuario |
| --- | --- | --- |
| Equipos (CMDB) | `1562` | `evelyn.nunez@minsur.com` |
| Tickets | `15019` | `uebit20@minsur.com` |

Mientras `Aranda:UserOverride:Enabled` sea `true`, el valor de
`X-Collaborator-Username` se registra en el log pero se ignora para resolver al
usuario, y no se llama a `GET /users`. La verificación de propiedad de tickets
se hace contra el usuario resuelto, por lo que sigue siendo coherente con el
usuario fijo.

Para volver a la validación real basta con poner
`Aranda:UserOverride:Enabled` en `false` (o eliminar la sección completa); no
hay que tocar código. Al retirar el parche de forma definitiva se borran
`ArandaUserOverrideOptions.cs`, la propiedad `ArandaOptions.UserOverride`, los
bloques marcados con `PARCHE TEMPORAL` en `EquipoService` y `TicketService`, y
sus pruebas asociadas.

### Ejecución y pruebas

```powershell
dotnet run --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj
dotnet test .\ArandaGateway.slnx
```

Swagger UI está disponible en `http://localhost:5112/swagger`. Mientras se
define SSO, las operaciones reciben el username en `X-Collaborator-Username`
que es su usuario o correo.

Los contratos y reglas pueden probarse localmente. La validación end-to-end
contra Aranda real está hecha: `GET /api/equipos`, `GET /api/tickets` y
`GET /api/tickets/{caseNumber}` responden `200`, y `POST /api/tickets` responde
`201`. La anulación está bloqueada en APIM (ver deuda técnica). El desafío de
Cloudflare ya no bloquea la operación: se reintenta.

### Deuda técnica

Las soluciones temporales vigentes y lo que hace falta para retirarlas están en
[docs/deuda-tecnica.md](docs/deuda-tecnica.md): la cookie de sesión manual, la
creación de tickets bloqueada por la Sede, la API de usuarios ausente en APIM,
el usuario fijo por dominio y el desafío de Cloudflare. Ninguno se resuelve con
código del gateway: dependen de datos de catálogo de Aranda o de que se
publiquen operaciones en APIM.

Cuatro de las cinco operaciones están validadas contra Aranda real: las tres
consultas y la creación, que devolvió `201` con el caso RF-58501 el 11 de
septiembre de 2026. **La anulación devuelve `502` con `ARANDA_404`**: APIM no
publica `PUT /api/v9/item/{id}`, aunque la operación funciona llamando directo
a Aranda.
