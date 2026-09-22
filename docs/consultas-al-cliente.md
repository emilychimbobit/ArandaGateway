# Consultas y avisos al cliente

Todo lo que está bloqueado del lado del cliente (Minsur / Electrodata / quien
administra APIM). Ninguno de estos puntos se resuelve con código del gateway:
esperan una publicación, una credencial, un dato de catálogo o una decisión.

Lo que sí depende del equipo de desarrollo está en
[`deuda-tecnica.md`](deuda-tecnica.md).

Estado al 21 de septiembre de 2026.

## Resumen

| # | Pendiente | Responsable | Bloquea |
| --- | --- | --- | --- |
| 1 | Publicar dos operaciones en APIM | Admin de APIM | Anulación (REQ_06), identidad real |
| 2 | ~~Credenciales de usuario de servicio + tenant alias~~ — **cerrado, ver punto 2** | — | — |
| 3 | Decisión: salida por APIM o directo a Aranda | Arquitectura / seguridad | Punto 1 |
| 4 | Cuenta que debe figurar como autor de los tickets | Minsur | Trazabilidad y reportes |
| 5 | Catálogo de causales de anulación (`reasonId`) | Electrodata | Reportes por motivo |
| 6 | Confirmar `RegistryTypeId` y `UnitId` por sede | Electrodata | Producción multi-sede |
| 7 | Cloudflare desafía el tráfico de APIM | Admin de APIM | Disponibilidad en horario de oficina |
| 8 | Catálogo oficial de errores de Aranda | Minsur | Mensajes al usuario |
| 9 | Ventana y marcado de tickets de prueba | Mesa de Ayuda | Pruebas en QA |
| 10 | Confirmar el flujo de estados | Minsur | Criterio de abiertos y anulables |
| 11 | `ModelId` del modelo de incidentes (IM) y la regla para elegir el tipo | Minsur | Creación de incidentes |
| 12 | Cloudflare bloquea todo adjunto con contenido binario | Admin de Cloudflare | Adjuntar archivos (REQ_04) |

Los puntos 1 y 12 son los críticos: mientras el 1 siga abierto la solución **no
debe llegar a producción con usuarios reales**, y mientras el 12 siga abierto
**REQ_04 no funciona con archivos reales**.

---

## 1. Dos operaciones faltan en APIM

**Hallazgo del 11 de septiembre de 2026, revalidado el 15.** Las operaciones
responden `404` de APIM (`{ "statusCode": 404, "message": "Resource not found" }`,
formato de APIM, no de Aranda) y sin embargo funcionan llamando directo a
`https://mesadeayuda.divisionminera.com/ASMSAPI`, con la misma credencial y
cookie que ya usa el gateway:

| Operación | Para qué sirve | APIM | Directo a Aranda |
| --- | --- | --- | --- |
| `PUT /api/v9/item/{id}` | Anular un ticket | `404` | `200` |
| `GET /api/v9/user/{username}/detail` | Resolver al colaborador | `404` | `200` |

En el spec del repositorio `/api/v9/item/{id}` figura **solo con GET**, por eso
la anulación falla: `POST /api/tickets/{caseNumber}/cancellation` devuelve `502`
con `ARANDA_404`. Reproducido el 15 de septiembre de 2026 sobre `RF-59276`. El
código del gateway es correcto; se verificó anulando RF-58496 y RF-58497 directo
contra Aranda, donde el `PUT` respondió `200`.

`POST /api/v9/authentication/` también responde `404` en APIM, pero ya no se
pide: ver el punto 2.

**Qué pedir.** Publicar las dos operaciones en el producto
`fcintgestionaranda/v1`. Las dos ya están descritas en
`docs/iac/apim/API-FC-INT-GestionAranda.json` (v1.1.0) y se pueden publicar tal
cual: `ObtenerDetalleUsuario` estaba desde el principio y `ActualizarCaso` se
agregó el 21 de septiembre de 2026.

**Lo que faltaba de nuestro lado, ya resuelto el 21 de septiembre de 2026.** Ese
spec declaraba `/api/v9/item/{id}` **solo con `get`**, así que el `put` de la
anulación no estaba descrito y Victor no podía publicar lo que no figuraba. Ya
está: la operación `ActualizarCaso` y los esquemas
`ArandaUpdateTicketRequest` / `ArandaUpdateTicketResult` están en el spec, con
el cuerpo que respondió `200` directo contra Aranda el 15 de septiembre. El
catálogo de causales del punto 5 (`reasonId`) no bloquea esto: el gateway hoy no
envía ese campo.

**Reproducido el 16 de septiembre de 2026.** `PUT /api/v9/item/{id}` por APIM
devuelve `404` de APIM en 4 de 4 intentos (12:32 hora Lima) y **sin encabezado
`CF-Ray`**: la petición muere en APIM y nunca llega a Cloudflare. Es un tema de
publicación en APIM, no del WAF ni del desafío del punto 7.

**Qué se desbloquea.** REQ_06 completo y el retiro del parche de usuario fijo
(punto 9 de `deuda-tecnica.md`).

---

## 2. Login automático: descartado del alcance

**Cerrado el 15 de septiembre de 2026 por decisión del equipo de desarrollo.**
Las credenciales de usuario de servicio y el `x-aranda-tenant-alias` real nunca
llegaron, y no se va a seguir pidiendo. **La sesión por cookie manual deja de ser
un parche y pasa a ser el diseño definitivo.** Lo que sigue documenta el riesgo
que eso implica, para que quede aceptado de forma explícita y no heredado.

**Cómo opera.** La salida hacia Aranda necesita la cookie de sesión
`AuthCookieASMS` además del token de `X-Authorization`. La cookie se instala a
mano por `PUT /admin/aranda-session` y vive solo en la memoria del proceso.
`ArandaSessionKeepAliveService` la mantiene viva mientras el proceso siga en pie.

**Riesgo operativo permanente.** Cada despliegue, reinicio, reciclaje del App
Service o instancia nueva por escalado pierde la cookie viva y vuelve a la
semilla de configuración, que caduca a los ~10 minutos sin uso. Es decir: **el
servicio queda respondiendo `401` hasta que una persona pegue una cookie fresca
a mano.** El despliegue tarda ~7 minutos, más que la vida de la cookie, así que
pasarla por configuración casi nunca alcanza la ventana.

Consecuencias que el cliente debe aceptar:

1. No hay recuperación automática ante un reinicio. Requiere intervención manual
   con una persona disponible.
2. No se puede escalar horizontalmente sin instalar la cookie en cada instancia.
3. `/admin/aranda-session` queda como ruta de operación permanente, con lo que el
   punto 1 de `deuda-tecnica.md` (hoy es anónima) pasa de deuda a requisito de
   seguridad bloqueante.

**Si el cliente quisiera revertir la decisión,** el contrato del login ya está
levantado en `API-V9.postman_collection_2508` y haría falta: credenciales de un
usuario de servicio, el `x-aranda-tenant-alias` real (en la colección aparece
`qextreme`, del entorno demo del proveedor) y publicar
`POST /api/v9/authentication/` y `POST /api/v9/authentication/renewtoken` en
APIM, que hoy responden `404`.

---

## 3. Decisión de arquitectura: APIM o directo a Aranda

El punto 1 depende de que alguien publique operaciones en APIM. Existe una
alternativa que no depende de nadie: **apuntar el gateway directo a Aranda**.
Resolvería de golpe la anulación, el parche de usuario fijo y el desafío de
Cloudflare del punto 7.

**Medido el 15 de septiembre de 2026,** con la misma credencial y en el mismo
momento por ambos caminos:

| Operación | Por APIM | Directo |
| --- | --- | --- |
| `PUT /api/v9/item/{id}` (anular) | `404` | `200` |
| `GET /api/v9/user/{username}/detail` | `404` | `200` |
| Búsqueda de casos | 13 de 20 desafiadas | 0 de 20 desafiadas |

`GET user/{username}/detail` responde `200` con el correo completo
(`uebit20@minsur.com`); con el usuario a secas devuelve `UserNameDidNotFound`.
Retirar el parche de usuario fijo es viable en cuanto esa ruta esté disponible,
siempre que `X-Collaborator-Username` lleve el correo.

**Salvedad.** Todo se midió desde la red del equipo de desarrollo. El App
Service sale con IP de Azure y Cloudflare podría puntuarla distinto, así que el
0% no está comprobado desde el entorno desplegado.

A cambio, saltarse APIM contradice el diseño acordado: se pierde la puerta
única, su control de acceso por suscripción y su telemetría.

Una variante intermedia es una segunda URL base solo para las operaciones
ausentes, pero deja el sistema con dos caminos de salida y dos juegos de reglas.

**Qué pedir.** Que lo resuelva quien definió que la salida fuera por APIM. Es
una decisión de arquitectura y seguridad, no técnica.

---

## 4. Aranda ignora el `authorId` que envía el gateway

**Qué pasa.** El gateway envía `authorId: 2` (`ARANDA SERVICES USER`) en cada
creación, tal como está configurado en `Aranda:AuthorId`. Aranda descarta ese
valor y registra como autor al dueño de la sesión autenticada.

**Evidencia.** La prueba controlada `RF-58501`, creada el 11 de septiembre de
2026 desde el gateway, volvió con:

```json
"authorId": 15036,
"authorName": "Especialistatoken"
```

`Especialistatoken` es la cuenta de servicio del token configurado en
`Aranda:ApiKey` / `Aranda:AuthCookie`. Se descartó que hubiera una
sobreescritura local: `AuthorId` vale `2` en `appsettings.json` desde el commit
inicial `dc30fe8` (26 de agosto de 2026) y no aparece en user-secrets, ni en
variables de entorno, ni en la IaC. Es coherente con el resto del contrato:
`consoleType: "specialist"`, y el autor lo resuelve el servidor por credencial.

**Consecuencia.** Todo ticket creado por el bot figura en Aranda como registrado
por la cuenta de servicio, no por `ARANDA SERVICES USER` ni por el colaborador.
Cualquier reporte, tablero o auditoría que agrupe por autor va a atribuir el
100 % de los tickets del bot a esa única cuenta.

**Qué pedir.**

1. Confirmar qué cuenta debe figurar como autor de los tickets del bot.
2. Si la respuesta es `ARANDA SERVICES USER` (id `2`), entregar las credenciales
   de esa cuenta para reemplazar la sesión: es un cambio de credencial, no de
   configuración. El gateway ya pide el `authorId` correcto y seguirá haciéndolo.
3. Verificar que esa cuenta tenga rol de consola de especialista. Si es una
   cuenta de integración sin ese rol, la creación va a fallar con otro error.

**Qué NO hacer.** Cambiar `Aranda:AuthorId` no tiene efecto. El valor ya es el
deseado y Aranda lo pisa igual.

---

## 5. Catálogo de causales de anulación

**Qué pasa.** Aranda tipifica el motivo de anulación en `reasonId` /
`reasonName`. `RF-58501` fue cancelado por Mesa de Ayuda con
`reasonId: 2, reasonName: "Caso Duplicado"`.

El gateway no puede enviar ese campo: `ArandaUpdateTicketRequest` no lo tiene.
Manda el motivo como texto libre en `Commentary`.

**Consecuencia.** Toda anulación hecha desde el bot queda con causal nula y
fuera de los reportes de Aranda que agrupan por motivo.

**Qué pedir.** El catálogo de causales de anulación con sus IDs, y cuál debe
usarse para una anulación solicitada por el propio usuario. Con el ID, agregar
el campo es trivial del lado del gateway (punto 10 de `deuda-tecnica.md`).

---

## 6. Confirmar `RegistryTypeId` y `UnitId` por sede

**Qué pasa.** La creación exige ambos. Para QA se configuraron
provisionalmente `Correo` (`4608`) y `Minsur Lima` (`5875`), validados con la
prueba controlada `RF-56025`.

`UnitId: 5875` funciona hoy **solo porque el parche de usuario fijo hace que
todos los tickets sean del mismo colaborador**, que es de Lima. Cuando se retire
el parche y entren colaboradores de otras sedes, Aranda va a rechazar la
creación con `InvalidOrganizationArea`: la unidad no corresponderá al cliente.

**Qué pedir.**

1. Confirmar si `Correo` (`4608`) es el tipo de registro correcto para tickets
   originados en un bot de Teams, o si existe uno específico.
2. Cómo debe resolverse la sede de cada colaborador: si Aranda la deriva sola
   del usuario, o si el gateway debe mapearla y con qué fuente.

---

## 7. Cloudflare desafía el tráfico que llega desde APIM

**Qué pasa.** Cloudflare, delante de Aranda, responde `403` con
`Cf-Mitigated: challenge` y `cType: "managed"`. Es un *Managed Challenge*:
exige ejecutar JavaScript de navegador, así que **ninguna integración
servidor-a-servidor puede resolverlo**, por muchos reintentos que haga.

**Medición del 15 de septiembre de 2026.** Con la misma credencial, el mismo
cliente y en el mismo momento:

| Camino | Peticiones desafiadas |
| --- | --- |
| Por APIM | 13 de 20 (65%) |
| Directo a Aranda | 0 de 20 (0%) |

La tasa por APIM fluctúa: horas antes del mismo día era del 25%, y en otra
tanda del 10%. El desafío no distingue por tipo de petición —se midió JSON
contra multipart sin diferencia apreciable— sino por el camino.

También depende del cliente: el `User-Agent` de `curl` es desafiado el 100% de
las veces (20 de 20), mientras que Postman y el gateway rondan el 20-25%. Por
eso **diagnosticar con `curl` crudo no sirve**: siempre da `403`.

**Qué explica.** El corte del 14 de septiembre, en que la creación falló durante
el horario laboral y "se arregló sola" cerca de las 6 p.m., sin cambios de
nuestro lado: en las ventanas de tasa alta los tres reintentos se agotan y la
operación cae. También explica que obtener una cookie de sesión por Postman
saliera "a veces sí, a veces no".

**Muestra con Ray ID, del 16 de septiembre de 2026, solo por APIM.** 40
peticiones entre las 12:24:48 y las 12:25:12 (hora Lima; 17:24:48–17:25:12 UTC),
diez por cada endpoint que consume el gateway. Resultado: **10 desafíos de 40
intentos (25 %)**, repartidos entre los cuatro endpoints.

| Endpoint | Desafiadas | Ray ID y hora Lima |
| --- | --- | --- |
| `GET /api/v9/item/{id}` | 2 de 10 | `a3c191308c8cc98f-IAD` (12:24:51), `a3c19133b982d683-IAD` (12:24:52) |
| `POST /api/v9/item/search` | 2 de 10 | `a3c1914c29244b25-IAD` (12:24:56), `a3c1915ecfcc332e-IAD` (12:24:59) |
| `POST /api/v9/ci/cisbyuserandprojects` | 3 de 10 | `a3c191697a62d6d4-IAD` (12:25:00), `a3c1917b7af3071c-IAD` (12:25:03), `a3c1917eb875beb1-IAD` (12:25:04) |
| `POST /api/v9/file/` | 3 de 10 | `a3c1919349f4e98e-IAD` (12:25:07), `a3c191a02c83c978-IAD` (12:25:08), `a3c191a36aa2f8be-IAD` (12:25:10) |

El desafío toca **todos** los endpoints del MVP, en la misma ventana de 25
segundos y con peticiones idénticas entre sí. Las no desafiadas de esa misma
tanda respondieron `401` del origen —la página de IIS—, que es la prueba de que
atravesaron Cloudflare: el `401` se decide en Aranda y el `403` con
`Cf-Mitigated` en el borde.

`POST /api/v9/item/` (crear caso) no se incluyó en la muestra para no registrar
tickets reales; usa el mismo camino y la misma política, y su corte del 14 de
septiembre es el síntoma conocido.

Las dos operaciones que hoy solo existen por el camino directo (punto 1) también
son desafiadas: `PUT /api/v9/item/{id}` dio 1 desafío de 10 intentos
—`a3c290478dc68259-GYE`, 15:19:00 hora Lima, con un id inexistente para no
modificar ningún caso— y `GET /api/v9/user/{username}/detail` dio 1 de 6 en la
tanda de las 12:21 (`a3c18cc35b35b3c7-MIA`, 12:21:50), aunque 0 de 10 a las
15:19. La exclusión debe cubrir el prefijo `/ASMSAPI/api/v9/*` completo, no una
operación a la vez.

**Qué pedir.** Incorporar el origen de APIM a la lista de permitidos de
Cloudflare, o excluir `/ASMSAPI/api/v9/*` del desafío para ese origen.

**Prioridad: alta.** Es causa raíz de indisponibilidad intermitente en horario
de oficina, no una molestia de diagnóstico. `ArandaRetryHandler` lo absorbe
mientras la tasa sea baja, pero cada reintento consume el presupuesto de
`Aranda:TimeoutSeconds` y con tasa alta no alcanza.

---

## 8. Catálogo oficial de errores

`DEF.md` declara una dependencia externa: el anexo
`BIT2026_MINSUR-Catálogo errores_ARANDA_V1.0`. No se ha recibido.

**Qué pedir.** El anexo. Mientras no esté, los mensajes de error visibles al
usuario son los definidos inicialmente por el equipo y no son configurables en
el MVP.

---

## 9. Ventana y marcado de tickets de prueba

**Qué pasa.** `RF-58501`, creado el 11 de septiembre de 2026 a las 10:04 (hora
Lima) con asunto `[PRUEBA BOT] Validacion por gateway`, fue tomado y cancelado
por un analista de Mesa de Ayuda a las 14:32 con causal `Caso Duplicado`.
Comportamiento razonable de su parte: entraron varios tickets de prueba
parecidos a su bandeja.

**Qué pedir.** Acordar con Mesa de Ayuda cómo distinguir los tickets de prueba
del gateway —un prefijo de asunto, una categoría dedicada o una ventana horaria
convenida— para que las pruebas de integración no generen ruido ni consuman su
tiempo.

**Parcialmente resuelto.** Mesa de Ayuda pidió que todo ticket del bot lleve el
prefijo en el asunto, así que se agregó `Aranda:SubjectPrefix`, hoy en
`[PRUEBA BOT]`. Es el interruptor de la marca: en `null` o vacío el asunto va
tal cual lo escribió el colaborador, sin tocar código. Queda por acordar la
ventana de pruebas.

---

## 10. Flujo de estados: confirmarlo

**Hallazgo del 14 de septiembre de 2026.** Aranda expone el flujo de estados de
cada modelo en `GET /api/v9/model/{modelId}/{itemType}/states`. Llamando directo
a Aranda (la ruta **no está publicada en APIM**) se levantó el flujo completo
del modelo 17 / itemType 4, que es el de las solicitudes de servicio:

| ID | Nombre | `stageName` | `isFinal` | Transiciones |
| --- | --- | --- | --- | --- |
| 59 | Registrado | — | false | 60, 61 |
| 60 | Asignado | InProgress | false | 61, 62, 63, 64, 65 |
| 61 | Cancelado | — | **true** | — |
| 62 | En Aprobacion | OnHold | false | por levantar |
| 63 | Pendiente usuario | OnHold | false | por levantar |
| 64 | Pendiente proveedor | OnHold | false | por levantar |
| 65 | En Proceso | InProgress | false | 60, 61, 63, 64, 66 |
| 66 | Resuelto | Solved | false | 65, 67 |
| 67 | Cerrado | ClosedByDefault | **true** | — |

Sin `stateId` en la consulta, la operación devuelve el estado inicial (59). Con
`?stateId=N` devuelve los estados alcanzables desde N, así que el flujo se
recorre saltando de estado en estado.

**Qué corrigió esto en el gateway.** Las listas de estados de `TicketService`
tenían tres nombres que no existen en el modelo: `Solucionado`, `Anulado` y
`Registrado/Asignado` (este último es la forma en que el DEF enuncia dos estados
distintos, 59 y 60). Ya están corregidas con los nombres reales.

**Qué no se puede deducir de las banderas.** Ni `isClosed` ni `isFinal` sirven
para saber si un ticket terminó: **Resuelto (66) llega con las dos en falso**,
porque desde ahí Aranda permite volver a En Proceso (65). El criterio queda por
nombre de estado, apoyado en `stageName`.

**Qué pedir.**

1. Confirmar que este flujo es el de producción y no solo el del ambiente
   consultado.
2. Decidir si un ticket **Resuelto (66)** debe seguir visible en el listado del
   colaborador, dado que es reabrible.

**Lo que NO se pide: publicar `states` en APIM.** Se evaluó leer el flujo desde
Aranda en caliente y **se descartó el 15 de septiembre de 2026**: administrar el
flujo de estados no está dentro del alcance de este equipo. Los estados quedan
escritos en la configuración del gateway (`TicketService` y las claves
`Aranda:*StateId`), y un cambio de flujo en Aranda requiere despliegue. Si Mesa
de Ayuda modifica el modelo 17 sin avisar, el criterio de abiertos y anulables
queda desactualizado en silencio: por eso el punto 1 de esta lista pide
confirmar el flujo por escrito.

---

## 11. Modelo de incidentes (IM) y regla para elegir el tipo

Hoy el gateway solo crea solicitudes de servicio: `ModelId` 17, `itemType` 4,
estado inicial 59, estado de anulación 61. `IncidentModelId`,
`IncidentInitialStateId` e `IncidentCancellationStateId` están en `null`, así que
el tipo incidente está deshabilitado.

**Qué pedir.**

1. El `ModelId` del modelo de incidentes. Con él se levanta su flujo de estados
   igual que el del punto 10, sin necesidad de que dicten los IDs de estado.
2. Si `CategoryId`, `ServiceId`, `ImpactId`, `UrgencyId` y `GroupId` cambian para
   incidentes o se mantienen los de RF.
3. La regla de negocio para elegir entre RF e IM: si el colaborador lo indica en
   la conversación, si se deriva de la categoría o el servicio, o si siempre es
   uno de los dos. El gateway ya soporta ambos tipos; falta la regla.

---

## 12. Cloudflare bloquea todo adjunto con contenido binario

**Hallazgo del 16 de septiembre de 2026.** Adjuntar falla con `403` cuando el
cuerpo de la petición lleva contenido binario comprimido real —alta entropía—,
que es el caso de cualquier archivo de usuario: un PDF de Office, un escaneo,
una imagen. La respuesta es una página de bloqueo de Cloudflare
—`Server: cloudflare`, "Sorry, you have been blocked ... divisionminera.com"—
**sin** el encabezado `Cf-Mitigated`, así que no es el desafío del punto 7: es
una regla del WAF que descarta la petición por su contenido.

**Es determinista, no intermitente.** Depende del contenido, no del momento ni
del camino. Cada archivo se probó dos veces con el mismo resultado:

| Archivo | Tamaño | Bytes no ASCII | Resultado |
| --- | --- | --- | --- |
| PDF de texto plano, sin comprimir | 776 B | 0 | `200` |
| PDF de texto plano, sin comprimir | 1,05 MB | 0 | `200` |
| PDF con stream de texto muy repetitivo | 5,2 KB | 1 990 | `200` |
| PDF con stream de bytes aleatorios | 1,8 KB | 588 | `403` |
| PDF de texto variado (informe de 1 página) | 17 KB | 8 571 | `403` |
| PDF con imagen (escaneo simulado) | 256 KB | 125 813 | `403` |
| PDF real de oficina | 1,27 MB | sí | `403` |

**No es la cantidad de bytes no ASCII**: un PDF con 1 990 bytes no ASCII pasa y
otro con 588 se bloquea. Lo que distingue a los que pasan es que su contenido
comprimido proviene de texto ASCII muy repetitivo; en cuanto el archivo tiene
entropía de archivo real —texto variado, una imagen, cualquier documento
generado por una herramienta de oficina— se bloquea. La regla exacta solo puede
leerse desde la consola de Cloudflare.

Esto descarta también **el tamaño**, y por un margen amplio: un PDF de texto
plano de **7,78 MB** atraviesa Cloudflare y llega al servidor de Aranda
—responde `401` de IIS, no `403` del borde—, mientras uno de **17 KB** con
contenido de documento real se bloquea en el borde. El que se bloquea es 460
veces más liviano. Por el gateway, con su límite de 3 MB, un PDF ASCII de
2,79 MB sube correctamente en 6 de 8 intentos: los dos fallos son el desafío
intermitente del punto 7, no este bloqueo.

Descarta además a **APIM**: llamando directo a
`https://mesadeayuda.divisionminera.com/ASMSAPI/api/v9/file/` el binario también
recibe `403` de Cloudflare, mientras el ASCII llega al origen. Tampoco es del
gateway: **el propio portal de Aranda** (`Mis casos` → `Adjuntos` →
`Adjuntar archivo`), desde el navegador y con sesión de usuario, responde
"No se pudo adjuntar archivo" con los mismos archivos.

Los reintentos no ayudan y no se ejecutan a propósito: `ArandaRetryHandler`
solo repite el desafío (`Cf-Mitigated`) y el `429`. Repetir un bloqueo por
contenido daría `403` las tres veces.

**Tampoco hay salida por código.** Aranda v9 solo acepta el archivo como
`multipart/form-data` con la parte `Data0`; su API no ofrece variante en base64
ni otra codificación que evite los bytes crudos.

**Qué pedir.** Al administrador de Cloudflare de `divisionminera.com`:

1. Revisar en Security → Events los eventos de bloqueo con estos Ray ID e
   identificar la regla que dispara:
   `a3c0ecdfdec207fb` y `a3c0ed42dbaf07fb` (16 de septiembre de 2026, 15:32 UTC,
   `POST /ASMSAPI/api/v9/file/`).
2. Crear una excepción (*skip*) de esa regla —o del conjunto administrado
   completo— para `POST /ASMSAPI/api/v9/file/` y para la ruta de adjuntos del
   portal. Es la misma regla que rompe el portal, así que el pedido no es solo
   por la integración.

**Prioridad: alta.** REQ_04 no funciona con archivos reales. Hoy solo suben
archivos de texto ASCII o comprimidos de texto repetitivo, lo que en la práctica
significa ningún archivo de usuario.

**Cuidado con la validación anterior.** El `200` registrado el 15 de septiembre
se obtuvo con un PDF de prueba de 776 B escrito a mano, sin streams comprimidos:
fue un **falso positivo**. Toda prueba de adjuntos debe hacerse con un archivo
real.

---

## Pendientes de definición aún abiertos

`.github/copilot-instructions.md` mantiene la lista completa de puntos que no
deben resolverse por inferencia. Los que siguen abiertos y dependen del cliente:

- mecanismo técnico de SSO e identidad delegada del colaborador;
- identificador confiable y mapeo entre colaborador y usuario de Aranda
  (ver punto 1);
- vigencia y rotación de la API key técnica de Aranda;
- IDs por ambiente para modelo, servicio, categoría, impacto, urgencia, grupo y
  estados: hoy solo están validados los de QA;
- cantidad máxima de adjuntos, y si los 3 MB aplican por archivo o por solicitud
  completa;
- si Aranda es la fuente de CMDB para REQ_10.
