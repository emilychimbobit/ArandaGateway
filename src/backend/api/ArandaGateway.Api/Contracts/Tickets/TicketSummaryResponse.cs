namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record TicketSummaryResponse(
    string CaseNumber,
    string Subject,
    string Status,
    DateTimeOffset OpenedAt);
