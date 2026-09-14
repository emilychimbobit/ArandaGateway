# Consultas y avisos al cliente

Todo lo que está bloqueado del lado del cliente (Minsur / Electrodata / quien
administra APIM). Ninguno de estos puntos se resuelve con código del gateway:
esperan una publicación, una credencial, un dato de catálogo o una decisión.

Lo que sí depende del equipo de desarrollo está en
[`deuda-tecnica.md`](deuda-tecnica.md).

Estado al 14 de septiembre de 2026.

## Resumen

| # | Pendiente | Responsable | Bloquea |
| --- | --- | --- | --- |
| 1 | Publicar tres operaciones en APIM | Admin de APIM | Anulación (REQ_06), identidad real |
| 2 | Credenciales de usuario de servicio + tenant alias | Electrodata | Login automático |
| 3 | Decisión: salida por APIM o directo a Aranda | Arquitectura / seguridad | Puntos 1 y 2 |
| 4 | Cuenta que debe figurar como autor de los tickets | Minsur | Trazabilidad y reportes |
| 5 | Catálogo de causales de anulación (`reasonId`) | Electrodata | Reportes por motivo |
| 6 | Confirmar `RegistryTypeId` y `UnitId` por sede | Electrodata | Producción multi-sede |
| 7 | Política de APIM para el desafío de Cloudflare | Admin de APIM | Nada (paliado) |
| 8 | Catálogo oficial de errores de Aranda | Minsur | Mensajes al usuario |
| 9 | Ventana y marcado de tickets de prueba | Mesa de Ayuda | Pruebas en QA |
| 10 | Confirmar el flujo de estados y publicar `states` en APIM | Minsur / Admin de APIM | Criterio de abiertos y anulables |
| 11 | `ModelId` del modelo de incidentes (IM) y la regla para elegir el tipo | Minsur | Creación de incidentes |

Los puntos 1 y 2 son los críticos: mientras sigan abiertos, la solución **no
debe llegar a producción con usuarios reales**.

---

## 1. Tres operaciones faltan en APIM

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

**Qué pedir.** Publicar las tres operaciones en el producto
`fcintgestionaranda/v1`. El spec ya está en
`docs/iac/apim/API-FC-INT-GestionAranda.json`.

**Qué se desbloquea.** REQ_06 completo, el retiro del parche de usuario fijo
(punto 9 de `deuda-tecnica.md`) y el login automático del punto 2 de acá.

---

## 2. Credenciales de usuario de servicio y tenant alias

**Qué pasa.** La salida hacia Aranda necesita la cookie de sesión
`AuthCookieASMS` además del token de `X-Authorization`. Hoy se instala a mano y
vive solo en la memoria del proceso: cada despliegue, reinicio o instancia nueva
la pierde, y la semilla de configuración caduca a los ~10 minutos sin uso.

Aranda expone el login y el contrato está en
`API-V9.postman_collection_2508`:

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
intervención humana.

**Qué pedir.**

1. Credenciales de un usuario de servicio de Aranda.
2. El valor real de `x-aranda-tenant-alias`. En la colección aparece
   `qextreme`, que es del entorno demo del proveedor.
3. Publicar las dos rutas de autenticación en APIM (ver punto 1).

**Qué se desbloquea.** Desaparece `Aranda:AuthCookie`, el keep-alive y la
instalación manual de cookie por `PUT /admin/aranda-session`.

---

## 3. Decisión de arquitectura: APIM o directo a Aranda

Los puntos 1 y 2 dependen de que alguien publique operaciones en APIM. Existe
una alternativa que no depende de nadie: **apuntar el gateway directo a
Aranda**. Resolvería de golpe la anulación, el parche de usuario fijo y la
cookie manual.

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

## 7. Política de APIM para el desafío de Cloudflare

**Qué pasa.** Cloudflare, delante de Aranda, responde `403` con
`Cf-Mitigated: challenge` de forma intermitente, incluso a peticiones idénticas
que funcionaron un momento antes. APIM no lo evita porque reenvía los
encabezados del cliente al backend.

**Qué pedir.** Que la política de APIM normalice el `User-Agent` hacia el
backend, y que se excluya del desafío la ruta `/ASMSAPI/api/v9/*` para el origen
de APIM.

**Prioridad: la más baja de la lista.** `ArandaRetryHandler` ya lo absorbe con
hasta tres intentos y lo hace invisible. Vale retomarlo si el `403` aparece con
frecuencia en los logs de producción, porque cada reintento consume el
presupuesto de `Aranda:TimeoutSeconds`.

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

## 10. Flujo de estados: confirmarlo y publicar `states` en APIM

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
3. Publicar `GET /api/v9/model/{modelId}/{itemType}/states` en el producto
   `fcintgestionaranda/v1`. Con ella el gateway lee el flujo desde Aranda en vez
   de llevar los estados escritos en su configuración, y un cambio de flujo deja
   de requerir despliegue.

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
