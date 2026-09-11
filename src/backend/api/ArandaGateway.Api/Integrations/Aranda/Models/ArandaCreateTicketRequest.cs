using System.Text.Json.Serialization;

namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaCreateTicketRequest
{
    public required long CategoryId { get; init; }

    public string ConsoleType { get; init; } = "specialist";

    public required long CustomerId { get; init; }

    public required long ApplicantId { get; init; }

    public required string Description { get; init; }

    public required long ItemType { get; init; }

    public int ItemVersion { get; init; }

    public required long ImpactId { get; init; }

    public required long UrgencyId { get; init; }

    public required long ModelId { get; init; }

    public required long ProjectId { get; init; }

    public required long RegistryTypeId { get; init; }

    /// <summary>
    /// Unidad organizacional. Es opcional: Aranda responde
    /// <c>InvalidOrganizationArea</c> cuando recibe una unidad que no
    /// corresponde al cliente, así que sin un valor válido configurado se omite
    /// del cuerpo y Aranda resuelve el área por su cuenta.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? UnitId { get; init; }

    public required long ServiceId { get; init; }

    public required long StateId { get; init; }

    public required long AuthorId { get; init; }

    public required long GroupId { get; init; }

    public int TempItemId { get; init; } = -2;

    public required string Subject { get; init; }

    public IReadOnlyList<object> ListAdditionalField { get; init; } = [];

    /// <summary>
    /// Sin este indicador Aranda rechaza la creación con
    /// <c>InvalidOrganizationArea</c>, incluso con una unidad válida: es lo que
    /// hace que resuelva el área organizacional en lugar de exigirla ya
    /// resuelta. Verificado el 11 de septiembre de 2026 comparando con una
    /// creación correcta: era la única diferencia del cuerpo.
    /// </summary>
    public bool Validate { get; init; } = true;
}
