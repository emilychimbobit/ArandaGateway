using ArandaGateway.Api.Contracts.Tickets;

namespace ArandaGateway.Api.Application.Tickets;

public interface ITicketService
{
    Task<TicketOperationResult<RespuestaCrearTicket>> CreateTicketAsync(
        SolicitudCrearTicket request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clasificación fija con la que se creará el ticket. No consulta Aranda:
    /// sale de la configuración, y por eso sirve para el resumen que el agente
    /// muestra antes de confirmar el registro.
    /// </summary>
    RespuestaClasificacionTicket GetClassification();

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
