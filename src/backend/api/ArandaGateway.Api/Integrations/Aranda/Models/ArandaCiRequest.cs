namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCiRequest
{
    public required IReadOnlyList<ArandaProjectFilter> Projects { get; init; }
    public long? UserId { get; init; }
}