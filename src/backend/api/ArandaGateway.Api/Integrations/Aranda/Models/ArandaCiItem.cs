using System.Text.Json.Serialization;

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
    // El CMDB devuelve estas fechas en el formato heredado
    // "/Date(1728432000000+0000)/", no como epoch numerico.
    [JsonConverter(typeof(ArandaEpochMillisecondsConverter))]
    public long? AcceptDate { get; init; }

    [JsonConverter(typeof(ArandaEpochMillisecondsConverter))]
    public long? ResponsibleDate { get; init; }
}
