namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCreatedTicket
{
    public required long Id { get; init; }

    public required string IdByProject { get; init; }
}
