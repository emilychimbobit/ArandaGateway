namespace ArandaGateway.Api.Integrations.Aranda.Models;

/// <summary>
/// Elemento de configuración (CI) devuelto por
/// api/v9/ci/cisbyuserandprojects. El identificador llega como "Id".
/// </summary>
public sealed record ArandaCiItem
{
    public required long Id { get; init; }
    public string? Name { get; init; }
    public long? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? Description { get; init; }
    public string? LicenseNumber { get; init; }
    public long? ImageId { get; init; }
    public bool IsClosed { get; init; }
    public long? StateId { get; init; }
    public string? StateName { get; init; }
    public string? StringStatusColor { get; init; }
    public long? AcceptDate { get; init; }
    public long? ResponsibleDate { get; init; }
}
