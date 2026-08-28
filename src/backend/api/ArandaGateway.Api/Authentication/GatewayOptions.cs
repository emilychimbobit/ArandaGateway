using System.ComponentModel.DataAnnotations;

namespace ArandaGateway.Api.Authentication;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    [Required]
    public required string ApiKey { get; init; }
}
