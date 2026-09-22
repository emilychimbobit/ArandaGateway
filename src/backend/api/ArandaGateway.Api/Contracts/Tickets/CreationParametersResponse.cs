namespace ArandaGateway.Api.Contracts.Tickets;

/// <summary>
/// Todo lo que el gateway aplica por su cuenta al crear un ticket y que el
/// colaborador no escribe: la clasificación fija del REQ_04, el prefijo que se
/// antepone al asunto y los límites que la creación hace cumplir.
///
/// <para>
/// Lo devuelve <c>GET /api/tickets/parametros-creacion</c> para el resumen que
/// el agente muestra antes de confirmar (REQ_04, paso 6). Va aparte de la
/// creación a propósito: el resumen se arma <em>antes</em> de registrar, así
/// que <c>POST /api/tickets</c> ya no lo repite.
/// </para>
/// </summary>
public sealed record RespuestaParametrosCreacion(
    RespuestaClasificacionTicket Clasificacion,
    string? PrefijoAsunto,
    RespuestaLimitesTicket Limites);

/// <summary>
/// Límites que la creación hace cumplir. Viajan en el resumen para que el
/// agente recorte el texto antes de llamar, en vez de descubrirlos con un 400.
/// </summary>
/// <param name="MaxAsunto">
/// Máximo del asunto <em>ya prefijado y codificado</em>, no del texto que
/// escribe el colaborador: con el prefijo puesto el margen es menor.
/// </param>
/// <param name="MaxDescripcion">
/// Máximo de la descripción codificada. Es política del gateway, no de Aranda.
/// </param>
/// <param name="MaxBytesAdjunto">Tamaño máximo de cada archivo adjunto.</param>
/// <param name="ExtensionesPermitidas">
/// Extensiones que acepta el adjunto, en minúsculas y con punto.
/// </param>
public sealed record RespuestaLimitesTicket(
    int MaxAsunto,
    int MaxDescripcion,
    long MaxBytesAdjunto,
    IReadOnlyList<string> ExtensionesPermitidas);
