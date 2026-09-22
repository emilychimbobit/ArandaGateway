using ArandaGateway.Api.Contracts.Tickets;

namespace ArandaGateway.Api.Application.Tickets;

public interface ITicketService
{
    Task<TicketOperationResult<RespuestaCrearTicket>> CreateTicketAsync(
        SolicitudCrearTicket request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Todo lo que el gateway aplicará al crear el ticket sin que el
    /// colaborador lo escriba: clasificación fija, prefijo del asunto y
    /// límites. No consulta Aranda —sale de la configuración—, y por eso sirve
    /// para el resumen que el agente muestra antes de confirmar el registro.
    /// </summary>
    RespuestaParametrosCreacion GetCreationParameters();

    Task<TicketOperationResult<IReadOnlyList<RespuestaResumenTicket>>>
        ListOpenTicketsAsync(CancellationToken cancellationToken);

    Task<TicketDetailResult> GetTicketDetailAsync(
        string caseNumber,
        CancellationToken cancellationToken);

    Task<TicketOperationResult<RespuestaAnularTicket>> CancelTicketAsync(
        string caseNumber,
        SolicitudAnularTicket request,
        CancellationToken cancellationToken);

    Task<TicketOperationResult<RespuestaAdjuntarArchivo>>
        UploadAttachmentAsync(
            string caseNumber,
            TicketAttachment attachment,
            CancellationToken cancellationToken);
}
