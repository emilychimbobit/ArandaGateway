namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaAttachmentUpload(
    long TicketId,
    long ItemType,
    string FileName,
    string ContentType,
    Stream Content,
    string? Description);
