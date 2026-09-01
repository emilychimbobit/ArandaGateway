namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaUpdateTicketRequest
{
    public required long CategoryId { get; init; }

    public string ConsoleType { get; init; } = "specialist";

    public required long ItemType { get; init; }

    public required long ItemVersion { get; init; }

    public required long ModelId { get; init; }

    public required long ProjectId { get; init; }

    public required long RegistryTypeId { get; init; }

    public required long ServiceId { get; init; }

    public required long StateId { get; init; }

    public required string Commentary { get; init; }

    public long UnitId { get; init; }

    public IReadOnlyList<object> ListAdditionalField { get; init; } = [];
}
