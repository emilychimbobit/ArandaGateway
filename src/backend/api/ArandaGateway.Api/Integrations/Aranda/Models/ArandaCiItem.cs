namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCiItem
{
    public required long Id { get; init; }
    public string? Name { get; init; }
    public string? CiTypeName { get; init; }
    public string? ModelName { get; init; }
    public string? Code { get; init; }
    public string? StateName { get; init; }
}