namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCiRequest
{
    public required IReadOnlyList<ArandaCiProjectFilter> Projects { get; init; }
    public long? UserId { get; init; }
}

/// <summary>
/// Filtro de proyecto para el endpoint de CMDB. Aranda espera la propiedad
/// "id" en este endpoint, a diferencia de "project" que usa la búsqueda de casos.
/// </summary>
public sealed record ArandaCiProjectFilter(long Id);
