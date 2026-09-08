namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record RespuestaDetalleTicket(
    string CaseNumber,
    string Status,
    string? ResolverGroup,
    DateTimeOffset? LastUpdatedAt,
    string? Solution);
