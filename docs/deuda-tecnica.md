# Deuda técnica

Soluciones temporales y bloqueos abiertos, con lo que hace falta para
resolverlos. Ninguno se arregla con código del gateway: dependen de datos de
catálogo de Aranda o de que se publiquen operaciones en APIM.

Estado al 11 de septiembre de 2026.

---

## 1. Sesión de Aranda por cookie manual

**Qué hay hoy.** La salida hacia Aranda necesita la cookie de sesión
`AuthCookieASMS` además del token de `X-Authorization`. Se configura a mano en
`Aranda:AuthCookie` (variable de entorno `Aranda__AuthCookie`, nunca en
`appsettings.json`: es una credencial).

El gateway la sostiene por su cuenta mientras el proceso viva:
`ArandaSessionCookieHandler` adopta la cookie que Aranda renueva en cada
respuesta y `ArandaSessionKeepAliveService` la toca cada 5 minutos para que no
caduque por inactividad. Verificado: la sesión sobrevivió 15 minutos sin
tráfico de usuarios, y el gateway seguía respondiendo 200 con una cookie
rotada cuando la semilla de configuración ya daba 401.

**Qué falta.** La cookie viva está solo en memoria. En cada arranque de proceso
se vuelve a leer la semilla de configuración, que a los ~10 minutos sin uso ya
está vencida. Afecta a cada despliegue, reinicio o reciclaje del App Service,
y a cada instancia nueva si se escala horizontalmente.

**Solución permanente.** Que el gateway inicie sesión solo. Aranda expone el
endpoint y el contrato está en `API-V9.postman_collection_2508`:

```
POST /api/v9/authentication/
  X-Authorization: Bearer <token de integración>    <- el que ya se usa
  x-aranda-tenant-alias: <alias del tenant>
  { "consoleType": 1, "providerId": 0, "userName": "...", "password": "..." }

POST /api/v9/authentication/renewtoken
  Authorization: <token de sesión>
  "<token de sesión>"
```

El login se autentica con el token de integración que ya está configurado más
usuario y contraseña, así que el gateway podría obtener y renovar su sesión sin
intervención.

**Bloqueo.** Ninguna de las dos rutas está publicada en APIM: responden
`404 { "statusCode": 404, "message": "Resource not found" }`. Hace falta que se
publiquen, credenciales de un usuario de servicio de Aranda y el valor real de
`x-aranda-tenant-alias` (en la colección aparece `qextreme`, del entorno demo).

Al resolverse desaparece `Aranda:AuthCookie` y con ella
`ArandaSessionKeepAliveService`.

**Paliativo en uso.** `PUT /admin/aranda-session` instala la cookie en caliente,
sin reiniciar ni redesplegar. Nació de un problema concreto: el despliegue tarda
unos 7 minutos, más que la vida de la cookie, así que pasarla por configuración
obliga a coordinar el cambio en una ventana que casi nunca se alcanza. Con esta
ruta la cookie se instala cuando la aplicación ya está arriba.

No elimina la deuda: sigue siendo una credencial renovada a mano, y con varias
instancias hay que instalarla en cada una, porque la sesión vive en la memoria
de cada proceso.

**Otro paliativo posible,** si el login tarda: persistir la cookie rotada fuera
del proceso (archivo en almacenamiento persistente, o Redis/Blob con varias
instancias) para que un reinicio corto la recupere en vez de caer a la semilla.
No cubre paradas largas.

---

## 2. Una cookie vencida es peor que ninguna

**Resuelto lo principal.** La creación de tickets funciona: `POST /api/tickets`
devolvió `201` con RF-58501 el 11 de septiembre de 2026. Faltaba el indicador
`validate: true` en el cuerpo, sin el cual Aranda responde `400`
`InvalidOrganizationArea` aunque la Sede sea válida. `Aranda:UnitId` queda en
5875 (Minsur Lima), que sí es correcta.

**Lo que queda como riesgo.** Durante el diagnóstico se observó que una
petición **sin** cookie puede funcionar mientras la misma petición **con una
cookie vencida** falla con `401`. La creación por APIM funciona sin cookie; el
gateway, al arrancar con la semilla caducada, la envía y recibe `401`.

O sea: el gateway arrastra la cookie vencida y rompe operaciones que sin ella
habrían funcionado. Agrava el problema del punto 1, porque el arranque con
semilla caducada no degrada parcialmente, sino que tumba todo.

**Posible mejora.** Detectar el `401` de sesión, descartar la cookie en memoria
y reintentar una vez sin ella. Recuperaría las operaciones que no necesitan
sesión en lugar de fallar en bloque. No está implementado: conviene medir antes
qué operaciones realmente funcionan sin cookie, porque las consultas sí la
exigen.

---

## 3. Tres operaciones faltan en APIM y funcionan directo contra Aranda

**Hallazgo del 11 de septiembre de 2026.** Tres operaciones responden `404` de
APIM (`{ "statusCode": 404, "message": "Resource not found" }`, formato de
APIM, no de Aranda) y sin embargo funcionan llamando directo a
`https://mesadeayuda.divisionminera.com/ASMSAPI`, con la misma credencial y
cookie que ya usa el gateway:

| Operación | Para qué sirve | APIM | Directo a Aranda |
| --- | --- | --- | --- |
| `PUT /api/v9/item/{id}` | Anular un ticket | `404` | `200` |
| `GET /api/v9/user/{username}/detail` | Resolver al colaborador | `404` | `200` |
| `POST /api/v9/authentication/` | Iniciar sesión | `404` | responde `400 ValidationError` con credenciales vacías, es decir opera |

En el spec del repositorio `/api/v9/item/{id}` figura **solo con GET**, por eso
la anulación falla: `POST /api/tickets/{caseNumber}/cancellation` devuelve `502`
con `ARANDA_404`. El código del gateway es correcto; se verificó anulando
RF-58496 y RF-58497 directo contra Aranda, donde el `PUT` respondió `200`.

**Las dos salidas.**

1. **Publicar las tres en APIM.** Mantiene el diseño actual, con todo el
   tráfico por la puerta de entrada. Depende de quien administra APIM.
2. **Apuntar el gateway directo a Aranda.** Resolvería de golpe la anulación,
   el parche de usuario fijo y la cookie manual, sin pedir nada a nadie. A
   cambio, saltarse APIM contradice el diseño: se pierde la puerta única, su
   control de acceso por suscripción y su telemetría.

La segunda es una decisión de arquitectura y seguridad, no técnica: conviene
resolverla con quien definió que la salida fuera por APIM. Una variante
intermedia es una segunda URL base solo para las operaciones ausentes, pero
deja el sistema con dos caminos de salida y dos juegos de reglas.

---

## 4. Usuario fijo por dominio (`Aranda:UserOverride`)

**Qué hay hoy.** La resolución del colaborador está reemplazada por un usuario
fijo: `1562` (`evelyn.nunez@minsur.com`) para equipos y `15019`
(`uebit20@minsur.com`) para tickets. El encabezado
`X-Collaborator-Username` se registra en el log pero se ignora.

**Riesgo.** Cualquier colaborador ve los equipos de Evelyn y opera los tickets
de uebit20. **No debe llegar a producción con usuarios reales.** Es el punto
más delicado de esta lista.

**Solución permanente.** Publicar `GET /api/v9/user/{username}/detail` en APIM
y poner `Aranda:UserOverride:Enabled` en `false`. No requiere cambios de
código.

**Bloqueo.** La operación existe en el spec del repositorio
(`docs/iac/apim/API-FC-INT-GestionAranda.json`) pero no está publicada en el
producto `fcintgestionaranda/v1`: responde `404` de APIM. Ver el punto 3: la
operación sí funciona llamando directo a Aranda.

Al retirarse se borran `ArandaUserOverrideOptions.cs`, la propiedad
`ArandaOptions.UserOverride`, los bloques marcados `PARCHE TEMPORAL` en
`EquipoService` y `TicketService`, y sus pruebas.

---

## 5. Desafío de Cloudflare

**Qué hay hoy.** Cloudflare, delante de Aranda, responde `403` con
`Cf-Mitigated: challenge` de forma intermitente, incluso a peticiones
idénticas que funcionaron un momento antes. APIM no lo evita porque reenvía los
encabezados del cliente al backend.

`ArandaRetryHandler` lo absorbe con hasta tres intentos. Las escrituras solo se
reintentan ante rechazos del borde (desafío y `429`), donde hay certeza de que
Aranda no ejecutó nada; nunca ante `5xx` o tiempo de espera agotado, para no
duplicar tickets.

**Solución permanente.** Que la política de APIM normalice el `User-Agent`
hacia el backend y que se excluya del desafío la ruta `/ASMSAPI/api/v9/*` para
el origen de APIM.

**Prioridad.** La más baja de la lista: el reintento ya lo hace invisible. Vale
retomarla si el `403` aparece en los logs de producción con frecuencia, porque
cada reintento consume el presupuesto de `Aranda:TimeoutSeconds`.
