namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record CreateTicketRequest(
    TicketKind Type,
    string Subject,
    string Description);
