namespace ArandaGateway.Api.Contracts.Tickets;

/// <summary>
/// Clasificación fija del REQ_04 con la que el gateway crea todo ticket. Viaja
/// dentro de <see cref="RespuestaParametrosCreacion"/>, que devuelve
/// <c>GET /api/tickets/parametros-creacion</c> para el resumen previo a la
/// confirmación.
/// </summary>
public sealed record RespuestaClasificacionTicket(
    string Servicio,
    string Impacto,
    string Urgencia,
    string Categoria,
    string Grupo);
