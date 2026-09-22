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

La salida hacia Aranda necesita **dos** credenciales:

| Secreto | Encabezado | Quién lo exige |
| --- | --- | --- |
| `Aranda:ApiKey` | `X-Authorization` | Aranda |
| `Aranda:SubscriptionKey` | `Ocp-Apim-Subscription-Key` | Azure API Management |
| `Aranda:AuthCookie` | `Cookie` | opcional, ver abajo |

`SubscriptionKey` solo hace falta cuando `Aranda:BaseUrl` apunta a APIM
(`https://apim-servicios.azure-api.net/fcintgestionaranda/v1`) y no directo a
Aranda; sin ella APIM responde 401 antes de enrutar.

`AuthCookie` **ya no es necesaria**. El 15 de septiembre de 2026 se comprobó que
Aranda responde `200` con solo el token de `ApiKey`, tanto directo como por
APIM: nueve de diez búsquedas y el adjunto completo salieron sin enviar cookie.
Por eso `Aranda:SessionCookieEnabled` está en `false`.

Lo que sí rompe es mandar una cookie **caducada**: Aranda devuelve 401 con la
página de IIS "You do not have permission to view this directory or page." en
peticiones que sin cookie habrían funcionado. De ahí el interruptor, que apaga
también la adopción del `Set-Cookie` para que la sesión no se encienda sola.

#### El interruptor de la sesión por cookie

`Aranda:SessionCookieEnabled` decide si el gateway opera con sesión o solo con
el token. En `false` —el valor actual— no envía `AuthCookie`, no adopta las
cookies que devuelve Aranda y no ejecuta el latido.

Si Aranda vuelve a exigir sesión no hace falta redesplegar: basta instalar una
cookie con `PUT /admin/aranda-session`, que **reactiva el envío en caliente**.
Desde ahí el comportamiento vuelve a ser el de siempre, descrito abajo.

#### Cómo se mantiene viva la sesión, cuando está encendida

Aranda usa expiración deslizante: devuelve un `Set-Cookie` renovado en cada
respuesta y lo que mata la sesión es la **inactividad**, no el tiempo
transcurrido. Se midió una sesión muerta tras unos 10 minutos sin tráfico.

El gateway hace dos cosas para que no caduque:

- `ArandaSessionCookieHandler` guarda la cookie de cada respuesta y la usa en
  la siguiente. `Aranda:AuthCookie` es solo la **semilla** del primer request.
- `ArandaSessionKeepAliveService` consulta Aranda cada
  `Aranda:SessionKeepAliveMinutes` minutos (5 por omisión, `0` desactiva) para
  reiniciar el contador aunque no haya tráfico de usuarios. Evalúa en cada tick
  si hay sesión, así que también sostiene una instalada en caliente.

#### Renovar la sesión sin reiniciar (`/admin/aranda-session`)

La cookie caduca en pocos minutos y un despliegue completo tarda más que eso,
así que renovarla por configuración obliga a coordinar el cambio en una ventana
muy corta. Para soporte hay una ruta que la instala en caliente:

```bash
curl -X PUT "https://HOST/admin/aranda-session" \
  -H "Content-Type: application/json" \
  -d '{"cookie":"AuthCookieASMS=VALOR"}'

curl "https://HOST/admin/aranda-session"
curl "https://HOST/admin/aranda-session/value"
```

El `PUT` reemplaza la sesión en memoria y enciende el envío aunque
`SessionCookieEnabled` esté en `false`. El `GET` informa si hay sesión y cuándo
se renovó, sin revelar la cookie. `GET /aranda-session/value` sí devuelve el
valor vivo, o `404` si no hay sesión. Tras instalarla, el latido la mantiene
viva mientras el proceso siga arriba.

`/value` existe porque Aranda rota la cookie en cada respuesta y la vigente solo
vive en memoria: sin esa ruta, un despliegue la perdía sin forma de
recuperarla. El procedimiento seguro es leerla, desplegar y reinstalarla.

Esto no reemplaza a `Aranda:AuthCookie`, que sigue siendo la semilla del
arranque; evita el redespliegue cuando la sesión muere en caliente.

**Riesgo asumido.** Estas rutas son anónimas, igual que el resto de la gateway,
y el App Service responde desde internet: se comprobó llamando a
`https://ase-gestionaranda-dev.azurewebsites.net/health` sin pasar por APIM. En
consecuencia, cualquiera que conozca la URL puede instalar la cookie con la que
la gateway opera contra Aranda, dejarla inoperativa enviando una inválida, o
**leer la sesión viva** por `/value` y suplantar a la cuenta de servicio. Es
distinto del resto de los endpoints anónimos, que solo leen con una credencial
fija: estas rutas **cambian con qué credencial actúa el servicio, y entregan
esa credencial**. Cada lectura de `/value` queda registrada en el log.

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

#### Lo que se aplica solo se consulta aparte de la creación

El DEF manda mostrar un resumen y confirmarlo antes de registrar
(REQ_04, paso 6). El agente no tiene esos datos escritos en el topic: los pide
al gateway, para que no se desincronicen si cambia la configuración de arriba.

`GET /api/tickets/parametros-creacion` devuelve **antes** de crear todo lo que
el gateway aplica sin que el colaborador lo escriba. No consulta Aranda ni
exige colaborador:

| Campo | De dónde sale |
|---|---|
| `clasificacion` | `Aranda:Classification` — los cinco valores del REQ_04 |
| `prefijoAsunto` | `Aranda:SubjectPrefix`, o `null` si no hay ninguno |
| `limites.maxAsunto` | fijo en `TicketService`: 400, límite real de Aranda |
| `limites.maxDescripcion` | `Aranda:MaxDescriptionLength` |
| `limites.maxBytesAdjunto` | `Aranda:MaxAttachmentBytes` |
| `limites.extensionesPermitidas` | fijas en `TicketService`, según el REQ_04 |

`POST /api/tickets` **no** repite nada de esto: responde solo `caseNumber` y
`status`. Se separó a pedido del cliente, porque el resumen se arma antes de
confirmar y ahí es donde se necesitan; repetirlos en la creación daba dos
fuentes para el mismo dato.

Los nombres de la clasificación van aparte de los IDs porque Aranda no los
devuelve al crear. Nada valida que un par (ID, nombre) coincida con el
catálogo real: **si se cambia un ID hay que cambiar su nombre en el mismo
despliegue.** El prefijo y los límites sí son la fuente real: el mismo código
que los publica es el que los hace cumplir al crear.

La creación exige `RegistryTypeId` y `UnitId`. Para QA se configuraron
provisionalmente `Correo` (`4608`) y `Minsur Lima` (`5875`), validados mediante
la prueba controlada `RF-56025`. Estos valores deben confirmarse con el equipo
de Aranda antes de promover la solución a producción. En una actualización,
Aranda exige `RegistryTypeId` y acepta `UnitId = 0` para conservar la sede
existente.

#### Límites de longitud de los campos

**El asunto no puede pasar de 400 caracteres.** Con 401 Aranda responde `500`
con `FailureAddItem`, sin truncar ni decir qué campo sobra. Medido el 16 de
septiembre de 2026 por bisección entre 313 y 4000: 400 se guarda intacto, 401
falla. El límite cuenta **caracteres, no bytes** — un asunto de 400 con tildes
y `ñ` entra completo.

`CreateTicketAsync` lo valida y devuelve `400` con un mensaje claro en lugar de
dejar que Aranda tire el `500`. La validación mide el asunto **ya marcado con
el prefijo y ya codificado**, que es lo que realmente viaja.

**Aranda no limita la descripción:** 100 000 caracteres vuelven completos. El
tope de `Aranda:MaxDescriptionLength` —20 000 por omisión— es política del
gateway, no un dato de Aranda: `POST /api/tickets` está publicado en APIM y sin
tope aceptaría un cuerpo de cualquier tamaño. Deja más de diez veces los 1500
del formulario. Por eso se configura, mientras el asunto va fijo en el código:
sus 400 son un límite real de la plataforma, no una decisión nuestra.

El texto se escapa con un `HtmlEncoder` restringido a `BasicLatin` y
`Latin1Supplement`, no con `HtmlEncoder.Default`. El de omisión escapa todo lo
que no sea ASCII, así que cada tilde ocupaba seis caracteres (`&#xF3;`) y Mesa
de Ayuda las veía crudas en la consola: los tickets `RF-59352` y `RF-59349`
quedaron registrados como `Tercera validaci&#xF3;n`. Además ese inflado
consumía el presupuesto de 400: un asunto de 150 con 48 acentos o más lo
rompía. Se sigue escapando `<`, `>`, `&`, `"` y `'`.

Con esto, los 150 caracteres de asunto del formulario entran con margen.

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

### Observabilidad (Application Insights)

La telemetría se activa **solo si hay cadena de conexión**. Se lee de
`ApplicationInsights:ConnectionString` o de
`APPLICATIONINSIGHTS_CONNECTION_STRING`, que es la que inyecta Azure App
Service al vincular el recurso. Sin ella no se registra nada: el SDK arrancaría
igual y se quedaría reintentando envíos que nadie recibe, así que en local y en
las pruebas queda inactiva.

La gateway aparece como `aranda-gateway` en el mapa de aplicaciones, con el
atributo `deployment.environment` para separar dev de producción cuando ambos
comparten recurso.

Desde la versión 3 el SDK de Application Insights se apoya en OpenTelemetry, de
modo que el nombre del servicio y el entorno se declaran como atributos de
recurso; la API clásica de `ITelemetryInitializer` ya no existe.

Qué llega a la telemetría y qué no: las peticiones entrantes, las dependencias
salientes hacia Aranda y las trazas del log, incluido el cuerpo recortado de
los errores de Aranda, que es lo que distingue un `401` de APIM de una sesión
caducada. **No** se registran encabezados ni cuerpos de las peticiones, así que
ni el token de `X-Authorization`, ni la cookie de sesión, ni la clave de
`X-Admin-Key` salen en la telemetría.

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
`GET /api/tickets/{caseNumber}` responden `200` y `POST /api/tickets` responde
`201` desde el 15 de septiembre de 2026. La anulación está bloqueada en APIM y
**adjuntar está bloqueado en Cloudflare** para cualquier archivo con contenido
binario real, incluido el portal de Aranda (ver
[docs/consultas-al-cliente.md](docs/consultas-al-cliente.md), puntos 1 y 12). El
desafío de Cloudflare ya no bloquea las demás operaciones: se reintenta.

El `200` de `POST /api/tickets/{caseNumber}/attachments` registrado el 15 de
septiembre fue un falso positivo: se probó con un PDF de texto plano de 776 B.
Los adjuntos se prueban con archivos reales.

#### Limpiar los tickets que dejan las pruebas

Cada `POST /api/tickets` de prueba deja un ticket real en Mesa de Ayuda, y la
anulación por el gateway responde `502` con `ARANDA_404` porque APIM publica
solo `get` en `/api/v9/item/{id}`. Se cierran yendo directo a Aranda:

```powershell
$env:ARANDA_DIRECT_URL = 'https://mesadeayuda.divisionminera.com/ASMSAPI'
$env:ARANDA_TOKEN      = 'Bearer <token>'
$env:ARANDA_COOKIE     = 'AuthCookieASMS=<cookie viva>'

node tools/anular-tickets-prueba.mjs RF-59557 RF-59468
```

El script lee cada ticket antes de anularlo y toma de ahí el `itemVersion` y
los ids del caso: con un `itemVersion` que no es el vigente Aranda rechaza la
actualización, y los tickets ya tocados por Mesa de Ayuda no están en la
versión 1. Después del `PUT` vuelve a leer el estado, porque un `200` por sí
solo no prueba que haya cambiado.

**Necesita cookie desde el 18 de septiembre de 2026.** Ese día `/ASMSAPI` empezó
a responder `401` con el HTML de IIS («You do not have permission to view this
directory or page.») a toda petición sin `AuthCookieASMS`, directo y por APIM,
con token válido, con token inválido y sin token: el rechazo es anterior a la
autenticación de la aplicación. Con una cookie viva las mismas llamadas
responden `200`. `ARANDA_COOKIE` es opcional en el script, pero hoy hace falta;
la cookie se saca de una sesión abierta en el portal.

**Tiene que ser Node.** Desde una máquina de desarrollo, `curl`, PowerShell y
`HttpClient` de .NET reciben el Managed Challenge de Cloudflare en el 100% de
los intentos; Node pasa porque comparte fingerprint TLS con `PostmanRuntime`,
que es el único cliente que el borde deja entrar. Cambiar solo el `User-Agent`
no alcanza: PowerShell con UA de Postman también es desafiado. El desafío
igual es intermitente, así que el script reintenta.

El 16 de septiembre de 2026 se cerraron así los ocho tickets `[PRUEBA BOT]` que
quedaban abiertos (RF-59120, RF-59122, RF-59276, RF-59349, RF-59352, RF-59423,
RF-59468 y RF-59557), y el 18 los siete siguientes (RF-59934, RF-60204,
RF-60205, RF-60206, RF-60207, RF-60208 y RF-60209), estos ya con cookie.

### Pendientes

Están separados por quién puede resolverlos.

[docs/consultas-al-cliente.md](docs/consultas-al-cliente.md) reúne lo bloqueado
del lado del cliente: las dos operaciones ausentes en APIM, la decisión de salir
por APIM o directo a Aranda, la cuenta que debe figurar como autor, el catálogo
de causales de anulación, los IDs de sede y tipo de registro por confirmar, la
política contra el desafío de Cloudflare y el catálogo oficial de errores.

Dos frentes se cerraron por decisión del equipo el 15 de septiembre de 2026: el
**login automático** queda fuera del alcance y el **flujo de estados** se
mantiene escrito en la configuración, sin leerlo desde Aranda. El primero dejó
de tener costo ese mismo día, al comprobarse que la cookie no es necesaria: sin
sesión que reponer, no hay nada que automatizar.

[docs/deuda-tecnica.md](docs/deuda-tecnica.md) reúne lo que se cierra con código
de este repositorio: la protección de `/admin/aranda-session`, el campo
`solution` sin llenar, la falta de log en la anulación, el manejo de la cookie
vencida y varias correcciones menores.

Cinco de las seis operaciones están validadas contra Aranda real: las tres
consultas, la creación —que devolvió `201` con el caso RF-58501 el 11 de
septiembre de 2026— y el adjunto, que responde `200` desde el 15 de septiembre.

**La anulación devuelve `502` con `ARANDA_404`**: APIM no publica
`PUT /api/v9/item/{id}`, aunque la operación funciona llamando directo a
Aranda. Se reprodujo el 15 de septiembre de 2026 sobre RF-59276.

El adjunto falló durante días con `502` y `ARANDA_400`. La causa resultó ser un
campo del formulario: Aranda exige `IsPublic` y responde
`{"exceptionMessage":"PublicValueIsRequired"}` sin él, pese a que el manual de
integración v9 no lo lista entre los campos requeridos. La colección
`API-V9.postman_collection_2508` sí lo trae, y resultó la fuente correcta.
