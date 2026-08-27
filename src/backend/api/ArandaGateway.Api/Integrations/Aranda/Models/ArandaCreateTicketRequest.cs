namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCreateTicketRequest
{
    public required long CategoryId { get; init; }

    public int ConsoleType { get; init; } = 2;

    public required long CustomerId { get; init; }

    public required long ApplicantId { get; init; }

    public required string Description { get; init; }

    public required long ItemType { get; init; }

    public int ItemVersion { get; init; }

    public required long ImpactId { get; init; }

    public required long UrgencyId { get; init; }

    public required long ModelId { get; init; }

    public required long ProjectId { get; init; }

    public required long RegistryTypeId { get; init; }

    public required long ServiceId { get; init; }

    public required long StateId { get; init; }

    public required long AuthorId { get; init; }

    public required long GroupId { get; init; }

    public int TempItemId { get; init; } = -2;

    public required string Subject { get; init; }

    public IReadOnlyList<object> ListAdditionalField { get; init; } = [];
}
