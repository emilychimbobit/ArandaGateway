namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record CancelTicketRequest(
    string Reason,
    bool Confirmed);
