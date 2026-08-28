namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record CancelTicketResponse(
    string CaseNumber,
    string Status);
