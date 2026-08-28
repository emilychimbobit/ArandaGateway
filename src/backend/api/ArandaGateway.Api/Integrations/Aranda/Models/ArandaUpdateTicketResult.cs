namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaUpdateTicketResult
{
    public required long ItemVersion { get; init; }

    public required bool Result { get; init; }
}
