namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaUser
{
    public required long Id { get; init; }

    public required string UserName { get; init; }

    public string? Email { get; init; }

    public required string Name { get; init; }

    public required bool IsActive { get; init; }
}
