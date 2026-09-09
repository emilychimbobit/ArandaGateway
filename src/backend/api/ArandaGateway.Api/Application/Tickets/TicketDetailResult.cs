using ArandaGateway.Api.Contracts.Tickets;

namespace ArandaGateway.Api.Application.Tickets;

public sealed record TicketDetailResult(
    TicketDetailResultStatus Status,
    RespuestaDetalleTicket? Ticket = null);

public enum TicketDetailResultStatus
{
    Success,
    MissingCollaborator,
    NotFoundOrNotOwned
}
