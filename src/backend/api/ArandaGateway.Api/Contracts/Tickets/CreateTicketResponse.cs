namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record CreateTicketResponse(
    string CaseNumber,
    string Status);
