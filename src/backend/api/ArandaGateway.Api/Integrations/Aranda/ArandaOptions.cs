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

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;
}
