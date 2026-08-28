namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaFileUploadResult
{
    public string? Description { get; init; }

    public required string FileName { get; init; }

    public required bool Result { get; init; }

    public string? Url { get; init; }
}
