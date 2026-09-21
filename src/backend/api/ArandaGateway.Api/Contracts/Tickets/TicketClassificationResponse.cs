namespace ArandaGateway.Api.Contracts.Tickets;

/// <summary>
/// Clasificación fija del REQ_04 con la que el gateway crea todo ticket. La
/// devuelve <c>GET /api/tickets/clasificacion</c> para el resumen que el
/// agente muestra antes de confirmar, y viaja también en la respuesta de
/// creación para que el mensaje posterior diga lo que realmente se aplicó.
/// </summary>
public sealed record RespuestaClasificacionTicket(
    string Servicio,
    string Impacto,
    string Urgencia,
    string Categoria,
    string Grupo);
