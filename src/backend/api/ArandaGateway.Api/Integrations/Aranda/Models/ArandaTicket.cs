namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaTicket
{
    public required long Id { get; init; }

    public string? IdByProject { get; init; }

    public long? CustomerId { get; init; }

    public string? CustomerUserName { get; init; }

    public string? Subject { get; init; }

    public required long StateId { get; init; }

    public string? StateName { get; init; }

    public long? OpenedDate { get; init; }

    public long? ModifiedDate { get; init; }

    public string? GroupName { get; init; }

    public string? CommentaryNoHtml { get; init; }

    public required bool IsClosed { get; init; }

    public required long ItemVersion { get; init; }

    public required long ModelId { get; init; }

    public required long ProjectId { get; init; }

    public long? RegistryTypeId { get; init; }

    public required long ServiceId { get; init; }

    public required long CategoryId { get; init; }

    public required long ItemType { get; init; }
}
