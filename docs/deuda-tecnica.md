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

**Paliativo intermedio,** si el login tarda: persistir la cookie rotada fuera
del proceso (archivo en almacenamiento persistente, o Redis/Blob con varias
instancias) para que un reinicio corto la recupere en vez de caer a la semilla.
No cubre paradas largas.

---

## 2. Creación de tickets bloqueada: falta la Sede válida

**Qué pasa.** `POST /api/tickets` responde `502` con `ARANDA_400`. Aranda
rechaza la creación con `InvalidOrganizationArea` en `AddItem`.

**Diagnóstico.** El campo obligatorio es `unitId`, que Aranda expone como
recurso `BusinessArea` con etiqueta "Sedes" y `mandatory: true` en el modelo
17. Lo verificado el 11 de septiembre de 2026:

| Intento | Resultado |
| --- | --- |
| `unitId` omitido | `UnitId IsRequired` |
| `unitId: 0` | `InvalidOrganizationArea` |
| `unitId: 5875` + categoría 988 / servicio 51 (la del bot) | `InvalidOrganizationArea` |
| `unitId: 5875` + categoría 775 / servicio 8 (de un ticket real) | Pasa el área y pide el campo adicional `'Sede'` |

O sea: **5875 (Minsur Lima) es válida, pero no para la categoría y el servicio
que usa el bot.** No depende del usuario: falla igual con uebit20 (sin área
organizacional) y con Evelyn (`companyId` 5007, `cityId` 6135). Tampoco del
autor: falla con `authorId` 2 y con 15036.

La categoría 988 + servicio 51 está bien elegida por otro motivo: es la única
de las probadas **sin campos adicionales obligatorios** (la 775 + 8 exige
`Sede`, `Pais`, `Fecha de Inicio`, `Fecha de termino` y `Sedes`).

El README daba esta combinación por validada el 1 de septiembre de 2026 con la
prueba RF-56025. Ese ticket existe y se creó con `unitId = null`, lo que hoy
Aranda ya no acepta: la regla cambió entre esa fecha y el 11 de septiembre.

**Qué falta.** El `unitId` (Sede/BusinessArea) válido para la categoría 988 y
el servicio 51. Es un dato de catálogo: no se puede listar desde fuera porque
las rutas de catálogo de áreas de negocio no responden
(`/api/v9/businessarea`, `/api/v9/project/1/units` y variantes dan 404, y
`/api/v9/project/1/locations` da 500 por un error interno de Aranda).

**A quién preguntar.** Al equipo de Aranda: qué Sede corresponde a la categoría
"Ticket creado por bot" (988) con el servicio "Por categorizar" (51). Con ese
número se cambia `Aranda:UnitId` y la creación queda validada, sin desarrollo.

El gateway ya admite `UnitId` nulo (se omite del cuerpo) por si en algún
momento Aranda vuelve a resolver el área por su cuenta.

---

## 3. API de usuarios: funciona directo, falta en APIM

**Hallazgo del 11 de septiembre de 2026.**
`GET /ASMSAPI/api/v9/user/{username}/detail` **responde 200 llamando directo a
Aranda**, con la misma credencial y cookie que ya usa el gateway. Devuelve el
usuario completo, incluidos `companyId` y `cityId`.

Lo que falla es exclusivamente la publicación en APIM, que responde `404`.

Eso abre una alternativa al parche de usuario fijo que no depende de que
publiquen nada: apuntar esa única operación directo a Aranda en lugar de a
APIM. Queda por decidir si es aceptable para la política de red y seguridad,
porque saltarse APIM para una operación contradice el diseño actual.

El endpoint de login (`POST /api/v9/authentication/`) también responde directo:
devuelve `400 ValidationError` con credenciales vacías, es decir, el servicio
está operativo. Misma consideración.

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
producto `fcintgestionaranda/v1`: responde `404` de APIM.

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
