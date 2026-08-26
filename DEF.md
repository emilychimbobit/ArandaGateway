# GitHub Copilot Instructions — MINSUR AgenticAISparkLab / ARANDA
>
> **Fuente funcional:** Documento de especificaciones funcionales ARANDA — AgenticAISparkLab MINSUR, versión **2.1**, actualizado el **28/07/2026**.

## 1. Propósito de este contexto

Este repositorio forma parte del MVP del **Agente Conversacional de Mesa de Ayuda de MINSUR**. GitHub Copilot debe usar este documento como contexto funcional y como conjunto de restricciones al proponer código, flujos, validaciones, modelos, APIs, mensajes, pruebas o documentación.

### Regla principal

- El DEF es la fuente de verdad funcional para el MVP.
- No inventar reglas, catálogos, estados, campos, endpoints, credenciales, permisos ni comportamientos no definidos.
- Cuando falte un dato técnico, dejarlo parametrizable, documentar el supuesto o marcarlo como `TODO`/pendiente de definición.
- Priorizar seguridad, trazabilidad, aislamiento por usuario y mensajes comprensibles para usuarios no técnicos.

---

## 2. Resumen del proyecto

El proyecto busca construir o reingenierizar un agente conversacional de Mesa de Ayuda integrado principalmente con **Microsoft Teams**. El agente debe permitir a colaboradores de MINSUR realizar autoservicio y atención asistida mediante lenguaje natural.

Capacidades principales del MVP:

1. Autenticación e identificación del colaborador.
2. Consulta de preguntas frecuentes.
3. Solicitud y entrega de manuales de usuario.
4. Generación de tickets de incidente o requerimiento en Aranda.
5. Visualización de tickets propios.
6. Anulación de tickets propios.
7. Consulta del estado de un caso específico.
8. Desbloqueo de cuenta de red.
9. Cambio o restablecimiento de clave de red.
10. Revisión de equipos/activos asignados al colaborador.

### Alcance de entrega

- El entregable principal del MVP llega hasta **QA**.
- El despliegue a producción está a cargo del cliente, con acompañamiento del Equipo BIT.
- No se asume soporte operativo permanente ni evolutivo posterior.

---

## 3. Ecosistema e integraciones

El agente interactúa con los siguientes componentes funcionales:

- **Microsoft Teams:** canal corporativo principal y único definido para el MVP.
- **Identidad corporativa / SSO:** mecanismo definido por MINSUR; el DEF indica preferencia por Microsoft Entra ID o el mecanismo corporativo aprobado.
- **Aranda Service Management:** creación, consulta, detalle y anulación de tickets.
- **SharePoint:** repositorio documental corporativo para preguntas frecuentes y manuales.
- **IA Search / buscador de conocimiento:** el documento PDF de preguntas frecuentes alimenta la búsqueda de FAQ.
- **CMDB / inventario de activos:** consulta de equipos asignados al colaborador.
- **Autoservicio AD / integración de cuenta de red:** desbloqueo y cambio/restablecimiento de contraseña.
- **OTP por SMS:** verificación reforzada para acciones sensibles.
- **Logs:** trazabilidad de interacciones y operaciones.

### Importante sobre logging

El DEF menciona **Logs de Power Platform** en algunos requerimientos y **Logs de Azure Function** en otros, mientras que los requisitos no funcionales priorizan Azure Function para acciones técnicas. No unificar esta diferencia por cuenta propia: respetar la arquitectura vigente del componente o dejar el destino de logging configurable hasta que sea confirmado.

---

## 4. Principios funcionales obligatorios

### 4.1 Identidad y autorización

- Toda operación debe estar asociada al colaborador autenticado.
- El usuario solo puede consultar o modificar información propia.
- Está prohibido mostrar, consultar, anular o ejecutar acciones sobre cuentas, tickets o activos de terceros.
- Si no puede validarse la propiedad del recurso, la operación debe fallar de forma segura y no revelar detalle.

### 4.2 Acciones sensibles

Para desbloqueo de cuenta y cambio/restablecimiento de contraseña:

- Requerir validación de identidad.
- Requerir verificación reforzada mediante OTP.
- Si la verificación falla, no ejecutar la acción.
- No almacenar, registrar ni exponer contraseñas o credenciales.

### 4.3 Confirmación antes de transacciones críticas

Solicitar confirmación explícita antes de ejecutar operaciones críticas, especialmente:

- creación de tickets cuando ya se ha reunido la información necesaria;
- anulación de tickets;
- cualquier modificación sensible.

El flujo debe presentar un resumen entendible antes de la confirmación cuando aplique.

### 4.4 Respuestas basadas en conocimiento

- Para FAQ y documentos, responder únicamente con contenido autorizado y disponible.
- No inventar respuestas cuando no exista información suficiente.
- Si la consulta no puede resolverse, ofrecer generación de ticket/escalamiento a Mesa de Ayuda.

### 4.5 Experiencia de usuario

- Mensajes en español.
- Lenguaje claro y orientado a usuarios no técnicos.
- Evitar mensajes técnicos de integración, stack traces, códigos internos o detalles innecesarios.
- Comunicar siempre resultado, restricción, error y siguiente paso cuando corresponda.
- Solicitar solo la información mínima requerida para ejecutar la operación.

### 4.6 Trazabilidad

- Registrar consultas y acciones para auditoría, seguimiento y soporte.
- No incluir contraseñas, credenciales ni información sensible en logs.
- Los errores de integración deben quedar trazados técnicamente aunque el mensaje al usuario sea simplificado.

---

# 5. Requerimientos funcionales

## REQ_01 — Autenticación e identificación del colaborador

### Objetivo

Identificar al colaborador que interactúa desde Microsoft Teams utilizando su identidad corporativa y asociar la sesión conversacional con dicho usuario.

### Reglas

- Usar el contexto de identidad corporativa/SSO de MINSUR.
- Permitir únicamente consultas y operaciones sobre información propia.
- La identidad debe poder utilizarse para relacionar al usuario con tickets, cuentas y activos.
- Para desbloqueo o cambio/restablecimiento de clave, aplicar OTP antes de ejecutar la acción.

### Funcionalidades habilitadas por identidad

- Cambio de contraseña.
- Gestión de tickets: creación, listado, detalle y anulación.
- Búsqueda de manuales.
- Preguntas frecuentes.

### Criterios mínimos de aceptación

- Se identifica correctamente al colaborador autenticado.
- No se permite acceder a información de terceros.
- Si la verificación requerida falla, no se ejecuta la acción.
- La operación queda trazada.

---

## REQ_02 — Consulta de preguntas frecuentes

### Objetivo

Permitir consultas frecuentes de Mesa de Ayuda en lenguaje natural y responder desde una base de conocimiento administrable.

### Fuente de conocimiento

- Documento de preguntas frecuentes en formato **PDF**.
- El PDF alimenta el mecanismo de IA Search/búsqueda definido para la solución.
- Puede consultarse contenido autorizado en SharePoint cuando aplique.

### Comportamiento esperado

1. Recibir la pregunta en lenguaje natural desde Teams.
2. Identificar la intención.
3. Buscar una respuesta en la base de conocimiento autorizada.
4. Responder de forma clara y no técnica.
5. Solicitar feedback de resolución mediante pulgar arriba/pulgar abajo.
6. Si no existe respuesta suficiente o el usuario indica que no fue resuelto, ofrecer ticket/escalamiento mediante REQ_04.
7. Registrar la consulta para analítica, seguimiento y mejora de la base de conocimiento.

### Restricción crítica

Si la información disponible es insuficiente, **no generar contenido inventado**.

---

## REQ_03 — Solicitud y entrega de manuales de usuario

### Objetivo

Permitir que el colaborador solicite manuales de aplicativos corporativos y reciba la documentación vigente desde SharePoint.

### Flujo

1. El usuario solicita un manual desde Teams.
2. Identificar el aplicativo por nombre formal o nombre común interno.
3. Buscar documentación relacionada en el repositorio autorizado de SharePoint.
4. Cuando exista documentación vigente, entregar la referencia/enlace correspondiente al documento relacionado con la consulta.
5. Si no existe o no está vigente, informar la situación y ofrecer ticket/escalamiento mediante REQ_04.

### Restricciones

- Solo entregar documentación autorizada.
- No generar un manual sustituto si el documento no existe.
- La actualización y mantenimiento de manuales corresponde al responsable funcional de MINSUR, no al agente.

---

## REQ_04 — Generación de tickets en Aranda

### Objetivo

Permitir que el colaborador registre un incidente o requerimiento en Aranda Service Management desde Teams sin ingresar al portal tradicional.

### Flujo funcional

1. Detectar que el usuario desea generar un ticket.
2. Capturar la necesidad en lenguaje natural.
3. Si la información es insuficiente, solicitar una descripción con mayor detalle.
4. Aplicar la clasificación definida para el MVP.
5. Solicitar únicamente los datos mínimos faltantes.
6. Presentar un resumen y confirmar antes del registro final.
7. Crear el ticket mediante APIs seguras de Aranda.
8. Asignar el grupo resolutor definido para Mesa de Ayuda/Mesa de Servicios.
9. Informar el número de caso generado y el resultado.
10. Registrar trazabilidad técnica.

### Clasificación fija indicada por el DEF

Usar el siguiente catálogo mientras no exista una definición posterior aprobada:

| Campo | Valor |
|---|---|
| Servicio | Por categorizar |
| Impacto | Bajo |
| Urgencia | Bajo |
| Categoría | Ticket creado por bot |
| Grupo | Mesa de Ayuda |

No inventar IDs de Aranda para estos valores. Los IDs reales deben provenir de configuración, catálogo o integración.

### Adjuntos

Formatos permitidos:

- `xlsx`
- `docx`
- `ppt`
- `pdf`
- `png`
- `jpg`

Reglas indicadas:

- Se permite enviar **N archivos por solicitud**.
- El DEF establece un máximo de **3 MB**, como medida preventiva por limitaciones de transporte entre Copilot Studio, Power Platform, conectores y Azure Function.
- El DEF no define el valor de `N`; no asumirlo.
- El DEF no aclara de forma inequívoca si los 3 MB son por archivo o por solicitud completa; mantener la regla configurable y confirmar antes de fijarla en código.
- Cuando el documento supere el límite permitido, indicar que debe gestionarse por correo a `mesadeayuda@divisionminera.com`.

### Manejo de errores

- Existe una dependencia externa: **ANEXO `BIT2026_MINSUR-Catálogo errores_ARANDA_V1.0`**.
- Los mensajes de error visibles al usuario se definen inicialmente y no son configurables en el MVP.
- Evitar mensajes técnicos.
- Registrar el intento fallido para trazabilidad.

### Restricciones

- No registrar el ticket si faltan datos mínimos obligatorios.
- No modificar el core de Aranda.
- La calidad de la clasificación depende de los catálogos y reglas disponibles para el MVP.

---

## REQ_05 — Visualización de tickets del colaborador

### Objetivo

Mostrar al colaborador autenticado sus tickets asociados en Aranda.

### Información visible

Mostrar únicamente tickets **abiertos** y los siguientes campos:

- Número de caso.
- Asunto.
- Estado.
- Fecha de apertura.

### Reglas

- Consultar solo tickets del usuario autenticado.
- El usuario no puede filtrar por estado o fecha en el MVP.
- Si no existen tickets, informarlo claramente.
- La consulta es de solo lectura y no modifica Aranda.

---

## REQ_06 — Anulación de tickets propios

### Objetivo

Permitir que el colaborador anule un ticket propio cuando el estado sea anulable y Aranda permita la operación.

### Datos de entrada

- Solicitud en lenguaje natural.
- Número de ticket.
- Motivo de anulación en texto libre.
- Confirmación explícita.

### Validaciones obligatorias

Antes de anular, validar que:

1. el ticket exista;
2. pertenezca al usuario autenticado;
3. esté en un estado anulable;
4. el usuario haya indicado un motivo;
5. el usuario confirme explícitamente la acción.

### Estados anulables definidos

- Registrado/Asignado.
- En proceso.

### Estados no anulables definidos

- Pendiente por usuario.
- Pendiente Proveedor.
- Cerrado.
- Resuelto.
- En Aprobación.

### Restricción crítica

Si el usuario no confirma, el ticket **no debe anularse**.

---

## REQ_07 — Consulta de estado de un caso específico

### Objetivo

Permitir consultar el estado y avance de un ticket específico del usuario autenticado.

### Formas de selección

- Ingresar el número de caso.
- Seleccionar un caso desde el listado de tickets propios.

### Validaciones

- El caso debe existir.
- Debe pertenecer al colaborador autenticado.
- Si no puede validarse la propiedad, no mostrar detalle.

### Información a mostrar cuando esté disponible

- Estado.
- Grupo resolutor.
- Última actualización.
- Solución.

La consulta es de solo lectura.

---

## REQ_08 — Desbloqueo de cuenta de red

### Objetivo

Permitir el desbloqueo de la cuenta de red del colaborador desde Teams mediante un proceso seguro de autoservicio.

### Flujo obligatorio

1. Validar identidad corporativa.
2. Aplicar verificación reforzada mediante OTP.
3. Consultar el estado de la cuenta de red.
4. Si la cuenta está bloqueada y la integración lo permite, ejecutar el desbloqueo.
5. Confirmar el resultado.
6. Si no puede completarse, informar la situación y ofrecer ticket/escalamiento.
7. Registrar trazabilidad.

### Reglas

- El OTP se envía por SMS al celular registrado.
- La operación solo puede ejecutarse cuando la verificación sea exitosa.
- Si la cuenta no está bloqueada, informar el estado y no realizar cambios.
- No almacenar credenciales ni contraseñas.

---

## REQ_09 — Cambio o restablecimiento de clave de red

### Objetivo

Permitir que el colaborador cambie o restablezca su clave de red desde Teams con verificación reforzada.

### Flujo obligatorio

1. Validar identidad corporativa.
2. Generar/enviar OTP por SMS al celular registrado.
3. Validar vigencia/corrección del código.
4. Solicitar la nueva clave mediante un mecanismo seguro.
5. Validar la política de complejidad definida en el DEF.
6. Ejecutar el cambio/restablecimiento solo si la verificación fue exitosa.
7. Confirmar el resultado sin mostrar la contraseña.
8. Si falla, ofrecer ticket/escalamiento.
9. Registrar la acción sin datos sensibles.

### Política de complejidad indicada por el DEF

- Mínimo **8 caracteres**.
- Debe cumplir los 4 criterios:
  - una mayúscula;
  - una minúscula;
  - un número;
  - un carácter especial de los definidos en el DEF: `@`, `#`, `+`, `$`.

No ampliar ni cambiar esta política sin una definición funcional aprobada.

### Restricciones críticas

- No almacenar la contraseña.
- No registrar la contraseña en logs.
- No mostrar la contraseña en mensajes o trazas.
- Si no cumple la política, no ejecutar el cambio y solicitar una nueva clave o informar la restricción.

---

## REQ_10 — Revisión de equipos asignados

### Objetivo

Permitir que el colaborador consulte los equipos y activos de TI asignados a su identidad.

### Fuente

- Inventario / CMDB de MINSUR.

### Campos visibles definidos

- Tipo.
- Modelo.
- Código.
- Estado.

### Reglas

- Consultar únicamente activos del usuario autenticado.
- No mostrar activos de terceros.
- La función es de solo lectura: no actualizar, reasignar ni modificar activos.
- Si no existen activos asociados, informarlo.
- Si el usuario detecta una inconsistencia, ofrecer generación de ticket/escalamiento mediante REQ_04.
- Si la fuente no está disponible o no puede validarse la propiedad, no mostrar detalle.

---

# 6. Requerimientos no funcionales

## 6.1 Seguridad

El código y los flujos generados deben respetar lo siguiente:

- Autenticación mediante identidad corporativa.
- Mínimo privilegio.
- Acceso únicamente a recursos propios del colaborador.
- OTP para acciones sensibles.
- No almacenar ni exponer contraseñas, credenciales o información sensible.
- Integraciones mediante canales seguros y cifrado en tránsito.
- Credenciales técnicas autorizadas.
- Confirmación explícita para transacciones críticas.
- Registro de acciones sin datos sensibles.

## 6.2 Usabilidad

- Experiencia conversacional clara y en español.
- Mensajes comprensibles para usuarios no técnicos.
- Solicitar solo datos mínimos necesarios.
- Explicar errores/restricciones y siguientes pasos.
- Cuando no se pueda resolver, ofrecer ticket/escalamiento.
- Evitar ambigüedades en operaciones críticas mediante resumen + confirmación.
- Mantener consistencia de interacción en Microsoft Teams.

## 6.3 Disponibilidad y operación

- La disponibilidad objetivo depende de Power Platform/Microsoft; el DEF no define un porcentaje contractual específico.
- El tiempo de respuesta debe ser mínimo en condiciones normales, pero el DEF no define un SLA numérico.
- La solución debe considerar una base referencial de **2.000 colaboradores**.
- Debe soportar la concurrencia esperada en horas pico sin degradación significativa.
- Las integraciones con Aranda, SharePoint y CMDB deben ser seguras, controladas y trazables.

---

# 7. Reglas para generación de código con Copilot

Cuando Copilot proponga implementación para este proyecto:

1. **No hardcodear credenciales, tokens, secretos, IDs de catálogo ni URLs sensibles.** Usar configuración/variables de entorno/secret stores según la arquitectura del repositorio.
2. **No confiar en IDs enviados por el cliente para autorizar acceso.** Validar siempre la relación entre el usuario autenticado y el recurso solicitado.
3. **Aplicar fail-closed:** si falla identidad, autorización, validación de propiedad u OTP, no ejecutar la operación.
4. **No registrar contraseñas ni OTPs en texto plano.**
5. **Sanitizar logs y errores** para evitar exposición de información sensible.
6. **Separar mensaje técnico de mensaje de usuario:** el usuario recibe una explicación clara; el log conserva el detalle técnico permitido.
7. **No ejecutar operaciones destructivas sin confirmación** cuando el DEF lo exige.
8. **No crear comportamiento alternativo silencioso** cuando falla una integración. Informar y derivar al flujo de ticket/escalamiento cuando corresponda.
9. **No modificar recursos de terceros** aunque el usuario proporcione manualmente un identificador válido.
10. **Mantener reglas de negocio parametrizables** cuando el DEF no proporciona IDs técnicos, endpoints, número máximo de adjuntos, SLA o valores de ambiente.
11. **No asumir que el nombre visible de un catálogo equivale a su ID técnico.** Resolver IDs desde configuración o servicio de catálogo.
12. **Para consultas documentales, no alucinar contenido.** Si no hay evidencia suficiente, devolver no-resuelto y habilitar escalamiento.

---

# 8. Casos que siempre deben probarse

Copilot debe considerar pruebas positivas, negativas y de autorización para cada funcionalidad.

## Identidad/autorización

- Usuario autenticado consulta sus propios datos.
- Usuario intenta consultar ticket/activo/cuenta de un tercero.
- No existe mapeo de identidad con Aranda/CMDB.

## FAQ/documentos

- Respuesta encontrada.
- Respuesta insuficiente.
- Documento inexistente.
- Documento no autorizado/no vigente.
- Feedback negativo del usuario y derivación a ticket.

## Creación de ticket

- Descripción suficiente.
- Descripción insuficiente y solicitud de detalle.
- Confirmación positiva.
- Confirmación cancelada.
- Error de API Aranda.
- Adjuntos permitidos.
- Archivo que supera el límite configurado.
- Formato no permitido.

## Visualización/estado de tickets

- Usuario con tickets abiertos.
- Usuario sin tickets.
- Ticket inexistente.
- Ticket perteneciente a otro usuario.
- API no disponible.

## Anulación

- Ticket propio en estado anulable.
- Ticket propio en estado no anulable.
- Ticket de tercero.
- Usuario no confirma.
- Error durante la anulación.

## Autoservicio AD

- OTP válido.
- OTP inválido/vencido.
- Cuenta no bloqueada en flujo de desbloqueo.
- Error de integración.
- Contraseña válida.
- Contraseña que no cumple complejidad.
- Verificación fallida: nunca ejecutar la acción.

## Activos

- Usuario con activos.
- Usuario sin activos.
- Activo de tercero.
- CMDB no disponible.
- Asociación identidad-activo no verificable.

---

# 9. Dependencias y puntos no definidos por el DEF

Copilot no debe completar estos puntos por inferencia. Deben resolverse mediante configuración, documentación técnica adicional o definición funcional:

- Contratos/endpoints exactos de Aranda.
- IDs técnicos de servicio, impacto, urgencia, categoría y grupo.
- Mapeo exacto de identidad corporativa con usuarios de Aranda y CMDB.
- Contratos/endpoints exactos del Autoservicio AD.
- Mecanismo técnico exacto de SSO.
- Valor de `N` para cantidad máxima de archivos adjuntos.
- Si el límite de 3 MB aplica por archivo o al total de la solicitud.
- SLA/SLO numérico de disponibilidad y tiempo de respuesta.
- Destino único de logging cuando haya diferencia entre Power Platform y Azure Function.
- Contenido del anexo `BIT2026_MINSUR-Catálogo errores_ARANDA_V1.0`.
- IDs, nombres y valores específicos por ambiente DEV/QA/PROD.
- Cualquier regla no expresamente documentada en el DEF.

Cuando una implementación dependa de uno de estos puntos, preferir una abstracción/configuración y dejar claramente indicado qué valor debe ser proporcionado.

---

# 10. Criterio de finalización para cambios de código

Un cambio relacionado con este MVP se considera funcionalmente alineado cuando:

- respeta el requerimiento REQ correspondiente;
- valida identidad y propiedad del recurso;
- no expone datos sensibles;
- maneja errores sin revelar detalles técnicos al usuario;
- conserva trazabilidad técnica;
- no inventa reglas o datos faltantes;
- incluye pruebas para caminos exitosos, errores y accesos no autorizados;
- mantiene el alcance de solo lectura en funcionalidades que no deben modificar Aranda/CMDB;
- implementa confirmación explícita donde el DEF la exige;
- ofrece escalamiento/ticket cuando la funcionalidad no puede resolverse automáticamente.

---

## Referencia rápida de requerimientos

| ID | Funcionalidad | Sistema principal |
|---|---|---|
| REQ_01 | Autenticación e identificación | Teams + identidad corporativa |
| REQ_02 | Preguntas frecuentes | IA Search + PDF/SharePoint |
| REQ_03 | Manuales de usuario | SharePoint |
| REQ_04 | Crear ticket | Aranda Service Management |
| REQ_05 | Listar tickets propios | Aranda Service Management |
| REQ_06 | Anular ticket propio | Aranda Service Management |
| REQ_07 | Estado de caso específico | Aranda Service Management |
| REQ_08 | Desbloquear cuenta de red | Autoservicio AD + OTP |
| REQ_09 | Cambiar/restablecer clave | Autoservicio AD + OTP |
| REQ_10 | Consultar equipos asignados | CMDB / inventario |

