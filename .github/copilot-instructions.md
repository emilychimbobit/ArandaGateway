# Contexto del repositorio

Este repositorio implementará una puerta de enlace para Aranda Service Management. La solución debe ser una **Minimal API con .NET 10** y exponer únicamente las capacidades de Aranda necesarias para los requerimientos funcionales de `DEF.md`.

`DEF.md` es la fuente de verdad funcional. La colección `API-V9.postman_collection_2508` es una referencia técnica del API externo, no una especificación de endpoints que deban publicarse íntegramente.

## Comandos

Ejecutar desde la raíz del repositorio:

```powershell
# Restaurar y compilar
dotnet restore .\ArandaGateway.slnx
dotnet build .\ArandaGateway.slnx --no-restore

# Ejecutar la API
dotnet run --project .\src\backend\api\ArandaGateway.Api\ArandaGateway.Api.csproj

# Ejecutar todas las pruebas
dotnet test .\ArandaGateway.slnx --no-restore

# Ejecutar una prueba por nombre completamente calificado
dotnet test .\tests\ArandaGateway.Api.Tests\ArandaGateway.Api.Tests.csproj --filter "FullyQualifiedName~Namespace.Class.Method"

# Comprobar formato
dotnet format .\ArandaGateway.slnx --verify-no-changes
```

La solución usa `ArandaGateway.slnx`, la API apunta a `net10.0` y las pruebas usan xUnit.

## Alcance funcional de la gateway

La primera versión cubre únicamente las operaciones de Aranda asociadas a:

| Requerimiento | Capacidad pública |
|---|---|
| REQ_04 | Crear un ticket y adjuntar archivos permitidos |
| REQ_05 | Listar los tickets abiertos del colaborador |
| REQ_06 | Anular un ticket propio después de validar motivo, estado y confirmación |
| REQ_07 | Consultar el estado y avance de un ticket propio |

Contratos públicos previstos:

- `POST /api/tickets`
- `GET /api/tickets`
- `GET /api/tickets/{caseNumber}`
- `POST /api/tickets/{caseNumber}/cancellation`
- `POST /api/tickets/{caseNumber}/attachments`

No agregar endpoints públicos para otras operaciones de la colección Postman sin un requerimiento aprobado en `DEF.md`. En particular, la autenticación, renovación y cierre de sesión de Aranda son detalles internos del cliente de integración y nunca deben exponerse como passthrough.

La consulta de CIs para REQ_10 queda fuera del alcance confirmado hasta establecer si Aranda es la CMDB oficial de MINSUR. La búsqueda de usuarios y los catálogos de servicios, categorías o estados solo son dependencias internas cuando resulten necesarios para identidad, autorización o resolución de IDs.

## Arquitectura objetivo

- Alojar la Minimal API en `src/backend/api/ArandaGateway.Api`.
- Alojar sus pruebas en `tests/ArandaGateway.Api.Tests`.
- Agrupar endpoints por capacidad, evitando concentrar toda la aplicación en `Program.cs`.
- Mantener los casos de uso de creación, listado, detalle, anulación y adjuntos separados del transporte HTTP.
- Encapsular Aranda detrás de un cliente HTTP tipado y una interfaz; los endpoints no deben construir solicitudes HTTP hacia Aranda directamente.
- Separar los DTOs públicos de los modelos internos de Aranda. No filtrar al consumidor nombres de campos, tokens, estados internos ni estructuras de error del proveedor.
- Centralizar URL base, API key, IDs de proyecto/modelo/catálogos, límites y timeouts mediante Options validadas al iniciar.
- Enviar `Aranda:ApiKey` sin modificaciones en el encabezado `X-Authorization`. El secreto ya incluye el prefijo `Bearer`; no anteponer otro prefijo ni usar el encabezado estándar `Authorization`.
- No configurar ni enviar un tenant alias mientras no exista una definición posterior del cliente.
- Usar los valores confirmados `ProjectId = 1` y `AuthorId = 2` desde `ArandaOptions`; no repetirlos como literales en casos de uso o modelos.
- Usar `ProblemDetails` para errores HTTP y mantener separados el mensaje funcional en español y el detalle técnico permitido en logs.
- Propagar `CancellationToken` desde el endpoint hasta las llamadas externas.

La responsabilidad principal de cada área existente se mantiene:

- `src/backend/api/`: gateway y lógica de aplicación para Aranda.
- `src/frontend/`: canales web, Power Apps y Teams/Copilot; no colocar estas implementaciones dentro de la gateway.
- `src/ia/`: agentes, funciones, modelos y observabilidad de IA.
- `src/integration/`: otros conectores y procesos de integración de datos.
- `data/`: activos de SQL, Cosmos DB y Azure AI Search.
- `docs/`: arquitectura, diagramas, IaC, requisitos y manuales.

## Mapeo técnico hacia Aranda

Usar solo las operaciones de la colección que soporten el alcance:

- crear caso: `POST /api/v9/item/`;
- buscar casos: `POST /api/v9/item/search`;
- obtener caso: `GET /api/v9/item/{id}`;
- actualizar/anular caso: `PUT /api/v9/item/{id}`, sujeto a confirmar el contrato exacto de anulación;
- adjuntar archivo: `POST /api/v9/file/`;
- consultar avance: detalle del caso y, cuando sea necesario, `GET /api/v9/item/{id}/history/list`;
- consultar estados o transiciones: endpoints de estados del modelo, solo como dependencia interna;
- iniciar, renovar y cerrar sesión: endpoints de autenticación de Aranda, solo dentro del cliente técnico.

Los IDs y valores de ejemplo del Postman no son configuración válida. No copiar IDs de usuario, proyecto, modelo, servicio, categoría, estado, impacto, urgencia, grupo ni archivos desde sus payloads.

## Reglas funcionales obligatorias

- Aplicar fail-closed: si no puede comprobarse existencia, propiedad, estado permitido o una confirmación requerida, no consultar detalles sensibles ni ejecutar modificaciones.
- No confiar en un identificador de colaborador o ticket enviado por el cliente para autorizar acceso. La propiedad debe verificarse contra Aranda usando una identidad confiable.
- Listar únicamente tickets abiertos y devolver solo número de caso, asunto, estado y fecha de apertura.
- El detalle de un caso puede devolver estado, grupo resolutor, última actualización y solución, cuando Aranda proporcione esos datos.
- Para anular, exigir ticket existente y propio, motivo no vacío, confirmación explícita y un estado anulable. Los estados funcionalmente anulables son `Registrado/Asignado` y `En proceso`.
- No anular tickets en `Pendiente por usuario`, `Pendiente Proveedor`, `Cerrado`, `Resuelto` o `En Aprobación`.
- Para crear tickets, usar los valores funcionales definidos en REQ_04, pero resolver sus IDs técnicos desde configuración o catálogos: servicio `Por categorizar`, impacto `Bajo`, urgencia `Bajo`, categoría `Ticket creado por bot` y grupo `Mesa de Ayuda`.
- Validar adjuntos contra los formatos `xlsx`, `docx`, `ppt`, `pdf`, `png` y `jpg`. Mantener configurables la cantidad máxima y la interpretación del límite de 3 MB hasta que sean confirmadas.
- No inventar respuestas, estados, transiciones, IDs o comportamientos que no estén definidos por el DEF o confirmados en un contrato técnico.

## Seguridad y observabilidad

La autenticación de la gateway queda pendiente de definición con el cliente. Temporalmente los endpoints serán abiertos, pero:

- aislar la obtención de la identidad actual detrás de una abstracción reemplazable para incorporar posteriormente Microsoft Entra ID;
- obtener temporalmente el username mediante `X-Collaborator-Username` y resolverlo con `GET /api/v9/user/{username}/detail`; no incluir el username en DTOs públicos;
- no considerar REQ_05, REQ_06 ni REQ_07 listos para producción mientras no exista una identidad autenticada y un mapeo confiable con Aranda;
- no reenviar al consumidor credenciales ni tokens de Aranda;
- no almacenar secretos en archivos versionados;
- no registrar tokens, credenciales, cuerpos de adjuntos ni otros datos sensibles;
- sanitizar logs y respuestas de error;
- registrar trazabilidad suficiente para correlacionar una solicitud con la operación de Aranda sin exponer secretos.

## Convenciones de implementación

- Mantener nombres técnicos de C# en inglés y mensajes destinados al usuario en español.
- Usar `System.Text.Json` y las capacidades integradas de ASP.NET Core salvo que exista una necesidad comprobada de otra dependencia.
- Modelar errores esperados como resultados explícitos; no usar excepciones para control de flujo ni capturas amplias que conviertan fallos en respuestas exitosas.
- No implementar un proxy transparente. Validar y transformar todas las entradas antes de llamar a Aranda y reducir las respuestas al contrato público.
- Agregar pruebas positivas, negativas y de propiedad para cada caso de uso. Las integraciones deben probarse con un servidor HTTP simulado, nunca contra un ambiente real como parte de la suite automatizada.

## Pendientes de definición

No resolver por inferencia:

- autenticación de la gateway y mecanismo técnico de SSO;
- identificador confiable y mapeo entre colaborador y usuario de Aranda;
- vigencia y rotación de la API key técnica de Aranda;
- IDs por ambiente para modelo, servicio, categoría, impacto, urgencia, grupo y estados;
- payload y transición exactos para anular un caso;
- campos de Aranda que determinan propiedad, número de caso, grupo resolutor, actualización y solución;
- cantidad máxima de adjuntos y si los 3 MB aplican por archivo o por solicitud;
- si Aranda es la fuente de CMDB para REQ_10;
- catálogo oficial de errores `BIT2026_MINSUR-Catálogo errores_ARANDA_V1.0`.

Cuando una implementación dependa de estos puntos, usar una abstracción o configuración validada y dejar el pendiente visible; no introducir un valor silencioso.

## Flujo de ramas

El flujo documentado usa `develop` para desarrollo e integración, `qa` para validación y `main` protegida para producción.
