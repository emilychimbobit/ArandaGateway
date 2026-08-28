namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record TicketDetailResponse(
    string CaseNumber,
    string Status,
    string? ResolverGroup,
    DateTimeOffset? LastUpdatedAt,
    string? Solution);
