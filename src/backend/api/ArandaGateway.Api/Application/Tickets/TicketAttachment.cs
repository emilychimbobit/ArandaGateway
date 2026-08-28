namespace ArandaGateway.Api.Application.Tickets;

public sealed record TicketAttachment(
    string FileName,
    string ContentType,
    long Length,
    Stream Content,
    string? Description);
