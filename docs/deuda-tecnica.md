# Deuda técnica

Trabajo pendiente que se resuelve con código de este repositorio, sin depender
de terceros. Cada punto puede tomarse y cerrarse hoy.

Lo que está bloqueado en el cliente —publicaciones en APIM, credenciales, datos
de catálogo, decisiones de arquitectura— está en
[`consultas-al-cliente.md`](consultas-al-cliente.md).

Estado al 15 de septiembre de 2026.

## Resumen

| # | Pendiente | Tipo | Prioridad |
| --- | --- | --- | --- |
| 1 | `/admin/aranda-session` sin protección | Seguridad | Bloquea producción |
| 2 | `solution` nunca se llena (REQ_07 incompleto) | Requisito | Alta |
| 3 | La anulación no deja rastro en el log | Trazabilidad | Alta |
| 4 | ~~Una cookie vencida tumba operaciones~~ — resuelto con el interruptor | Robustez | Cerrado |
| 5 | `subject` ausente en el detalle del ticket | Contrato | Media |
| 6 | `Trim()` inconsistente entre filtros de estado | Corrección | Baja |
| 7 | La cookie viva no sobrevive al reinicio | Robustez | Baja |
| 8 | Un test fija un valor que Aranda ignora | Pruebas | Baja |

Los puntos 9 y 10 son trabajo preparado que arranca cuando el cliente
desbloquee lo suyo.

---

## 1. `/admin/aranda-session` es anónimo

La ruta se implementó con una clave de autorización (`X-Admin-Key`) y esa
protección **se retiró por decisión del equipo** el 11 de septiembre de 2026.

El App Service responde desde internet: se comprobó llamando a
`https://ase-gestionaranda-dev.azurewebsites.net/health` sin VPN y sin pasar por
APIM. Con la ruta abierta, cualquiera que conozca la URL puede:

- instalar la cookie con la que la gateway opera contra Aranda, de modo que el
  servicio pase a actuar con la sesión de un tercero;
- dejarla inoperativa instalando una cookie inválida;
- **leer la cookie viva** por `GET /admin/aranda-session/value` y suplantar a la
  cuenta de servicio contra Aranda, con acceso a casos reales.

**Sobre la lectura de la cookie.** Se agregó el 15 de septiembre de 2026 por
decisión del equipo, sabiendo que expone una credencial por una ruta anónima.
El motivo es concreto: Aranda rota la cookie en cada respuesta y la viva solo
existe en la memoria del proceso, así que un despliegue la perdía sin forma de
recuperarla. Ese día eso bloqueó el despliegue de un arreglo ya probado. La
lectura queda registrada en el log con un `LogWarning` por cada consulta.

Con `Aranda:SessionCookieEnabled` en `false` la superficie es menor —no hay
sesión que leer ni robar— pero la ruta sigue expuesta para cuando se encienda.

Es distinto del resto de los endpoints anónimos, que solo leen con una
credencial fija y no pueden alterarla.

**Cuánto pesa hoy.** Con `Aranda:SessionCookieEnabled` en `false` el gateway
opera sin sesión, así que estas rutas quedan en reserva y no hay credencial que
robar por ellas. Eso baja el riesgo real, pero no lo cierra: siguen anónimas y
expuestas, y el día que la sesión se encienda vuelven a ser la vía de operación
—una por despliegue y por reinicio— con la cookie viva legible desde internet.
Una ruta anónima que entrega credenciales no debería quedar así de forma
indefinida.

**Mitigaciones,** por orden de menor fricción:

1. Restringir `/admin/*` por IP con las reglas de acceso del App Service. No
   requiere código ni recordar ningún encabezado. Depende de quien administra el
   App Service.
2. Reponer la clave de autorización. El código está en el historial:
   `git show ed32658 -- src/backend/api/ArandaGateway.Api/Endpoints/AdminEndpoints.cs`.

**No debe llegar así a producción con usuarios reales.**

---

## 2. `solution` nunca se llena

`RespuestaDetalleTicket` expone `Solution`, pero `GetTicketDetailAsync` lo pasa
como `null` fijo (`TicketService.cs:191`). `TicketServiceTests.cs:24` congela ese
comportamiento con `Assert.Null`.

REQ_07 pide mostrar la solución cuando esté disponible, así que el requisito
está incompleto.

**Qué hace falta.** `ArandaTicket` ya deserializa `CommentaryNoHtml`
(`ArandaTicket.cs:25`) y hoy nadie lo lee. Falta confirmar contra un ticket
resuelto real que ese sea el campo donde Aranda deja la solución —en `RF-58501`
llegó vacío, pero fue cancelado, no resuelto— y luego mapearlo, ajustando el
test.

---

## 3. La anulación no deja rastro en el log

`CancelTicketAsync` (`TicketService.cs:194-281`) no escribe ninguna entrada de
log. El único log del servicio es el aviso del parche de usuario fijo
(`TicketService.cs:351`).

Application Insights registra la petición HTTP automáticamente, así que quedan
el momento, el código de respuesta y la IP de origen, pero no quién pidió la
anulación: el encabezado `X-Collaborator-Username` no se registra.

Esto salió a la luz investigando quién había cancelado `RF-58501`. Se pudo
responder solo porque Aranda guarda `modifierName`; desde los logs del gateway
era imposible.

**Qué hace falta.** Un `LogInformation` en el camino de cancelación con número
de caso, colaborador del encabezado y usuario efectivo en Aranda. Media hora de
trabajo. Conviene extenderlo a creación y adjuntos por el mismo motivo.

---

## 4. Una cookie vencida es peor que ninguna — resuelto con el interruptor

Se observó que una petición **sin** cookie puede funcionar mientras la misma
petición **con una cookie vencida** falla con `401`. El gateway, al arrancar con
la semilla caducada, la enviaba y recibía `401`: arrastraba la cookie vencida y
rompía operaciones que sin ella habrían funcionado.

**Medido el 15 de septiembre de 2026.** La sospecha de que "las consultas sí
exigen cookie" era falsa. Llamando sin cookie, solo con el token de
`Aranda:ApiKey`:

- búsqueda de casos directo a Aranda: `200` en 8 de 8;
- búsqueda por APIM: `200` en 9 de 10, el fallo restante por el desafío de
  Cloudflare;
- adjuntar archivo por APIM: `200`;
- `GET user/{username}/detail` directo: `200`.

**Cómo quedó.** Se agregó `Aranda:SessionCookieEnabled`, hoy en `false`: el
gateway no envía la semilla, no adopta el `Set-Cookie` de las respuestas y no
ejecuta el latido. La adopción también se apaga a propósito, porque si no el
interruptor se encendería solo con la primera respuesta de Aranda y volvería a
arrastrar una cookie que luego caduca.

**Lo que queda.** No se implementó el descarte automático del `401` de sesión
con reintento sin cookie. Con el interruptor apagado no hace falta; volvería a
ser necesario si alguien enciende la sesión.

---

## 5. `subject` ausente en el detalle del ticket

`RespuestaResumenTicket` incluye el asunto; `RespuestaDetalleTicket` no. Quien
consulta un caso por su número no recibe de qué trata.

Aranda ya lo devuelve y `ArandaTicket.Subject` ya lo deserializa: es agregar la
propiedad al record y al mapeo.

Conviene confirmar con el cliente si REQ_07 lo admite, ya que la lista de campos
del DEF no lo nombra.

---

## 6. `Trim()` inconsistente entre filtros de estado

El filtro de tickets abiertos normaliza con `Trim()` antes de comparar
(`TicketService.cs:554`). La validación de estados anulables no lo hace
(`TicketService.cs:227`).

Un `"En proceso "` con espacio final devuelto por Aranda pasaría como abierto y
fallaría como no anulable, con un `409` inexplicable para el usuario.

No se ha observado en la práctica. Arreglo de una línea.

---

## 7. La cookie viva no sobrevive al reinicio

`ArandaSessionCookieHandler` adopta la cookie que Aranda renueva en cada
respuesta y `ArandaSessionKeepAliveService` la toca cada 5 minutos para que no
caduque por inactividad. Verificado: la sesión sobrevivió 15 minutos sin tráfico
de usuarios, y el gateway seguía respondiendo `200` con una cookie rotada cuando
la semilla de configuración ya daba `401`.

Pero la cookie viva está solo en memoria. En cada arranque se vuelve a leer la
semilla de configuración, que a los ~10 minutos sin uso ya está vencida. Afecta
a cada despliegue, reinicio o reciclaje del App Service, y a cada instancia
nueva si se escala horizontalmente.

**Paliativo en uso.** `PUT /admin/aranda-session` instala la cookie en caliente,
sin reiniciar ni redesplegar. Nació de un problema concreto: el despliegue tarda
unos 7 minutos, más que la vida de la cookie, así que pasarla por configuración
obliga a coordinar el cambio en una ventana que casi nunca se alcanza.

**Mejora posible.** Persistir la cookie rotada fuera del proceso —archivo en
almacenamiento persistente, o Redis/Blob con varias instancias— para que un
reinicio corto la recupere en vez de caer a la semilla. No cubre paradas largas.

**Ojo.** Esto dejó de ser urgente el 15 de septiembre de 2026, por dos motivos.

El primero es que **la cookie ya no es necesaria**: con
`Aranda:SessionCookieEnabled` en `false` no hay sesión que perder en un
reinicio (ver punto 4). El problema solo reaparece si alguien la enciende.

El segundo es que ahora la sesión viva **se puede leer** con
`GET /admin/aranda-session/value`, así que el procedimiento ante un despliegue
es leerla, desplegar y reinstalarla con el `PUT`. No es persistencia
automática, pero cubre el caso que bloqueaba: ese día hubo un arreglo probado
que no se pudo desplegar porque la cookie viva era irrecuperable.

Persistirla fuera del proceso sigue siendo la solución completa, y sería lo
correcto el día que la sesión vuelva a hacer falta de forma permanente.

---

## 8. Un test fija un valor que Aranda ignora

`TicketServiceTests.cs:139` asegura `Assert.Equal(2, client.LastCreateRequest?.AuthorId)`
y `README.md:148` describe `AuthorId = 2` como "confirmado".

Aranda descarta ese campo y usa el dueño de la sesión: `RF-58501` volvió con
`authorId: 15036` (`Especialistatoken`). El detalle está en el punto 4 de
`consultas-al-cliente.md`.

El test es verde y correcto —el gateway sí envía lo configurado— pero da la
impresión de que el valor controla al autor real. Conviene un comentario en el
test y una nota en el README que digan que el campo es inerte, para que nadie
vuelva a perder tiempo persiguiéndolo.

---

## 9. Retiro del parche de usuario fijo

*Arranca cuando se resuelva el punto 1 de `consultas-al-cliente.md`.*

La resolución del colaborador está reemplazada por un usuario fijo: `1562`
(`evelyn.nunez@minsur.com`) para equipos y `15019` (`uebit20@minsur.com`) para
tickets. El encabezado `X-Collaborator-Username` se registra en el log pero se
ignora.

**Riesgo.** Cualquier colaborador ve los equipos de Evelyn y opera los tickets
de uebit20. **No debe llegar a producción con usuarios reales.**

Apagarlo no requiere código: basta `Aranda:UserOverride:Enabled = false`. El
retiro definitivo sí, y borra `ArandaUserOverrideOptions.cs`, la propiedad
`ArandaOptions.UserOverride`, los bloques marcados `PARCHE TEMPORAL` en
`EquipoService` y `TicketService`, y sus pruebas.

Al retirarlo aparece el problema de `UnitId` descrito en el punto 6 de
`consultas-al-cliente.md`: conviene resolver ambos en la misma tanda.

---

## 10. Causal de anulación

*Arranca cuando se resuelva el punto 5 de `consultas-al-cliente.md`.*

`ArandaUpdateTicketRequest` no tiene campo `reasonId`, así que las anulaciones
del bot quedan sin causal tipificada en Aranda.

Con el ID del catálogo en mano es agregar la propiedad al record y pasarla desde
`CancelTicketAsync`. El motivo en texto libre sigue yendo en `Commentary`.
