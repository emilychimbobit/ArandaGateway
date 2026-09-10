using System.ComponentModel.DataAnnotations;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaOptions
{
    public const string SectionName = "Aranda";

    [Required]
    public required Uri BaseUrl { get; init; }

    [Required]
    public required string ApiKey { get; init; }

    [Range(1, long.MaxValue)]
    public long ProjectId { get; init; }

    [Range(1, long.MaxValue)]
    public long AuthorId { get; init; }

    public long? CategoryId { get; init; }

    public long? ServiceId { get; init; }

    public long? ImpactId { get; init; }

    public long? UrgencyId { get; init; }

    public long? GroupId { get; init; }

    public long? RegistryTypeId { get; init; }

    public long? UnitId { get; init; }

    public long? IncidentModelId { get; init; }

    public long? IncidentInitialStateId { get; init; }

    public long? IncidentCancellationStateId { get; init; }

    public long? ServiceRequestModelId { get; init; }

    public long? ServiceRequestInitialStateId { get; init; }

    public long? ServiceRequestCancellationStateId { get; init; }

    [Range(1, 100)]
    public int SearchPageSize { get; init; } = 50;

    [Range(1, 3_145_728)]
    public long MaxAttachmentBytes { get; init; } = 3_145_728;

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// PARCHE TEMPORAL: usuario fijo por dominio mientras la API de usuarios
    /// de Aranda no esté disponible. Ver <see cref="ArandaUserOverrideOptions"/>.
    /// Con <c>Enabled = false</c> o sin la sección, el gateway vuelve a
    /// resolver el colaborador contra Aranda.
    /// </summary>
    public ArandaUserOverrideOptions? UserOverride { get; init; }
}
