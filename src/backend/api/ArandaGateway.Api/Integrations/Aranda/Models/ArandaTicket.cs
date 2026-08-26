namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaTicket
{
    public required long Id { get; init; }

    public required string IdByProject { get; init; }

    public required long CustomerId { get; init; }

    public required string CustomerUserName { get; init; }

    public required string Subject { get; init; }

    public required long StateId { get; init; }

    public required string StateName { get; init; }

    public required long OpenedDate { get; init; }

    public required long ModifiedDate { get; init; }

    public required string GroupName { get; init; }

    public string? CommentaryNoHtml { get; init; }

    public required bool IsClosed { get; init; }

    public required long ItemVersion { get; init; }

    public required long ModelId { get; init; }

    public required long ProjectId { get; init; }

    public required long ServiceId { get; init; }

    public required long CategoryId { get; init; }

    public required long ItemType { get; init; }
}
