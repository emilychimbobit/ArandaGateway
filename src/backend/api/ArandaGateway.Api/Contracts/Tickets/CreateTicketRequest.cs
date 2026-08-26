namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record CreateTicketRequest(
    string Subject,
    string Description);
