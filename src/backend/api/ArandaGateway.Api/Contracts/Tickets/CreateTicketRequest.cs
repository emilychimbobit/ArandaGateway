namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record SolicitudCrearTicket(
    TicketKind Type,
    string Subject,
    string Description);
