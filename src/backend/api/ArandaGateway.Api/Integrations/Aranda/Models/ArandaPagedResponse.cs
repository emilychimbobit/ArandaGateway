namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaPagedResponse<T>
{
    public required IReadOnlyList<T> Content { get; init; }

    public required long TotalItems { get; init; }

    public required long TotalPage { get; init; }
}
