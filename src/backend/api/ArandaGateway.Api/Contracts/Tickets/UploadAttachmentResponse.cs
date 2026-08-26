namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record UploadAttachmentResponse(
    string FileName,
    bool Uploaded);
