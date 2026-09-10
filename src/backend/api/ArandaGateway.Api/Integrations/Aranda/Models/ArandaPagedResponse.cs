namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaPagedResponse<T>
{
    public required IReadOnlyList<T> Content { get; init; }

    public required long TotalItems { get; init; }

    /// <summary>
    /// Opcional: el endpoint de CMDB no devuelve este campo.
    /// </summary>
    public long? TotalPage { get; init; }
}
