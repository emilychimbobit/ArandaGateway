namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record RespuestaResumenTicket(
    string CaseNumber,
    string Subject,
    string Status,
    DateTimeOffset OpenedAt);
